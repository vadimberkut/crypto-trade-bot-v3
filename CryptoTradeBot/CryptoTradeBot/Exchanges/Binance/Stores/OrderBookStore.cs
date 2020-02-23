using CryptoTradeBot.Exchanges.Binance.Models;
using CryptoTradeBot.Host.Exchanges.Binance.Utils;
using CryptoTradeBot.Infrastructure.Enums;
using Newtonsoft.Json;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace CryptoTradeBot.Exchanges.Binance.Stores
{
    /// <summary>
    /// NB: symbols are stored in lower case as WSS channel names
    /// </summary>
    public class OrderBookStore
    {
        private readonly BinanceExchangeUtil _exchangeUtil;


        private ConcurrentDictionary<string, OrderBookSymbolModel> _store = new ConcurrentDictionary<string, OrderBookSymbolModel>();
        private object _lock = new object();

        public OrderBookStore(
            BinanceExchangeUtil exchangeUtil
        )
        {
            _exchangeUtil = exchangeUtil;
        }

        public void ReplaceSymbolOrderBook(string symbol, OrderBookSymbolModel orderBook)
        {
            // order for sure
            orderBook.SortBidsAndAsks();

            _store.TryAdd(symbol.ToLowerInvariant(), orderBook);
        }

        public string SerializeToJson(Formatting formatting = Formatting.None)
        {
            return JsonConvert.SerializeObject(this._store, formatting);
        }

        public void ImportFromJson(string json)
        {
            var nextStore = JsonConvert.DeserializeObject<ConcurrentDictionary<string, OrderBookSymbolModel>>(json);
            
            this._store.Clear();
            foreach (var pair in nextStore)
            {
                // order for sure
                var orderBook = new OrderBookSymbolModel()
                {
                    Bids = pair.Value.Bids,
                    Asks = pair.Value.Asks,
                };
                orderBook.SortBidsAndAsks();

                this._store.TryAdd(pair.Key.ToLowerInvariant(), orderBook);
            }
        }

        public OrderBookSymbolModel GetOrderBookForSymbol(string symbol)
        {
            if(this._store.TryGetValue(symbol.ToLowerInvariant(), out OrderBookSymbolModel result))
            {
                return result;
            }
            return null;
        }

        /// <summary>
        /// Converts source asset to target with specified amount.
        /// <br/>
        /// Doesn't takes into account the amount when calculation the total (uses best bid/ask, doesn't watch order book depth)
        /// <br/>
        /// NB: Doesn't support not direct conversions. E.g. IOTA -> TRON as it requires intermediate conversions. 
        /// E.g. IOTA -> USDT -> TRON
        /// </summary>
        public decimal ConvertAssets(string sourceAsset, string targetAsset, decimal sourceAmount)
        {
            if(sourceAsset == targetAsset)
            {
                return sourceAmount;
            }

            var symbols = _exchangeUtil.GetSymbolsForAssets(sourceAsset, targetAsset);
            symbols = symbols.Select(x => x.ToLowerInvariant()).ToList();
            var storeEntries = _store.Where(x => symbols.Contains(x.Key)).ToList();
            if(storeEntries.Count() == 0)
            {
                throw new InvalidOperationException($"Can't convert '{sourceAsset}' to '{targetAsset}'. There is no order books in store for the found conversion symbols: {string.Join(",", symbols)}.");
            }
            var firstEntry = storeEntries.First();
            string conversionSymbol = firstEntry.Key.ToUpperInvariant();
            var conversionSymbolOrderBook = firstEntry.Value;
            var symbolAction = _exchangeUtil.GetSymbolAction(conversionSymbol, sourceAsset);

            decimal actionTotal;
            var bestBid = conversionSymbolOrderBook.Bids[0];
            var bestAsk = conversionSymbolOrderBook.Asks[0];

            if (symbolAction == SymbolAction.Buy)
            {
                actionTotal = sourceAmount / bestAsk.Price;
                actionTotal = actionTotal - (actionTotal * this._exchangeUtil.MakerFee); // maker because you are the maker (MARKET order)
            }
            else if (symbolAction == SymbolAction.Sell)
            {
                actionTotal = sourceAmount * bestBid.Price;
                actionTotal = actionTotal - (actionTotal * this._exchangeUtil.MakerFee); // maker because you are the maker (MARKET order)
            }
            else
            {
                throw new InvalidOperationException($"Can't convert '{sourceAsset}' to '{targetAsset}'. Unable to determine the action for symbol '{conversionSymbol}' and asset '{sourceAsset}'.");
            }

            return actionTotal;
        }
    }
}
