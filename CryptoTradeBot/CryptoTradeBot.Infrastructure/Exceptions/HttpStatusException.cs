using Microsoft.Net.Http.Headers;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;

namespace CryptoTradeBot.Infrastructure.Exceptions
{
    public class HttpStatusException : Exception
    {
        private static string GetFormattedMessage(HttpResponseMessage httpResponseMessage)
        {
            string stringResponse = httpResponseMessage.Content.ReadAsStringAsync().GetAwaiter().GetResult();
            var message = $"Response status code does not indicate success: {httpResponseMessage.RequestMessage.Method} {httpResponseMessage.RequestMessage.RequestUri.ToString()} {httpResponseMessage.StatusCode}.";
            message += Environment.NewLine;
            message += $"{httpResponseMessage.RequestMessage.Method} {httpResponseMessage.RequestMessage.RequestUri.ToString()} {httpResponseMessage.StatusCode}.";
            message += Environment.NewLine;
            message += $"Server response: {stringResponse}";

            if (httpResponseMessage.StatusCode == HttpStatusCode.Unauthorized || httpResponseMessage.StatusCode == HttpStatusCode.Forbidden)
            {
                if (httpResponseMessage.Headers.Contains(HeaderNames.WWWAuthenticate))
                {
                    message += Environment.NewLine;
                    message += $"{HeaderNames.WWWAuthenticate}: {JsonConvert.SerializeObject(httpResponseMessage.Headers.GetValues(HeaderNames.WWWAuthenticate))}";
                    // TODO - add header with erros (don't remember how it is named)
                }
            }

            return message;
        }

        public HttpResponseMessage HttpResponseMessage;
        public HttpStatusCode HttpStatusCode => HttpResponseMessage.StatusCode;

        public HttpStatusException(HttpResponseMessage httpResponseMessage) : base(GetFormattedMessage(httpResponseMessage))
        {
            this.HttpResponseMessage = httpResponseMessage;
        }
    }
}
