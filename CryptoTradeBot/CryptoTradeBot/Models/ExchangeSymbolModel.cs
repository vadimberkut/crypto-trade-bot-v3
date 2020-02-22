using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace CryptoTradeBot.Host.Models
{
    public class ExchangeSymbolModel
    {
        public string Symbol { get; set; }
        public string Pair { get; set; }
        public string Base { get; set; }
        public string Quote { get; set; }

        /// <summary>
        /// Digits after point.
        /// E.g. for BTC quotes 0.00400450
        /// </summary>
        public int QuotePrecision { get; set; }

        /// <summary>
        /// Returns if the asset is base or quote for this symbol
        /// </summary>
        public SymbolAssetType GetSymbolAssetType(string asset)
        {
            if(Base == asset)
            {
                return SymbolAssetType.Base;
            }
            else if (Quote == asset)
            {
                return SymbolAssetType.Quote;
            }
            return SymbolAssetType.None;
        }
    }

    public enum SymbolAssetType
    {
        None = 0,
        Base = 1,
        Quote = 2,
    }
}
