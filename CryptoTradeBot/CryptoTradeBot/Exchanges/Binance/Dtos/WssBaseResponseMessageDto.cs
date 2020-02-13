using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace CryptoTradeBot.Exchanges.Binance.Dtos
{
    /*
     {
          "result": null,
          "id": 312
        }
     */

    /// <summary>
    /// Response for command messages (subscribe, unsubscribe, set properties 
    /// </summary>
    public class WssBaseResponseMessageDto
    {
        /// <summary>
        /// Id that is sent to server during request and used to tie request/response. Must be of type unsigned integer.
        /// </summary>
        public uint Id { get; set; }
        public object Result { get; set; }
    }
}
