using CryptoTradeBot.Infrastructure.Extensions;
using CryptoTradeBot.Infrastructure.Models;
using CryptoTradeBot.StrategyRunner.Settings;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace CryptoTradeBot.StrategyRunner.Models
{
    public class StrategyRunSummaryModel
    {
        public StrategyRunSummaryModel(
            StrategyRunnerSettings strategyRunnerSettings,
            StrategyDefinitionModel strategyDefinition,
            AssetsToTestSettings assetsToTestSettings,
            List<SimpleTradeResultModel> results,
            int barsCount
        )
        {
            StrategyRunnerSettings = strategyRunnerSettings;
            StrategyDefinition = strategyDefinition;
            AssetsToTestSettings = assetsToTestSettings;
            Results = results ?? new List<SimpleTradeResultModel>();
            BarsCount = barsCount;
        }

        public StrategyRunnerSettings StrategyRunnerSettings { get; private set; }
        public StrategyDefinitionModel StrategyDefinition { get; private set; }
        public AssetsToTestSettings AssetsToTestSettings { get; private set; }
        public List<SimpleTradeResultModel> Results { get; private set; }
        public int BarsCount { get; private set; }

        // TODO - handle no results case
        #region Computed

        private bool IsAnyResults => Results != null && Results.Any();
        private bool IsNoResults => !IsAnyResults;

        private IEnumerable<SimpleTradeResultModel> ProfitResults => Results.Where(x => x.Pnl > 0);
        private IEnumerable<SimpleTradeResultModel> LossResults => Results.Where(x => x.Pnl <= 0);

        private int TradesCount => Results.Count;
        private int ProfitTradesCount => ProfitResults.Count();
        private int LossTradesCount => LossResults.Count();

        private decimal ProfitTradesPercent => Math.Round((decimal)ProfitTradesCount / (decimal)TradesCount, 2);
        private decimal LossTradesPercent => Math.Round((decimal)LossTradesCount / (decimal)TradesCount, 2);

        private int ClosedByStopLossTradesCount => Results.Count(x => x.IsClosedByStopLoss);
        private int ClosedByTakeProfitTradesCount => Results.Count(x => x.IsClosedByTakeProfit);
        private int ClosedByDurationTimeoutTradesCount => Results.Count(x => x.IsClosedByDurationTimeout);
        private int ClosedByEarlyCloseTradesCount => Results.Count(x => x.IsClosedByEarlyClose);
        private int ClosedByDurationTimeoutProfitTradesCount => ProfitResults.Count(x => x.IsClosedByDurationTimeout);
        private int ClosedByDurationTimeoutLossTradesCount => LossResults.Count(x => x.IsClosedByDurationTimeout);

        private int MaxProfitTradesInARow => Results.Select(x => (item: x, currentCount: 0, max: 0)).Aggregate((prev, curr) =>
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
        private int MaxLossTradesInARow => Results.Select(x => (item: x, currentCount: 0, max: 0)).Aggregate((prev, curr) =>
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

        private TimeSpan MinOpenProfitPositionDuration => ProfitResults.Min(x => x.PositionDuration);
        private TimeSpan AvgerageOpenProfitPositionDuration => TimeSpan.FromSeconds(ProfitResults.Average(x => x.PositionDuration.TotalSeconds));
        private TimeSpan MaxOpenProfitPositionDuration => ProfitResults.Max(x => x.PositionDuration);
        private TimeSpan MinOpenLossPositionDuration => LossResults.Min(x => x.PositionDuration);
        private TimeSpan AvgerageOpenLossPositionDuration => TimeSpan.FromSeconds(LossResults.Average(x => x.PositionDuration.TotalSeconds));
        private TimeSpan MaxOpenLossPositionDuration => LossResults.Max(x => x.PositionDuration);

        private decimal MinProfit => Math.Round(ProfitResults.Min(x => x.Pnl), 2);
        private decimal AverageProfit => Math.Round(ProfitResults.Aggregate(0m, (avg, x) => avg + (x.Pnl / Results.Count)), 2);
        private decimal MaxProfit => Math.Round(ProfitResults.Max(x => x.Pnl), 2);
        private decimal MinLoss => Math.Round(LossResults.Max(x => x.Pnl), 2);
        private decimal AverageLoss => Math.Round(LossResults.Aggregate(0m, (avg, x) => avg + (x.Pnl / Results.Count)), 2);
        private decimal MaxLoss => Math.Round(LossResults.Min(x => x.Pnl), 2);

        private decimal MinProfitPercent => Math.Round(ProfitResults.Min(x => x.PnlPercent), 4);
        private decimal AverageProfitPercent => Math.Round(ProfitResults.Aggregate(0m, (avg, x) => avg + (x.PnlPercent / Results.Count)), 4);
        private decimal MaxProfitPercent => Math.Round(ProfitResults.Max(x => x.PnlPercent), 4);
        private decimal MinLossPercent => Math.Round(LossResults.Max(x => x.PnlPercent), 4);
        private decimal AverageLossPercent => Math.Round(LossResults.Aggregate(0m, (avg, x) => avg + (x.PnlPercent / Results.Count)), 4);
        private decimal MaxLossPercent => Math.Round(LossResults.Min(x => x.PnlPercent), 4);



        private decimal InitalBalance => IsNoResults ? 0 : Results.First().BalanceBefore;
        private decimal FinalBalance => IsNoResults ? 0 : Results.Last().Balance;

        private decimal TotalPnl => Math.Round(Results.Aggregate(0m, (avg, x) => avg + x.Pnl), 2);
        private decimal TotalPnlPercent => Math.Round(TotalPnl / InitalBalance, 4);

        #endregion

        public void PrintOutSummary(ILogger logger)
        {
            logger.LogInformation("");
            logger.LogInformation("Strategy run summary:");
            logger.LogInformation($"Strategy={StrategyRunnerSettings.StrategyName}");
            logger.LogInformation($"Symbol={AssetsToTestSettings.Symbol}");
            logger.LogInformation($"BarInterval={AssetsToTestSettings.BarChartIntervalConfig.Name}");
            logger.LogInformation($"Range: From={AssetsToTestSettings.From.ToUtcString()}, To={AssetsToTestSettings.To.ToUtcString()}; Timespan={AssetsToTestSettings.To.Subtract(AssetsToTestSettings.From).ToString()}");
            logger.LogInformation($"BarsCount={BarsCount}");
            logger.LogInformation($"TradesCount={TradesCount}");
            logger.LogInformation($"ProfitTradesCount={ProfitTradesCount} ({ProfitTradesPercent}%)");
            logger.LogInformation($"LossTradesCount={LossTradesCount} ({LossTradesPercent}%)");

            logger.LogInformation($"ClosedByStopLossTradesCount={ClosedByStopLossTradesCount}");
            logger.LogInformation($"ClosedByTakeProfitTradesCount={ClosedByTakeProfitTradesCount}");
            logger.LogInformation($"ClosedByDurationTimeoutTradesCount={ClosedByDurationTimeoutTradesCount} (profit={ClosedByDurationTimeoutProfitTradesCount}, loss={ClosedByDurationTimeoutLossTradesCount})");
            logger.LogInformation($"ClosedByEarlyCloseTradesCount={ClosedByEarlyCloseTradesCount}");

            logger.LogInformation($"MaxProfitTradesInARow={MaxProfitTradesInARow}");
            logger.LogInformation($"MaxLossTradesInARow={MaxLossTradesInARow}");
            logger.LogInformation($"Profit position duration=[{MinOpenProfitPositionDuration}; ...; {AvgerageOpenProfitPositionDuration}; ...; {MaxOpenProfitPositionDuration}]");
            logger.LogInformation($"Loss position duration=[{MinOpenLossPositionDuration}; ...; {AvgerageOpenLossPositionDuration}; ...; {MaxOpenLossPositionDuration}]");
            logger.LogInformation($"");

            logger.LogInformation($"Profit=[{MinProfit}; ...; {AverageProfit}; ...; {MaxProfit}]");
            logger.LogInformation($"Loss=[{MinLoss}; ...; {AverageLoss}; ...; {MaxLoss}]");
            logger.LogInformation($"");

            logger.LogInformation($"Profit %=[{MinProfitPercent}; ...; {AverageProfitPercent}; ...; {MaxProfitPercent}]");
            logger.LogInformation($"Loss %=[{MinLossPercent}; ...; {AverageLossPercent}; ...; {MaxLossPercent}]");
            logger.LogInformation($"");

            logger.LogInformation($"InitalBalance={InitalBalance}");
            logger.LogInformation($"FinalBalance={FinalBalance}");
            logger.LogInformation($"TotalPnl={TotalPnl}");
            logger.LogInformation($"TotalPnl %={TotalPnlPercent}");
            logger.LogInformation("");
            logger.LogInformation("----------------------------------------------------");
            logger.LogInformation("");
        }
    }
}
