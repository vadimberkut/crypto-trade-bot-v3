using CryptoTradeBot.Exchanges.Binance.Dtos;
using CryptoTradeBot.Exchanges.Binance.Models;
using CryptoTradeBot.Exchanges.Binance.Stores;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace CryptoTradeBot.Exchanges.Binance.Handlers
{
    public class WssBookDepthHandler : IWssMessageHandler
    {
        private readonly ILogger<WssBookDepthHandler> _logger;
        private readonly OrderBookStore _orderBookStore;

        public WssBookDepthHandler(
            ILogger<WssBookDepthHandler> logger,
            OrderBookStore orderBookStore
        )
        {
            _logger = logger;
            _orderBookStore = orderBookStore;
        }

        public async Task HandleMessageAsync(string stream, string message)
        {
            var messageObject = JsonConvert.DeserializeObject<WssCombinedStreamPayloadDto<WssPartialBookDepthDto>>(message);
            // _logger.LogInformation($"Message received in stream '{stream}': ");

            var parts = stream.Split('@');
            string symbol = parts[0];

            _orderBookStore.ReplaceSymbolOrderBook(symbol, new OrderBookSymbolModel() 
            { 
                Asks = messageObject.Data.Asks.Select(x => new OrderBookEntryModel() { 
                    Price = x.Price,
                    Quantity = x.Quantity,
                }).ToList(),
                Bids = messageObject.Data.Bids.Select(x => new OrderBookEntryModel()
                {
                    Price = x.Price,
                    Quantity = x.Quantity,
                }).ToList(),
            });
        }
    }
}
