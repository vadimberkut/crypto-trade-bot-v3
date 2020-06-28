using CryptoTradeBot.Exchanges.Binance.Dtos;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Websocket.Client;
using Microsoft.Extensions.Options;
using CryptoTradeBot.Exchanges.Binance;
using CryptoTradeBot.Exchanges.Binance.Handlers;
using System.IO;
using CryptoTradeBot.Exchanges.Binance.Stores;
using System.Diagnostics;
using CryptoTradeBot.Host.Exchanges.Binance.Clients;
using CryptoTradeBot.WorkerServices;
using CryptoTradeBot.Host.Algorithms.CirclePathAlgorithm;
using CryptoTradeBot.Host.Interfaces;
using CryptoTradeBot.Host.Exchanges.Binance.Utils;
using CryptoTradeBot.WebHost.Algorithms.CirclePathAlgorithm.Models;
using CryptoTradeBot.Infrastructure.Extensions;
using CryptoTradeBot.WebHost.Exchanges.Binance;
using CryptoTradeBot.Infrastructure.Models;
using CryptoTradeBot.StrategyRunner;
using CryptoTradeBot.StrategyRunner.Settings;
using CryptoTradeBot.StrategyRunner.Interfaces;
using CryptoTradeBot.Infrastructure.Enums;
using CryptoTradeBot.StrategyRunner.Enums;
using CryptoTradeBot.StrategyRunner.Models;

namespace CryptoTradeBot.Simulation.Workers
{
    public class AlgorithmSimulationWorkerService : IHostedService
    {
        private readonly ILogger<AlgorithmSimulationWorkerService> _logger;
        private readonly IOptions<ApplicationSettings> _config;
        private readonly IServiceProvider _serviceProvider;
        private readonly BinanceHttpClient _binanceHttpClient;
        private readonly BinanceWssClient _binanceWssClient;

        public AlgorithmSimulationWorkerService(
            ILogger<AlgorithmSimulationWorkerService> logger,
            IOptions<ApplicationSettings> config,
            IServiceProvider serviceProvider,
            BinanceHttpClient binanceHttpClient,
            BinanceWssClient binanceWssClient
        )
        {
            _logger = logger;
            _config = config;
            _serviceProvider = serviceProvider;
            _binanceHttpClient = binanceHttpClient;
            _binanceWssClient = binanceWssClient;
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            // from book
            //await StartCirclePathSimulationAsync(cancellationToken);
            //await StartLarryWilliamsBookRangeBreakSimulationAsync(cancellationToken);
            //await StartLarryWilliamsBookStrikingDaysSimulationAsync(cancellationToken);
            
            // custom
            await StartCustomNestedBarBreakthroughSimulationAsync(cancellationToken);
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Stopping...");
            return Task.CompletedTask;
        }

        private async Task StartCirclePathSimulationAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Starting simulation...");

            var circlePathAlgorithm = new CirclePahtAlgorithm(
                _serviceProvider.GetRequiredService<ILogger<CirclePahtAlgorithm>>(),
                _serviceProvider.GetRequiredService<OrderBookStore>(),
                _serviceProvider.GetRequiredService<BinanceExchangeUtil>()
            );

            const string orderBookSaveDirPath = "../CryptoTradeBot/data-logs/binance/order-book-store";

            if (!Directory.Exists(orderBookSaveDirPath))
            {
                throw new Exception($"Dicrectory with data logs doesn't exist!");
            }

            var orderBookStore = _serviceProvider.GetRequiredService<OrderBookStore>();
            var filePathes = Directory.GetFiles(orderBookSaveDirPath).OrderBy(x => x).ToList();
            DateTime? prevSnapshotAt = null;
            DateTime? nextSnapshotMustBeAt = null;
            TimeSpan maxIntervalBetweenSnapshots = TimeSpan.FromSeconds((10 + 5));
            TimeSpan snapshotToSelectWindow = TimeSpan.FromSeconds(60);
            TimeSpan simulationTime = TimeSpan.FromSeconds(0);
            List<CirclePathSolutionItemModel> bestSolutionsFroEachIteration = new List<CirclePathSolutionItemModel>();
            for (int i = 0; i < filePathes.Count; i += 1)
            {
                string filePath = filePathes[i];
                string fileName = Path.GetFileNameWithoutExtension(filePath);

                _logger.LogInformation($"----------Processing ({i + 1}/{filePathes.Count}) '{fileName}'...");

                DateTime currentSnapshotAt = DateTime.ParseExact(fileName, "yyyyMMdd'T'HHmmss'.'fff'Z'", null);
                DateTime.SpecifyKind(currentSnapshotAt, DateTimeKind.Utc);

                // throttle
                nextSnapshotMustBeAt = nextSnapshotMustBeAt ?? DateTime.MinValue;
                if (currentSnapshotAt < nextSnapshotMustBeAt)
                {
                    prevSnapshotAt = currentSnapshotAt;
                    continue;
                }

                var intervalBetweenSnapshots = currentSnapshotAt.Subtract(prevSnapshotAt.GetValueOrDefault());
                if (prevSnapshotAt == null || intervalBetweenSnapshots > maxIntervalBetweenSnapshots)
                {
                    prevSnapshotAt = currentSnapshotAt;
                    continue;
                }

                simulationTime = simulationTime.Add(intervalBetweenSnapshots);

                // setup store from snaphot
                string fileContent = File.ReadAllText(filePath);
                orderBookStore.ImportFromJson(fileContent);

                // run algorith
                //string startAsset = "IOTA";
                //decimal startAssetAmount = 1000;
                //string startAsset = "BTC";
                //decimal startAssetAmount = 0.1m;
                string startAsset = "USDT";
                decimal startAssetAmount = 100;
                var solutions = circlePathAlgorithm.Solve(startAsset, startAssetAmount);

                if (solutions.Count != 0)
                {
                    // take the most profitable
                    bestSolutionsFroEachIteration.Add(solutions.OrderByDescending(x => x.SimulationResult.EstimatedProfitInStartAsset).First());

                    foreach (var solution in solutions)
                    {
                        _logger.LogInformation($"Solution: amount={solution.SimulationResult.TargetStartAssetAmount}, profit={solution.SimulationResult.EstimatedProfitInStartAsset}, profit USDT={solution.SimulationResult.EstimatedProfitInUSDTAsset}.");
                    }
                }

                _logger.LogInformation($"----------Processed ({i + 1}/{filePathes.Count}) '{fileName}'.");

                prevSnapshotAt = currentSnapshotAt;
                nextSnapshotMustBeAt = currentSnapshotAt.Add(snapshotToSelectWindow);
            }

            // take only uniq results - filter out repeatable
            var aggregatedDictionary = bestSolutionsFroEachIteration
                .Select(solution => new Tuple<string, CirclePathSolutionItemModel>(solution.PathId, solution))
                .Aggregate
                    <Tuple<string, CirclePathSolutionItemModel>, Dictionary<string, IEnumerable<CirclePathSolutionItemModel>>>
                    (new Dictionary<string, IEnumerable<CirclePathSolutionItemModel>>(), (accum, curr) =>
                    {
                        if (!accum.ContainsKey(curr.Item1))
                        {
                            accum.Add(curr.Item1, new List<CirclePathSolutionItemModel>());
                        }
                        (accum[curr.Item1] as List<CirclePathSolutionItemModel>).Add(curr.Item2);
                        return accum;
                    });

            var aggregatedUniqueDictionary = aggregatedDictionary
                .Distinct()
                .DistinctByKeyValues(x => x.SimulationResult.EstimatedProfitInStartAsset)
                .ToDictionary(x => x.Key, y => y.Value);

            var uniqueBestSolutionsFroEachIteration = aggregatedUniqueDictionary.SelectMany(x => x.Value).ToList();

            //// 2
            //var dict = new Dictionary<string, Dictionary<string, CirclePathSolutionItemModel>>();
            //foreach (var item in bestSolutionsFroEachIteration)
            //{
            //    if(!dict.ContainsKey(item.PathId))
            //    {
            //        dict.Add(item.PathId, new Dictionary<string, CirclePathSolutionItemModel>());
            //    }
            //    if (!dict[item.PathId].ContainsKey(item.SimulationResult.EstimatedProfitInStartAsset.ToString("0.########")))
            //    {
            //        dict[item.PathId].Add(item.SimulationResult.EstimatedProfitInStartAsset.ToString("0.########"), item);
            //    }
            //}
            //var dcitres = dict.ToList().Select(x => x.Value).SelectMany(x => x.Values.Select(y => y)).ToList();

            _logger.LogInformation($"");
            _logger.LogInformation($"Simulation is done. Real time simulated: {simulationTime}.");

            _logger.LogInformation($"");
            _logger.LogInformation($"Solution list:");
            foreach (var solution in uniqueBestSolutionsFroEachIteration)
            {
                _logger.LogInformation($"Solution: amount={solution.SimulationResult.TargetStartAssetAmount}, profit={solution.SimulationResult.EstimatedProfitInStartAsset}, profit USDT={solution.SimulationResult.EstimatedProfitInUSDTAsset}, pathId={solution.PathId}.");
            }

