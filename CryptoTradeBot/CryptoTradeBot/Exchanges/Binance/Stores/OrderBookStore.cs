﻿using CryptoTradeBot.Exchanges.Binance.Models;
using Newtonsoft.Json;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace CryptoTradeBot.Exchanges.Binance.Stores
{
    public class OrderBookStore
    {
        private ConcurrentDictionary<string, OrderBookSymbolModel> _store = new ConcurrentDictionary<string, OrderBookSymbolModel>();
        private object _lock = new object();

        public OrderBookStore()
        {

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
    }
}
