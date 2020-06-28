using CryptoTradeBot.Infrastructure.Enums;
using CryptoTradeBot.Infrastructure.Models;
using CryptoTradeBot.StrategyRunner.Enums;
using CryptoTradeBot.StrategyRunner.Interfaces;
using CryptoTradeBot.StrategyRunner.Models;
using CryptoTradeBot.StrategyRunner.Settings;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
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


        public async Task<IEnumerable<StrategyRunSummaryModel>> RunAsync()
        {
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

                _currentBalance = _settings.InitialBalance;
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
                                    _logger.LogWarning($"Stoploss={currentStoplossPercent}%, but can't be greater than {_settings.MaxStoplossPercent.Value}%!");
                                    _status = StrategyRunnerStatus.NoOpenedPosition;
                                    positionWaitOpeningDurationInBars = 0;
                                    continue;
                                }
                            }
                            if(positionOpeningDetails.TakeprofitPrice != null)
                            {
                                decimal currentTakeprofitPercent = Math.Round(1 - positionOpeningDetails.OpenPrice / positionOpeningDetails.TakeprofitPrice.Value, 2);
                                if (_settings.MaxTakeprofitPercent != null && currentTakeprofitPercent > _settings.MaxTakeprofitPercent.Value)
                                {
                                    _logger.LogWarning($"Takeprofit={currentTakeprofitPercent}%, but can't be greater than {_settings.MaxTakeprofitPercent.Value}%!");
                                    _status = StrategyRunnerStatus.NoOpenedPosition;
                                    positionWaitOpeningDurationInBars = 0;
                                    continue;
                                }
                            }
                           

                            _status = StrategyRunnerStatus.WaitingPositionOpening;
                            positionWaitOpeningDurationInBars = 0;

                            continue;
                        }
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

        public PositionPnlModel CalcCurrentPositionPnl(decimal closePrice)
        {
            return this.CalcPositionPnl(_currentPosition, closePrice);
        }
    }
}