            // calc total profit
            decimal totalEstimatedProfit = uniqueBestSolutionsFroEachIteration.Aggregate<CirclePathSolutionItemModel, decimal>(0, (accum, curr) => accum + curr.SimulationResult.EstimatedProfitInStartAsset);
            decimal totalEstimatedProfitUsdt = uniqueBestSolutionsFroEachIteration.Aggregate<CirclePathSolutionItemModel, decimal>(0, (accum, curr) => accum + curr.SimulationResult.EstimatedProfitInUSDTAsset);
            _logger.LogInformation($"");
            _logger.LogInformation($"Solution total: profit={totalEstimatedProfit}, profit USDT={totalEstimatedProfitUsdt}.");
        }

        private async Task<GeneralSymbolBarHistoryModel> BinanceLoadSymbolCandlestickHistory(string symbol, DateTime from, DateTime to, string candlestickInterval)
        {
            var binanceHttpClient = this._serviceProvider.GetRequiredService<BinanceHttpClient>();

            string historyDataStoreDirectoryPath = Path.Combine(Directory.GetCurrentDirectory(), "./market-data-store/");
            string candlestickDataFileNameFormat = "{0}__{1}__{2}__{3}.json"; // symbol__from__to__interval.json

            if (!Directory.Exists(historyDataStoreDirectoryPath))
            {
                Directory.CreateDirectory(historyDataStoreDirectoryPath);
            }

            // download history data if not downloaded already or read from it
            string candlestickDataFilePath = Path.Combine(
                historyDataStoreDirectoryPath,
                String.Format(
                    candlestickDataFileNameFormat,
                    symbol,
                    //assetToTest.From.ToString("yyyyMMdd'T'HHmmss'.'fff'Z'"),
                    //assetToTest.To.ToString("yyyyMMdd'T'HHmmss'.'fff'Z'"),
                    from.ToString("yyyyMMdd'Z'"),
                    to.ToString("yyyyMMdd'Z'"),
                    candlestickInterval
                )
            );
            GeneralSymbolBarHistoryModel symbolCandlestickHistory;
            if (File.Exists(candlestickDataFilePath))
            {
                _logger.LogInformation($"Loading from file...");
                string content = File.ReadAllText(candlestickDataFilePath);
                symbolCandlestickHistory = JsonConvert.DeserializeObject<GeneralSymbolBarHistoryModel>(content);
            }
            else
            {
                _logger.LogInformation($"Loading through API...");

                var dto = await binanceHttpClient.GetCandlestickDataAsync(symbol, candlestickInterval, from, to, 1000);
                symbolCandlestickHistory = new GeneralSymbolBarHistoryModel()
                {
                    Symbol = symbol,
                    From = from,
                    To = to,
                    BarInterval = candlestickInterval,
                    Bars = dto.Candles.OrderBy(x => x.OpenTime).Select(x => new GeneralBarModel()
                    {
                        OpenTime = x.OpenTime,
                        CloseTime = x.CloseTime,
                        OpenPrice = x.OpenPrice,
                        HighPrice = x.HighPrice,
                        LowPrice = x.LowPrice,
                        ClosePrice = x.ClosePrice,
                        Volume = x.Volume,
                        QuoteAssetVolume = x.QuoteAssetVolume,
                    }).ToList(),
                };

                _logger.LogInformation($"Saving to file...");
                File.WriteAllText(candlestickDataFilePath, JsonConvert.SerializeObject(symbolCandlestickHistory));
            }

            return symbolCandlestickHistory;
        }

        /// <summary>
        /// Buy when range is broken
        /// </summary>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        private async Task StartLarryWilliamsBookRangeBreakSimulationAsync(CancellationToken cancellationToken)
        {
            var assetsToTest = new[]
            {
                new 
                {
                    Symbol = "BTCUSDT",
                    From = DateTime.UtcNow.Subtract(TimeSpan.FromDays(3 * 12 * 30)),
                    To = DateTime.UtcNow,
                    CandlestickInterval = "4h",
                },    
            };

            foreach (var assetToTest in assetsToTest)
            {
                _logger.LogInformation($"Symbol={assetToTest.Symbol}.");
                var symbolCandlestickHistory = await BinanceLoadSymbolCandlestickHistory(assetToTest.Symbol, assetToTest.From, assetToTest.To, assetToTest.CandlestickInterval);

                // test
                int length = symbolCandlestickHistory.Bars.Count;
                const decimal volatilityRangeEntryPercent = 0.5m;
                decimal? volatilityRangeStopLossPercent = null; // null - no stop loss 
                decimal initialQuoteAssetAmount = 1000; // in quote asset. E.g. BTCUSDT 1000USDT
                decimal maxDrowdownPercent = 0.8m; // from initial balance
                decimal makerFeePercent = 0.0025m;
                decimal currentQuoteAssetAmount = initialQuoteAssetAmount;
                var testResults = new List<SimpleTradeResultModel>();

                for (int i = 5; i < length - 1; i++)
                {
                    var prevCandle3Days = symbolCandlestickHistory.Bars[i - 3];
                    var prevCandle = symbolCandlestickHistory.Bars[i - 1];
                    var currentCandle = symbolCandlestickHistory.Bars[i];
                    var nextCandle = symbolCandlestickHistory.Bars[i + 1];

                    // calc position volatilityRange
                    Func<decimal> getVolatilityRangeWithSimpleStrategy = () =>
                    {
                        return prevCandle.HighPrice - prevCandle.LowPrice;
                    };
                    Func<decimal> getVolatilityRangeWithBuyerStrengthStrategy = () =>
                    {
                        return prevCandle.ClosePrice - prevCandle.LowPrice;
                    };
                    Func<decimal> getVolatilityRangeWith3daysAnalysisStrategy = () =>
                    {

                        decimal _3daysRange1 = Math.Abs(prevCandle3Days.HighPrice - prevCandle.LowPrice);
                        decimal _3daysRange2 = Math.Abs(prevCandle.HighPrice - prevCandle3Days.LowPrice);
                        return Math.Max(_3daysRange1, _3daysRange2);
                    };

                    decimal volatilityRange = getVolatilityRangeWith3daysAnalysisStrategy();


                    decimal longPositionOpenPrice = currentCandle.OpenPrice + volatilityRange * volatilityRangeEntryPercent;
                    decimal? longPositionStoplossPrice = volatilityRangeStopLossPercent == null ? default(decimal?) : longPositionOpenPrice - volatilityRange * volatilityRangeStopLossPercent.Value;

                    // position entered
                    if(currentCandle.HighPrice > longPositionOpenPrice)
                    {
                        // calc position close
                        decimal longPositionClosePrice;
                        bool isClosedByStopLoss = false;
                        if (longPositionStoplossPrice != null && currentCandle.LowPrice <= longPositionStoplossPrice.Value)
                        {
                            // closed by stop loss
                            longPositionClosePrice = longPositionStoplossPrice.Value;
                            isClosedByStopLoss = true;
                        }
                        else
                        {
                            // closed by take profit (next day open price)
                            longPositionClosePrice = nextCandle.OpenPrice;
                        }

                        // calc PnL
                        decimal quoteAssetAmountBefore = currentQuoteAssetAmount;
                        
                        decimal baseAssetAmount = quoteAssetAmountBefore / longPositionOpenPrice;
                        baseAssetAmount = baseAssetAmount - baseAssetAmount * makerFeePercent; // fee
                        
                        decimal quoteAssetAmountAfter = baseAssetAmount * longPositionClosePrice;
                        quoteAssetAmountAfter = quoteAssetAmountAfter - quoteAssetAmountAfter * makerFeePercent; // fee

                        decimal pnl = quoteAssetAmountAfter - quoteAssetAmountBefore;
                        currentQuoteAssetAmount = quoteAssetAmountAfter;

                        var result = new SimpleTradeResultModel
                        {
                            Balance = currentQuoteAssetAmount,
                            Pnl = pnl,
                            PnlPercent = pnl / quoteAssetAmountBefore,
                            IsClosedByStopLoss = isClosedByStopLoss,
                        };
                        testResults.Add(result);

                        // don't trade next day (the day when position closed on open)
                        i += 1;

                        // check drowdown
                        decimal drowdownPercent = 1 - result.Balance / initialQuoteAssetAmount;
                        if (drowdownPercent >= maxDrowdownPercent)
                        {
                            _logger.LogInformation($"Reached max drowdown of {Math.Round(maxDrowdownPercent * 100, 2)}%.");
                            break;
                        }
                    }
                }

                // add placeholders for profit and loss trade
                if(!testResults.Any(x => x.Pnl > 0))
                {
                    testResults.Add(new SimpleTradeResultModel()
                    {
                        Pnl = 0.001m,
                        PnlPercent = 0.00001m,
                    });
                }
                if (!testResults.Any(x => x.Pnl <= 0))
                {
                    testResults.Add(new SimpleTradeResultModel()
                    {
                        Pnl = -0.001m,
                        PnlPercent = -0.00001m,
                    });
                }

                // stats
                int candlesCountCount = symbolCandlestickHistory.Bars.Count;
                int tradesCount = testResults.Count;
                int profitTradesCount = testResults.Count(x => x.Pnl > 0);
                int lossTradesCount = testResults.Count(x => x.Pnl <= 0);
                int closedByStopLossTradesCount = testResults.Count(x => x.IsClosedByStopLoss);
                decimal minProfit = Math.Round(testResults.Where(x => x.Pnl > 0).Min(x => x.Pnl), 2);
                decimal averageProfit = Math.Round(testResults.Where(x => x.Pnl > 0).Aggregate(0m, (avg, x) => avg + (x.Pnl / testResults.Count)), 2);
                decimal maxProfit = Math.Round(testResults.Where(x => x.Pnl > 0).Max(x => x.Pnl), 2);
                decimal minLoss = Math.Round(testResults.Where(x => x.Pnl <= 0).Max(x => x.Pnl), 2);
                decimal averageLoss = Math.Round(testResults.Where(x => x.Pnl <= 0).Aggregate(0m, (avg, x) => avg + (x.Pnl / testResults.Count)), 2);
                decimal maxLoss = Math.Round(testResults.Where(x => x.Pnl <= 0).Min(x => x.Pnl), 2);
                decimal initalBalance = initialQuoteAssetAmount;
                decimal finalBalance = testResults.Last().Balance;
                decimal totalPnl = Math.Round(testResults.Aggregate(0m, (avg, x) => avg + x.Pnl), 2);
                decimal totalPnlPercent = Math.Round(totalPnl / initialQuoteAssetAmount, 3);

                _logger.LogInformation("");
                _logger.LogInformation($"Symbol={assetToTest.Symbol}");
                _logger.LogInformation($"CandlestickInterval={assetToTest.CandlestickInterval}");
                _logger.LogInformation($"Range: From={assetToTest.From.ToUtcString()}, To={assetToTest.To.ToUtcString()}; Timespan={assetToTest.To.Subtract(assetToTest.From).ToString()}");
                _logger.LogInformation($"Timespan={assetToTest.To.Subtract(assetToTest.From).ToString()}");
                _logger.LogInformation($"candlesCountCount={candlesCountCount}");
                _logger.LogInformation($"tradesCount={tradesCount}");
                _logger.LogInformation($"profitTradesCount={profitTradesCount}");
                _logger.LogInformation($"lossTradesCount={lossTradesCount}");
                _logger.LogInformation($"closedByStopLossTradesCount={closedByStopLossTradesCount}");
                _logger.LogInformation($"minProfit={minProfit}");
                _logger.LogInformation($"averageProfit={averageProfit}");
                _logger.LogInformation($"maxProfit={maxProfit}");
                _logger.LogInformation($"minLoss={minLoss}");
                _logger.LogInformation($"averageLoss={averageLoss}");
                _logger.LogInformation($"maxLoss={maxLoss}");
                _logger.LogInformation($"initalBalance={initalBalance}");
                _logger.LogInformation($"finalBalance={finalBalance}");
                _logger.LogInformation($"totalPnl={totalPnl}");
                _logger.LogInformation($"totalPnlPercent={totalPnlPercent}");

                _logger.LogInformation($"Symbol={assetToTest.Symbol}. Done.");
                _logger.LogInformation("");
            }
        }

