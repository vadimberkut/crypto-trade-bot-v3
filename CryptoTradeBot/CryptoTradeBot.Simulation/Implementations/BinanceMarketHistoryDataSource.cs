using CryptoTradeBot.Host.Exchanges.Binance.Clients;
using CryptoTradeBot.Infrastructure.Models;
using CryptoTradeBot.StrategyRunner.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Microsoft.AspNetCore.Hosting;

namespace CryptoTradeBot.Simulation.Implementations
{
    public class BinanceMarketHistoryDataSource : IMarketHistoryDataSource
    {
        private readonly ILogger<BinanceMarketHistoryDataSource> _logger;
        private readonly IWebHostEnvironment _webHostEnvironment;
        private readonly IServiceProvider _serviceProvider;
        private readonly BinanceHttpClient _binanceHttpClient;

        public BinanceMarketHistoryDataSource(
            ILogger<BinanceMarketHistoryDataSource> logger,
            IWebHostEnvironment webHostEnvironment,
            IServiceProvider serviceProvider,
            BinanceHttpClient binanceHttpClient
        )
        {
            _logger = logger;
            _webHostEnvironment = webHostEnvironment;
            _serviceProvider = serviceProvider;
            _binanceHttpClient = binanceHttpClient;
        }

        public async Task<GeneralSymbolBarHistoryModel> GetSymbolHistoryDataAsync(string symbol, string barInterval, DateTime from, DateTime to)
        {
            var binanceHttpClient = this._serviceProvider.GetRequiredService<BinanceHttpClient>();

            string historyDataStoreDirectoryPath = Path.Combine(_webHostEnvironment.ContentRootPath, "./market-data-store/");
            string barkDataFileNameFormat = "{0}__{1}__{2}__{3}.json"; // symbol__interval_from__to.json

            if (!Directory.Exists(historyDataStoreDirectoryPath))
            {
                Directory.CreateDirectory(historyDataStoreDirectoryPath);
            }

            // download history data if not downloaded already or read from it
            string barkDataFilePath = Path.Combine(
                historyDataStoreDirectoryPath,
                String.Format(
                    barkDataFileNameFormat,
                    symbol,
                    barInterval,
                    from.ToString("yyyyMMdd'Z'"),
                    to.ToString("yyyyMMdd'Z'")
                )
            );
            GeneralSymbolBarHistoryModel symbolBarkHistory;
            if (File.Exists(barkDataFilePath))
            {
                _logger.LogInformation($"Loading from file...");
                string content = File.ReadAllText(barkDataFilePath);
                symbolBarkHistory = JsonConvert.DeserializeObject<GeneralSymbolBarHistoryModel>(content);
            }
            else
            {
                _logger.LogInformation($"Loading through API...");

                // TODO: split from-to on intervals according to limit and BarkInterval
                var dto = await binanceHttpClient.GetCandlestickDataAsync(symbol, barInterval, from, to, 1000);
                symbolBarkHistory = new GeneralSymbolBarHistoryModel()
                {
                    Symbol = symbol,
                    From = from,
                    To = to,
                    BarInterval = barInterval,
                    Bars = dto.Candles.OrderBy(x => x.OpenTime).Select(x => new GeneralBarModel()
                    {
                        OpenTime = x.OpenTime,
                        CloseTime = x.CloseTime,
                        OpenPrice = x.OpenPrice,
                        HighPrice = x.HighPrice,
                        LowPrice = x.LowPrice,
                        ClosePrice = x.ClosePrice,
                        Volume = x.Volume,
                        QuoteAssetVolume = x.QuoteAssetVolume,
                    }).ToList(),
                };

                _logger.LogInformation($"Saving to file...");
                File.WriteAllText(barkDataFilePath, JsonConvert.SerializeObject(symbolBarkHistory));
            }

            return symbolBarkHistory;
        }
    }
}
