using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace CryptoTradeBot.Exchanges.Binance.Dtos
{
    public class WssCombinedStreamPayloadDto<T>
    {
        public string Stream { get; set; }
        public T Data { get; set; }
    }
}
