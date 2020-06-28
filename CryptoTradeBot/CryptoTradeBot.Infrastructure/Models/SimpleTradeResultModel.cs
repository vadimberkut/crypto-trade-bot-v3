using CryptoTradeBot.Infrastructure.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace CryptoTradeBot.Infrastructure.Models
{
    public class SimpleTradeResultModel
    {
        public decimal BalanceBefore { get; set; }
        public decimal Balance { get; set; }
        public decimal Pnl { get; set; }
        public decimal PnlPercent { get; set; }
        public bool IsClosedByStopLoss { get; set; }
        public bool IsClosedByTakeProfit { get; set; }
        public bool IsClosedByDurationTimeout { get; set; }
        public bool IsClosedByEarlyClose { get; set; }
        public int PositionDurationInBars { get; set; }
        public TimeSpan PositionDuration { get; set; }

        public OrderDirection OrderDirection { get; set; }
        public OrderType OpenOrderType { get; set; }
        public OrderType CloseOrderType { get; set; }
        public decimal OpenPrice { get; set; }
        public decimal ClosePrice { get; set; }
        public DateTime OpenedAt { get; set; }
        public DateTime ClosedAt { get; set; }

        /// <summary>
        /// Intermediate results during position was opened. Each item represents 1 bar.
        /// E.g. opened on bar 450 closed on 460. List contains 10 records with intermediate pnl for each bar.
        /// </summary>
        public List<PositionPnlModel> IntermediateResults { get; set; }
    }
}