        /// <summary>
        /// Larry Williams Striking days (bars\candles). Page 121.
        /// 1. Buy 1. Day closes below prev day min. Buy if next day price grows above current day max.
        /// 2. Sell 1. Day closes above prev day max. Buy if next day price falls below current day min.
        /// 3. Buy 2. Day closes near the open price afar from max in 25% bar bottom. Buy if next day price grows above current day max.
        /// 4. Sell 2. Day closes near the open price afar from min in 25% bar top. Sell if next day price falls below current day min.
        /// Summary: 
        /// On the average gives small profit during 3 month, during year gives at least not loss. 
        /// Solely depends on parameters which can give desired results, but in general it's obvious that strategy not going to work.
        /// Tested on BTCUSDT, DASHUSDT, IOTAUSDT.
        /// Maybe it works for 2000s markets or some traditional markets. I don't know, but on crypto I didn't manage to get 
        /// > 60% of profit trades.
        /// </summary>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        private async Task StartLarryWilliamsBookStrikingDaysSimulationAsync(CancellationToken cancellationToken)
        {
            var assetsToTest = new[]
            {
                new
                {
                    Symbol = "BTCUSDT",
                    From = DateTime.UtcNow.Subtract(TimeSpan.FromDays(12 * 30)),
                    To = DateTime.UtcNow,
                    CandlestickInterval = "4h",
                },
                //new
                //{
                //    Symbol = "IOTAUSDT",
                //    From = DateTime.UtcNow.Subtract(TimeSpan.FromDays(12 * 30)),
                //    To = DateTime.UtcNow,
                //    CandlestickInterval = "4h",
                //},
                // new
                //{
                //    Symbol = "DASHUSDT",
                //    From = DateTime.UtcNow.Subtract(TimeSpan.FromDays(12 * 30)),
                //    To = DateTime.UtcNow,
                //    CandlestickInterval = "4h",
                //},
            };
            
            foreach (var assetToTest in assetsToTest)
            {
                _logger.LogInformation($"Symbol={assetToTest.Symbol}.");

                var symbolCandlestickHistory = await BinanceLoadSymbolCandlestickHistory(assetToTest.Symbol, assetToTest.From, assetToTest.To, assetToTest.CandlestickInterval);

                // Calculates pnl
                Func<string, decimal, decimal, decimal, decimal, (decimal pnl, decimal pnlPercent, decimal quoteAssetAmountAfter)> calcPositionPnl =
                    (_orderType, _quoteAssetAmountBefore, _openPrice, _closePrice, _makerFeePercent) =>
                    {
                        if (_orderType != "long")
                        {
                            throw new NotImplementedException();
                        }

                        decimal _baseAssetAmount = _quoteAssetAmountBefore / _openPrice;
                        _baseAssetAmount = _baseAssetAmount - _baseAssetAmount * _makerFeePercent; // fee

                                    decimal _quoteAssetAmountAfter = _baseAssetAmount * _closePrice;
                        _quoteAssetAmountAfter = _quoteAssetAmountAfter - _quoteAssetAmountAfter * _makerFeePercent; // fee

                                    decimal _pnl = _quoteAssetAmountAfter - _quoteAssetAmountBefore;
                        decimal _pnlPercent = Math.Round(_pnl / _quoteAssetAmountBefore, 4);
                        return (pnl: _pnl, pnlPercent: _pnlPercent, quoteAssetAmountAfter: _quoteAssetAmountAfter);
                    };


                // test
                int length = symbolCandlestickHistory.Bars.Count;

                const int maxPositionOpenCheckDurationInBars = 3; // how long to wait after open signal was received. when expired - cancel ([bar condition true; current bar])
                const int maxOpenPositionDurationInBars = 15; // how long to keep opened position if neither stoploss and takeprofit was hit ([bar opened; bar when closing])
                decimal maxDrawdownPercent = 0.8m; // from initial balance
                decimal makerFeePercent = 0.0025m;
                decimal? stopLossPercent = 0.08m; // null - no stop loss 
                decimal? takeProfitPercent = 0.10m; // null - no take profit
                decimal initialQuoteAssetAmount = 1000; // in quote asset. E.g. BTCUSDT 1000USDT
                decimal currentQuoteAssetAmount = initialQuoteAssetAmount;
                
                var testResults = new List<SimpleTradeResultModel>();

                string positionStatus = "NoPosition"; // NoPosition, CheckingPositionOpenCondition, CheckingPositionCloseCondition, 
                decimal longPositionOpenPrice = 0;
                decimal? longPositionStoplossPrice = null;
                decimal? longPositionTakeprofitPrice = null;
                int positionCheckingOpenConditionDurationInBars = 0;
                int positionDurationInBars = 0;

                for (int i = 1; i < length - 1; i++)
                {
                    var prevCandle = symbolCandlestickHistory.Bars[i - 1];
                    var currentCandle = symbolCandlestickHistory.Bars[i];
                    var nextCandle = symbolCandlestickHistory.Bars[i + 1];

                    // check whether current bar is striking bar
                    Func<GeneralBarModel, GeneralBarModel, bool> checkBuy1StrikingBar = (_prevBar, _currentBar) =>
                    {
                        return _currentBar.ClosePrice < _prevBar.LowPrice;
                    };
                    Func<GeneralBarModel, GeneralBarModel, bool> checkSell1StrikingBar = (_prevBar, _currentBar) =>
                    {
                        return _currentBar.ClosePrice > _prevBar.HighPrice;
                    };
                    Func<GeneralBarModel, GeneralBarModel, bool> checkBuy2StrikingBar = (_prevBar, _currentBar) =>
                    {
                        const decimal inRangeFromHighPercent = 0.25m;
                        decimal rangeStart = _currentBar.LowPrice;
                        decimal rangeEnd = _currentBar.LowPrice + (_currentBar.HighPrice - _currentBar.LowPrice) * inRangeFromHighPercent;
                        return _currentBar.OpenPrice >= rangeStart && _currentBar.OpenPrice <= rangeEnd &&
                               _currentBar.ClosePrice >= rangeStart && _currentBar.ClosePrice <= rangeEnd; 
                               //_currentBar.ClosePrice <= _currentBar.OpenPrice; // optional
                    };
                    Func<GeneralBarModel, GeneralBarModel, bool> checkSell2StrikingBar = (_prevBar, _currentBar) =>
                    {
                        const decimal inRangeFromHighPercent = 0.25m;
                        decimal rangeStart = _currentBar.HighPrice - (_currentBar.HighPrice - _currentBar.LowPrice) * inRangeFromHighPercent;
                        decimal rangeEnd = _currentBar.HighPrice;
                        return _currentBar.OpenPrice >= rangeStart && _currentBar.OpenPrice <= rangeEnd &&
                               _currentBar.ClosePrice >= rangeStart && _currentBar.ClosePrice <= rangeEnd;
                               //_currentBar.ClosePrice >= _currentBar.OpenPrice; // optional
                    };
                    
                    if(positionStatus == "NoPosition")
                    {
                        // detect and define position open condition 
                        if (false && checkBuy1StrikingBar(prevCandle, currentCandle))
                        {
                            longPositionOpenPrice = currentCandle.HighPrice;
                            // longPositionStoplossPrice = stopLossPercent == null ? default(decimal?) : longPositionOpenPrice - longPositionOpenPrice * stopLossPercent.Value;
                            longPositionStoplossPrice = currentCandle.LowPrice - currentCandle.LowPrice * 0.01m; // adaptive stoploss
                            longPositionTakeprofitPrice = takeProfitPercent == null ? default(decimal?) : longPositionOpenPrice + longPositionOpenPrice * takeProfitPercent.Value;

                            positionStatus = "CheckingPositionOpenCondition";

                            // count bars while checking for position open condition
                            positionCheckingOpenConditionDurationInBars += 1;

                            continue;
                        }
                        else if(checkBuy2StrikingBar(prevCandle, currentCandle))
                        {
                            longPositionOpenPrice = currentCandle.HighPrice;
                            //  longPositionStoplossPrice = stopLossPercent == null ? default(decimal?) : longPositionOpenPrice - longPositionOpenPrice * stopLossPercent.Value;
                            longPositionStoplossPrice = currentCandle.LowPrice - currentCandle.LowPrice * 0.01m; // adaptive stoploss
                            longPositionTakeprofitPrice = takeProfitPercent == null ? default(decimal?) : longPositionOpenPrice + longPositionOpenPrice * takeProfitPercent.Value;

                            positionStatus = "CheckingPositionOpenCondition";

                            // count bars while checking for position open condition
                            positionCheckingOpenConditionDurationInBars += 1;

                            continue;
                        }
                    }
                    else if(positionStatus == "CheckingPositionOpenCondition")
                    {
                        // count bars while checking for position open condition
                        positionCheckingOpenConditionDurationInBars += 1;

                        // terminate if waiting too long for particular open signal
                        if (positionCheckingOpenConditionDurationInBars >= maxPositionOpenCheckDurationInBars)
                        {
                            positionStatus = "NoPosition";
                            positionCheckingOpenConditionDurationInBars = 0;
                        }

                        // check for position open condition
                        if (currentCandle.HighPrice >= longPositionOpenPrice)
                        {
                            positionStatus = "CheckingPositionCloseCondition";
                            positionCheckingOpenConditionDurationInBars = 0;

                            // process current bar again by different status as it can trigger stoploss or close condition.
                            // i.e. possibility to open and close position during the same bar
                            i -= 1;

                            //// don't process current bar. only check stoploss and takeprofit on next bar
                            //// so allow market to move and confirm/decline our expectations regarding direction
                            //i += 0;
                            //// count bars while in opened position
                            //positionDurationInBars += 1;

                            continue;
                        }

                        continue;
                    }
                    else if (positionStatus == "CheckingPositionCloseCondition")
                    {
                        // count bars while in opened position
                        positionDurationInBars += 1;

                        // check for stoploss condition
                        // check for takeprofit condition
                        // check for termination by duration condition
                        decimal longPositionClosePrice = 0;
                        bool isClosed = false;
                        bool isClosedByStopLoss = false;
                        bool isClosedByTakeProfit= false;
                        bool isClosedByDurationTimeout = false;
                        if (longPositionStoplossPrice != null && currentCandle.LowPrice <= longPositionStoplossPrice.Value)
                        {
                            // close by stop loss
                            longPositionClosePrice = longPositionStoplossPrice.Value;
                            isClosed = true;
                            isClosedByStopLoss = true;
                        }
                        else if(longPositionTakeprofitPrice != null && currentCandle.HighPrice >= longPositionTakeprofitPrice.Value)
                        {
                            // close by take profit
                            longPositionClosePrice = longPositionTakeprofitPrice.Value;
                            isClosed = true;
                            isClosedByTakeProfit = true;
                        }
                        else if(positionDurationInBars >= maxOpenPositionDurationInBars)
                        {
                            // close by duration
                            longPositionClosePrice = currentCandle.ClosePrice; // take close time
                            isClosed = true;
                            isClosedByDurationTimeout = true;
                        }

                        //// calc current PnL
                        var closePriceTemp = currentCandle.ClosePrice;
                        var pnlTuple0 = calcPositionPnl("long", currentQuoteAssetAmount, longPositionOpenPrice, closePriceTemp, makerFeePercent);
                        _logger.LogInformation($"--- current pnl={Math.Round(pnlTuple0.pnl, 2)} ({pnlTuple0.pnlPercent}%)");
                        if(isClosed)
                        {
                            _logger.LogInformation($"--- CLOSED");
                            _logger.LogInformation($"---");
                        }

                        // position closed
                        if (isClosed)
                        {
                            // calc PnL
                            var pnlTuple = calcPositionPnl("long", currentQuoteAssetAmount, longPositionOpenPrice, longPositionClosePrice, makerFeePercent);
                            currentQuoteAssetAmount = pnlTuple.quoteAssetAmountAfter;

                            var candlestickIntervalConfig = BinanceConfig.GetBarChartInterval(assetToTest.CandlestickInterval);

                            var result = new SimpleTradeResultModel
                            {
                                Balance = currentQuoteAssetAmount,
                                Pnl = pnlTuple.pnl,
                                PnlPercent = pnlTuple.pnlPercent,
                                IsClosedByStopLoss = isClosedByStopLoss,
                                IsClosedByTakeProfit = isClosedByTakeProfit,
                                IsClosedByDurationTimeout = isClosedByDurationTimeout,
                                PositionDuration = positionDurationInBars * candlestickIntervalConfig.TimeSpan,
                            };
                            testResults.Add(result);

                            positionStatus = "NoPosition";
                            positionCheckingOpenConditionDurationInBars = 0;
                            positionDurationInBars = 0;

                            // check drowdown
                            decimal drowdownPercent = 1 - result.Balance / initialQuoteAssetAmount;
                            if (drowdownPercent >= maxDrawdownPercent)
                            {
                                _logger.LogInformation($"Reached max drowdown of {Math.Round(maxDrawdownPercent * 100, 2)}%.");
                                break;
                            }
                        }
                    }
                }

                // add placeholders for profit and loss trade
                if (!testResults.Any(x => x.Pnl > 0))
                {
                    testResults.Add(new SimpleTradeResultModel()
                    {
                        Pnl = 0.001m,
                        PnlPercent = 0.00001m,
                    });
                }
                if (!testResults.Any(x => x.Pnl <= 0))
                {
                    testResults.Add(new SimpleTradeResultModel()
                    {
                        Pnl = -0.001m,
                        PnlPercent = -0.00001m,
                    });
                }

                var profitTrades = testResults.Where(x => x.Pnl > 0);
                var lossTrades = testResults.Where(x => x.Pnl <= 0);

                // stats
                int barsCount = symbolCandlestickHistory.Bars.Count;
                int tradesCount = testResults.Count;
                int profitTradesCount = profitTrades.Count();
                int lossTradesCount = lossTrades.Count();
                decimal profitTradesPercent = Math.Round((decimal)profitTradesCount / (decimal)tradesCount, 2);
                decimal lossTradesPercent = Math.Round((decimal)lossTradesCount / (decimal)tradesCount, 2);
                int closedByStopLossTradesCount = testResults.Count(x => x.IsClosedByStopLoss);
                int closedByTakeProfitTradesCount = testResults.Count(x => x.IsClosedByTakeProfit);
                int closedByDurationTimeoutTradesCount = testResults.Count(x => x.IsClosedByDurationTimeout);
                int closedByDurationTimeoutProfitTradesCount = profitTrades.Count(x => x.IsClosedByDurationTimeout);
                int closedByDurationTimeoutLossTradesCount = lossTrades.Count(x => x.IsClosedByDurationTimeout);
                int maxProfitTradesInARow = testResults.Select(x => (item: x, currentCount: 0, max: 0)).Aggregate((prev, curr) =>
                {
                    if(curr.item.Pnl > 0)
                    {
                        prev.currentCount += 1;
                        prev.max = Math.Max(prev.currentCount, prev.max);
                    }
                    else if (curr.item.Pnl <= 0)
                    {
                        prev.currentCount = 0;
                    }
                    return prev;
                }).max;
                int maxLossTradesInARow = testResults.Select(x => (item: x, currentCount: 0, max: 0)).Aggregate((prev, curr) =>
                {
                    if (curr.item.Pnl <= 0)
                    {
                        prev.currentCount += 1;
                        prev.max = Math.Max(prev.currentCount, prev.max);
                    }
                    else if (curr.item.Pnl > 0)
                    {
                        prev.currentCount = 0;
                    }
                    return prev;
                }).max;
                
                TimeSpan minOpenProfitPositionDuration = profitTrades.Min(x => x.PositionDuration);
                TimeSpan avgerageOpenProfitPositionDuration = TimeSpan.FromSeconds(profitTrades.Average(x => x.PositionDuration.TotalSeconds));
                TimeSpan maxOpenProfitPositionDuration = profitTrades.Max(x => x.PositionDuration);
                TimeSpan minOpenLossPositionDuration = lossTrades.Min(x => x.PositionDuration);
                TimeSpan avgerageOpenLossPositionDuration = TimeSpan.FromSeconds(lossTrades.Average(x => x.PositionDuration.TotalSeconds));
                TimeSpan maxOpenLossPositionDuration = lossTrades.Max(x => x.PositionDuration);

                decimal minProfit = Math.Round(profitTrades.Min(x => x.Pnl), 2);
                decimal averageProfit = Math.Round(profitTrades.Aggregate(0m, (avg, x) => avg + (x.Pnl / testResults.Count)), 2);
                decimal maxProfit = Math.Round(profitTrades.Max(x => x.Pnl), 2);
                decimal minLoss = Math.Round(lossTrades.Max(x => x.Pnl), 2);
                decimal averageLoss = Math.Round(lossTrades.Aggregate(0m, (avg, x) => avg + (x.Pnl / testResults.Count)), 2);
                decimal maxLoss = Math.Round(lossTrades.Min(x => x.Pnl), 2);

                decimal minProfitPercent = Math.Round(profitTrades.Min(x => x.PnlPercent), 4);
                decimal averageProfitPercent = Math.Round(profitTrades.Aggregate(0m, (avg, x) => avg + (x.PnlPercent / testResults.Count)), 4);
                decimal maxProfitPercent = Math.Round(profitTrades.Max(x => x.PnlPercent), 4);
                decimal minLossPercent = Math.Round(lossTrades.Max(x => x.PnlPercent), 4);
                decimal averageLossPercent = Math.Round(lossTrades.Aggregate(0m, (avg, x) => avg + (x.PnlPercent / testResults.Count)), 4);
                decimal maxLossPercent = Math.Round(lossTrades.Min(x => x.PnlPercent), 4);

                decimal initalBalance = initialQuoteAssetAmount;
                decimal finalBalance = testResults.Last().Balance;
                decimal totalPnl = Math.Round(testResults.Aggregate(0m, (avg, x) => avg + x.Pnl), 2);
                decimal totalPnlPercent = Math.Round(totalPnl / initialQuoteAssetAmount, 4);

                _logger.LogInformation("");
                _logger.LogInformation($"Symbol={assetToTest.Symbol}");
                _logger.LogInformation($"CandlestickInterval={assetToTest.CandlestickInterval}");
                _logger.LogInformation($"Range: From={assetToTest.From.ToUtcString()}, To={assetToTest.To.ToUtcString()}; Timespan={assetToTest.To.Subtract(assetToTest.From).ToString()}");
                _logger.LogInformation($"Timespan={assetToTest.To.Subtract(assetToTest.From).ToString()}");
                _logger.LogInformation($"barsCount={barsCount}");
                _logger.LogInformation($"tradesCount={tradesCount}");
                _logger.LogInformation($"profitTradesCount={profitTradesCount} ({profitTradesPercent}%)");
                _logger.LogInformation($"lossTradesCount={lossTradesCount} ({lossTradesPercent}%)");
                _logger.LogInformation($"closedByStopLossTradesCount={closedByStopLossTradesCount}");
                _logger.LogInformation($"closedByTakeProfitTradesCount={closedByTakeProfitTradesCount}");
                _logger.LogInformation($"closedByDurationTimeoutTradesCount={closedByDurationTimeoutTradesCount} (profit={closedByDurationTimeoutProfitTradesCount}, loss={closedByDurationTimeoutLossTradesCount})");
                _logger.LogInformation($"maxProfitTradesInARow={maxProfitTradesInARow}");
                _logger.LogInformation($"maxLossTradesInARow={maxLossTradesInARow}");
                _logger.LogInformation($"profit position duration=[{minOpenProfitPositionDuration}; {avgerageOpenProfitPositionDuration}; {maxOpenProfitPositionDuration}]");
                _logger.LogInformation($"loss position duration=[{minOpenLossPositionDuration}; {avgerageOpenLossPositionDuration}; {maxOpenLossPositionDuration}]");
                _logger.LogInformation($"");

                _logger.LogInformation($"profit=[{minProfit}; {averageProfit}; {maxProfit}]");
                _logger.LogInformation($"loss=[{minLoss}; {averageLoss}; {maxLoss}]");
                _logger.LogInformation($"");

                _logger.LogInformation($"profit percent=[{minProfitPercent}; {averageProfitPercent}; {maxProfitPercent}]");
                _logger.LogInformation($"loss percent=[{minLossPercent}; {averageLossPercent}; {maxLossPercent}]");
                _logger.LogInformation($"");

                _logger.LogInformation($"initalBalance={initalBalance}");
                _logger.LogInformation($"finalBalance={finalBalance}");
                _logger.LogInformation($"totalPnl={totalPnl}");
                _logger.LogInformation($"totalPnlPercent={totalPnlPercent}");

                _logger.LogInformation($"Symbol={assetToTest.Symbol}. Done.");
                _logger.LogInformation("");
                _logger.LogInformation("----------------------------------------------------");
                _logger.LogInformation("");
            }
        }

