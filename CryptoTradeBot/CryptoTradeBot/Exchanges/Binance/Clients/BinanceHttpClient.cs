using CryptoTradeBot.Host.Exchanges.Binance.Dtos;
using CryptoTradeBot.Infrastructure.AplicationSettings;
using CryptoTradeBot.Infrastructure.Utils;
using CryptoTradeBot.WebHost.Exchanges.Binance;
using CryptoTradeBot.WebHost.Exchanges.Binance.Dtos;
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


        #region Market Data endpoints

        /// <summary>
        /// Ordered by ASC
        /// </summary>
        public async Task<HttpCandlestickDataResponseDto> GetCandlestickDataAsync(
            string symbol, 
            string candlestickInterval, 
            DateTime? from = null, 
            DateTime? to = null, 
            int limit = 500
        )
        {
            limit = Math.Max(limit, 1000);

            // split from-to on intervals according to limit and candlestickInterval
            // as Binance allows max 1000 items to request, but date range could be huge (e.g. 1 year of 1h intervals)
            if (from.Value != null && to.Value != null)
            {
                var candlestickIntervalConfig = BinanceConfig.GetCandlestickChartInterval(candlestickInterval);
                TimeSpan requestedRange = to.Value.Subtract(from.Value);
                int candlestickIntervalsInDateRange = Convert.ToInt32(Math.Round(requestedRange.TotalMilliseconds / candlestickIntervalConfig.TimeSpan.TotalMilliseconds, 0));

                if(candlestickIntervalsInDateRange > limit)
                {
                    int requestCount = Convert.ToInt32(Math.Ceiling((double)candlestickIntervalsInDateRange / (double)limit));
                    TimeSpan requestTimeSpan = limit * candlestickIntervalConfig.TimeSpan; // E.g. 4h * 1000 = 4000 hours
                    var aggregatedResult = new HttpCandlestickDataResponseDto();
                    DateTime localFrom = from.Value;
                    DateTime localTo = from.Value;
                    for (int i = 0; i < requestCount; i++)
                    {
                        localFrom = localTo;
                        localTo = localFrom.Add(requestTimeSpan);
                        localTo = localTo <= DateTime.UtcNow ? localTo : DateTime.UtcNow;

                        var intermediateResult = await _GetCandlestickDataAsync(symbol, candlestickInterval, localFrom, localTo, limit);
                        aggregatedResult.Candles.AddRange(intermediateResult.Candles);
                    }

                    // order asc
                    aggregatedResult.Candles = aggregatedResult.Candles.OrderBy(x => x.OpenTime).ToList();

                    return aggregatedResult;
                }
            }

            return await _GetCandlestickDataAsync(symbol, candlestickInterval, from, to, limit);
        }
        private async Task<HttpCandlestickDataResponseDto> _GetCandlestickDataAsync(
            string symbol,
            string candlestickInterval,
            DateTime? from,
            DateTime? to,
            int limit
        )
        {
            string startTime = from.Value != null ? new DateTimeOffset(from.Value).ToUnixTimeMilliseconds().ToString() : string.Empty;
            string endTime = to.Value != null ? new DateTimeOffset(to.Value).ToUnixTimeMilliseconds().ToString() : string.Empty;

            var httpResponse = await _httpUtil.GetAsync($"{_binanceConfig.HttpApiUrl}/api/{_apiVersion}/klines?symbol={symbol}&interval={candlestickInterval}&startTime={startTime}&endTime={endTime}&limit={limit}");
            _httpUtil.EnsureSuccessStatusCode(httpResponse);
            string httpContent = await httpResponse.Content.ReadAsStringAsync();
            var response = JsonConvert.DeserializeObject<IEnumerable<List<object>>>(httpContent);
            var dto = new HttpCandlestickDataResponseDto()
            {
                Candles = response.Select(candle =>
                {
                    return new HttpCandlestickDataItemResponseDto()
                    {
                        OpenTime = DateTimeOffset.FromUnixTimeMilliseconds(long.Parse(candle[0].ToString())).UtcDateTime,
                        OpenPrice = decimal.Parse(candle[1].ToString()),
                        HighPrice = decimal.Parse(candle[2].ToString()),
                        LowPrice = decimal.Parse(candle[3].ToString()),
                        ClosePrice = decimal.Parse(candle[4].ToString()),
                        Volume = decimal.Parse(candle[5].ToString()),
                        CloseTime = DateTimeOffset.FromUnixTimeMilliseconds(long.Parse(candle[6].ToString())).UtcDateTime,
                        QuoteAssetVolume = decimal.Parse(candle[7].ToString()),
                        NumberOfTrades = int.Parse(candle[8].ToString()),
                        TakerBuyBaseAssetVolume = decimal.Parse(candle[9].ToString()),
                        TakerBuyQuoteAssetVolume = decimal.Parse(candle[10].ToString()),
                        ApiIgnoredValue = candle[11].ToString(),

                    };
                }).ToList(),
            };

            // order asc
            dto.Candles = dto.Candles.OrderBy(x => x.OpenTime).ToList();

            return dto;
        }

        #endregion
    }
}
