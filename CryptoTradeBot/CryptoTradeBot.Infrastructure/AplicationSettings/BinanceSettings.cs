using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace CryptoTradeBot.Infrastructure.AplicationSettings
{
    public class BinanceSettings
    {
        public string HttpApiUrl { get; set; }
        public string WssUrl { get; set; }
        public string ApiKey { get; set; }
        public string SecretKey { get; set; }
    }
}
