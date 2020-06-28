using System;
using System.Collections.Generic;
using System.Text;

namespace CryptoTradeBot.Infrastructure.Models
{
    public class PositionPnlModel
    {
        public decimal Pnl { get; set; }
        public decimal PnlPercent { get; set; }

        /// <summary>
        /// Balance before opening position
        /// </summary>
        public decimal QuoteAssetAmountBefore { get; set; }

        /// <summary>
        /// Balance after closing position
        /// </summary>
        public decimal QuoteAssetAmountAfter { get; set; }
    }
}
