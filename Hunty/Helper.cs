using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using Dalamud.Utility;
using Lumina.Text;

namespace Hunty;

public static class Helper
{
    // From Ottermandias
    public static string ToTitleCaseExtended(SeString s, sbyte article)
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
    
    public static string ToTitleCaseExtended(Group s)
    {
        var sb        = new StringBuilder(s.Value);
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

    public static int Parse(Group s) => int.Parse(s.Value);
    
    public static TValue GetOrCreate<TKey, TValue>(this IDictionary<TKey, TValue> dict, TKey key) 
        where TValue : new()
    {
        if (!dict.TryGetValue(key, out TValue val))
        {
            val = new TValue();
            dict.Add(key, val);
        }

        return val;
    }
}