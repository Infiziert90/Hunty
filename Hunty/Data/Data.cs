using System.Collections.Generic;

namespace Hunty.Data;

public static class StaticData
{
    public static int JobInMemory(string name)
    {
        return name switch
        {
            "Gladiator" => 0,
            "Pugilist" => 1,
            "Marauder" => 2,
            "Lancer" => 3,
            "Archer" => 4,
            "Conjurer" => 5,
            "Thaumaturge" => 6,
            "Arcanist" => 7,
            "Maelstrom" => 8,
            "Twin Adder" => 9,
            "Immortal Flames" => 10,
            "Rogue" => 11,
            _ => 0
        };
    }

    public static readonly List<string> GrandCompanies = new()
    {
        "No GC", 
        "Maelstrom", 
        "Twin Adder", 
        "Immortal Flames"
    };
}