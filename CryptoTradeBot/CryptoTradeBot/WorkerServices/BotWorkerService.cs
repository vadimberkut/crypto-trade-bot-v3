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

namespace CryptoTradeBot.WorkerServices
{
    public class BotWorkerService : IHostedService
    {
        private readonly ILogger<BotWorkerService> _logger;
        private readonly IOptions<ApplicationSettings> _config;
        private readonly IServiceProvider _serviceProvider;
        private readonly BinanceWssClient _binanceWssClient;

        public BotWorkerService(
            ILogger<BotWorkerService> logger,
            IOptions<ApplicationSettings> config,
            IServiceProvider serviceProvider,
            BinanceWssClient binanceWssClient
        )
        {
            _logger = logger;
            _config = config;
            _serviceProvider = serviceProvider;
            _binanceWssClient = binanceWssClient;
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Starting...");

            int depth = 20; // 5, 10 ,20
            var bookStreams = new List<string>()
            {
                // BTC
                $"ethbtc@depth{depth}",
                $"xrpbtc@depth{depth}",
                $"hbarbtc@depth{depth}",
                $"bnbbtc@depth{depth}",
                $"ltcbtc@depth{depth}",
                $"chzbtc@depth{depth}",
                $"wrxbtc@depth{depth}",
                $"etcbtc@depth{depth}",
                $"xtzbtc@depth{depth}",
                $"trxbtc@depth{depth}",
                $"eosbtc@depth{depth}",
                $"adabtc@depth{depth}",
                $"neobtc@depth{depth}",
                $"bchbtc@depth{depth}",
                $"xlmbtc@depth{depth}",
                $"xmrbtc@depth{depth}",
                $"iotabtc@depth{depth}",
                $"dashbtc@depth{depth}",

                // USDT
                $"btcusdt@depth{depth}",
                $"ethusdt@depth{depth}",
                $"xrpusdt@depth{depth}",
                $"bnbusdt@depth{depth}",
                $"ltcusdt@depth{depth}",
                $"bchusdt@depth{depth}",
                $"etcusdt@depth{depth}",
                $"eosusdt@depth{depth}",
                $"trxusdt@depth{depth}",
                $"hbarusdt@depth{depth}",
                $"adausdt@depth{depth}",
                $"xlmusdt@depth{depth}",
                $"xtzusdt@depth{depth}",
                $"neousdt@depth{depth}",
                $"dashusdt@depth{depth}",
                $"iotausdt@depth{depth}",
                $"xmrusdt@depth{depth}",

                // BNB
                $"fttbnb@depth{depth}",
                $"xrpbnb@depth{depth}",
                $"hbarbnb@depth{depth}",
                $"trxbnb@depth{depth}",
                $"xtzbnb@depth{depth}",
                $"chzbnb@depth{depth}",
                $"ltcbnb@depth{depth}",
                $"eosbnb@depth{depth}",
                $"adabnb@depth{depth}",
                $"bchbnb@depth{depth}",
                $"etcbnb@depth{depth}",
                $"iotabnb@depth{depth}",
                $"xlmbnb@depth{depth}",
                $"neobnb@depth{depth}",
                $"dashbnb@depth{depth}",
                $"xmrbnb@depth{depth}",
                $"lskbnb@depth{depth}",

                // BUSD
                $"btcbusd@depth{depth}",
                $"xrpbusd@depth{depth}",
                $"ethbusd@depth{depth}",
                $"bnbbusd@depth{depth}",
                $"ltcbusd@depth{depth}",
                $"trxbusd@depth{depth}",
                $"xlmbusd@depth{depth}",
                $"bchbusd@depth{depth}",
                $"eosbusd@depth{depth}",
                $"adabusd@depth{depth}",
                $"xtzbusd@depth{depth}",
                $"etcbusd@depth{depth}",
                $"neobusd@depth{depth}",
                $"dashbusd@depth{depth}",

                // TUSD
                $"btctusd@depth{depth}",
                $"ethtusd@depth{depth}",
                $"bchtusd@depth{depth}",
                $"xrptusd@depth{depth}",
                $"ltctusd@depth{depth}",
                $"xlmtusd@depth{depth}",
                $"trxtusd@depth{depth}",
                $"neotusd@depth{depth}",
                $"adatusd@depth{depth}",
                $"eostusd@depth{depth}",
                $"bnbtusd@depth{depth}",
                $"usdctusd@depth{depth}",

                // USDC
                $"btcusdc@depth{depth}",
                $"ethusdc@depth{depth}",
                $"xrpusdc@depth{depth}",
                $"ltcusdc@depth{depth}",
                $"bchusdc@depth{depth}",
                $"trxusdc@depth{depth}",
                $"bnbusdc@depth{depth}",
                $"eosusdc@depth{depth}",
                $"neousdc@depth{depth}",
                $"adausdc@depth{depth}",

                // ETH
                $"bnbeth@depth{depth}",
                $"xrpeth@depth{depth}",
                $"eoseth@depth{depth}",
                $"trxeth@depth{depth}",
                $"adaeth@depth{depth}",
                $"ltceth@depth{depth}",
                $"etceth@depth{depth}",
                $"xlmeth@depth{depth}",
                $"neoeth@depth{depth}",
                $"iotaeth@depth{depth}",
                $"xmreth@depth{depth}",
                $"dasheth@depth{depth}",

                // TRX

                // XRP
                 $"trxxrp@depth{depth}",
            };

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
            this.SetInterval(async () =>
            {
                Directory.CreateDirectory(orderBookSaveDirPath);

                string fileName = $"{DateTime.UtcNow.ToString("yyyyMMdd'T'HHmmss'.'fff'Z'")}.log";

                var orderBookStore = _serviceProvider.GetService<OrderBookStore>();
                string contents = orderBookStore.SerializeToJson(Formatting.None);
                File.WriteAllText(Path.Combine(orderBookSaveDirPath, fileName), contents);
                _logger.LogInformation("Saved order book on disk.");
            }, TimeSpan.FromSeconds(10), cancellationToken);
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Stopping...");

            _binanceWssClient.Dispose();

            return Task.CompletedTask;
        }

        private void SetInterval(Func<Task> action, TimeSpan interval, CancellationToken cancellationToken)
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
