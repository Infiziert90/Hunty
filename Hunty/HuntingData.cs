using System;
using System.Collections.Generic;
using System.Numerics;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using Newtonsoft.Json;

using static Hunty.Utils;

namespace Hunty;

public class HuntingData
{
    public Dictionary<uint, List<HuntingRank>> JobRanks { get; set; } = new();
}

[Serializable]
public class HuntingRank
{
    public List<HuntingTask> Tasks { get; set; } = [];
}

[Serializable]
public class HuntingTask
{
    public string Name = string.Empty;
    public List<HuntingMonster> Monsters { get; set; } = [];
}

[Serializable]
public class HuntingMonster
{
    public uint Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public byte Count { get; set; }
    public uint Icon { get; set; }

    public List<HuntingMonsterLocation> Locations { get; set; } = [];

    [JsonIgnore] public HuntingMonsterLocation GetLocation => Locations[0]; // game gives up to 3 locations
    [JsonIgnore] public string GetCoordinates => Locations[0].MapLink.CoordinateString;
    [JsonIgnore] public bool IsOpenWorld => !Locations[0].IsDuty;
}

[Serializable]
public class HuntingMonsterLocation
{
    public uint Terri { get; set; }
    public uint Map { get; set; }
    public uint Zone { get; set; }

    public float xCoord { get; set; } = 0;
    public float yCoord { get; set; } = 0;

    [JsonIgnore] public string Name = string.Empty;
    [JsonIgnore] public bool IsDuty;
    [JsonIgnore] public uint DutyKey;
    [JsonIgnore] public string DutyName = string.Empty;
    [JsonIgnore] public MapLinkPayload MapLink = null!;
    [JsonIgnore] public Vector2 Coords => new(xCoord, yCoord);

    public HuntingMonsterLocation(uint terri, uint map)
    {
        Terri = terri;
        Map = map;
    }

    [JsonConstructor]
    public HuntingMonsterLocation(uint terri, uint map, uint zone)
    {
        Terri = terri;
        Map = map;
        Zone = zone;
    }

    public void InitLocation()
    {
        var mapSheet = Sheets.TerritoryTypeSheet.GetRow(Terri);

        Name = ToTitleCaseExtended(mapSheet.Map.Value.PlaceName.Value.Name);
        MapLink = new MapLinkPayload(Terri, Map, xCoord, yCoord);
        if (Zone == 0)
            return;

        var zoneSheet = Sheets.TerritoryTypeSheet.GetRow(Zone)!;
        var content = Sheets.ContentFinderConditionSheet.FirstOrNull(x => x.TerritoryType.RowId == zoneSheet.RowId);
        if (content == null)
            return;

        if (ToTitleCaseExtended(content.Value.Name) == "")
            return;

        IsDuty = true;
        DutyName = ToTitleCaseExtended(content.Value.Name);
        DutyKey = content.Value.RowId;
    }
}

public struct MonsterProgress
{
    public readonly int Killed = 0;
    public readonly bool Done = false;

    public MonsterProgress(int killed) => Killed = killed;
    public MonsterProgress(bool done) => Done = done;
}
