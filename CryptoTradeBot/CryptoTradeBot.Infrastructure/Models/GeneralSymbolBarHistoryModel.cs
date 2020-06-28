using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace CryptoTradeBot.Infrastructure.Models
{
    public class GeneralSymbolBarHistoryModel
    {
        public GeneralSymbolBarHistoryModel()
        {
            Bars = new List<GeneralBarModel>();
        }

        public string Symbol { get; set; }
        public DateTime From { get; set; }
        public DateTime To { get; set; }

        /// <summary>
        /// Candlestick Interval
        /// </summary>
        public string BarInterval { get; set; }
        public List<GeneralBarModel> Bars { get; set; }
    }
}
