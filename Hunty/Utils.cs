using System.Collections.Generic;
using System.Linq;
using System.Text;
using Dalamud.Utility;
using Hunty.Data;
using Lumina.Text;

namespace Hunty;

public static class Utils
{
    // From Ottermandias
    public static string ToTitleCaseExtended(SeString s, sbyte article = 0)
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

    public static string ToTitleCaseExtended(string s)
    {
        var sb        = new StringBuilder(s);
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
}