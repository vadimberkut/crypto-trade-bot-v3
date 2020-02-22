using CryptoTradeBot.Host.Models;
using CryptoTradeBot.Infrastructure.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace CryptoTradeBot.Host.Interfaces
{
    /// <summary>
    /// Abstracted of exchange util with helper methods.
    /// </summary>
    public interface IExchangeUtil
    {
        string ExchangeName { get; }

        /// <summary>
        /// The one who places LIMIT order and it is filfilled by order placed by taker
        /// </summary>
        decimal MakerFee { get; }
       
        /// <summary>
        /// The one who places order, usually MARKET, and fulfils already placed LIMIT order by maker.
        /// </summary>
        decimal TakerFee { get; }

        /// <summary>
        /// Returns available or selected symbols for exchange.
        /// </summary>
        /// <returns></returns>
        List<string> GetSymbols();

        List<string> GetSymbols(IEnumerable<string> requestedSymbols);

        List<string> GetSymbolsForAssets(string asset1, string asset2);

        /// <summary>
        /// Returns available or selected assets for exchange.
        /// </summary>
        /// <returns></returns>
        List<string> GetAssets();

        public List<string> GetAssets(IEnumerable<string> requestedSymbols);

        /// <summary>
        /// Parses symbol to get its info
        /// </summary>
        ExchangeSymbolModel GetSymbolInfo(string symbol);

        /// <summary>
        /// arses symbol to get its info
        /// </summary>
        ExchangeSymbolModel GetSymbolInfoFromPair(string pair);

        /// <summary>
        /// Returns action based on symbol and asset according to if asset is base or quote.
        /// </summary>
        SymbolAction GetSymbolAction(string symbol, string asset);
    }
}
