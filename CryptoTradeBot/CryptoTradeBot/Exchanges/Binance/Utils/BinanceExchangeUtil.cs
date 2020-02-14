using CryptoTradeBot.Host.Exchanges.Binance.Clients;
using CryptoTradeBot.Host.Exchanges.Binance.Dtos;
using CryptoTradeBot.Host.Interfaces;
using CryptoTradeBot.Host.Models;
using CryptoTradeBot.Infrastructure.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace CryptoTradeBot.Host.Exchanges.Binance.Utils
{
    /// <summary>
    /// Binance symbol==pair
    /// </summary>
    public class BinanceExchangeUtil : IExchangeUtil
    {
        private readonly BinanceHttpClient _binanceHttpClient;

        private HttpExchangeInformationResponseDto _exchangeInfo;

        private const string _exchangeName = "Binance";

        public BinanceExchangeUtil(
            BinanceHttpClient binanceHttpClient
        )
        {
            _binanceHttpClient = binanceHttpClient;

            // get info
            // TODO: handle exception
            _exchangeInfo = _binanceHttpClient.ExchangeInformationAsync().GetAwaiter().GetResult();
        }

        public List<string> GetSymbols()
        {
            var symbolInfos = this._exchangeInfo.Symbols.ToList();
            var symbols = symbolInfos.Select(x => x.Symbol).Distinct().ToList();
            return symbols;
        }

        public List<string> GetAssets()
        {
            var symbolInfos = this._exchangeInfo.Symbols.ToList();
            var assets = symbolInfos.Select(x => new[] { x.BaseAsset, x.QuoteAsset }).SelectMany(x => x).Distinct().ToList();
            return assets;
        }

        public ExchangeAssetModel ConvertPairToAssets(string pair)
        {
            return this.ConvertSymbolToAssets(pair);
        }

        public ExchangeAssetModel ConvertSymbolToAssets(string symbol)
        {
            var symbolInfo = this._exchangeInfo.Symbols.FirstOrDefault(x => x.Symbol == symbol);
            if (symbolInfo == null)
            {
                throw new Exception($"Can't find info for symbol '{symbol}' in '{_exchangeName}' exchange info.");
            }

            return new ExchangeAssetModel()
            {
                Symbol = symbolInfo.Symbol,
                Pair = symbolInfo.Symbol,
                Base = symbolInfo.BaseAsset,
                Quote = symbolInfo.QuoteAsset,
            };
        }

        /// <summary>
        /// tIOTUSD, IOT -> sell
        /// tIOTUSD, USD -> buy
        /// </summary>
        public SymbolAction GetSymbolAction(string symbol, string balanceAsset)
        {
            var symbolInfo = this._exchangeInfo.Symbols.FirstOrDefault(x => x.Symbol == symbol);
            if (symbolInfo == null)
            {
                throw new Exception($"Can't find info for symbol '{symbol}' in '{_exchangeName}' exchange info.");
            }

            if(symbolInfo.BaseAsset == balanceAsset)
            {
                return SymbolAction.Sell;
            }
            if (symbolInfo.QuoteAsset == balanceAsset)
            {
                return SymbolAction.Buy;
            }

            return SymbolAction.None;
        }
    }
}
