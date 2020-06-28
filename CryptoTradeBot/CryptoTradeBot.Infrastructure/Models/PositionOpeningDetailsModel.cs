using CryptoTradeBot.Infrastructure.Enums;
using System;
using System.Collections.Generic;
using System.Text;

namespace CryptoTradeBot.Infrastructure.Models
{
    public class PositionOpeningDetailsModel
    {
        public OrderDirection OrderDirection { get; set; }
        public OrderType OrderType { get; set; }

        /// <summary>
        /// Indicates which bar price will trigger condition to open position
        /// </summary>
        public BarPriceType PositionOpenTriggerPriceType { get; set; }

        public decimal OpenPrice { get; set; }
        public decimal? StoplossPrice { get; set; }
        public decimal? TakeprofitPrice { get; set; }
    }
}
