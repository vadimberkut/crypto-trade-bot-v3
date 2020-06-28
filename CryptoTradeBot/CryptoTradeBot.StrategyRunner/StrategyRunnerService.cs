using CryptoTradeBot.Infrastructure.Enums;
using CryptoTradeBot.Infrastructure.Models;
using CryptoTradeBot.StrategyRunner.Enums;
using CryptoTradeBot.StrategyRunner.Interfaces;
using CryptoTradeBot.StrategyRunner.Models;
using CryptoTradeBot.StrategyRunner.Settings;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CryptoTradeBot.StrategyRunner
{
    public class StrategyRunnerService
    {
        private readonly ILogger<StrategyRunnerService> _logger;
        private StrategyRunnerSettings _settings;
        private IMarketHistoryDataSource _marketHistoryDataSource;
        private StrategyDefinitionModel _strategyDefinition;

        private StrategyRunnerStatus _status = StrategyRunnerStatus.NoOpenedPosition;
        private decimal _currentBalance = 0;
        private CurrentPositionModel _currentPosition = null;

        public StrategyRunnerService
        (
            ILogger<StrategyRunnerService> logger
        )
        {
            _logger = logger;
        }


        #region Fluent Api

        public StrategyRunnerService WithSettings(StrategyRunnerSettings settings)
        {
            _settings = settings;
            return this;
        }

        public StrategyRunnerService WithMarketHistoryDataSource(IMarketHistoryDataSource marketHistoryDataSource)
        {
            _marketHistoryDataSource = marketHistoryDataSource;
            return this;
        }

        public StrategyRunnerService WithStrategyDefinition(StrategyDefinitionModel strategyDefinition)
        {
            _strategyDefinition = strategyDefinition;
            return this;
        }

        #endregion


        /// <summary>
        /// Runs strategy
        /// </summary>
        public async Task<IEnumerable<StrategyRunSummaryModel>> RunAsync()
        {
            // validation
            if(
                _settings == null ||
                _marketHistoryDataSource == null ||
                _strategyDefinition == null
            )
            {
                throw new Exception("Check you configured Strategy Runner properly with Fluent API!");
            }

            var runSummary = new List<StrategyRunSummaryModel>();

            foreach (var assetToTest in _settings.AssetsToTest)
            {
                // load market data
                var marketHistoryData = await _marketHistoryDataSource.GetSymbolHistoryDataAsync(
                    assetToTest.Symbol, 
                    assetToTest.BarChartIntervalConfig.Name, 
                    assetToTest.From,
                    assetToTest.To
                );
                var bars = marketHistoryData.Bars;

                // reset
                _status = StrategyRunnerStatus.NoOpenedPosition;
                _currentBalance = _settings.InitialBalance;
                _currentPosition = null;

                var results = new List<SimpleTradeResultModel>();
                int positionWaitOpeningDurationInBars = 0;
                PositionOpeningDetailsModel positionOpeningDetails = null;

                for (int currentBarIndex = _settings.StartBarIndex.GetValueOrDefault(0); currentBarIndex < bars.Count - 1; currentBarIndex++)
                {
                    var currentBar = bars[currentBarIndex];

                    if (_status == StrategyRunnerStatus.NoOpenedPosition)
                    {
                        // check for position opening condition
                        if (_strategyDefinition.PositionOpeningPredicate(bars, currentBarIndex, currentBar))
                        {
                            // start waiting for position opening condition trigger
                            positionOpeningDetails = _strategyDefinition.PositionOpeningDetailsGetter(bars, currentBarIndex, currentBar);

                            // calc and validate stoploss and takeprofit percents relative to open price
                            // TODO: make it work for SHORT
                            if(positionOpeningDetails.StoplossPrice != null)
                            {
                                decimal currentStoplossPercent = Math.Round(1 - positionOpeningDetails.StoplossPrice.Value / positionOpeningDetails.OpenPrice, 2);
                                if (_settings.MaxStoplossPercent != null && currentStoplossPercent > _settings.MaxStoplossPercent.Value)
                                {
                                    if(_strategyDefinition.IsLogIntermediateResults)
                                    {
                                        _logger.LogWarning($"Stoploss={currentStoplossPercent}%, but can't be greater than {_settings.MaxStoplossPercent.Value}%!");
                                    }
                                    _status = StrategyRunnerStatus.NoOpenedPosition;
                                    positionWaitOpeningDurationInBars = 0;
                                    positionOpeningDetails = null;
                                    continue;
                                }
                            }
                            if(positionOpeningDetails.TakeprofitPrice != null)
                            {
                                decimal currentTakeprofitPercent = Math.Round(1 - positionOpeningDetails.OpenPrice / positionOpeningDetails.TakeprofitPrice.Value, 2);
                                if (_settings.MaxTakeprofitPercent != null && currentTakeprofitPercent > _settings.MaxTakeprofitPercent.Value)
                                {
                                    if (_strategyDefinition.IsLogIntermediateResults)
                                    {
                                        _logger.LogWarning($"Takeprofit={currentTakeprofitPercent}%, but can't be greater than {_settings.MaxTakeprofitPercent.Value}%!");
                                    }
                                    _status = StrategyRunnerStatus.NoOpenedPosition;
                                    positionWaitOpeningDurationInBars = 0;
                                    positionOpeningDetails = null;
                                    continue;
                                }
                            }
                           

                            _status = StrategyRunnerStatus.WaitingPositionOpening;
                            positionWaitOpeningDurationInBars = 0;

                            continue;
                        }

                        continue;
                    }
                    else if (_status == StrategyRunnerStatus.WaitingPositionOpening)
                    {
                        // count bars while checking for position open condition
                        positionWaitOpeningDurationInBars += 1;

                        // terminate if waiting too long for particular open signal
                        if (positionWaitOpeningDurationInBars >= _strategyDefinition.MaxPositionOpeningWaitDurationInBars)
                        {
                            _status = StrategyRunnerStatus.NoOpenedPosition;
                            positionWaitOpeningDurationInBars = 0;
                            positionOpeningDetails = null;
                            continue;
                        }

                        // check open price reached
                        // TODO - SHORT
                        decimal positionOpenTriggerPrice = currentBar.GetPriceByType(positionOpeningDetails.PositionOpenTriggerPriceType);
                        if (positionOpenTriggerPrice >= positionOpeningDetails.OpenPrice)
                        {
                            // position opened
                            _status = StrategyRunnerStatus.OpenedPosition;
                            positionWaitOpeningDurationInBars = 0;

                            // use specified % of balance in trades
                            decimal balanceForPosition = _currentBalance;
                            if (_settings.BalancePerTradePercent != null)
                            {
                                balanceForPosition = _currentBalance * _settings.BalancePerTradePercent.Value;
                            }

                            _currentPosition = new CurrentPositionModel()
                            {
                                OrderDirection = positionOpeningDetails.OrderDirection,
                                OrderType = positionOpeningDetails.OrderType,
                                QuoteAssetAmountBefore = balanceForPosition,
                                OpenPrice = positionOpeningDetails.OpenPrice,
                                StoplossPrice = positionOpeningDetails.StoplossPrice,
                                TakeprofitPrice = positionOpeningDetails.TakeprofitPrice,
                                PositionDurationInBars = 0,
                                PositionOpenBarIndex = currentBarIndex,
                                PositionOpenBar = currentBar,
                                IntermediateResults = new List<PositionPnlModel>(),
                            };

                            // don't process current bar. check stoploss and takeprofit only on next bar
                            currentBarIndex += 0;

                            continue;
                        }

                        continue;
                    }
                    else if (_status == StrategyRunnerStatus.OpenedPosition)
                    {
                        // count bars while in opened position
                        _currentPosition.PositionDurationInBars += 1;

                        // check for stoploss condition
                        // check for takeprofit condition
                        // check for termination by duration condition
                        decimal? longPositionClosePrice = null;
                        bool isClosed = false;
                        bool isClosedByStopLoss = false;
                        bool isClosedByTakeProfit = false;
                        bool isClosedByDurationTimeout = false;
                        bool isClosedByEarlyClose = false;
                        if (positionOpeningDetails.StoplossPrice != null && currentBar.LowPrice <= positionOpeningDetails.StoplossPrice.Value)
                        {
                            // close by stop loss
                            longPositionClosePrice = positionOpeningDetails.StoplossPrice.Value;
                            isClosed = true;
                            isClosedByStopLoss = true;
                        }
                        else if (positionOpeningDetails.TakeprofitPrice != null && currentBar.HighPrice >= positionOpeningDetails.TakeprofitPrice.Value)
                        {
                            // close by take profit
                            longPositionClosePrice = positionOpeningDetails.TakeprofitPrice.Value;
                            isClosed = true;
                            isClosedByTakeProfit = true;
                        }
                        else if (_strategyDefinition.MaxOpenPositionDurationInBars != null && _currentPosition.PositionDurationInBars >= _strategyDefinition.MaxOpenPositionDurationInBars.Value)
                        {
                            // close by duration
                            longPositionClosePrice = currentBar.ClosePrice;
                            isClosed = true;
                            isClosedByDurationTimeout = true;
                        }
                        else if(_strategyDefinition.PositionEarlyCloseFunc != null)
                        {
                            // early close with custom condition
                            var earlyCloseModel = _strategyDefinition.PositionEarlyCloseFunc(bars, currentBarIndex, currentBar, _currentPosition, CalcCurrentPositionPnl);
                            if (earlyCloseModel != null)
                            {
                                longPositionClosePrice = earlyCloseModel.ClosePrice;
                                isClosed = true;
                                isClosedByEarlyClose = true;
                            }
                        }

                        // calc pnl
                        if(longPositionClosePrice == null)
                        {
                            longPositionClosePrice = currentBar.ClosePrice;
                        }
                        var pnlModel = this.CalcCurrentPositionPnl(longPositionClosePrice.Value);

                        // save intermediate results
                        _currentPosition.IntermediateResults.Add(pnlModel);

                        // log
                        if (_strategyDefinition.IsLogIntermediateResults)
                        {
                            _logger.LogInformation($"=== Current pnl={Math.Round(pnlModel.Pnl, 2)} ({pnlModel.PnlPercent}%).");
                            if (isClosedByStopLoss)
                            {
                                _logger.LogInformation($"=== Closing by stoploss.");
                            }
                            if (isClosedByTakeProfit)
                            {
                                _logger.LogInformation($"=== Closing by takeptofit.");
                            }
                            if (isClosedByDurationTimeout)
                            {
                                _logger.LogInformation($"=== Closing by duration timeout.");
                            }
                            if (isClosedByEarlyClose)
                            {
                                _logger.LogInformation($"=== Closing by early close.");
                            }
                            if (isClosed)
                            {
                                _logger.LogInformation($"=== Position CLOSED");
                                _logger.LogInformation(string.Empty);
                            }
                        }

                        // position closed
                        if (isClosed)
                        {
                            // update balance
                            //_currentBalance = pnlModel.QuoteAssetAmountAfter;
                            _currentBalance += pnlModel.Pnl;


                            var result = new SimpleTradeResultModel
                            {
                                BalanceBefore = pnlModel.QuoteAssetAmountBefore,
                                Balance = _currentBalance,
                                Pnl = pnlModel.Pnl,
                                PnlPercent = pnlModel.PnlPercent,
                                IsClosedByStopLoss = isClosedByStopLoss,
                                IsClosedByTakeProfit = isClosedByTakeProfit,
                                IsClosedByDurationTimeout = isClosedByDurationTimeout,
                                IsClosedByEarlyClose = isClosedByEarlyClose,
                                PositionDurationInBars = _currentPosition.PositionDurationInBars,
                                PositionDuration = _currentPosition.PositionDurationInBars * assetToTest.BarChartIntervalConfig.TimeSpan,
                                OrderDirection = _currentPosition.OrderDirection,
                                OpenOrderType = _currentPosition.OrderType,
                                CloseOrderType = OrderType.Market, // TODO
                                OpenPrice = _currentPosition.OpenPrice,
                                ClosePrice = longPositionClosePrice.Value,
                                OpenedAt = _currentPosition.PositionOpenBar.OpenTime,
                                ClosedAt = currentBar.CloseTime,
                                IntermediateResults = _currentPosition.IntermediateResults,
                            };
                            results.Add(result);

                            _status = StrategyRunnerStatus.NoOpenedPosition;

                            // check drawdown
                            if(_settings.MaxDrawdownPercent != null)
                            {
                                decimal drawdownPercent = 1 - result.Balance / _settings.InitialBalance;
                                if (drawdownPercent >= _settings.MaxDrawdownPercent)
                                {
                                    _logger.LogInformation($"Reached max drawdown of {Math.Round(_settings.MaxDrawdownPercent.Value * 100, 2)}%.");
                                    break;
                                }
                            }
                        }
                    }
                }

                runSummary.Add(new StrategyRunSummaryModel(
                    _settings,
                    _strategyDefinition,
                    assetToTest,
                    results,
                    bars.Count
                ));
            }

            return runSummary;
        }

        /// <summary>
        /// Returns insights for strategy run summaries
        /// </summary>
        public void GetInsights(IEnumerable<StrategyRunSummaryModel> strategyRunSummaries)
        {
            foreach (var strategyRunSummary in strategyRunSummaries)
            {
                const int firstN = 2;
                const int minIntermediateN = 2;
                const int lastN = 1;

                int minIntermediateResults1 = firstN + lastN;
                int minIntermediateResults2 = firstN + minIntermediateN + lastN;

                const int veryFastPositionMaxDurationInBars = 2;

                // (-+) loss position (at the start) goes profit (at the close)
                int lossPositionGoesProfitCount = strategyRunSummary.Results
                    .Where(x => x.IntermediateResults.Count >= minIntermediateResults1)
                    .Count(x => x.IntermediateResults.Take(firstN).Any(y => y.IsLoss) && x.IntermediateResults.TakeLast(lastN).Any(y => y.IsProfit));

                // (+-) profit position (at the start) goes loss (at the close)
                int profitPositionGoesLossCount = strategyRunSummary.Results
                    .Where(x => x.IntermediateResults.Count >= minIntermediateResults1)
                    .Count(x => x.IntermediateResults.Take(firstN).Any(y => y.IsProfit) && x.IntermediateResults.TakeLast(lastN).Any(y => y.IsLoss));

                // (-+-) loss postion goes profit and then back loss
                int lossPositionGoestProfitAndThenBackLossCount = strategyRunSummary.Results
                   .Where(x => x.IntermediateResults.Count >= minIntermediateResults2)
                   .Count(x => x.IntermediateResults.Take(firstN).Any(y => y.IsLoss) && 
                               x.IntermediateResults.Skip(firstN).SkipLast(lastN).Any(y => y.IsProfit) &&
                               x.IntermediateResults.TakeLast(lastN).Any(y => y.IsLoss)
                   );

                // (+-+) profit postion goes loss and then back profit
                int profitPositionGoestLossAndThenBackProfitCount = strategyRunSummary.Results
                   .Where(x => x.IntermediateResults.Count >= minIntermediateResults2)
                   .Count(x => x.IntermediateResults.Take(firstN).Any(y => y.IsProfit) &&
                               x.IntermediateResults.Skip(firstN).SkipLast(lastN).Any(y => y.IsLoss) &&
                               x.IntermediateResults.TakeLast(lastN).Any(y => y.IsProfit)
                   );

                // postion closed by takeproft very fast
                int positionClosedByTakeprofitVeryFast = strategyRunSummary.Results
                    .Count(x => x.IsClosedByTakeProfit && x.PositionDurationInBars <= veryFastPositionMaxDurationInBars);

                // postion closed by stoploss very fast
                int positionClosedByStoplossVeryFast = strategyRunSummary.Results
                    .Count(x => x.IsClosedByStopLoss && x.PositionDurationInBars <= veryFastPositionMaxDurationInBars);

                // ===============================================================================================================================
                var profitMaxPercents = new (decimal, decimal)[] 
                {
                    (0m, 0.01m),
                    (0.01m, 0.02m),
                    (0.02m, 0.03m),
                    (0.03m, 0.04m),
                    (0.04m, 0.05m),
                    (0.05m, 0.06m),
                    (0.06m, 0.07m),
                };
                var lossMaxPercents = profitMaxPercents.Select(x => (x.Item1 == 0 ? 0 : x.Item1 * -1, x.Item2 == 0 ? 0 : x.Item2 * -1)).ToArray();

                // profit position at the start with pnl % > N goes profit
                var profitPositionWithPercentPercentGoesProfitCount = profitMaxPercents.Select(percent =>
                {
                    int count = strategyRunSummary.Results
                       .Where(x => x.IntermediateResults.Count >= minIntermediateResults1)
                       .Count(x => x.IntermediateResults.Take(firstN).Any(y => y.IsProfit && y.PnlPercent >= percent.Item1 && y.PnlPercent < percent.Item2) &&
                                   x.IntermediateResults.TakeLast(lastN).Any(y => y.IsProfit)
                       );
                    return new { Percent = percent, Count = count, };
                }).ToList();
                
                // profit position at the start with pnl % > N goes loss
                var profitPositionWithPercentPercentGoesLossCount = profitMaxPercents.Select(percent =>
                {
                    int count = strategyRunSummary.Results
                       .Where(x => x.IntermediateResults.Count >= minIntermediateResults1)
                       .Count(x => x.IntermediateResults.Take(firstN).Any(y => y.IsProfit && y.PnlPercent >= percent.Item1 && y.PnlPercent < percent.Item2) &&
                                   x.IntermediateResults.TakeLast(lastN).Any(y => y.IsLoss)
                       );
                    return new { Percent = percent, Count = count, };
                }).ToList();

                // loss position at the start with pnl % < N goes loss
                var lossPositionWithPercentPercentGoesLossCount = lossMaxPercents.Select(percent =>
                {
                    int count = strategyRunSummary.Results
                       .Where(x => x.IntermediateResults.Count >= minIntermediateResults1)
                       .Count(x => x.IntermediateResults.Take(firstN).Any(y => y.IsLoss && y.PnlPercent <= percent.Item1 && y.PnlPercent > percent.Item2) &&
                                   x.IntermediateResults.TakeLast(lastN).Any(y => y.IsLoss)
                       );
                    return new { Percent = percent, Count = count, };
                }).ToList();

                // loss position at the start with pnl % < N goes profit
                var lossPositionWithPercentPercentGoesProfitCount = lossMaxPercents.Select(percent =>
                {
                    int count = strategyRunSummary.Results
                       .Where(x => x.IntermediateResults.Count >= minIntermediateResults1)
                       .Count(x => x.IntermediateResults.Take(firstN).Any(y => y.IsLoss && y.PnlPercent <= percent.Item1 && y.PnlPercent > percent.Item2) &&
                                   x.IntermediateResults.TakeLast(lastN).Any(y => y.IsProfit)
                       );
                    return new { Percent = percent, Count = count, };
                }).ToList();

            }
        }

        #region Private

        /// <summary>
        /// Calculates pnl for provided position
        /// </summary>
        private PositionPnlModel CalcPositionPnl(CurrentPositionModel currentPosition, decimal closePrice)
        {
            // TODO
            if (currentPosition.OrderDirection != OrderDirection.Long)
            {
                throw new NotImplementedException();
            }

            decimal feePercent = currentPosition.OrderType == OrderType.Market ? _settings.TakerFeePercent : _settings.MakerFeePercent;

            decimal baseAssetAmount = currentPosition.QuoteAssetAmountBefore / currentPosition.OpenPrice;
            baseAssetAmount = baseAssetAmount - baseAssetAmount * feePercent; // fee

            decimal quoteAssetAmountAfter = baseAssetAmount * closePrice;
            quoteAssetAmountAfter = quoteAssetAmountAfter - quoteAssetAmountAfter * feePercent; // fee

            decimal pnl = quoteAssetAmountAfter - currentPosition.QuoteAssetAmountBefore;
            decimal pnlPercent = Math.Round(pnl / currentPosition.QuoteAssetAmountBefore, 4);
            return new PositionPnlModel()
            {
                Pnl = pnl,
                PnlPercent = pnlPercent,
                QuoteAssetAmountBefore = currentPosition.QuoteAssetAmountBefore,
                QuoteAssetAmountAfter = quoteAssetAmountAfter,
            };
        }

        /// <summary>
        /// Calculates pnl for current open position
        /// </summary>
        public PositionPnlModel CalcCurrentPositionPnl(decimal closePrice)
        {
            return this.CalcPositionPnl(_currentPosition, closePrice);
        }

        #endregion
    }
}
