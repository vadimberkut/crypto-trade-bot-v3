using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace CryptoTradeBot.Exchanges.Binance.Models
{
    /// <summary>
    /// Represents order book for a specific symbol
    /// </summary>
    public class OrderBookSymbolModel
    {
        public OrderBookSymbolModel()
        {
            Bids = new List<OrderBookEntryModel>();
            Asks = new List<OrderBookEntryModel>();
        }

        public List<OrderBookEntryModel> Bids { get; set; }
        public List<OrderBookEntryModel> Asks { get; set; }
    }

    public class OrderBookEntryModel
    {
        public decimal Price { get; set; }
        public decimal Quantity { get; set; }
    }
}
