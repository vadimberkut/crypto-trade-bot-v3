﻿using CryptoTradeBot.Infrastructure.Extensions;
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
            AssetToTestSettings assetsToTestSettings,
            List<SimpleTradeResultModel> results,
            int barsCount
        )
        {
            StrategyRunnerSettings = strategyRunnerSettings;
            StrategyDefinition = strategyDefinition;
            AssetToTestSettings = assetsToTestSettings;
            Results = results ?? new List<SimpleTradeResultModel>();
            BarsCount = barsCount;
        }

        public StrategyRunnerSettings StrategyRunnerSettings { get; private set; }
        public StrategyDefinitionModel StrategyDefinition { get; private set; }
        public AssetToTestSettings AssetToTestSettings { get; private set; }
        public List<SimpleTradeResultModel> Results { get; private set; }
        public int BarsCount { get; private set; }


        #region Computed

        private bool IsAnyResults => Results != null && Results.Any();
        private bool IsNoResults => !IsAnyResults;

        private IEnumerable<SimpleTradeResultModel> ProfitResults => Results.Where(x => x.Pnl > 0);
        private IEnumerable<SimpleTradeResultModel> LossResults => Results.Where(x => x.Pnl <= 0);

        private int TradesCount => Results.Count;
        private int ProfitTradesCount => ProfitResults.Count();
        private int LossTradesCount => LossResults.Count();

        private decimal ProfitTradesPercent => IsNoResults ? 0 : Math.Round((decimal)ProfitTradesCount / (decimal)TradesCount, 2);
        private decimal LossTradesPercent => IsNoResults ? 0 : Math.Round((decimal)LossTradesCount / (decimal)TradesCount, 2);

        private int ClosedByStopLossTradesCount => Results.Count(x => x.IsClosedByStopLoss);
        private int ClosedByTakeProfitTradesCount => Results.Count(x => x.IsClosedByTakeProfit);
        private int ClosedByDurationTimeoutTradesCount => Results.Count(x => x.IsClosedByDurationTimeout);
        private int ClosedByEarlyCloseTradesCount => Results.Count(x => x.IsClosedByEarlyClose);
        private int ClosedByDurationTimeoutProfitTradesCount => ProfitResults.Count(x => x.IsClosedByDurationTimeout);
        private int ClosedByDurationTimeoutLossTradesCount => LossResults.Count(x => x.IsClosedByDurationTimeout);

        private int MaxProfitTradesInARow => IsNoResults ? 0 : Results.Select(x => (item: x, currentCount: 0, max: 0)).Aggregate((prev, curr) =>
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
        private int MaxLossTradesInARow => IsNoResults ? 0 : Results.Select(x => (item: x, currentCount: 0, max: 0)).Aggregate((prev, curr) =>
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

        private TimeSpan MinOpenProfitPositionDuration => IsNoResults ? TimeSpan.Zero : ProfitResults.Min(x => x.PositionDuration);
        private TimeSpan AvgerageOpenProfitPositionDuration => IsNoResults ? TimeSpan.Zero : TimeSpan.FromSeconds(ProfitResults.Average(x => x.PositionDuration.TotalSeconds));
        private TimeSpan MaxOpenProfitPositionDuration => IsNoResults ? TimeSpan.Zero : ProfitResults.Max(x => x.PositionDuration);
        private TimeSpan MinOpenLossPositionDuration => IsNoResults ? TimeSpan.Zero : LossResults.Min(x => x.PositionDuration);
        private TimeSpan AvgerageOpenLossPositionDuration => IsNoResults ? TimeSpan.Zero : TimeSpan.FromSeconds(LossResults.Average(x => x.PositionDuration.TotalSeconds));
        private TimeSpan MaxOpenLossPositionDuration => IsNoResults ? TimeSpan.Zero : LossResults.Max(x => x.PositionDuration);

        private decimal MinProfit => IsNoResults ? 0 : Math.Round(ProfitResults.Min(x => x.Pnl), 2);
        private decimal AverageProfit => IsNoResults ? 0 : Math.Round(ProfitResults.Aggregate(0m, (avg, x) => avg + (x.Pnl / Results.Count)), 2);
        private decimal MaxProfit => IsNoResults ? 0 : Math.Round(ProfitResults.Max(x => x.Pnl), 2);
        private decimal MinLoss => IsNoResults ? 0 : Math.Round(LossResults.Max(x => x.Pnl), 2);
        private decimal AverageLoss => IsNoResults ? 0 : Math.Round(LossResults.Aggregate(0m, (avg, x) => avg + (x.Pnl / Results.Count)), 2);
        private decimal MaxLoss => IsNoResults ? 0 : Math.Round(LossResults.Min(x => x.Pnl), 2);

        private decimal MinProfitPercent => IsNoResults ? 0 : Math.Round(ProfitResults.Min(x => x.PnlPercent), 4);
        private decimal AverageProfitPercent => IsNoResults ? 0 : Math.Round(ProfitResults.Aggregate(0m, (avg, x) => avg + (x.PnlPercent / Results.Count)), 4);
        private decimal MaxProfitPercent => IsNoResults ? 0 : Math.Round(ProfitResults.Max(x => x.PnlPercent), 4);
        private decimal MinLossPercent => IsNoResults ? 0 : Math.Round(LossResults.Max(x => x.PnlPercent), 4);
        private decimal AverageLossPercent => IsNoResults ? 0 : Math.Round(LossResults.Aggregate(0m, (avg, x) => avg + (x.PnlPercent / Results.Count)), 4);
        private decimal MaxLossPercent => IsNoResults ? 0 : Math.Round(LossResults.Min(x => x.PnlPercent), 4);


        private decimal InitalBalance => IsNoResults ? 0 : Results.First().BalanceBefore;
        private decimal FinalBalance => IsNoResults ? 0 : Results.Last().Balance;

        private decimal TotalPnl => IsNoResults ? 0 : Math.Round(Results.Aggregate(0m, (avg, x) => avg + x.Pnl), 2);
        private decimal TotalPnlPercent => IsNoResults ? 0 : Math.Round(TotalPnl / InitalBalance, 4);

        #endregion

        public void PrintOutSummary(ILogger logger)
        {
            logger.LogInformation("");
            logger.LogInformation("Strategy run summary:");
            logger.LogInformation($"Strategy={StrategyRunnerSettings.StrategyName}");
            logger.LogInformation($"Symbol={AssetToTestSettings.Symbol}");
            logger.LogInformation($"BarInterval={AssetToTestSettings.BarChartIntervalConfig.Name}");
            logger.LogInformation($"Range: From={AssetToTestSettings.From.ToUtcString()}, To={AssetToTestSettings.To.ToUtcString()}; Timespan={AssetToTestSettings.To.Subtract(AssetToTestSettings.From).ToString()}");
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
