using CryptoTradeBot.WebHost.Exchanges.Binance;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace CryptoTradeBot.WebHost.Algorithms.CirclePathAlgorithm
{
    public static class CirclePahtAlgorithmConfig
    {
        /// <summary>
        /// Symbols that are used in alg
        /// TODO: pass as dynamic param
        /// </summary>
        public static List<string> AllowedSymbols = BinanceConfig.HighVolumeSymbols;
    }
}
