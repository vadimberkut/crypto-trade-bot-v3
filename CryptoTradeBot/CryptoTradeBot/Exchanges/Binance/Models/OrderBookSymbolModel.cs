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

        public void SortBidsAndAsks()
        {
            // descending
            Bids.Sort((x1, x2) =>
            {
                if (x1.Price > x2.Price)
                {
                    return -1;
                }
                else if (x1.Price < x2.Price)
                {
                    return 1;
                }
                return 0;
            });

            // ascending
            Asks.Sort((x1, x2) =>
            {
                if (x1.Price > x2.Price)
                {
                    return 1;
                }
                else if (x1.Price < x2.Price)
                {
                    return -1;
                }
                return 0;
            });
        }

        public OrderBookEntryModel AggregateTopBestBidsByPredicate(int entryCount, decimal maxPriceDeviationPercentFromTheBestEntry, decimal maxTotalAmount)
        {
            return this._AggregateTopBestBidsOrAsksByPredicate(this.Bids, entryCount, maxPriceDeviationPercentFromTheBestEntry, maxTotalAmount);
        }

        public OrderBookEntryModel AggregateTopBestAsksByPredicate(int entryCount, decimal maxPriceDeviationPercentFromTheBestEntry, decimal maxTotalAmount)
        {
            return this._AggregateTopBestBidsOrAsksByPredicate(this.Asks, entryCount, maxPriceDeviationPercentFromTheBestEntry, maxTotalAmount);
        }

        private OrderBookEntryModel _AggregateTopBestBidsOrAsksByPredicate(List<OrderBookEntryModel> source, int entryCount, decimal maxPriceDeviationPercentFromTheBestEntry, decimal maxTotalAmount)
        {
            var topEntries = source.Take(entryCount).ToList();

            int entryCountByPrice = 1;
            decimal firstEntryPrice = topEntries.First().Price;
            for (int i = 1; i < topEntries.Count(); i++)
            {
                var topEntry = topEntries[i];

                decimal deviation = Math.Abs(firstEntryPrice - topEntry.Price); // take into account bids/asks
                decimal deviationPercent = deviation / firstEntryPrice;

                // E.g. we want to take top 5 with 0.5% deviation. 
                // if 4th top bid is more than -0.5% from the 1st, but 3rd is not - take only top 3.
                if (deviationPercent > maxPriceDeviationPercentFromTheBestEntry)
                {
                    break;
                }
                entryCountByPrice += 1;
            }
            topEntries = topEntries.Take(entryCountByPrice).ToList();

            // limit entry count by total amount
            maxTotalAmount = Math.Max(maxTotalAmount, topEntries.First().Quantity);
            int entryCountByTotalAmount = 1;
            decimal currrentTotalAmount = 0;
            for (int i = 0; i < topEntries.Count(); i++)
            {
                var topEntry = topEntries[i];
                currrentTotalAmount += topEntry.Quantity;
                if(currrentTotalAmount > maxTotalAmount)
                {
                    break;
                }
                entryCountByTotalAmount += 1;
            }
            topEntries = topEntries.Take(entryCountByTotalAmount).ToList();

            var aggregated = topEntries.Aggregate<OrderBookEntryModel, OrderBookEntryModel>(null, (accum, curr) =>
            {
                if (accum == null)
                {
                    return new OrderBookEntryModel()
                    {
                        Price = curr.Price,
                        Quantity = curr.Quantity,
                    };
                }

                accum.Price = (accum.Price + curr.Price) / 2m; // mean
                accum.Quantity += curr.Quantity;
                return accum;
            });
            return aggregated;
        }
    }

    public class OrderBookEntryModel
    {
        public decimal Price { get; set; }
        public decimal Quantity { get; set; }
    }
}
