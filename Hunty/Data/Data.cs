using System.Collections.Generic;

namespace Hunty.Data;

public static class StaticData
{
    public static int JobInMemory(uint key)
    {
        return key switch
        {
            1 => 0,
            2 => 1,
            3 => 2,
            4 => 3,
            5 => 4,
            6 => 5,
            7 => 6,
            26 => 7,
            10001 => 8,
            10002 => 9,
            10003 => 10,
            29 => 11,
            _ => 0
        };
    }

    public static readonly List<string> GermanPronouns = new()
    {
        "er",
        "e",
        "es"
    };
}