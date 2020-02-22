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
using CryptoTradeBot.WebHost.Algorithms.CirclePathAlgorithm.Models;
using CryptoTradeBot.Infrastructure.Extensions;

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
            DateTime? prevSnapshotAt = null;
            DateTime? nextSnapshotMustBeAt = null;
            TimeSpan maxIntervalBetweenSnapshots = TimeSpan.FromSeconds((10 + 5));
            TimeSpan snapshotToSelectWindow = TimeSpan.FromSeconds(60);
            TimeSpan simulationTime = TimeSpan.FromSeconds(0);
            List<CirclePathSolutionItemModel> bestSolutionsFroEachIteration = new List<CirclePathSolutionItemModel>();
            for (int i = 0; i < filePathes.Count; i += 1)
            {
                string filePath = filePathes[i];
                string fileName = Path.GetFileNameWithoutExtension(filePath);

                _logger.LogInformation($"----------Processing ({i + 1}/{filePathes.Count}) '{fileName}'...");

                DateTime currentSnapshotAt = DateTime.ParseExact(fileName, "yyyyMMdd'T'HHmmss'.'fff'Z'", null);
                DateTime.SpecifyKind(currentSnapshotAt, DateTimeKind.Utc);

                // throttle
                nextSnapshotMustBeAt = nextSnapshotMustBeAt ?? DateTime.MinValue;
                if (currentSnapshotAt < nextSnapshotMustBeAt)
                {
                    prevSnapshotAt = currentSnapshotAt;
                    continue;
                }

                var intervalBetweenSnapshots = currentSnapshotAt.Subtract(prevSnapshotAt.GetValueOrDefault());
                if (prevSnapshotAt == null || intervalBetweenSnapshots > maxIntervalBetweenSnapshots)
                {
                    prevSnapshotAt = currentSnapshotAt;
                    continue;
                }

                simulationTime = simulationTime.Add(intervalBetweenSnapshots);

                // setup store from snaphot
                string fileContent = File.ReadAllText(filePath);
                orderBookStore.ImportFromJson(fileContent);

                // run algorith
                //string startAsset = "IOTA";
                //decimal startAssetAmount = 1000;
                string startAsset = "BTC";
                decimal startAssetAmount = 0.1m;
                var solutions = circlePathAlgorithm.Solve(startAsset, startAssetAmount);

                if(solutions.Count != 0)
                {
                    // take the most profitable
                    bestSolutionsFroEachIteration.Add(solutions.OrderByDescending(x => x.SimulationResult.EstimatedProfitInStartAsset).First());
                    
                    foreach (var solution in solutions)
                    {
                        _logger.LogInformation($"Solution: amount={solution.SimulationResult.TargetStartAssetAmount}, profit={solution.SimulationResult.EstimatedProfitInStartAsset}, profit USDT={solution.SimulationResult.EstimatedProfitInUSDTAsset}.");
                    }
                }

                _logger.LogInformation($"----------Processed ({i + 1}/{filePathes.Count}) '{fileName}'.");

                prevSnapshotAt = currentSnapshotAt;
                nextSnapshotMustBeAt = currentSnapshotAt.Add(snapshotToSelectWindow);
            }

            // take only uniq results - filter out repeatable
            var aggregatedDictionary = bestSolutionsFroEachIteration
                .Select(solution => new Tuple<string, CirclePathSolutionItemModel>(solution.PathId, solution))
                .Aggregate
                    <Tuple<string, CirclePathSolutionItemModel>, Dictionary<string, IEnumerable<CirclePathSolutionItemModel>>>
                    (new Dictionary<string, IEnumerable<CirclePathSolutionItemModel>>(), (accum, curr) =>
                    {
                        if(!accum.ContainsKey(curr.Item1))
                        {
                            accum.Add(curr.Item1, new List<CirclePathSolutionItemModel>());
                        }
                        (accum[curr.Item1] as List<CirclePathSolutionItemModel>).Add(curr.Item2);
                        return accum;
                    });

            var aggregatedUniqueDictionary = aggregatedDictionary
                .Distinct()
                .DistinctByKeyValues(x => x.SimulationResult.EstimatedProfitInStartAsset)
                .ToDictionary(x => x.Key, y => y.Value);

            var uniqueBestSolutionsFroEachIteration = aggregatedUniqueDictionary.SelectMany(x => x.Value).ToList();

            //// 2
            //var dict = new Dictionary<string, Dictionary<string, CirclePathSolutionItemModel>>();
            //foreach (var item in bestSolutionsFroEachIteration)
            //{
            //    if(!dict.ContainsKey(item.PathId))
            //    {
            //        dict.Add(item.PathId, new Dictionary<string, CirclePathSolutionItemModel>());
            //    }
            //    if (!dict[item.PathId].ContainsKey(item.SimulationResult.EstimatedProfitInStartAsset.ToString("0.########")))
            //    {
            //        dict[item.PathId].Add(item.SimulationResult.EstimatedProfitInStartAsset.ToString("0.########"), item);
            //    }
            //}
            //var dcitres = dict.ToList().Select(x => x.Value).SelectMany(x => x.Values.Select(y => y)).ToList();

            _logger.LogInformation($"");
            _logger.LogInformation($"Simulation is done. Real time simulated: {simulationTime}.");

            _logger.LogInformation($"");
            _logger.LogInformation($"Solution list:");
            foreach (var solution in uniqueBestSolutionsFroEachIteration)
            {
                _logger.LogInformation($"Solution: amount={solution.SimulationResult.TargetStartAssetAmount}, profit={solution.SimulationResult.EstimatedProfitInStartAsset}, profit USDT={solution.SimulationResult.EstimatedProfitInUSDTAsset}, pathId={solution.PathId}.");
            }

            // calc total profit
            decimal totalEstimatedProfit = uniqueBestSolutionsFroEachIteration.Aggregate<CirclePathSolutionItemModel, decimal>(0, (accum, curr) => accum + curr.SimulationResult.EstimatedProfitInStartAsset);
            decimal totalEstimatedProfitUsdt = uniqueBestSolutionsFroEachIteration.Aggregate<CirclePathSolutionItemModel, decimal>(0, (accum, curr) => accum + curr.SimulationResult.EstimatedProfitInUSDTAsset);
            _logger.LogInformation($"");
            _logger.LogInformation($"Solution total: profit={totalEstimatedProfit}, profit USDT={totalEstimatedProfitUsdt}.");
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Stopping...");
            return Task.CompletedTask;
        }
    }
}
