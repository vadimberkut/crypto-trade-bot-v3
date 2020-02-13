using CryptoTradeBot.Exchanges.Binance.Models;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace CryptoTradeBot.Exchanges.Binance.Stores
{
    public class OrderBookStore
    {
        private Dictionary<string, OrderBookSymbolModel> _store = new Dictionary<string, OrderBookSymbolModel>();

        public OrderBookStore()
        {

        }

        public void ReplaceSymbolOrderBook(string symbol, OrderBookSymbolModel orderBook)
        {
            if(!_store.ContainsKey(symbol))
            {
                _store.Add(symbol, null);
            }

            _store[symbol] = orderBook;
        }

        public string SerializeToJson(Formatting formatting = Formatting.None)
        {
            return JsonConvert.SerializeObject(this._store, formatting);
        }
    }
}
