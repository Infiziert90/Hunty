using System;
using System.Collections.Generic;

namespace Hunty;

[Serializable]
public class HuntingData
{
    public Dictionary<string, HuntingRank> Classes { get; set; } = new();
}

[Serializable]
public class HuntingRank
{
    public List<List<HuntingMonster>> Monsters { get; set; } = new();
}

[Serializable]
public class HuntingMonster
{
    public string Name { get; set; }
    public byte Count { get; set; }

    public List<HuntingMonsterLocation> Locations { get; set; } = new();
}

[Serializable]
public class HuntingMonsterLocation
{
    public uint Terri { get; set; }
    public uint Map { get; set; }

    public float xCoord { get; set; } = 0;
    public float yCoord { get; set; } = 0;

    public HuntingMonsterLocation(uint terri, uint map)
    {
        Terri = terri;
        Map = map;
    }
}