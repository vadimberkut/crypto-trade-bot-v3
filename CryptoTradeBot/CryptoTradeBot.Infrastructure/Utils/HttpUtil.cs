using CryptoTradeBot.Infrastructure.AplicationSettings;
using CryptoTradeBot.Infrastructure.Config;
using CryptoTradeBot.Infrastructure.Exceptions;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace CryptoTradeBot.Infrastructure.Utils
{
    public class HttpUtil
    {
        private readonly ILogger<HttpUtil> _logger;
        private readonly IHttpClientFactory _httpClientFactory;

        private HttpClient __httpClient = null;
        private HttpClient _client => __httpClient ?? _httpClientFactory.CreateClient();

        public HttpUtil(
            ILogger<HttpUtil> logger,
            IHttpClientFactory httpClientFactory
        )
        {
            _logger = logger;
            _httpClientFactory = httpClientFactory;
        }

        #region Http helper methods

        public async Task<HttpResponseMessage> GetAsync(string url, string accessToken = null)
        {
            var requestMessage = new HttpRequestMessage(HttpMethod.Get, url);
            if (!String.IsNullOrEmpty(accessToken))
            {
                requestMessage.Headers.Authorization = new AuthenticationHeaderValue(JwtBearerDefaults.AuthenticationScheme, accessToken);
            }
            HttpResponseMessage httpResponse = await _client.SendAsync(requestMessage);
            return httpResponse;
        }

        public async Task<HttpResponseMessage> PostJsonAsync<T>(string url, T postData, string accessToken = null)
        {
            var requestMessage = new HttpRequestMessage(HttpMethod.Post, url);
            if (!String.IsNullOrEmpty(accessToken))
            {
                requestMessage.Headers.Authorization = new AuthenticationHeaderValue(JwtBearerDefaults.AuthenticationScheme, accessToken);
            }
            requestMessage.Content = new StringContent(JsonConvert.SerializeObject(postData, SerializationConfig.GetDefaultJsonSerializerSettings()), Encoding.UTF8, "application/json");
            var responseMessage = await _client.SendAsync(requestMessage);
            return responseMessage;
        }

        /// <summary>
        /// Sends postData as 'Content-Type: application/x-www-form-urlencoded'
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="url"></param>
        /// <param name="postData"></param>
        /// <param name="accessToken"></param>
        /// <returns></returns>
        public async Task<HttpResponseMessage> PostXWWWFormUrlencodedAsync<T>(string url, T postData, string accessToken = null)
        {
            var requestMessage = new HttpRequestMessage(HttpMethod.Post, url);
            if (!String.IsNullOrEmpty(accessToken))
            {
                requestMessage.Headers.Authorization = new AuthenticationHeaderValue(JwtBearerDefaults.AuthenticationScheme, accessToken);
            }

            var keyValuePairs = new List<KeyValuePair<string, string>>();
            var props = typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance);
            foreach (var prop in props)
            {
                keyValuePairs.Add(new KeyValuePair<string, string>(prop.Name, prop.GetValue(postData).ToString()));
            }

            var formContent = new FormUrlEncodedContent(keyValuePairs);

            requestMessage.Content = formContent;
            var responseMessage = await _client.SendAsync(requestMessage);
            return responseMessage;
        }

        public async Task<HttpResponseMessage> PutJsonAsync<T>(string url, T postData, string accessToken = null)
        {
            var requestMessage = new HttpRequestMessage(HttpMethod.Put, url);
            if (!String.IsNullOrEmpty(accessToken))
            {
                requestMessage.Headers.Authorization = new AuthenticationHeaderValue(JwtBearerDefaults.AuthenticationScheme, accessToken);
            }
            requestMessage.Content = new StringContent(JsonConvert.SerializeObject(postData, SerializationConfig.GetDefaultJsonSerializerSettings()), Encoding.UTF8, "application/json");
            var responseMessage = await _client.SendAsync(requestMessage);
            return responseMessage;
        }

        public async Task<HttpResponseMessage> DeleteAsync(string url, string accessToken = null)
        {
            var requestMessage = new HttpRequestMessage(HttpMethod.Delete, url);
            if (!String.IsNullOrEmpty(accessToken))
            {
                requestMessage.Headers.Authorization = new AuthenticationHeaderValue(JwtBearerDefaults.AuthenticationScheme, accessToken);
            }
            var responseMessage = await _client.SendAsync(requestMessage);
            return responseMessage;
        }

        public async Task<HttpResponseMessage> DeleteAsync<T>(string url, T postData, string accessToken = null)
        {
            var requestMessage = new HttpRequestMessage(HttpMethod.Delete, url);
            if (!String.IsNullOrEmpty(accessToken))
            {
                requestMessage.Headers.Authorization = new AuthenticationHeaderValue(JwtBearerDefaults.AuthenticationScheme, accessToken);
            }
            requestMessage.Content = new StringContent(JsonConvert.SerializeObject(postData, SerializationConfig.GetDefaultJsonSerializerSettings()), Encoding.UTF8, "application/json");
            var responseMessage = await _client.SendAsync(requestMessage);
            return responseMessage;
        }

        #endregion

        #region Assertion Helpers

        /// <summary>
        /// Ensures that Http response has successfull status code. If not throws an exception with
        /// detailed server response
        /// </summary>
        /// <param name="httpResponse"></param>
        public void EnsureSuccessStatusCode(HttpResponseMessage httpResponse)
        {
            if (!httpResponse.IsSuccessStatusCode)
            {
                throw new HttpStatusException(httpResponse);
            }
        }

        #endregion
    }
}
