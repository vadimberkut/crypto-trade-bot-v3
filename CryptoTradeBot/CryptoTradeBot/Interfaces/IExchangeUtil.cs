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
        /// <summary>
        /// Returns available or selected symbols for exchange.
        /// </summary>
        /// <returns></returns>
        List<string> GetSymbols();

        /// <summary>
        /// Returns available or selected assets for exchange.
        /// </summary>
        /// <returns></returns>
        List<string> GetAssets();

        /// <summary>
        /// Parses symbol to get base and quto assets
        /// </summary>
        ExchangeAssetModel ConvertSymbolToAssets(string symbol);

        /// <summary>
        /// Parses symbol to get base and quto assets
        /// </summary>
        ExchangeAssetModel ConvertPairToAssets(string pair);

        /// <summary>
        /// Returns action based on symbol and asset according to if asset is base or quote.
        /// </summary>
        SymbolAction GetSymbolAction(string symbol, string asset);
    }
}
