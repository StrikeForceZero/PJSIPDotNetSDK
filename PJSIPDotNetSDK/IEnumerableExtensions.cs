using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SIPBandClient.Util
{
    public static class EnumerableExtensions
    {
        public static void ForEach<T>(this IEnumerable<T> items, Action<T> action, Boolean safe = true)
        {
            if (safe)
            {
                foreach (T item in items.ToList())
                    action(item);
                return;
            }

            foreach (T item in items)
                action(item);
        }

        public static List<T> ConvertAll<s, T>(this IEnumerable<s> source, Converter<s, T> converter)
        {
            return new List<s>(source).ConvertAll(converter);
        }

        public static List<T> FindAll<T>(this IEnumerable<T> source, Predicate<T> predicate)
        {
            return new List<T>(source).FindAll(predicate);
        }
    }
}
