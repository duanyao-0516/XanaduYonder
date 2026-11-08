using System;
using System.Collections.Generic;

namespace XanaduYonder.Core.LinkMatch
{
    /// <summary>Stable visual identities; their shuffle never consumes board/session randomness.</summary>
    public static class LinkMatchPatterns
    {
        public const int AllCategories = 31;
        public static readonly string[] CategoryNames = { "水果", "食物", "动物", "自然", "物件" };
        private static readonly string[] ids = { "strawberry","red-apple","tangerine","lemon","banana","watermelon","grapes","cherries","peach","pear","pineapple","avocado","croissant","hamburger","pizza","doughnut","cupcake","carrot","broccoli","ear-of-corn","mushroom","cheese-wedge","egg","peanuts","dog-face","cat-face","fox","panda","frog","lion","pig-face","unicorn","butterfly","lady-beetle","octopus","tropical-fish","sunflower","hibiscus","four-leaf-clover","cactus","rainbow","fire","soccer-ball","basketball","rocket","wrapped-gift","musical-note","gem-stone" };
        public static bool ValidCategories(int categories) { return categories > 0 && (categories & ~AllCategories) == 0; }
        public static int CategoryFor(string id)
        {
            int i = Array.IndexOf(ids, id);
            return i < 0 ? 0 : i < 12 ? 1 : i < 24 ? 2 : i < 36 ? 4 : i < 42 ? 8 : 16;
        }
        public static string[] Pool(int categories)
        {
            if (!ValidCategories(categories)) throw new ArgumentOutOfRangeException(nameof(categories));
            var result = new List<string>();
            foreach (var id in ids) if ((CategoryFor(id) & categories) != 0) result.Add(id);
            return result.ToArray();
        }
        public static string[] Legacy(int kinds)
        {
            if (kinds < 1 || kinds > ids.Length) throw new ArgumentOutOfRangeException(nameof(kinds));
            var result = new string[kinds]; Array.Copy(ids, result, kinds); return result;
        }
        public static string[] Choose(int kinds, int categories, uint seed)
        {
            var pool = Pool(categories);
            if (kinds < 1 || kinds > pool.Length) throw new ArgumentException("所选类别的图案不足，请选择更多类别。");
            uint state = seed ^ 0xa341316cu; if (state == 0) state = 1;
            for (int i = pool.Length - 1; i > 0; i--)
            {
                state ^= state << 13; state ^= state >> 17; state ^= state << 5;
                int j = (int)(state % (uint)(i + 1)); var item = pool[i]; pool[i] = pool[j]; pool[j] = item;
            }
            var result = new string[kinds]; Array.Copy(pool, result, kinds); return result;
        }
        public static bool ValidMapping(string[] mapping, int kinds)
        {
            if (mapping == null || mapping.Length != kinds) return false;
            var unique = new HashSet<string>(StringComparer.Ordinal);
            foreach (var id in mapping) if (CategoryFor(id) == 0 || !unique.Add(id)) return false;
            return true;
        }
    }
}
