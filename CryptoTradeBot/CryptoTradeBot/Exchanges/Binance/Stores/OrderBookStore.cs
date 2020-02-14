using CryptoTradeBot.Exchanges.Binance.Models;
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
            _store.TryAdd(symbol, orderBook);
        }

        public string SerializeToJson(Formatting formatting = Formatting.None)
        {
            return JsonConvert.SerializeObject(this._store, formatting);
        }
    }
}
