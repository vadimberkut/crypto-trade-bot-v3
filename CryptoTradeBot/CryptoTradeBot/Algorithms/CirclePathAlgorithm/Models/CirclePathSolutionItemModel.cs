using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace CryptoTradeBot.WebHost.Algorithms.CirclePathAlgorithm.Models
{
    public class CirclePathSolutionItemModel
    {
        public List<string> Path { get; set; }
        public List<PathStateInstructionModel> Instructions { get; set; }
        public decimal EstimatedProfitInStartAsset { get; set; }
        public decimal AvailableStartAssetAmount { get; set; }
        public decimal TargetStartAssetAmount { get; set; }
    }
}
