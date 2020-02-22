using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace CryptoTradeBot.WebHost.Exchanges.Binance.Dtos
{
    /// <summary>
    /// Represents error message frm server
    /// </summary>
    public class WssBaseErrorResponseMessageDto
    {
        public int Code { get; set; }
        public string Msg { get; set; }
    }
}
