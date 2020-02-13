using CryptoTradeBot.Exchanges.Binance.Dtos;
using CryptoTradeBot.Exchanges.Binance.Handlers;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Linq;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;
using Websocket.Client;
using System.Reactive.Linq;
using System.Reactive.Concurrency;

namespace CryptoTradeBot.Exchanges.Binance
{
    public class BinanceWssClient : IDisposable
    {
        private readonly ILogger<BinanceWssClient> _logger;
        private readonly BinanceSettings _binanceConfig;

        private WebsocketClient _websocketClient;
        private Dictionary<uint, AutoResetEvent> _pendingAcknowledgements = new Dictionary<uint, AutoResetEvent>();
        private WssStreamMessageHandleManager _wssStreamMessageHandleManager = null;
        private bool _isConnectCalled = false;

        public BinanceWssClient(
            ILogger<BinanceWssClient> logger,
            BinanceSettings binanceConfig
        )
        {
            _logger = logger;
            _binanceConfig = binanceConfig;
        }

        public void SetWssStreamMessageHandleManager(WssStreamMessageHandleManager instance)
        {
            _wssStreamMessageHandleManager = instance;
        }

        public async Task ConnectAsync()
        {
            // Websocket.Client (wrapper of System.Net.WebSockets.ClientWebSocket)
            // NB: no way to disable its logs. Will span the whole console.
            // NB: added source to project to disable logs
            // https://github.com/Marfusios/websocket-client

            // WebSocketSharp
            // NB: doesn't support .NetStandart
            // https://github.com/sta/websocket-sharp

            // WebSocketSharp-netstandard (fork of WebSocketSharp)
            // NB: supports .NetStandart
            // https://github.com/PingmanTools/websocket-sharp/

            _websocketClient = new WebsocketClient(new Uri(_binanceConfig.WssUrl));
            _websocketClient.ReconnectTimeout = TimeSpan.FromSeconds(30);
            _websocketClient.ReconnectionHappened.Subscribe(info => 
            {
                _logger.LogInformation($"Reconnection happened, type: '{info.Type}'.");
            });
            _websocketClient.MessageReceived
                //.Where(msg => msg.)
                .ObserveOn(TaskPoolScheduler.Default) // run in parallel
                .Subscribe(async msg =>
                {
                    //Log.Information($"Message received: {msg}");

                    if (msg.MessageType == WebSocketMessageType.Text)
                    {
                        this.InternalHandleMessage(msg.Text);

                        if(_wssStreamMessageHandleManager != null)
                        {
                            await _wssStreamMessageHandleManager.HandleStreamMessageAsync(msg.Text);
                        }
                    }
                    else if (msg.MessageType == WebSocketMessageType.Binary)
                    {
                        throw new Exception($"Wss message type '{msg.MessageType}' isn't supported.");
                    }
                    else if (msg.MessageType == WebSocketMessageType.Close)
                    {
                        throw new Exception($"Wss message type '{msg.MessageType}' isn't supported.");
                    }
                });
            _websocketClient.Start();

            _isConnectCalled = true;

            await this.EnableCombinedStreamPayloadsAsync();
        }

        public async Task EnableCombinedStreamPayloadsAsync()
        {
            this.CheckConnectCalled();

            // set 'combined' property to tell that the single connection is combined
            // and used for more than one channels
            // normal message example: {...data goes directly here}
            // combined message example: {"stream":"iotausdt@depth10","data":{...}}
            var requestId = this.GetNextRequestId();
            _websocketClient.Send(JsonConvert.SerializeObject(new
            {
                method = "SET_PROPERTY",
                @params = new List<object>() {
                    "combined",
                    true
                },
                id = requestId,
            }));

            await this.WaitAcknowledgementAsync(requestId);
        }

        public async Task SubscribeToStreamAsync(string stream)
        {
            this.CheckConnectCalled();

            var requestId = this.GetNextRequestId();
            var subscribeRequest = new
            {
                method = "SUBSCRIBE",
                @params = new List<string>() {
                    stream
                },
                id = requestId,
            };
            _websocketClient.Send(JsonConvert.SerializeObject(subscribeRequest));

            await this.WaitAcknowledgementAsync(requestId);
        }

        private object _requestIdGenerateLock = new object();
        private uint _lastRequestId = 0;

        /// <summary>
        /// Returns request id hat must be unsigned int
        /// </summary>
        /// <returns></returns>
        private uint GetNextRequestId()
        {
            lock(_requestIdGenerateLock)
            {
                // reset if needed
                if (_lastRequestId == UInt32.MaxValue)
                {
                    _lastRequestId = 0;
                    // Interlocked.Exchange(ref _lastRequestId, 0);
                }

                _lastRequestId += 1;
                // Interlocked.Increment(ref _lastRequestId);

                return Convert.ToUInt32(_lastRequestId);
            }
        }

        private void CheckConnectCalled()
        {
            if (!_isConnectCalled)
            {
                throw new Exception($"Wss isn't connected. Call Connect first.");
            }
        }

        private Task<bool> WaitAcknowledgementAsync(uint requestId)
        {
            var exitEvent = new AutoResetEvent(false);
            _pendingAcknowledgements.Add(requestId, exitEvent);

            return Task.Run(() =>
            {
                // wait acknowledgement or timeout
                var received = exitEvent.WaitOne(TimeSpan.FromSeconds(10));
                if (!received)
                {
                    // if we get here then event was not handled
                    _logger.LogWarning($"Request with id '{requestId}' haven't been acknowledged.");
                    return false;
                }

                // if we get here then event was handled
                // _logger.LogInformation($"Request with id '{requestId}' have been acknowledged.");
                return true;
            });
        }

        private void InternalHandleMessage(string message)
        {
            var dto = JsonConvert.DeserializeObject<WssBaseResponseMessageDto>(message);
            if(dto != null && dto.Id != 0)
            {
                if(_pendingAcknowledgements.ContainsKey(dto.Id))
                {
                    _pendingAcknowledgements[dto.Id].Set();

                    // remove from pending
                    _pendingAcknowledgements.Remove(dto.Id);
                }
            }
        }

        // TODO: refactore
        public void Dispose()
        {
            _websocketClient.Dispose();
        }
    }
}
