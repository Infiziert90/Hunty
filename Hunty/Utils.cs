using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using Dalamud.Utility;
using Hunty.Data;
using Lumina.Text;
using Lumina.Text.ReadOnly;

namespace Hunty;

public static class Utils
{
    // From Ottermandias
    public static string ToTitleCaseExtended(ReadOnlySeString s, sbyte article = 0)
    {
        if (article == 1)
            return s.ToDalamudString().ToString();

        var sb        = new StringBuilder(s.ToDalamudString().ToString());
        var lastSpace = true;
        for (var i = 0; i < sb.Length; ++i)
        {
            if (sb[i] == ' ')
            {
                lastSpace = true;
            }
            else if (lastSpace)
            {
                lastSpace = false;
                sb[i]     = char.ToUpperInvariant(sb[i]);
            }
        }

        return sb.ToString();
    }

    public static string CorrectGermanNames(string name, sbyte pronoun)
    {
        if (name.Contains("[a]"))
        {
            var s = StaticData.GermanPronouns.ElementAtOrDefault(pronoun);
            name = name.Replace("[a]", s);
        }
        else if (name.Contains("[p]"))
        {
            name = name.Replace("[p]", "");
        }

        return name;
    }

    public static TValue GetOrCreate<TKey, TValue>(this IDictionary<TKey, TValue> dict, TKey key) where TValue : new()
    {
        if (!dict.TryGetValue(key, out TValue val))
        {
            val = new TValue();
            dict.Add(key, val);
        }

        return val;
    }

    /// <summary> Return the first object fulfilling the predicate or null for structs. </summary>
    /// <param name="values"> The enumerable. </param>
    /// <param name="predicate"> The predicate. </param>
    /// <returns> The first object fulfilling the predicate, or a null-optional. </returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static T? FirstOrNull<T>(this IEnumerable<T> values, Func<T, bool> predicate) where T : struct
    {
        foreach(var val in values)
            if (predicate(val))
                return val;

        return null;
    }
}
