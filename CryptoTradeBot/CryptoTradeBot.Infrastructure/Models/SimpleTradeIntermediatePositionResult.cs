using System;
using System.Collections.Generic;
using System.Text;

namespace CryptoTradeBot.Infrastructure.Models
{
    public class SimpleTradeIntermediatePositionResult
    {
        public decimal Pnl { get; set; }
        public decimal PnlPercent { get; set; }
    }
}
