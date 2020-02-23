using CryptoTradeBot.Exchanges.Binance.Dtos;
using CryptoTradeBot.Exchanges.Binance.Models;
using CryptoTradeBot.Exchanges.Binance.Stores;
using CryptoTradeBot.Host.Algorithms.CirclePathAlgorithm;
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
        private readonly CirclePahtAlgorithm _circlePahtAlgorithm;

        private DateTime _algLastRunAt = DateTime.MinValue;
        private DateTime _algNextRunAt = DateTime.MinValue;
        private TimeSpan _algRunInterval = TimeSpan.FromSeconds(10); // run once in N seconds period
        private bool isAlgExecuting = false;

        public WssBookDepthHandler(
            ILogger<WssBookDepthHandler> logger,
            OrderBookStore orderBookStore,
            CirclePahtAlgorithm circlePahtAlgorithm
        )
        {
            _logger = logger;
            _orderBookStore = orderBookStore;
            _circlePahtAlgorithm = circlePahtAlgorithm;
        }

        /// <summary>
        /// Handles messages for '<symbol>@depth<depth>' suscription
        /// </summary>
        /// <returns></returns>
        public async Task HandleMessageAsync(string stream, string message)
        {
            var messageObject = JsonConvert.DeserializeObject<WssCombinedStreamPayloadDto<WssPartialBookDepthDto>>(message);
            // _logger.LogInformation($"Message received in stream '{stream}': ");

            var parts = stream.Split('@');
            string symbol = parts[0];

            // update order book
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

            // run alg
            // TODO: move to separate repetable runner
            if(isAlgExecuting == false && DateTime.Now >= _algNextRunAt)
            {
                string startAsset = "BTC";
                decimal startAssetAmount = 0.1m;
                var solutions = _circlePahtAlgorithm.Solve(startAsset, startAssetAmount);

                _algLastRunAt = DateTime.Now;
                _algNextRunAt = DateTime.Now.Add(_algRunInterval);

                if (solutions.Count != 0)
                {
                    var solution = solutions.OrderByDescending(x => x.SimulationResult.EstimatedProfitInStartAsset).First();
                    _logger.LogInformation($"Solution: amount={solution.SimulationResult.TargetStartAssetAmount}, profit={solution.SimulationResult.EstimatedProfitInStartAsset}, profit USDT={solution.SimulationResult.EstimatedProfitInUSDTAsset}.");

                    // start executing alg solution
                    isAlgExecuting = true;
                }
            }
            
        }
    }
}
