using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace CryptoTradeBot.Host.Models
{
    public class ExchangeAssetModel
    {
        public string Symbol { get; set; }
        public string Pair { get; set; }
        public string Base { get; set; }
        public string Quote { get; set; }
    }
}
