using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace CryptoTradeBot.Infrastructure.Extensions
{
    public static class IEnumerableExtensions
    {
        /// <summary>
        /// DistinctBy extension for Dictionary<TKey, TValue>.
        /// <br/>
        /// Suppose that we have the next Dictionary:
        /// <br/>
        /// {key1: [{itemKey2: 1}, {itemKey2: 1}, {itemKey2: 2}], key2: [{itemKey2: 3}, {itemKey2: 3}]}
        /// <br/>
        /// The aim is to get the next DIctionary:
        /// <br/>
        /// {key1: [{itemKey2: 1}, {itemKey2: 2}], key2: [{itemKey2: 3}]}
        /// </summary>
        public static IEnumerable<KeyValuePair<TKey, IEnumerable<TValue>>> DistinctByKeyValues<TKey, TValue, TDistinctValue>(
            this IEnumerable<KeyValuePair<TKey, IEnumerable<TValue>>> source, 
            Func<TValue, TDistinctValue> keySelector
        )
        {
            return source
                .GroupBy(x => x.Key) // group by Dictionary key (key1)
                .Select(group =>
                {
                    // take a values list for each Dictionary key and 
                    // group that list by item property (key2)
                    var distinctForDictionaryKey = group
                        .SelectMany(x => x.Value)
                        .GroupBy(x => keySelector(x), (key2, group2) => group2.First())
                        .ToList();
                    return new KeyValuePair<TKey, IEnumerable<TValue>>(group.Key, distinctForDictionaryKey);
                    // return distinctForDictionaryKey.Select(distinctForDictionaryKeyItem => new KeyValuePair<TKey, IEnumerable<TValue>>(group.Key, distinctForDictionaryKeyItem));
                });
        }
    }
}
