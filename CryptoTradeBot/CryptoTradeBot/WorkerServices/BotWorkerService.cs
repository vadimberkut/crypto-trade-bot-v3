using Microsoft.Extensions.Hosting;
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

namespace CryptoTradeBot.WorkerServices
{
    public class BotWorkerService : IHostedService
    {
        public BotWorkerService()
        {

        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            // use Websocket.Client (wrapper of System.Net.WebSockets.ClientWebSocket)
            // https://github.com/Marfusios/websocket-client

            // string url = "wss://stream.binance.com:9443/ws/btcusdt@depth";
            string url = "wss://stream.binance.com:9443/ws";
            var exitEvent = new ManualResetEvent(false);

            using (var websocketClient = new WebsocketClient(new Uri(url)))
            {
                websocketClient.ReconnectTimeout = TimeSpan.FromSeconds(30);
                websocketClient.ReconnectionHappened.Subscribe(info =>
                    Log.Information($"Reconnection happened, type: {info.Type}"));

                websocketClient.MessageReceived.Subscribe(msg => 
                {
                    Log.Information($"Message received: {msg}");
                });
                websocketClient.Start();

                //// subscribe

                // Partial Book Depth Streams
                // Stream Names: <symbol>@depth<levels> OR <symbol>@depth<levels>@100ms
                var subscribeRequest = new
                {
                    method = "SUBSCRIBE",
                    @params = new List<string>() {
                        "btcusdt@depth10"
                    },
                    id = 1,
                };
                var subscribeRequestData = JsonConvert.SerializeObject(subscribeRequest);
                websocketClient.Send(subscribeRequestData);

                exitEvent.WaitOne();
            }
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }
    }
}
