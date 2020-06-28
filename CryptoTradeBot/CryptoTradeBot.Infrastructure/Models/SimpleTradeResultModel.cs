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
    }
}
