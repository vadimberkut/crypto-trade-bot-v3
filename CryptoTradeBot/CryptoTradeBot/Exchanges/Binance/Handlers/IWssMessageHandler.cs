using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace CryptoTradeBot.Exchanges.Binance.Handlers
{
    /// <summary>
    /// Each instance must be registered in DI
    /// </summary>
    public interface IWssMessageHandler
    {
        Task HandleMessageAsync(string stream, string message);
    }
}