        /// <summary>
        /// Custom nested bar strategy.
        /// 1. Current bar is nested bar. Prev bar is covering bar. Nested bar is labeled as nested only after it completes, so the condition only 
        /// becomes true on 3rd bar.
        /// 1.1 Covering bar must be no more that 2x range of nested bar.
        /// 1.2 -
        /// 2. Buy/Sell when high or low price +- 1% of covering bar range is broken.
        /// 3. Stoploss - below covering bar low - 10% of covering bar range, Takeprofit - covering bar high + covering bar range
        /// 4. Close open position after N bars if neither stoploss or takeprofit is hit
        /// </summary>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        private async Task MANUAL_StartCustomNestedBarBreakthroughSimulationAsync(CancellationToken cancellationToken)
        {
            const string strategyName = "CustomNestedBarBreakthrough";

            var assetsToTest = new[]
            {
                new
                {
                    Symbol = "BTCUSDT",
                    From = DateTime.UtcNow.Subtract(TimeSpan.FromDays(3 * 30)),
                    To = DateTime.UtcNow,
                    CandlestickInterval = "4h",
                },
                //new
                //{
                //    Symbol = "IOTAUSDT",
                //    From = DateTime.UtcNow.Subtract(TimeSpan.FromDays(12 * 30)),
                //    To = DateTime.UtcNow,
                //    CandlestickInterval = "4h",
                //},
                // new
                //{
                //    Symbol = "DASHUSDT",
                //    From = DateTime.UtcNow.Subtract(TimeSpan.FromDays(12 * 30)),
                //    To = DateTime.UtcNow,
                //    CandlestickInterval = "4h",
                //},
            };

            foreach (var assetToTest in assetsToTest)
            {
                _logger.LogInformation($"Symbol={assetToTest.Symbol}.");

                var symbolCandlestickHistory = await BinanceLoadSymbolCandlestickHistory(assetToTest.Symbol, assetToTest.From, assetToTest.To, assetToTest.CandlestickInterval);

                // Calculates pnl
                Func<string, decimal, decimal, decimal, decimal, (decimal pnl, decimal pnlPercent, decimal quoteAssetAmountAfter)> calcPositionPnl =
                    (_orderType, _quoteAssetAmountBefore, _openPrice, _closePrice, _makerFeePercent) =>
                    {
                        if (_orderType != "long")
                        {
                            throw new NotImplementedException();
                        }

                        decimal _baseAssetAmount = _quoteAssetAmountBefore / _openPrice;
                        _baseAssetAmount = _baseAssetAmount - _baseAssetAmount * _makerFeePercent; // fee

                        decimal _quoteAssetAmountAfter = _baseAssetAmount * _closePrice;
                        _quoteAssetAmountAfter = _quoteAssetAmountAfter - _quoteAssetAmountAfter * _makerFeePercent; // fee

                        decimal _pnl = _quoteAssetAmountAfter - _quoteAssetAmountBefore;
                        decimal _pnlPercent = Math.Round(_pnl / _quoteAssetAmountBefore, 4);
                        return (pnl: _pnl, pnlPercent: _pnlPercent, quoteAssetAmountAfter: _quoteAssetAmountAfter);
                    };


                // test
                int length = symbolCandlestickHistory.Bars.Count;

                const decimal coveringBarRangeCanBeGreaterThanNestedBarRangePercent = 0.25m;

                const int maxPositionOpenCheckDurationInBars = 5; // how long to wait after open signal was received. when expired - cancel ([bar condition true; current bar])
                const int maxOpenPositionDurationInBars = 12; // how long to keep opened position if neither stoploss and takeprofit was hit ([bar opened; bar when closing])
                decimal maxDrawdownPercent = 0.8m; // from initial balance
                decimal makerFeePercent = 0.0025m;
                decimal? stopLossPercent = null; // null - no stoploss or adaptive stoploss
                decimal? takeProfitPercent = null; // null - no takeprofit or adaptive takeprofit
                decimal? maxStoplossPercent = 0.05m; // for adaptive stoploss only (excluding value)
                decimal? maxTakeprofitPercent = 0.25m; // for adaptive takeprofit only (excluding value)
                decimal initialQuoteAssetAmount = 1000; // in quote asset. E.g. BTCUSDT 1000USDT
                decimal currentQuoteAssetAmount = initialQuoteAssetAmount;

                var testResults = new List<SimpleTradeResultModel>();

                string positionStatus = "NoPosition"; // NoPosition, CheckingPositionOpenCondition, CheckingPositionCloseCondition, 
                decimal longPositionOpenPrice = 0;
                decimal? longPositionStoplossPrice = null;
                decimal? longPositionTakeprofitPrice = null;
                int positionCheckingOpenConditionDurationInBars = 0;
                int positionDurationInBars = 0;

                for (int i = 2; i < length - 1; i++)
                {
                    var beforePrevBar = symbolCandlestickHistory.Bars[i - 2]; // supposed to be covering bar
                    var prevBar = symbolCandlestickHistory.Bars[i - 1]; // supposed to be nested bar
                    var currentBar = symbolCandlestickHistory.Bars[i]; // current bar when condition is checked

                    // check whether current bar is nested bar
                    Func<GeneralBarModel, GeneralBarModel, GeneralBarModel, bool> checkIsNestedBar = (_beforePrevBar, _prevBar, _currentBar) =>
                    {
                        decimal coveringBarRange = _beforePrevBar.HighPrice - _beforePrevBar.LowPrice;
                        decimal nestedBarRange = _prevBar.HighPrice - _prevBar.LowPrice;
                        return _beforePrevBar.HighPrice >= _prevBar.HighPrice &&
                               _beforePrevBar.LowPrice <= _prevBar.LowPrice &&
                               // check covering bar range isn't too big
                               coveringBarRange >= (nestedBarRange + nestedBarRange * coveringBarRangeCanBeGreaterThanNestedBarRangePercent);
                    };

                    if (positionStatus == "NoPosition")
                    {
                        // detect and define position open condition 
                        if (checkIsNestedBar(beforePrevBar, prevBar, currentBar))
                        {
                            decimal coveringBarRange = beforePrevBar.HighPrice - beforePrevBar.LowPrice;
                            decimal coveringBarOpenRangePercent = 0.01m;
                            decimal coveringBarStoplossRangePercent = 0.05m;
                            decimal coveringBarTakeprofitRangePercent = 5m;

                            longPositionOpenPrice = beforePrevBar.HighPrice + coveringBarRange * coveringBarOpenRangePercent;
                            longPositionStoplossPrice = stopLossPercent == null ? beforePrevBar.LowPrice - coveringBarRange * coveringBarStoplossRangePercent : longPositionStoplossPrice;
                            longPositionTakeprofitPrice = takeProfitPercent == null ? longPositionOpenPrice + coveringBarRange * coveringBarTakeprofitRangePercent : takeProfitPercent;

                            if(longPositionStoplossPrice == null)
                            {
                                throw new Exception("Stoploss can't be null!");
                            }
                            if (longPositionTakeprofitPrice == null)
                            {
                                throw new Exception("Takeprofit can't be null!");
                            }

                            // calc and validate adaptive stoploss and takeprofit percents relative to open price
                            decimal currentStoplossPercent = Math.Round(1 - longPositionStoplossPrice.Value / longPositionOpenPrice, 2);
                            decimal currentTakeprofitPercent = Math.Round(1 - longPositionOpenPrice / longPositionTakeprofitPrice.Value, 2);
                            if(maxStoplossPercent != null && currentStoplossPercent > maxStoplossPercent)
                            {
                                _logger.LogWarning($"Stoploss={currentStoplossPercent}%, but can't be greater than {maxStoplossPercent}%!");
                                positionStatus = "NoPosition";
                                positionCheckingOpenConditionDurationInBars = 0;
                                continue;
                            }
                            if (maxTakeprofitPercent != null && currentTakeprofitPercent > maxTakeprofitPercent)
                            {
                                _logger.LogWarning($"Takeprofit={currentTakeprofitPercent}%, but can't be greater than {maxTakeprofitPercent}%!");
                                positionStatus = "NoPosition";
                                positionCheckingOpenConditionDurationInBars = 0;
                                continue;
                            }

                            positionStatus = "CheckingPositionOpenCondition";

                            // count bars while checking for position open condition
                            positionCheckingOpenConditionDurationInBars += 1;

                            continue;
                        }
                    }
                    else if (positionStatus == "CheckingPositionOpenCondition")
                    {
                        // count bars while checking for position open condition
                        positionCheckingOpenConditionDurationInBars += 1;

                        // terminate if waiting too long for particular open signal
                        if (positionCheckingOpenConditionDurationInBars >= maxPositionOpenCheckDurationInBars)
                        {
                            positionStatus = "NoPosition";
                            positionCheckingOpenConditionDurationInBars = 0;
                            continue;
                        }

                        // check for position open condition
                        //if (currentBar.HighPrice >= longPositionOpenPrice)
                        if (currentBar.ClosePrice >= longPositionOpenPrice)
                        {
                            positionStatus = "CheckingPositionCloseCondition";
                            positionCheckingOpenConditionDurationInBars = 0;

                            // process current bar again by different status as it can trigger stoploss or close condition.
                            // i.e. possibility to open and close position during the same bar
                            //i -= 1;

                            // don't process current bar. check stoploss and takeprofit only on next bar
                            i += 0;
                            //// count bars while in opened position
                            //positionDurationInBars += 1;

                            continue;
                        }

                        continue;
                    }
                    else if (positionStatus == "CheckingPositionCloseCondition")
                    {
                        // count bars while in opened position
                        positionDurationInBars += 1;

                        // check for stoploss condition
                        // check for takeprofit condition
                        // check for termination by duration condition
                        decimal longPositionClosePrice = 0;
                        bool isClosed = false;
                        bool isClosedByStopLoss = false;
                        bool isClosedByTakeProfit = false;
                        bool isClosedByDurationTimeout = false;
                        if (longPositionStoplossPrice != null && currentBar.LowPrice <= longPositionStoplossPrice.Value)
                        {
                            // close by stop loss
                            longPositionClosePrice = longPositionStoplossPrice.Value;
                            isClosed = true;
                            isClosedByStopLoss = true;
                        }
                        else if (longPositionTakeprofitPrice != null && currentBar.HighPrice >= longPositionTakeprofitPrice.Value)
                        {
                            // close by take profit
                            longPositionClosePrice = longPositionTakeprofitPrice.Value;
                            isClosed = true;
                            isClosedByTakeProfit = true;
                        }
                        else if (positionDurationInBars >= maxOpenPositionDurationInBars)
                        {
                            // close by duration
                            longPositionClosePrice = currentBar.ClosePrice; // take close time
                            isClosed = true;
                            isClosedByDurationTimeout = true;
                        }

                        // close on first profit bar opening
                        var closePriceTemp1 = currentBar.OpenPrice;
                        var pnlTuple1 = calcPositionPnl("long", currentQuoteAssetAmount, longPositionOpenPrice, closePriceTemp1, makerFeePercent);
                        if (positionDurationInBars == 1 && pnlTuple1.pnl > 0)
                        {
                            // close on first profit bar opening
                            _logger.LogInformation($"+++ Close on first profit bar opening. +{pnlTuple1.pnl}");
                            longPositionClosePrice = currentBar.OpenPrice;
                            isClosed = true;
                            isClosedByDurationTimeout = true;
                        }
                        else
                        {
                            // calc current PnL
                            var closePriceTemp0 = currentBar.ClosePrice;
                            var pnlTuple0 = calcPositionPnl("long", currentQuoteAssetAmount, longPositionOpenPrice, closePriceTemp0, makerFeePercent);
                            _logger.LogInformation($"=== current pnl={Math.Round(pnlTuple0.pnl, 2)} ({pnlTuple0.pnlPercent}%)");
                        }

                        if (isClosed)
                        {
                            _logger.LogInformation($"=== CLOSED");
                            _logger.LogInformation($"===");
                        }

                        // position closed
                        if (isClosed)
                        {
                            // calc PnL
                            var pnlTuple = calcPositionPnl("long", currentQuoteAssetAmount, longPositionOpenPrice, longPositionClosePrice, makerFeePercent);
                            currentQuoteAssetAmount = pnlTuple.quoteAssetAmountAfter;

                            var candlestickIntervalConfig = BinanceConfig.GetBarChartInterval(assetToTest.CandlestickInterval);

                            var result = new SimpleTradeResultModel
                            {
                                Balance = currentQuoteAssetAmount,
                                Pnl = pnlTuple.pnl,
                                PnlPercent = pnlTuple.pnlPercent,
                                IsClosedByStopLoss = isClosedByStopLoss,
                                IsClosedByTakeProfit = isClosedByTakeProfit,
                                IsClosedByDurationTimeout = isClosedByDurationTimeout,
                                PositionDuration = positionDurationInBars * candlestickIntervalConfig.TimeSpan,
                            };
                            testResults.Add(result);

                            positionStatus = "NoPosition";
                            positionCheckingOpenConditionDurationInBars = 0;
                            positionDurationInBars = 0;

                            // check drowdown
                            decimal drowdownPercent = 1 - result.Balance / initialQuoteAssetAmount;
                            if (drowdownPercent >= maxDrawdownPercent)
                            {
                                _logger.LogInformation($"Reached max drowdown of {Math.Round(maxDrawdownPercent * 100, 2)}%.");
                                break;
                            }
                        }
                    }
                }

                // add placeholders for profit and loss trade
                if (!testResults.Any(x => x.Pnl > 0))
                {
                    testResults.Add(new SimpleTradeResultModel()
                    {
                        Pnl = 0.001m,
                        PnlPercent = 0.00001m,
                    });
                }
                if (!testResults.Any(x => x.Pnl <= 0))
                {
                    testResults.Add(new SimpleTradeResultModel()
                    {
                        Pnl = -0.001m,
                        PnlPercent = -0.00001m,
                    });
                }

                var profitTrades = testResults.Where(x => x.Pnl > 0);
                var lossTrades = testResults.Where(x => x.Pnl <= 0);

                // stats
                int barsCount = symbolCandlestickHistory.Bars.Count;
                int tradesCount = testResults.Count;
                int profitTradesCount = profitTrades.Count();
                int lossTradesCount = lossTrades.Count();
                
                decimal profitTradesPercent = Math.Round((decimal)profitTradesCount / (decimal)tradesCount, 2);
                decimal lossTradesPercent = Math.Round((decimal)lossTradesCount / (decimal)tradesCount, 2);
                
                int closedByStopLossTradesCount = testResults.Count(x => x.IsClosedByStopLoss);
                int closedByTakeProfitTradesCount = testResults.Count(x => x.IsClosedByTakeProfit);
                int closedByDurationTimeoutTradesCount = testResults.Count(x => x.IsClosedByDurationTimeout);
                int closedByDurationTimeoutProfitTradesCount = profitTrades.Count(x => x.IsClosedByDurationTimeout);
                int closedByDurationTimeoutLossTradesCount = lossTrades.Count(x => x.IsClosedByDurationTimeout);
                
                int maxProfitTradesInARow = testResults.Select(x => (item: x, currentCount: 0, max: 0)).Aggregate((prev, curr) =>
                {
                    if (curr.item.Pnl > 0)
                    {
                        prev.currentCount += 1;
                        prev.max = Math.Max(prev.currentCount, prev.max);
                    }
                    else if (curr.item.Pnl <= 0)
                    {
                        prev.currentCount = 0;
                    }
                    return prev;
                }).max;
                int maxLossTradesInARow = testResults.Select(x => (item: x, currentCount: 0, max: 0)).Aggregate((prev, curr) =>
                {
                    if (curr.item.Pnl <= 0)
                    {
                        prev.currentCount += 1;
                        prev.max = Math.Max(prev.currentCount, prev.max);
                    }
                    else if (curr.item.Pnl > 0)
                    {
                        prev.currentCount = 0;
                    }
                    return prev;
                }).max;

                TimeSpan minOpenProfitPositionDuration = profitTrades.Min(x => x.PositionDuration);
                TimeSpan avgerageOpenProfitPositionDuration = TimeSpan.FromSeconds(profitTrades.Average(x => x.PositionDuration.TotalSeconds));
                TimeSpan maxOpenProfitPositionDuration = profitTrades.Max(x => x.PositionDuration);
                TimeSpan minOpenLossPositionDuration = lossTrades.Min(x => x.PositionDuration);
                TimeSpan avgerageOpenLossPositionDuration = TimeSpan.FromSeconds(lossTrades.Average(x => x.PositionDuration.TotalSeconds));
                TimeSpan maxOpenLossPositionDuration = lossTrades.Max(x => x.PositionDuration);

                decimal minProfit = Math.Round(profitTrades.Min(x => x.Pnl), 2);
                decimal averageProfit = Math.Round(profitTrades.Aggregate(0m, (avg, x) => avg + (x.Pnl / testResults.Count)), 2);
                decimal maxProfit = Math.Round(profitTrades.Max(x => x.Pnl), 2);
                decimal minLoss = Math.Round(lossTrades.Max(x => x.Pnl), 2);
                decimal averageLoss = Math.Round(lossTrades.Aggregate(0m, (avg, x) => avg + (x.Pnl / testResults.Count)), 2);
                decimal maxLoss = Math.Round(lossTrades.Min(x => x.Pnl), 2);

                decimal minProfitPercent = Math.Round(profitTrades.Min(x => x.PnlPercent), 4);
                decimal averageProfitPercent = Math.Round(profitTrades.Aggregate(0m, (avg, x) => avg + (x.PnlPercent / testResults.Count)), 4);
                decimal maxProfitPercent = Math.Round(profitTrades.Max(x => x.PnlPercent), 4);
                decimal minLossPercent = Math.Round(lossTrades.Max(x => x.PnlPercent), 4);
                decimal averageLossPercent = Math.Round(lossTrades.Aggregate(0m, (avg, x) => avg + (x.PnlPercent / testResults.Count)), 4);
                decimal maxLossPercent = Math.Round(lossTrades.Min(x => x.PnlPercent), 4);

                decimal initalBalance = initialQuoteAssetAmount;
                decimal finalBalance = testResults.Last().Balance;
                decimal totalPnl = Math.Round(testResults.Aggregate(0m, (avg, x) => avg + x.Pnl), 2);
                decimal totalPnlPercent = Math.Round(totalPnl / initialQuoteAssetAmount, 4);

                _logger.LogInformation("");
                _logger.LogInformation($"Strategy={strategyName}");
                _logger.LogInformation($"Symbol={assetToTest.Symbol}");
                _logger.LogInformation($"CandlestickInterval={assetToTest.CandlestickInterval}");
                _logger.LogInformation($"Range: From={assetToTest.From.ToUtcString()}, To={assetToTest.To.ToUtcString()}; Timespan={assetToTest.To.Subtract(assetToTest.From).ToString()}");
                _logger.LogInformation($"Timespan={assetToTest.To.Subtract(assetToTest.From).ToString()}");
                _logger.LogInformation($"barsCount={barsCount}");
                _logger.LogInformation($"tradesCount={tradesCount}");
                _logger.LogInformation($"profitTradesCount={profitTradesCount} ({profitTradesPercent}%)");
                _logger.LogInformation($"lossTradesCount={lossTradesCount} ({lossTradesPercent}%)");
                _logger.LogInformation($"closedByStopLossTradesCount={closedByStopLossTradesCount}");
                _logger.LogInformation($"closedByTakeProfitTradesCount={closedByTakeProfitTradesCount}");
                _logger.LogInformation($"closedByDurationTimeoutTradesCount={closedByDurationTimeoutTradesCount} (profit={closedByDurationTimeoutProfitTradesCount}, loss={closedByDurationTimeoutLossTradesCount})");
                _logger.LogInformation($"maxProfitTradesInARow={maxProfitTradesInARow}");
                _logger.LogInformation($"maxLossTradesInARow={maxLossTradesInARow}");
                _logger.LogInformation($"profit position duration=[{minOpenProfitPositionDuration}; {avgerageOpenProfitPositionDuration}; {maxOpenProfitPositionDuration}]");
                _logger.LogInformation($"loss position duration=[{minOpenLossPositionDuration}; {avgerageOpenLossPositionDuration}; {maxOpenLossPositionDuration}]");
                _logger.LogInformation($"");

                _logger.LogInformation($"profit=[{minProfit}; {averageProfit}; {maxProfit}]");
                _logger.LogInformation($"loss=[{minLoss}; {averageLoss}; {maxLoss}]");
                _logger.LogInformation($"");

                _logger.LogInformation($"profit percent=[{minProfitPercent}; {averageProfitPercent}; {maxProfitPercent}]");
                _logger.LogInformation($"loss percent=[{minLossPercent}; {averageLossPercent}; {maxLossPercent}]");
                _logger.LogInformation($"");

                _logger.LogInformation($"initalBalance={initalBalance}");
                _logger.LogInformation($"finalBalance={finalBalance}");
                _logger.LogInformation($"totalPnl={totalPnl}");
                _logger.LogInformation($"totalPnlPercent={totalPnlPercent}");

                _logger.LogInformation($"Symbol={assetToTest.Symbol}. Done.");
                _logger.LogInformation("");
                _logger.LogInformation("----------------------------------------------------");
                _logger.LogInformation("");
            }
        }

