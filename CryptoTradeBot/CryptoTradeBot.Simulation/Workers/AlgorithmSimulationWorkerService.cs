using CryptoTradeBot.Exchanges.Binance.Dtos;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Websocket.Client;
using Microsoft.Extensions.Options;
using CryptoTradeBot.Exchanges.Binance;
using CryptoTradeBot.Exchanges.Binance.Handlers;
using System.IO;
using CryptoTradeBot.Exchanges.Binance.Stores;
using System.Diagnostics;
using CryptoTradeBot.Host.Exchanges.Binance.Clients;
using CryptoTradeBot.WorkerServices;
using CryptoTradeBot.Host.Algorithms.CirclePathAlgorithm;
using CryptoTradeBot.Host.Interfaces;
using CryptoTradeBot.Host.Exchanges.Binance.Utils;

namespace CryptoTradeBot.Simulation.Workers
{
    public class AlgorithmSimulationWorkerService : IHostedService
    {
        private readonly ILogger<AlgorithmSimulationWorkerService> _logger;
        private readonly IOptions<ApplicationSettings> _config;
        private readonly IServiceProvider _serviceProvider;
        private readonly BinanceHttpClient _binanceHttpClient;
        private readonly BinanceWssClient _binanceWssClient;

        public AlgorithmSimulationWorkerService(
            ILogger<AlgorithmSimulationWorkerService> logger,
            IOptions<ApplicationSettings> config,
            IServiceProvider serviceProvider,
            BinanceHttpClient binanceHttpClient,
            BinanceWssClient binanceWssClient
        )
        {
            _logger = logger;
            _config = config;
            _serviceProvider = serviceProvider;
            _binanceHttpClient = binanceHttpClient;
            _binanceWssClient = binanceWssClient;
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Starting simulation...");

            var circlePathAlgorithm = new CirclePahtAlgorithm(
                _serviceProvider.GetRequiredService<ILogger<CirclePahtAlgorithm>>(),
                _serviceProvider.GetRequiredService<OrderBookStore>(),
                _serviceProvider.GetRequiredService<BinanceExchangeUtil>()
            );

            const string orderBookSaveDirPath = "../CryptoTradeBot/data-logs/binance/order-book-store";

            if(!Directory.Exists(orderBookSaveDirPath))
            {
                throw new Exception($"Dicrectory with data logs doesn't exist!");
            }

            var orderBookStore = _serviceProvider.GetRequiredService<OrderBookStore>();
            var filePathes = Directory.GetFiles(orderBookSaveDirPath).OrderBy(x => x).ToList();
            DateTime prevSnapshotAt = DateTime.MinValue;
            bool isFirstSnapshot = true;
            for(int i = 0; i < filePathes.Count; i++)
            {
                string filePath = filePathes[i];
                string fileName = Path.GetFileNameWithoutExtension(filePath);

                _logger.LogInformation($"----------Processing ({i + 1}/{filePathes.Count}) '{fileName}'...");

                DateTime currentSnapshotAt = DateTime.ParseExact(fileName, "yyyyMMdd'T'HHmmss'.'fff'Z'", null);
                DateTime.SpecifyKind(currentSnapshotAt, DateTimeKind.Utc);

                TimeSpan maxIntervalBetweenSnapshots = TimeSpan.FromSeconds(10 + 5);
                var interval = currentSnapshotAt.Subtract(prevSnapshotAt);
                if (interval > maxIntervalBetweenSnapshots && !isFirstSnapshot)
                {
                    continue;
                }

                // setup store from snaphot
                string fileContent = File.ReadAllText(filePath);
                orderBookStore.ImportFromJson(fileContent);

                // run algorith
                string startAsset = "IOTA";
                decimal startAssetAmount = 1000;
                var solutions = circlePathAlgorithm.Solve(startAsset, startAssetAmount);

                if(solutions.Count != 0)
                {
                    var a = 1;
                }

                _logger.LogInformation($"----------Processed ({i + 1}/{filePathes.Count}) '{fileName}'.");

                isFirstSnapshot = false;
                prevSnapshotAt = currentSnapshotAt;
            }

            _logger.LogInformation("Simulation is done.");
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Stopping...");
            return Task.CompletedTask;
        }
    }
}
