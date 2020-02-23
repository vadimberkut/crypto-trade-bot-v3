using CryptoTradeBot.Exchanges.Binance.Stores;
using CryptoTradeBot.Host.Algorithms.CirclePathAlgorithm.Models;
using CryptoTradeBot.Host.Interfaces;
using CryptoTradeBot.Host.Models;
using CryptoTradeBot.Infrastructure.Enums;
using CryptoTradeBot.WebHost.Algorithms.CirclePathAlgorithm;
using CryptoTradeBot.WebHost.Algorithms.CirclePathAlgorithm.Models;
using CryptoTradeBot.WebHost.Exchanges.Binance;
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
        private readonly OrderBookStore _orderBookStore; // TODO: pass as param each time
        private readonly IExchangeUtil _exchangeUtil; // TODO: pass as param each time

        private readonly List<string> _symbols; // TODO: pass as param each time
        private List<string> _states; // TODO: pass as param each time
        private List<StateTransitionModel> _stateTransitions;

        private const int _minAllowedPathLenght = 3; // 2 is meaningless, so use 3. E.g. of 2: IOTA -> USDT -> IOTA
        private const int _maxAllowedPathLength = 10;
        private const decimal _minAllowedPathProfitPercent = 0.0015m;

        private int _minPathLenght = 3;
        private int _maxPathLength = 3;
        private decimal _minPathProfitPercent = 0.0015m;

        public CirclePahtAlgorithm(
            ILogger<CirclePahtAlgorithm> logger,
            OrderBookStore orderBookStore,
            IExchangeUtil exchangeUtil
        )
        {
            _logger = logger;
            _orderBookStore = orderBookStore;
            _exchangeUtil = exchangeUtil;

            _symbols = exchangeUtil.GetSymbols(CirclePahtAlgorithmConfig.AllowedSymbols);
            _states = exchangeUtil.GetAssets(CirclePahtAlgorithmConfig.AllowedSymbols);

            // validation
            if (!(_minPathLenght >= _minAllowedPathLenght && _minPathLenght <= _maxAllowedPathLength))
            {
                throw new InvalidOperationException($"_minPathLenght must be in [{_minAllowedPathLenght};{_maxAllowedPathLength}].");
            }
            if (!(_maxPathLength >= _minAllowedPathLenght && _maxPathLength <= _maxAllowedPathLength))
            {
                throw new InvalidOperationException($"_maxPathLength must be in [{_minAllowedPathLenght};{_maxAllowedPathLength}].");
            }
            if (!(_minPathProfitPercent >= _minAllowedPathProfitPercent))
            {
                throw new InvalidOperationException($"_minPathProfitPercent must be greater than {_minAllowedPathProfitPercent}.");
            }

            this._BuildGraph();
        }

        // TODO: return decision object that can be consumed by algorithm executor
        public List<CirclePathSolutionItemModel> Solve(string startAsset, decimal startAssetAmount)
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

            // find possible pathes from start state (root) to end state (leaf) according to limitations
            // requirement: root == leaf
            // result: tree
            // TODO: cache as results are the same usually
            var pathFindResult = this._FindPathTree(startState, new List<string>() { startState });
            var pathFindResultTreeRoot = pathFindResult[0];
            // this._PrintPath(pathFindResultTreeRoot);

            // get final path list (not nested structure) from prev tree result
            // result: list of lists
            var pathList = this._BuildPathListFromTree(pathFindResultTreeRoot);
            // this._PrintPathList(pathList);

            // filter out circular pathes. 
            // e.g. IOT -> USD -> IOT -> USD -> IOT
            // i.e. where start state occurs more than twice (should apear only at the start and the end)
            pathList = pathList.Where(path => path.Count(state => state == startState) == 2).ToList();

            // filter out pathes with duplicate intermediate states. 
            // e.g. ... -> BCH -> USD -> BCH -> ...; ... USD -> ETH -> IOT -> USD -> ...
            pathList = pathList.Where(path => {
                var otherStates = path.GetRange(1, path.Count - 2);
                return otherStates.Count() == otherStates.Distinct().Count();
            }).ToList();

            // get solutions
            var solutions = this._ProcessPathListAndGetSolution(pathList, startAssetAmount);
            List<CirclePathSolutionItemModel> profitSolutions = new List<CirclePathSolutionItemModel>();

            ////// simulate solutions

            ////// simulate with MARKET orders
            //solutions = this._SimulateSolutionsWithMarketOrders(solutions, startAsset, startAssetAmount);

            //// filter out unprofitable solutions
            //profitSolutions = solutions.Where(solution =>
            //{
            //    return 
            //        solution.SimulationResult.EstimatedProfitInStartAsset > 0 && 
            //        solution.SimulationResult.EstimatedProfitInStartAsset >= 
            //            solution.SimulationResult.TargetStartAssetAmount * this._minPathProfitPercent;
            //}).ToList();

            ////this._logger.LogInformation($"");
            ////this._logger.LogInformation($"Result for MARKET orders:");
            ////this._logger.LogInformation($"Input: startAsset={startAsset}, startAssetAmount={startAssetAmount}.");
            ////this._logger.LogInformation($"Output: solutions={solutions.Count}, profitSolutions={profitSolutions.Count}.");
            ////this._logger.LogInformation($"");

            ////// simulate with LIMIT orders
            solutions = this._SimulateSolutionsWithLimitOrders(solutions, startAsset, startAssetAmount);

            // filter out unprofitable solutions
            profitSolutions = solutions.Where(solution =>
            {
                return
                    solution.SimulationResult.EstimatedProfitInStartAsset > 0 &&
                    solution.SimulationResult.EstimatedProfitInStartAsset >= 
                        solution.SimulationResult.TargetStartAssetAmount * this._minPathProfitPercent;
            }).ToList();

            //this._logger.LogInformation($"");
            //this._logger.LogInformation($"Result for LIMIT orders:");
            //this._logger.LogInformation($"Input: startAsset={startAsset}, startAssetAmount={startAssetAmount}.");
            //this._logger.LogInformation($"Output: solutions={solutions.Count}, profitSolutions={profitSolutions.Count}.");
            //this._logger.LogInformation($"");

            return profitSolutions;
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
                var symBolInfo = this._exchangeUtil.GetSymbolInfo(symbol);
                return new StateTransitionModel()
                {
                    Symbol = symBolInfo.Symbol,
                    State1 = symBolInfo.Base,
                    State2 = symBolInfo.Quote,
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
        /// Recursive.
        /// <br/>
        /// For list of transitions finds states that we can go from the specisifed state.
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

        /// <summary>
        /// Recursive.
        /// <br/>
        /// Finds a set of pathes which describes a valid transition from start state back to itself 
        /// according to min/max path length limitations.
        /// <br/>
        /// Result: A tree that represents valid pathes.
        /// </summary>
        /// <param name="absoluteStartState">Static param, doesn't change accross recursive calls.</param>
        /// <param name="relativeStartStates">Dynamic param that changes accross recursive calls.</param>
        /// <param name="pathLength">Dynamic param that changes accross recursive calls.</param>
        /// <returns></returns>
        private List<CirclePathTreeNodeModel> _FindPathTree(string absoluteStartState, List<string> relativeStartStates, int pathLength = -1)
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
                        // this._logger.LogInformation($"Found min length circle path: {absoluteStartState} -> {state}. len={pathLength}.");
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
                        // this._logger.LogInformation($"Max level reached: {absoluteStartState} -> ... -> {state}. len={pathLength}.");
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
                var nextResults = this._FindPathTree(absoluteStartState, nextStates, pathLength);
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
            this._logger.LogInformation($"----------START Circle path tree.");
            var pathList = this._GetPathesAsStringList(new List<CirclePathTreeNodeModel>() { treeRoot });
            foreach (var pathItem in pathList)
            {
                this._logger.LogInformation(pathItem);
            }
            this._logger.LogInformation($"----------END Circle path tree.");
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
                    return new List<string>() { $"{treeNode.State} (len={treeNode.PathLength})" };
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

        /// <summary>
        /// Traverses the pathes tree and returns a list of pathes.
        /// </summary>
        /// <param name="pathFindResultTreeNode"></param>
        /// <returns></returns>
        private List<List<string>> _BuildPathListFromTree(CirclePathTreeNodeModel pathFindResultTreeNode)
        {
            // recursion safety stop condition 0
            if (pathFindResultTreeNode == null)
            {
                return null;
            }

            //var result = new List<List<string>>();

            // recursion stop condition 1
            if (pathFindResultTreeNode.IsLeafNode)
            {
                return new List<List<string>>() 
                {
                    new List<string>() { pathFindResultTreeNode.State  }
                };
            }
            else
            {
                // recursive call
                var nextResults = pathFindResultTreeNode.NextNodes
                    .Select(nextNode => this._BuildPathListFromTree(nextNode))
                    .SelectMany(nextResult => nextResult) // flatten
                    .Select(nextResult => {
                        // prepend with current state
                        nextResult.Insert(0, pathFindResultTreeNode.State);
                        return nextResult;
                    })
                    .ToList();

                return nextResults;
            }
        }

        private void _PrintPathList(List<List<string>> pathList)
        {
            this._logger.LogInformation($"");
            this._logger.LogInformation($"----------START Circle path list.");
            foreach (var pathItem in pathList)
            {
                this._logger.LogInformation(string.Join(" ->", pathItem));
            }
            this._logger.LogInformation($"----------END Circle path list.");
            this._logger.LogInformation($"");
        }

        /// <summary>
        /// Not recursive.
        /// <br/>
        /// Returns solution for a path list.
        /// </summary>
        /// <param name="pathList"></param>
        private List<CirclePathSolutionItemModel> _ProcessPathListAndGetSolution(List<List<string>> pathList, decimal startAssetAmount)
        {
            var solutions = new List<CirclePathSolutionItemModel>();
            foreach (var path in pathList)
            {
                // get state instruction for each path
                var pathInstructions = new List<PathStateInstructionModel>();
                for (var i = 0; i < path.Count; i++)
                {
                    string state = path[i];
                    string nextState = null;

                    if (i == path.Count - 1)
                    {
                        // for the last state return 'do nothing' instruction
                        pathInstructions.Add(new PathStateInstructionModel()
                        {
                            IsStart = false,
                            IsEnd = true,
                            State = state,
                            NextState = null,
                            Action = SymbolAction.None,
                            Transition = null,
                        });
                        break;
                    }

                    if (i != path.Count - 1)
                    {
                        nextState = path[i + 1];
                    }

                    // find transition the next state
                    var transition = this._stateTransitions.FirstOrDefault(x => (x.State1 == state && x.State2 == nextState) || (x.State1 == nextState && x.State2 == state));
                    if (transition == null)
                    {
                        this._logger.LogError($"Can't find transition from '{state}' to '{nextState}'.");
                        break;
                    }

                    // determine action: buy/sell
                    SymbolAction action = SymbolAction.None;
                    if (i != path.Count - 1)
                    {
                        action = this._exchangeUtil.GetSymbolAction(transition.Symbol, state);
                        if (action == SymbolAction.None)
                        {
                            this._logger.LogError($"Can't find symbol action (buy/sell) for '{state}' using transition '{transition.Symbol}'.");
                            break;
                        }
                    }

                    pathInstructions.Add(new PathStateInstructionModel()
                    {
                        IsStart = i == 0,
                        IsEnd = false,
                        State = state,
                        NextState = nextState,
                        Action = action,
                        Transition = transition,
                    });
                }

                solutions.Add(new CirclePathSolutionItemModel()
                {
                    Path = path,
                    Instructions = pathInstructions,
                    SimulationResult = null,
                });
            }

            return solutions;
        }

        [Obsolete("Not consistent with LIMIT order simulation")]
        private List<CirclePathSolutionItemModel> _SimulateSolutionsWithMarketOrders(List<CirclePathSolutionItemModel> solutions, string startAsset, decimal startAssetAmount)
        {
            foreach (var solution in solutions)
            {
                // simulate path instructions and estimate profit
                // 1. use MARKET orders
                // 2. use LIMIT orders (TODO: add, but move to separate method to preserve already implemented logic)

                // TODO: figure out how to pinpoint the optimized start amount, so we get the first, the best orders in the book and not oversell/overbuy
                // TODO: due to too large start amount that can lead to losses at the last transitions. As some markets can not be liquid.
                decimal actualStartAssetAmount = startAssetAmount;
                decimal currentAssetAmount = startAssetAmount;
                bool isInterrupted = false;
                foreach (var pathInstruction in solution.Instructions)
                {
                    // if reached the end - exit
                    if (pathInstruction.IsEnd)
                    {
                        break;
                    }

                    var symbolOrderBook = this._orderBookStore.GetOrderBookForSymbol(pathInstruction.Transition.Symbol);
                    if (symbolOrderBook == null)
                    {
                        this._logger.LogError($"Can't find order book for '{pathInstruction.Transition.Symbol}'.");
                        isInterrupted = true;
                        break;
                    }
                    if (symbolOrderBook.Bids.Count == 0 || symbolOrderBook.Asks.Count == 0)
                    {
                        this._logger.LogError($"Order book for '{pathInstruction.Transition.Symbol}' is empty.");
                        isInterrupted = true;
                        break;
                    }

                    const decimal maxPriceDeviationPercentFromTheBestOrder = 0.002m;
                    var bestBid = symbolOrderBook.AggregateTopBestBidsByPredicate(5, maxPriceDeviationPercentFromTheBestOrder, startAssetAmount);
                    var bestAsk = symbolOrderBook.AggregateTopBestAsksByPredicate(5, maxPriceDeviationPercentFromTheBestOrder, startAssetAmount);

                    decimal bestBidTotal = bestBid.Price * bestBid.Quantity;
                    decimal bestAskTotal = bestAsk.Price * bestAsk.Quantity;

                    // detrmine the best start amount
                    if (pathInstruction.IsStart)
                    {
                        if (pathInstruction.Action == SymbolAction.Buy)
                        {
                            // E.g. 
                            // state1 = BTC, amount = 0.1, action = BUY
                            // state2 = ETH, best_ask_price = 0.03, best_ask_amount = 3, best_total = 0.9
                            // wantToBuyAmount = 0.1 BTC / 0.03 = 3.33 ETH
                            // canToBuyAmount = best_ask_amount = 3
                            // wantToBuyAmountInQuote = 3.33 ETH * 0.03 = 0.1 BTC
                            // canToBuyAmountInQuote = 3 ETH * 0.03 = 0.09 BTC
                            // actualStartAssetAmount = min(0.1 BTC, 0.09 BTC) = 0.09 BTC

                            // adjust amout to according to order book best values
                            decimal wantToBuyAmount = currentAssetAmount / bestAsk.Price; // according to wallet
                            decimal canToBuyAmount = bestAsk.Quantity; // according to order book

                            // convert back to quote
                            decimal wantToBuyAmountInQuote = wantToBuyAmount * bestAsk.Price;
                            decimal canToBuyAmountInQuote = bestAsk.Quantity * bestAsk.Price;

                            actualStartAssetAmount = Math.Min(wantToBuyAmountInQuote, canToBuyAmountInQuote);
                            currentAssetAmount = actualStartAssetAmount;
                        }
                        else if (pathInstruction.Action == SymbolAction.Sell)
                        {
                            // adjust amout to according to order book best values
                            decimal wantToSellAmount = currentAssetAmount; // according to wallet
                            decimal canToSellAmount = bestBid.Quantity; // according to order book
                            actualStartAssetAmount = Math.Min(wantToSellAmount, canToSellAmount);
                            currentAssetAmount = actualStartAssetAmount;
                        }
                    }

                    decimal actionTotal;
                    if (pathInstruction.Action == SymbolAction.Buy)
                    {
                        actionTotal = currentAssetAmount / bestAsk.Price;
                        actionTotal = actionTotal - (actionTotal * this._exchangeUtil.MakerFee); // maker because you are the maker (MARKET order)

                        // moved to the next asset
                        currentAssetAmount = actionTotal;
                    }
                    else if (pathInstruction.Action == SymbolAction.Sell)
                    {
                        actionTotal = currentAssetAmount * bestBid.Price;
                        actionTotal = actionTotal - (actionTotal * this._exchangeUtil.MakerFee); // maker because you are the maker (MARKET order)

                        // moved to the next asset
                        currentAssetAmount = actionTotal;
                    }
                }

                decimal estimatedProfit = 0;
                if (isInterrupted)
                {
                    estimatedProfit = 0;
                }
                else
                {
                    estimatedProfit = currentAssetAmount - actualStartAssetAmount;
                }

                solution.SimulationResult = new CirclePathSolutionItemSimlationResultModel()
                {
                    AvailableStartAssetAmount = startAssetAmount,
                    TargetStartAssetAmount = decimal.Round(actualStartAssetAmount, 8),
                    EstimatedProfitInStartAsset = decimal.Round(estimatedProfit, 8),
                    EstimatedProfitInUSDTAsset = decimal.Round(_orderBookStore.ConvertAssets(startAsset, BinanceConfig.USDTAsset, decimal.Round(estimatedProfit, 8)), 2),
                };
            }

            return solutions;
        }

        private List<CirclePathSolutionItemModel> _SimulateSolutionsWithLimitOrders(List<CirclePathSolutionItemModel> solutions, string startAsset, decimal startAssetAmount)
        {
            foreach (var solution in solutions)
            {
                // pinpoint the optimized start amount, so we get the first and the best price orders in the book and not oversell/overbuy
                // TODO: do estimation based on volume for recent period too. due to too large start amount that can lead to losses at the last transitions. As some markets can not be liquid.
                var estimatedAmounts = solution.Instructions.Select(pathInstruction =>
                {
                    // if reached the end - exit
                    if (pathInstruction.IsEnd)
                    {
                        return null;
                    }

                    try
                    {

                        // buy - place buy LIMIT order, but estimated based on opposite asks
                        // sell - place sell LIMIT order, but estimated based on opposite bids
                        var symbolInfo = this._exchangeUtil.GetSymbolInfo(pathInstruction.Transition.Symbol);
                        var symbolOrderBook = this._orderBookStore.GetOrderBookForSymbol(pathInstruction.Transition.Symbol);
                        const decimal maxPriceDeviationPercentFromTheBestOrder = 0.002m;
                        var bestBid = symbolOrderBook.AggregateTopBestBidsByPredicate(5, maxPriceDeviationPercentFromTheBestOrder, maxTotalAmount: decimal.MaxValue);
                        var bestAsk = symbolOrderBook.AggregateTopBestAsksByPredicate(5, maxPriceDeviationPercentFromTheBestOrder, maxTotalAmount: decimal.MaxValue);

                        // note: quatities are always specified in Base asset
                        if (pathInstruction.Action == SymbolAction.Buy)
                        {
                            decimal quantity = bestAsk.Quantity;
                            var result = new
                            {
                                Symbol = pathInstruction.Transition.Symbol,
                                EtimatedTotalInStartAsset = _orderBookStore.ConvertAssets(symbolInfo.Base, startAsset, quantity),
                                EtimatedTotalInUsdt = _orderBookStore.ConvertAssets(symbolInfo.Base, BinanceConfig.USDTAsset, quantity),
                            };
                            return result;
                        }
                        else if (pathInstruction.Action == SymbolAction.Sell)
                        {
                            decimal quantity = bestBid.Quantity;
                            var result = new
                            {
                                Symbol = pathInstruction.Transition.Symbol,
                                EtimatedTotalInStartAsset = _orderBookStore.ConvertAssets(symbolInfo.Base, startAsset, quantity),
                                EtimatedTotalInUsdt = _orderBookStore.ConvertAssets(symbolInfo.Base, BinanceConfig.USDTAsset, quantity),
                            };
                            return result;
                        }
                        else
                        {
                            return null;
                        }
                    }
                    catch(Exception ex)
                    {
                        _logger.LogError(ex, ex.Message);
                        return null;
                    }
                })
                    .Where(x => x != null)
                    .ToList();

                // TODO: add check for min aloowed order size on exchange
                const decimal estimateTakePercent = 0.5m;
                decimal optimalStartAssetAmount = estimatedAmounts.Min(x => x.EtimatedTotalInStartAsset);
                optimalStartAssetAmount = optimalStartAssetAmount * estimateTakePercent;
                decimal actualStartAssetAmount = Math.Min(startAssetAmount, optimalStartAssetAmount);

                // simulate path instructions and estimate profit
                decimal currentAssetAmount = actualStartAssetAmount;
                bool isInterrupted = false;
                foreach (var pathInstruction in solution.Instructions)
                {
                    // if reached the end - exit
                    if (pathInstruction.IsEnd)
                    {
                        break;
                    }

                    var symbolOrderBook = this._orderBookStore.GetOrderBookForSymbol(pathInstruction.Transition.Symbol);
                    if (symbolOrderBook == null)
                    {
                        this._logger.LogError($"Can't find order book for '{pathInstruction.Transition.Symbol}'.");
                        isInterrupted = true;
                        break;
                    }
                    if (symbolOrderBook.Bids.Count == 0 || symbolOrderBook.Asks.Count == 0)
                    {
                        this._logger.LogError($"Order book for '{pathInstruction.Transition.Symbol}' is empty.");
                        isInterrupted = true;
                        break;
                    }

                    const decimal maxPriceDeviationPercentFromTheBestOrder = 0.002m;
                    var bestBid = symbolOrderBook.AggregateTopBestBidsByPredicate(5, maxPriceDeviationPercentFromTheBestOrder, startAssetAmount);
                    var bestAsk = symbolOrderBook.AggregateTopBestAsksByPredicate(5, maxPriceDeviationPercentFromTheBestOrder, startAssetAmount);

                    decimal bestBidTotal = bestBid.Price * bestBid.Quantity;
                    decimal bestAskTotal = bestAsk.Price * bestAsk.Quantity;

                    // select best bid by adding the min price to the bid
                    // select best ask by subtracting the min price from the ask
                    var symbolInfo = this._exchangeUtil.GetSymbolInfo(pathInstruction.Transition.Symbol);
                    if (symbolInfo == null)
                    {
                        this._logger.LogError($"Can't find info for symbol '{pathInstruction.Transition.Symbol}' in '{this._exchangeUtil.ExchangeName}' exchange info.");
                        isInterrupted = true;
                        break;
                    }
                    string minSymbolPriceChangeStr = $"0.{string.Join("", Enumerable.Range(0, symbolInfo.QuotePrecision).Select((x, i) => i == 0 ? "1" : "0").Reverse())}";
                    decimal minSymbolPriceChangeValue = decimal.Parse(minSymbolPriceChangeStr);
                    decimal targetBestBidPrice = bestBid.Price + minSymbolPriceChangeValue;
                    decimal targetBestAskPrice = bestAsk.Price - minSymbolPriceChangeValue;

                    //// OSOLETE. Estimated above. Leave just as for example.
                    //// detrmine the best start amount
                    //if (pathInstruction.IsStart)
                    //{
                    //    if (pathInstruction.Action == SymbolAction.Buy)
                    //    {
                    //        // E.g. 
                    //        // state1 = BTC, amount = 0.1, action = BUY
                    //        // state2 = ETH, best_ask_price = 0.03, best_ask_amount = 3, best_total = 0.9
                    //        // wantToBuyAmount = 0.1 BTC / 0.03 = 3.33 ETH
                    //        // canToBuyAmount = best_ask_amount = 3
                    //        // wantToBuyAmountInQuote = 3.33 ETH * 0.03 = 0.1 BTC
                    //        // canToBuyAmountInQuote = 3 ETH * 0.03 = 0.09 BTC
                    //        // actualStartAssetAmount = min(0.1 BTC, 0.09 BTC) = 0.09 BTC

                    //        // adjust amount according to opposite order book top orders amount
                    //        decimal wantToBuyAmount = currentAssetAmount / targetBestBidPrice; // according to wallet
                    //        decimal canToBuyAmount = bestAsk.Quantity; // according to order book

                    //        // convert back to quote
                    //        decimal wantToBuyAmountInQuote = wantToBuyAmount * targetBestBidPrice;
                    //        decimal canToBuyAmountInQuote = bestAsk.Quantity * targetBestBidPrice;

                    //        actualStartAssetAmount = Math.Min(wantToBuyAmountInQuote, canToBuyAmountInQuote);
                    //        currentAssetAmount = actualStartAssetAmount;
                    //    }
                    //    else if (pathInstruction.Action == SymbolAction.Sell)
                    //    {
                    //        // adjust amount according to opposite order book top orders amount
                    //        decimal wantToSellAmount = currentAssetAmount; // according to wallet
                    //        decimal canToSellAmount = bestBid.Quantity; // according to order book
                    //        actualStartAssetAmount = Math.Min(wantToSellAmount, canToSellAmount);
                    //        currentAssetAmount = actualStartAssetAmount;
                    //    }
                    //}

                    decimal actionTotal;
                    if (pathInstruction.Action == SymbolAction.Buy)
                    {
                        actionTotal = currentAssetAmount / targetBestBidPrice;
                        actionTotal = actionTotal - (actionTotal * this._exchangeUtil.TakerFee); // taker because you are the taker (LIMIT order)

                        // moved to the next asset
                        currentAssetAmount = actionTotal;
                    }
                    else if (pathInstruction.Action == SymbolAction.Sell)
                    {
                        actionTotal = currentAssetAmount * targetBestAskPrice;
                        actionTotal = actionTotal - (actionTotal * this._exchangeUtil.TakerFee); // taker because you are the taker (LIMIT order)

                        // moved to the next asset
                        currentAssetAmount = actionTotal;
                    }
                }

                decimal estimatedProfit = 0;
                if (isInterrupted)
                {
                    estimatedProfit = 0;
                }
                else
                {
                    estimatedProfit = currentAssetAmount - actualStartAssetAmount;
                }

                solution.SimulationResult = new CirclePathSolutionItemSimlationResultModel()
                {
                    AvailableStartAssetAmount = startAssetAmount,
                    TargetStartAssetAmount = decimal.Round(actualStartAssetAmount, 8),
                    EstimatedProfitInStartAsset = decimal.Round(estimatedProfit, 8),
                    EstimatedProfitInUSDTAsset = decimal.Round(_orderBookStore.ConvertAssets(startAsset, BinanceConfig.USDTAsset, decimal.Round(estimatedProfit, 8)), 2),
                };
            }

            return solutions;
        }
    }
}
