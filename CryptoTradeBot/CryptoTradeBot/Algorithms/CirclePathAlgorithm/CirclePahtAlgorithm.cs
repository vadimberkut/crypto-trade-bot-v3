using CryptoTradeBot.Exchanges.Binance.Stores;
using CryptoTradeBot.Host.Algorithms.CirclePathAlgorithm.Models;
using CryptoTradeBot.Host.Interfaces;
using CryptoTradeBot.Host.Models;
using CryptoTradeBot.Infrastructure.Enums;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CryptoTradeBot.Host.Algorithms.CirclePathAlgorithm
{
    public class CirclePahtAlgorithm
    {
        private readonly ILogger<CirclePahtAlgorithm> _logger;
        private readonly OrderBookStore _orderBookStore;
        private readonly IExchangeUtil _exchangeUtil;

        private readonly List<string> _symbols;
        private List<string> _states;
        private List<StateTransitionModel> _stateTransitions;

        private const int _minAllowedPathLenght = 2;
        private const int _maxAllowedPathLength = 10;
        private const decimal _minAllowedPathProfitPercent = 0.002m;

        private int _minPathLenght = 2;
        private int _maxPathLength = 3;
        private decimal _minPathProfitPercent = 0.003m;

        public CirclePahtAlgorithm(
            ILogger<CirclePahtAlgorithm> logger,
            OrderBookStore orderBookStore,
            IExchangeUtil exchangeUtil
        )
        {
            _logger = logger;
            _orderBookStore = orderBookStore;
            _exchangeUtil = exchangeUtil;

            // TODO: use only high volume symbols
            _symbols = exchangeUtil.GetSymbols();
            _states = exchangeUtil.GetAssets();

            // TODO: add avanced validation
            _minPathLenght = Math.Max(_minPathLenght, _minAllowedPathLenght);
            _maxPathLength = Math.Min(_maxPathLength, _maxAllowedPathLength);
            _minPathProfitPercent = Math.Max(_minPathProfitPercent, _minAllowedPathProfitPercent);

            this._BuildGraph();
        }

        // TODO: return decision object that can be consumed by algorithm executor
        public void Solve(string startAsset)
        {
            /*
             * Symbol: usually is the same as pair, but can be in different format. E.g. For Bitfinex: tBTCUSD
             * Pair: base + quote asset. E.g. BTCUSD
             * 
             * State: the asset (currency) which is hold in wallet currently (graph node)
             * Edge: a connection between 2 states (graph edge) that determined by allowed symbols (asset pairs)
             * Transition: the process of going from state1 to state2. Transition is made by means of symbol and action.
             *      E.g. state1=BTC, state2=USDT, transition=BTCUSDT. BTCUSDT symbol allows to sell BTC and get USDT
             *      E.g. state1=USDT, state2=BTC, transition=BTCUSDT. BTCUSDT symbol allows to buy BTC using USDT
             * Transition action: the symbol can lead to 2 states based on action (BUY or SELL)
             *      E.g. state=USDT, symbol=BTCUSDT, action=BUY -> state=BTC
             *      E.g. state=BTC, symbol=BTCUSDT, action=SELL -> state=USDT
             *  PathLength: is measured in amount of transitions performed between states. I.e. it is number of edges. Min=1. Min for circle path=2.
             *      Number of passed states=transitions + 1
             *      E.g. BTC -> ETH -> BTC. Length=2
             *      E.g. BTC -> ETH -> USDT -> BTC. Length=3
             */

            string startState = startAsset;
            var pathFindResult = this._FindPath(startState, new List<string>() { startState });
            var solutionTreeRoot = pathFindResult[0];

            this._PrintPath(solutionTreeRoot);

            int a = 1;
        }

        /// <summary>
        /// Makes initial setups
        /// </summary>
        private void _BuildGraph()
        {
            // save assets as states
            this._states = this._exchangeUtil.GetAssets();

            // get state transitions (currency pair names) e.g. {symbol: tIOTUSD, state1: IOT, state2: USD, bidirectional: true}
            this._stateTransitions = _symbols.Select((symbol) => {
                var assetModel = this._exchangeUtil.ConvertSymbolToAssets(symbol);
                return new StateTransitionModel()
                {
                    Symbol = assetModel.Symbol,
                    State1 = assetModel.Base,
                    State2 = assetModel.Quote,
                };
            }).ToList();
        }

        /// <summary>
        /// Searches all transiotoins where given state is defined
        /// </summary>
        public List<StateTransitionModel> _FindStateTransitions(string state)
        {
            var result = this._stateTransitions.Where((transition) => {
                return transition.State1 == state || transition.State2 == state;
            }).ToList();
            return result;
        }

        /// <summary>
        /// Recursive. For list of transitions finds states that we can go from the specisifed state.
        /// <br/>
        /// Check doc about transitions.
        /// </summary>
        /// <param name="state"></param>
        /// <param name="fromState"></param>
        /// <returns></returns>
        public List<string> _GetSutableTransitionStatesForState(List<StateTransitionModel> transitions, string fromState)
        {
            var result = transitions.Select((transition) => {
                //if (transition.State1 != fromState)
                //    return transition.State1;
                //if (transition.State2 != fromState)
                //    return transition.State1;

                if (transition.State1 == fromState)
                    return transition.State2;
                if (transition.State2 == fromState)
                    return transition.State1;

                return null;
            })
            .Where(state => state != null)
            .ToList();
            return result;
        }

        private List<CirclePathTreeNodeModel> _FindPath(string absoluteStartState, List<string> relativeStartStates, int pathLength = -1)
        {
            pathLength += 1;

            List<CirclePathTreeNodeModel> result = relativeStartStates.Select(state =>
            {
                if (pathLength >= 1 && pathLength < this._minPathLenght)
                {
                    if (state == absoluteStartState)
                    {
                        // while we haven't reached the min length forbid absolute start state to apear in path
                        return null;
                    }
                }
                else if (pathLength >= this._minPathLenght && pathLength < this._maxPathLength)
                {
                    if (state == absoluteStartState)
                    {
                        // min length circle path reached
                        this._logger.LogInformation($"Found min length circle path: {absoluteStartState} -> {state}. len={pathLength}.");
                        return new CirclePathTreeNodeModel()
                        {
                            State = state,
                            NextNodes = null,
                            PathLength = pathLength,
                        };
                    }
                }
                else if (pathLength == this._maxPathLength)
                {
                    // max path length reached
                    if (state == absoluteStartState)
                    {
                        // ended in absolute start state - desired result
                        this._logger.LogInformation($"Max level reached: {absoluteStartState} -> ... -> {state}. len={pathLength}.");
                        return new CirclePathTreeNodeModel() 
                        {
                            State = state,
                            NextNodes = null,
                            PathLength = pathLength,
                        };
                    }
                    else
                    {
                        // ended in other state than start - discard this path
                        return null;
                    }
                }
                else if (pathLength > this._maxPathLength)
                {
                    // out of path length limits - discard this path
                    return null;
                }

                var stateTransitions = this._FindStateTransitions(state);
                var nextStates = this._GetSutableTransitionStatesForState(stateTransitions, state);

                // recursive call
                // traverse down to the tree
                var nextResults = this._FindPath(absoluteStartState, nextStates, pathLength);
                if (nextResults != null)
                {
                    var result = new CirclePathTreeNodeModel()
                    {
                        State = state,
                        NextNodes = nextResults,
                        PathLength = pathLength,
                    };
                    return result;
                }

                return null;
            }).ToList();

            // filter out discarded pathes
            result = result.Where(x => x != null).ToList();
            if (result.Count() == 0)
            {
                return null;
            }
            return result;
        }

        private void _PrintPath(CirclePathTreeNodeModel treeRoot)
        {
            this._logger.LogInformation($"");
            this._logger.LogInformation($"----------START Circle path list.");
            var pathList = this._GetPathesAsStringList(new List<CirclePathTreeNodeModel>() { treeRoot });
            foreach (var pathItem in pathList)
            {
                this._logger.LogInformation(pathItem);
            }
            this._logger.LogInformation($"----------END Circle path list.");
            this._logger.LogInformation($"");
        }
        private List<string> _GetPathesAsStringList(List<CirclePathTreeNodeModel> treeNodes)
        {
            if(treeNodes == null || treeNodes.Count == 0)
            {
                return null;
            }

            var treeNodesResults = treeNodes.Select(treeNode =>
            {
                if (treeNode.IsLeafNode)
                {
                    return new List<string>() { treeNode.State };
                }
                else
                {
                    var nextResults = this._GetPathesAsStringList(treeNode.NextNodes);
                    nextResults = nextResults.Where(x => x != null).ToList();

                    var results = new List<string>();
                    foreach (var nextResult in nextResults)
                    {
                        results.Add($"{treeNode.State} -> {nextResult}");
                    }

                    return results;
                }
            }).ToList();

            var filenalResult = treeNodesResults.SelectMany(x => x).ToList();
            return filenalResult;
        }
    }
}
