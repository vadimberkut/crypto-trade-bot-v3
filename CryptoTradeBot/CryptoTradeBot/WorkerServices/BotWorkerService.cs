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
using CryptoTradeBot.WebHost.Algorithms.CirclePathAlgorithm;

namespace CryptoTradeBot.WorkerServices
{
    public class BotWorkerService : IHostedService
    {
        private readonly ILogger<BotWorkerService> _logger;
        private readonly IOptions<ApplicationSettings> _config;
        private readonly IServiceProvider _serviceProvider;
        private readonly BinanceHttpClient _binanceHttpClient;
        private readonly BinanceWssClient _binanceWssClient;

        public BotWorkerService(
            ILogger<BotWorkerService> logger,
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
            _logger.LogInformation("Starting...");

            var isAvailable = await _binanceHttpClient.TestConnectivityAsync();
            if(!isAvailable)
            {
                throw new Exception($"Binance isn't available!");
            }

            var exchangeInfo = await _binanceHttpClient.ExchangeInformationAsync();
            exchangeInfo.Symbols = exchangeInfo.Symbols.Where(x => x.Status == "TRADING").ToList();

            int depth = 20; // 5, 10 ,20
            var bookStreams = CirclePahtAlgorithmConfig.AllowedSymbols.Select(symbol => $"{symbol.ToLowerInvariant()}@depth{depth}").ToList();

            var wssStreamMessageHandleManager = new WssStreamMessageHandleManager();
            foreach (var stream in bookStreams)
            {
                wssStreamMessageHandleManager.RegisterStreamHandler(stream, _serviceProvider.GetService<WssBookDepthHandler>());
            }

            _binanceWssClient.SetWssStreamMessageHandleManager(wssStreamMessageHandleManager);
            await _binanceWssClient.ConnectAsync();

            //// subscribe

            // Partial Book Depth Streams
            // Stream Names: <symbol>@depth<levels> OR <symbol>@depth<levels>@100ms
            await Task.WhenAll(bookStreams.Select(stream => _binanceWssClient.SubscribeToStreamAsync(stream)));

            // save order book to file periodically
            const string orderBookSaveDirPath = "./data-logs/binance/order-book-store";
            this._SetInterval(async () =>
            {
                Directory.CreateDirectory(orderBookSaveDirPath);

                string fileName = $"{DateTime.UtcNow.ToString("yyyyMMdd'T'HHmmss'.'fff'Z'")}.log";

                var orderBookStore = _serviceProvider.GetService<OrderBookStore>();
                string contents = orderBookStore.SerializeToJson(Formatting.None);
                File.WriteAllText(Path.Combine(orderBookSaveDirPath, fileName), contents);
                _logger.LogInformation("Saved order book on disk.");
            }, TimeSpan.FromSeconds(10), cancellationToken);

            // 
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Stopping...");

            _binanceWssClient.Dispose();

            return Task.CompletedTask;
        }

        private void _SetInterval(Func<Task> action, TimeSpan interval, CancellationToken cancellationToken)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                _logger.LogInformation("SetInterval: exit.");
                return;
            }

            ThreadPool.QueueUserWorkItem(async (state) =>
            {
                await this._IntervalJob(action, interval, cancellationToken);
            });
        }
      
        private async Task _IntervalJob(Func<Task> action, TimeSpan interval, CancellationToken cancellationToken, DateTime prevRun = default(DateTime))
        {
            await Task.Delay(interval, cancellationToken).ConfigureAwait(false);

            Stopwatch stopwatch = new Stopwatch();
            stopwatch.Start();
            prevRun = prevRun == default(DateTime) ? DateTime.UtcNow.Subtract(interval) : prevRun;
            TimeSpan passedFromPrevRun = DateTime.UtcNow.Subtract(prevRun);
            await Task.Run(action).ConfigureAwait(false);
            stopwatch.Stop();

            // _logger.LogInformation($"SetInterval: action has just been run. Elapsed time: '{stopwatch.Elapsed}'.Prev run was '{passedFromPrevRun}' ago.");


            if (cancellationToken.IsCancellationRequested)
            {
                _logger.LogInformation("SetInterval: exit.");
                return;
            }

            ThreadPool.QueueUserWorkItem(async (state) =>
            {
                await this._IntervalJob(action, interval, cancellationToken, DateTime.UtcNow);
            });
        }
    }
}
