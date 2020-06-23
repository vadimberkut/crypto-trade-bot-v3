using System;
using System.Collections.Generic;
using System.Text;

namespace CryptoTradeBot.Simulation.Models
{
    public class GeneralCandlestickModel
    {
        public DateTime OpenTime { get; set; }
        public DateTime CloseTime { get; set; }
        public decimal OpenPrice { get; set; }
        public decimal HighPrice { get; set; }
        public decimal LowPrice { get; set; }
        public decimal ClosePrice { get; set; }
        public decimal Volume { get; set; }
        public decimal QuoteAssetVolume { get; set; }
    }
}
