using CryptoTradeBot.Exchanges.Binance.Dtos;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace CryptoTradeBot.Exchanges.Binance.Handlers
{
    public class WssStreamMessageHandleManager
    {
        private Dictionary<string, List<IWssMessageHandler>> _messageHandlers = new Dictionary<string, List<IWssMessageHandler>>();

        public void RegisterStreamHandler(string stream, IWssMessageHandler wssMessageHandler)
        {
            if (!_messageHandlers.ContainsKey(stream))
            {
                _messageHandlers.Add(stream, new List<IWssMessageHandler>());
            }

            if (wssMessageHandler == null)
            {
                return;
            }

            _messageHandlers[stream].Add(wssMessageHandler);
        }

        public async Task HandleStreamMessageAsync(string message)
        {
            // test message is combined
            WssCombinedStreamPayloadDto<object> messageObject = null;
            try
            {
                messageObject = JsonConvert.DeserializeObject<WssCombinedStreamPayloadDto<object>>(message);
            }
            catch (Exception ex)
            {
                // message is of some other type. exit.
                return;
            }

            if (messageObject == null || messageObject.Stream == null || messageObject.Data == null)
            {
                // message is of some other type. exit.
                return;
            }

            if (!_messageHandlers.ContainsKey(messageObject.Stream))
            {
                return;
            }

            var handlers = _messageHandlers[messageObject.Stream];
            var tasks = handlers.Select(handler => handler.HandleMessageAsync(messageObject.Stream, message)).ToList();
            await Task.WhenAll(tasks);
        }
    }
}