        private async Task StartCustomNestedBarBreakthroughSimulationAsync(CancellationToken cancellationToken)
        {
            // finds covering bar in prev N bars
            // all subsequent bars must be in a range of the nested bar
            Func<List<GeneralBarModel>, int, int, (int coveringBarIndex, GeneralBarModel coveringBar)> findCoveringBarInPrevNBars = (_bars, _currentBarIndex, _n) =>
            {
                const decimal coveringBarRangeCanBeGreaterThanNestedBarRangePercent = 0.25m;

                int startIndex = Math.Max(_currentBarIndex - _n, 1); // must be at least 1 bar before
                GeneralBarModel coveringBar = null;
                int coveringBarIndex = -1;
                for (int i = startIndex; i < _currentBarIndex; i++)
                {
                    var _prevBar = _bars[i - 1];
                    var _currentBar = _bars[i];

                    if (coveringBar == null)
                    {
                        coveringBar = _prevBar; // assume that covering
                        coveringBarIndex = i - 1;
                    }

                    decimal coveringBarRange = coveringBar.HighPrice - coveringBar.LowPrice;
                    decimal nestedBarRange = _currentBar.HighPrice - _currentBar.LowPrice;

                    bool isNestedBar = coveringBar.HighPrice >= _currentBar.HighPrice &&
                           coveringBar.LowPrice <= _currentBar.LowPrice &&
                           // check covering bar range isn't too big
                           coveringBarRange >= (nestedBarRange + nestedBarRange * coveringBarRangeCanBeGreaterThanNestedBarRangePercent);

                    if (!isNestedBar)
                    {
                        coveringBar = null;
                        coveringBarIndex = -1;
                    }
                }

                return (coveringBarIndex: coveringBarIndex, coveringBar: coveringBar);
            };

            // checks whether there is nested bar in prev N bars
            // all subsequent bars must be in a range of the nested bar
            Func<List<GeneralBarModel>, int, int, bool> checkIsNestedBarInPrevNBars = (_bars, _currentBarIndex, _n) =>
            {
                var (coveringBarIndex, coveringBar) = findCoveringBarInPrevNBars(_bars, _currentBarIndex, _n);
                return coveringBar != null;
            };

            const int nPrevBarsToSearchCoveringBar = 12; // 2 days

            var strategyRunner = _serviceProvider.GetRequiredService<StrategyRunnerService>();
            var marketHistoryDataSource = _serviceProvider.GetRequiredService<IMarketHistoryDataSource>();
            strategyRunner
                .WithSettings(new StrategyRunnerSettings()
                {
                    StrategyName = "CustomNestedBarBreakthrough",
                    AssetsToTest = new List<AssetToTestSettings>()
                    {
                        new AssetToTestSettings()
                        {
                            Symbol = "BTCUSDT",
                            From = DateTime.UtcNow.Subtract(TimeSpan.FromDays(3 * 30)),
                            To = DateTime.UtcNow,
                            BarChartIntervalConfig = BinanceConfig.GetBarChartInterval("4h"),
                        },
                        //new AssetToTestSettings()
                        //{
                        //    Symbol = "IOTAUSDT",
                        //    From = DateTime.UtcNow.Subtract(TimeSpan.FromDays(12 * 30)),
                        //    To = DateTime.UtcNow,
                        //    BarChartIntervalConfig = BinanceConfig.GetBarChartInterval("4h"),
                        //},
                        //new AssetToTestSettings()
                        //{
                        //    Symbol = "DASHUSDT",
                        //    From = DateTime.UtcNow.Subtract(TimeSpan.FromDays(12 * 30)),
                        //    To = DateTime.UtcNow,
                        //    BarChartIntervalConfig = BinanceConfig.GetBarChartInterval("4h"),
                        //},
                    },
                    OrderDirections = new List<OrderDirection>()
                    {
                        OrderDirection.Long,
                    },
                    OrderTypes = new List<OrderType>()
                    {
                        OrderType.Market,
                    },
                    // assume Bittrex with high fees
                    MakerFeePercent = 0.0025m,
                    TakerFeePercent = 0.0025m,

                    MaxDrawdownPercent = 0.5m,
                    MaxStoplossPercent = 0.05m,
                    MaxTakeprofitPercent = 0.25m,
                    InitialBalance = 1000,
                    BalancePerTradePercent = 1m,
                    StartBarIndex = 2,
                })
                .WithMarketHistoryDataSource(marketHistoryDataSource)
                .WithStrategyDefinition(new StrategyDefinitionModel()
                {
                    MaxPositionOpeningWaitDurationInBars = 5,
                    MaxOpenPositionDurationInBars = 12,
                    IsLogIntermediateResults = true,
                    PositionOpeningPredicate = (bars, currentBarIndex, currentBar) =>
                    {
                        if (currentBarIndex < 2)
                        {
                            return false;
                        }

                        var beforePrevBar = bars[currentBarIndex - 2]; // supposed to be covering bar
                        var prevBar = bars[currentBarIndex - 1]; // supposed to be nested bar

                        // prev 2 bars form nested bar pattern
                        bool isPrecedingNestedBar = checkIsNestedBarInPrevNBars(bars, currentBarIndex, nPrevBarsToSearchCoveringBar);

                        // current bar closed higher/lower covering bar high/low
                        // TODO - SHORT
                        const decimal coveringBarOpenRangePercent = 0.01m;
                        decimal coveringBarRange = beforePrevBar.HighPrice - beforePrevBar.LowPrice;
                        bool isCurrentClosedHigher = currentBar.ClosePrice > beforePrevBar.HighPrice + coveringBarRange * coveringBarOpenRangePercent;

                        return isPrecedingNestedBar && isCurrentClosedHigher;
                    },
                    PositionOpeningDetailsGetter = (bars, currentBarIndex, currentBar) =>
                    {
                        var (coveringBarIndex, coveringBar) = findCoveringBarInPrevNBars(bars, currentBarIndex, nPrevBarsToSearchCoveringBar);

                        const decimal coveringBarStoplossRangePercent = 0.05m;
                        const decimal coveringBarTakeprofitRangePercent = 5m;
                        decimal coveringBarRange = coveringBar.HighPrice - coveringBar.LowPrice;

                        // open price - current bar close price
                        decimal longPositionOpenPrice = currentBar.ClosePrice;
                        decimal longPositionStoplossPrice = coveringBar.LowPrice - coveringBarRange * coveringBarStoplossRangePercent;
                        decimal longPositionTakeprofitPrice = coveringBar.HighPrice + coveringBarRange * coveringBarTakeprofitRangePercent;

                        return new PositionOpeningDetailsModel()
                        {
                            OrderDirection = OrderDirection.Long,
                            OrderType = OrderType.Market,
                            PositionOpenTriggerPriceType = BarPriceType.Close,
                            OpenPrice = longPositionOpenPrice,
                            StoplossPrice = longPositionStoplossPrice,
                            TakeprofitPrice = longPositionTakeprofitPrice,
                        };
                    },
                    PositionEarlyCloseFunc = (bars, currentBarIndex, currentBar, currentPosition, calcCurrentPnlFunc) =>
                    {
                        // use bailout exit strategy - close on first profit bar opening
                        // TODO - recheck this as I enter postion on bar close what is similar to next bar open.
                        // TODO - looks works as not expected
                        //if (currentPosition.PositionDurationInBars == 1)
                        //{
                        //    decimal closePrice = currentBar.OpenPrice;
                        //    var pnlModel = calcCurrentPnlFunc(closePrice);
                        //    if (pnlModel.Pnl > 0)
                        //    {
                        //        return new PositionEarlyCloseDetailsModel()
                        //        {
                        //            ClosePrice = closePrice,
                        //        };
                        //    }
                        //}
                        return null;
                    },
                })
                ;

            var strategyRunSummaries = await strategyRunner.RunAsync();

            // TODO - analyse each trade balance changes from open to close to determine likelyhood of
            // - loss postion goes profit
            // - profit position goes loss
            // - profit position with pnl % > N goes profit or loss

            // log summaries
            foreach (var strategyRunSummary in strategyRunSummaries)
            {
                strategyRunSummary.PrintOutSummary(_logger);
            }
        }
    }
}
