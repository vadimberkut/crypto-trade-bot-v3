using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace CryptoTradeBot
{
    public class ApplicationSettings
    {
        public BinanceSettings Binance { get; set; }
    }

    public class BinanceSettings
    {
        public string WssUrl { get; set; }
        public string ApiKey { get; set; }
        public string SecretKey { get; set; }
    }
}
