using CryptoTradeBot.Host.Exchanges.Binance.Dtos;
using CryptoTradeBot.Infrastructure.AplicationSettings;
using CryptoTradeBot.Infrastructure.Utils;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;

namespace CryptoTradeBot.Host.Exchanges.Binance.Clients
{
    // TODO: handle rate limited
    public class BinanceHttpClient
    {
        private readonly ILogger<BinanceHttpClient> _logger;
        private readonly BinanceSettings _binanceConfig;
        private readonly HttpUtil _httpUtil;

        private const string _apiVersion = "v3";

        public BinanceHttpClient(
            ILogger<BinanceHttpClient> logger,
            BinanceSettings binanceConfig,
            HttpUtil httpUtil
        )
        {
            _logger = logger;
            _binanceConfig = binanceConfig;
            _httpUtil = httpUtil;
        }

        #region General endpoints

        public async Task<bool> TestConnectivityAsync()
        {
            var httpResponse = await _httpUtil.GetAsync($"{_binanceConfig.HttpApiUrl}/api/{_apiVersion}/ping");
            if(httpResponse.IsSuccessStatusCode)
            {
                return true;
            }
            return false;
        }

        public async Task<HttpServerTimeResponseDto> CheckServerTimeAsync()
        {
            var httpResponse = await _httpUtil.GetAsync($"{_binanceConfig.HttpApiUrl}/api/{_apiVersion}/time");
            _httpUtil.EnsureSuccessStatusCode(httpResponse);
            string httpContent = await httpResponse.Content.ReadAsStringAsync();
            var dto = JsonConvert.DeserializeObject<HttpServerTimeResponseDto>(httpContent);
            return dto;
        }

        public async Task<HttpExchangeInformationResponseDto> ExchangeInformationAsync()
        {
            var httpResponse = await _httpUtil.GetAsync($"{_binanceConfig.HttpApiUrl}/api/{_apiVersion}/exchangeInfo");
            _httpUtil.EnsureSuccessStatusCode(httpResponse);
            string httpContent = await httpResponse.Content.ReadAsStringAsync();
            var dto = JsonConvert.DeserializeObject<HttpExchangeInformationResponseDto>(httpContent);
            return dto;
        }

        #endregion
    }
}
