using CryptoTradeBot.Infrastructure.Enums;
using CryptoTradeBot.Infrastructure.Models;
using System;
using System.Collections.Generic;
using System.Text;

namespace CryptoTradeBot.StrategyRunner.Settings
{
    public class StrategyRunnerSettings
    {
        public StrategyRunnerSettings()
        {
            AssetsToTest = new List<AssetsToTestSettings>();
            OrderDirections = new List<OrderDirection>();
            OrderTypes = new List<OrderType>();
        }

        public string StrategyName { get; set; }
        public List<AssetsToTestSettings> AssetsToTest { get; set; }
        public List<OrderDirection> OrderDirections { get; set; }
        public List<OrderType> OrderTypes { get; set; }

        /// <summary>
        /// Required if LIMIT orders are selected
        /// </summary>
        public decimal MakerFeePercent { get; set; }

        /// <summary>
        /// Required if MARKET orders are selected
        /// </summary>
        public decimal TakerFeePercent { get; set; }

        /// <summary>
        /// Indicates maximum percent of loss from initial balance.
        /// </summary>
        public decimal? MaxDrawdownPercent { get; set; }

        /// <summary>
        /// Optional. Position won't be processed if this value exceeded.
        /// </summary>
        public decimal? MaxStoplossPercent { get; set; }

        /// <summary>
        /// Optional. Position won't be processed if this value exceeded.
        /// </summary>
        public decimal? MaxTakeprofitPercent { get; set; }

        /// <summary>
        /// Initial quote asset amount. E.g. BTCUSDT, balance=100USDT
        /// </summary>
        public decimal InitialBalance { get; set; }

        /// <summary>
        /// Indicates percent of the balance that can be used to open positions.
        /// E.g. 0.7 means using max 70% of current balance for trades, while 30% is reserved.
        /// </summary>
        public decimal? BalancePerTradePercent { get; set; }

        /// <summary>
        /// First bar index to run strategy
        /// </summary>
        public int? StartBarIndex { get; set; }
    }

    public class AssetsToTestSettings
    {
        public string Symbol { get; set; }
        public BarChartIntervalConfig BarChartIntervalConfig { get; set; }
        public DateTime From { get; set; }
        public DateTime To { get; set; }
    }
}
