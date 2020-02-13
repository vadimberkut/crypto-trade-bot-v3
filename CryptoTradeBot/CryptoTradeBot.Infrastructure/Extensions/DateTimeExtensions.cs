using System;
using System.Collections.Generic;
using System.Text;

namespace CryptoTradeBot.Infrastructure.Extensions
{
    public static class DateTimeExtensions
    {
        public static string ToUtcString(this DateTime source)
        {
            return source
                //.ToString("s");
                .ToString("yyyy'-'MM'-'dd'T'HH':'mm':'ss'.'fff'Z'");
        }
    }
}
