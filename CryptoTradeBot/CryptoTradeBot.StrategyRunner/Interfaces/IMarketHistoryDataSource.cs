using CryptoTradeBot.Infrastructure.Models;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace CryptoTradeBot.StrategyRunner.Interfaces
{
    public interface IMarketHistoryDataSource
    {
        Task<GeneralSymbolBarHistoryModel> GetSymbolHistoryDataAsync(string symbol, string barInterval, DateTime from, DateTime to);
    }
}
