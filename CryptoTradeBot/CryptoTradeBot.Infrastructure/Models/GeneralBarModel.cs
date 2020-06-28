using CryptoTradeBot.Infrastructure.Enums;
using System;
using System.Collections.Generic;
using System.Text;

namespace CryptoTradeBot.Infrastructure.Models
{
    public class GeneralBarModel
    {
        public DateTime OpenTime { get; set; }
        public DateTime CloseTime { get; set; }
        public decimal OpenPrice { get; set; }
        public decimal HighPrice { get; set; }
        public decimal LowPrice { get; set; }
        public decimal ClosePrice { get; set; }
        public decimal Volume { get; set; }
        public decimal QuoteAssetVolume { get; set; }

        public decimal GetPriceByType(BarPriceType barPriceType)
        {
            switch(barPriceType)
            {
                case BarPriceType.High:
                    return HighPrice;
                case BarPriceType.Open:
                    return OpenPrice;
                case BarPriceType.Close:
                    return ClosePrice;
                case BarPriceType.Low:
                    return LowPrice;
                default:
                    throw new InvalidOperationException();
            }
        }
    }
}
