using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace CryptoTradeBot.WebHost.Algorithms.CirclePathAlgorithm.Models
{
    public class CirclePathSolutionItemModel
    {
        public CirclePathSolutionItemModel()
        {
            Instructions = new List<PathStateInstructionModel>();
        }

        public List<string> Path { get; set; }
        public List<PathStateInstructionModel> Instructions { get; set; }
        public CirclePathSolutionItemSimlationResultModel SimulationResult { get; set; }

        public string PathId => string.Join("->", Path);
    }

    public class CirclePathSolutionItemSimlationResultModel
    {
        public decimal EstimatedProfitInStartAsset { get; set; }
        public decimal EstimatedProfitInUSDTAsset { get; set; }
        public decimal AvailableStartAssetAmount { get; set; }
        public decimal TargetStartAssetAmount { get; set; }
    }
}
