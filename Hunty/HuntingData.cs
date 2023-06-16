using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using Lumina.Excel.GeneratedSheets;
using Newtonsoft.Json;
using static Hunty.Helper;

namespace Hunty;

public class HuntingData
{
    public Dictionary<uint, List<HuntingRank>> JobRanks { get; set; } = new();
}

[Serializable]
public class HuntingRank
{
    public List<HuntingTask> Tasks { get; set; } = new();
}

[Serializable]
public class HuntingTask
{
    public string Name;
    public List<HuntingMonster> Monsters { get; set; } = new();
}

[Serializable]
public class HuntingMonster
{
    public string Name { get; set; }
    public byte Count { get; set; }
    public uint Icon { get; set; }

    public List<HuntingMonsterLocation> Locations { get; set; } = new();

    public HuntingMonsterLocation GetLocation => Locations[0]; // game gives up to 3 locations
    public string GetCoordinates => Locations[0].MapLink.CoordinateString;
    public bool IsOpenWorld => !Locations[0].IsDuty;
}

[Serializable]
public class HuntingMonsterLocation
{
    public uint Terri { get; set; }
    public uint Map { get; set; }
    public uint Zone { get; set; } = 0;

    public float xCoord { get; set; } = 0;
    public float yCoord { get; set; } = 0;

    [JsonIgnore] public string Name = string.Empty;
    [JsonIgnore] public bool IsDuty = false;
    [JsonIgnore] public string DutyName = string.Empty;
    [JsonIgnore] public uint DutyKey = 0;
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
        var mapSheet = Plugin.Data.GetExcelSheet<TerritoryType>()!.GetRow(Terri)!;
        var contentSheet = Plugin.Data.GetExcelSheet<ContentFinderCondition>()!;

        Name = ToTitleCaseExtended(mapSheet.Map.Value!.PlaceName.Value!.Name, 0);
        MapLink = new MapLinkPayload(Terri, Map, xCoord, yCoord);

        if (Zone == 0) return;

        var zoneSheet = Plugin.Data.GetExcelSheet<TerritoryType>()!.GetRow(Zone)!;
        var content = contentSheet.FirstOrDefault(x => x.TerritoryType.Row == zoneSheet.RowId);
        if (content == null) return;

        if (ToTitleCaseExtended(content.Name, 0) == "") return;

        IsDuty = true;
        DutyName = ToTitleCaseExtended(content.Name, 0);
        DutyKey = content.RowId;
    }
}

public struct MonsterProgress
{
    public readonly int Killed = 0;
    public readonly bool Done = false;

    public MonsterProgress(int killed) => Killed = killed;
    public MonsterProgress(bool done) => Done = done;
}