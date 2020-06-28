using CryptoTradeBot.Infrastructure.Models;
using System;
using System.Collections.Generic;
using System.Text;

namespace CryptoTradeBot.StrategyRunner
{
    public delegate bool PositionOpeningPredicate
    (
        List<GeneralBarModel> bars,
        int currentBarIndex,
        GeneralBarModel currentBar
    );

    public delegate PositionOpeningDetailsModel PositionOpeningDetailsGetter
    (
       List<GeneralBarModel> bars,
       int currentBarIndex,
       GeneralBarModel currentBar
    );

    public delegate PositionPnlModel CalcCurrentPnlFunc
    (
        decimal positionClosePrice
    );

    public delegate PositionEarlyCloseDetailsModel PositionEarlyCloseFunc
    (
       List<GeneralBarModel> bars,
       int currentBarIndex,
       GeneralBarModel currentBar,
       CurrentPositionModel currentPosition,
       CalcCurrentPnlFunc calcCurrentPnlFunc
    );
}
