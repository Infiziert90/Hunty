using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading;
using CheapLoc;
using Dalamud.Interface;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Graphics.Scene;
using Hunty.Data;
using ImGuiNET;
using ImPlotNET;
using Lumina.Excel;
using Lumina.Excel.GeneratedSheets;

namespace Hunty.Windows;

public class CompanionWindow : Window, IDisposable
{
    private Plugin Plugin;
    private int selectedArea = 0;
    private string lastArea;

    private uint currentJob = 1;
    private string currentJobName = "";
    private uint currentGc = 1;
    private string currentGcName = "";
    private ushort currentTerritory = 0;
    private ushort lastTerritory = 0;

    private static Vector2 size = new(40, 40);
    private static readonly uint[] JobArray = { 1, 2, 3, 4, 5, 6, 7, 26, 29 };
    private static readonly Dictionary<uint, uint> RequirementIcons = new Dictionary<uint, uint> {
        { 1, 62001 },     /* GLA/PAL */
        { 2, 62002 },     /* PGL/MNK */
        { 3, 62003 },     /* MRD/WAR */
        { 4, 62004 },     /* LAN/DRG */
        { 5, 62005 },     /* ARC/BRD */
        { 6, 62006 },     /* CON/WHM */
        { 7, 62007 },     /* THM/BLM */
        { 26, 62026 },    /* ARC/SMN/SCH */
        { 29, 62029 },    /* ROG/NIN */
        { 10001, 60567 }, /* Maelstrom */
        { 10002, 60568 }, /* Twin Adder */
        { 10003, 60569 }, /* Immortal Flames */
    };

    private static Dictionary<string, string> monsterLanguage;
    private static ExcelSheet<ClassJob> ClassJobs = null!;

    public CompanionWindow(Plugin plugin) : base("Hunty Companion")
    {
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(600, 450),
            MaximumSize = new Vector2(float.MaxValue, float.MaxValue)
        };

        Plugin = plugin;
        ClassJobs = Plugin.Data.GetExcelSheet<ClassJob>()!;
    }

    public void Initialize()
    {
        monsterLanguage = StaticData.MonsterNames[Plugin.ClientState.ClientLanguage];
    }

    public void Dispose() { }

    public override void Draw()
    {
        var currentAreas = new Dictionary<string, List<(uint, HuntingMonster)>>();
        var territories = new Dictionary<string, uint>();
        uint[] jobs = { currentGc };
        foreach (var job in jobs.Concat(JobArray).ToArray())
        {
            var jobRank = Plugin.GetRankFromMemory(job);
            if (jobRank >= (job > 10000 ? 3 : 5))
                continue;

            var jobTasks = Plugin.HuntingData.JobRanks[job][jobRank].Tasks;
            foreach (var m in jobTasks.SelectMany(a => a.Monsters))
            {
                if (Plugin.GetMemoryProgress(job, Plugin.GetRankFromMemory(job))[m.Name].Done)
                    continue;
                foreach (var location in m.Locations)
                {
                    if (location.Name == string.Empty)
                        location.InitLocation();

                    currentAreas.GetOrCreate(location.Name).Add((job, m));
                    if (!territories.ContainsKey(location.Name))
                    {
                        territories[location.Name] = location.Terri;
                    }
                }
            }
        }

        var areaList = currentAreas.Keys.OrderBy(name => territories[name]).ToArray();

        bool areaSelected = false;
        if (lastTerritory != currentTerritory)
        {
            lastTerritory = currentTerritory;
            string territoryName = territories.FirstOrDefault(x => x.Value == currentTerritory).Key;
            if (territoryName != null)
            {
                areaSelected = true;
                selectedArea = Array.IndexOf(areaList, territoryName);
            }
        }
        if (!areaSelected)
        {
            selectedArea = areaList.Contains(lastArea)
                ? Array.IndexOf(areaList, lastArea)
                : 0;
        }

        float comboWidth = ImGui.GetContentRegionAvail().X
            - (2 * ImGui.GetStyle().ItemSpacing.X) /* Components margins */
            - (2 * 23 * ImGuiHelpers.GlobalScale) /* Size of 2 buttons */;
        ImGui.PushItemWidth(comboWidth);
        ImGui.Combo("##areaSelector", ref selectedArea, areaList, areaList.Length);
        DrawArrows(ref selectedArea, areaList.Length, 4);
        // ImGui.TextUnformatted(ImGui.GetItemRectSize().ToString()); // Detect button size
        lastArea = areaList[selectedArea];

        ImGuiHelpers.ScaledDummy(5);

        if (ImGui.BeginTable($"##monsterTable", 4))
        {
            ImGui.TableSetupColumn("##icon", ImGuiTableColumnFlags.None, 0.2f);
            ImGui.TableSetupColumn(Loc.Localize("Table Label: Monster", "Monster"));
            ImGui.TableSetupColumn(Loc.Localize("Table Label: Done", "Done"), ImGuiTableColumnFlags.None, 0.4f);
            ImGui.TableSetupColumn(Loc.Localize("Table Label: Coords", "Coords"), ImGuiTableColumnFlags.None, 1.5f);
            ImGui.TableHeadersRow();

            foreach (var (job, monster) in currentAreas[lastArea].OrderBy(a => a.Item2.Name))
            {
                var index = monster.Locations.FindIndex(l => l.Name == lastArea);
                if (index < 0)
                    continue;
                var monsterLocation = monster.Locations[index];

                var monsterProgress = Plugin.GetMemoryProgress(job, Plugin.GetRankFromMemory(job))[monster.Name];
                if (monsterProgress.Done)
                    continue;

                ImGui.TableNextColumn();
                DrawIcon(monster.Icon);

                ImGui.TableNextColumn();
                ImGui.TextUnformatted(monsterLanguage == null ? Helper.ToTitleCaseExtended(monster.Name) : monsterLanguage[monster.Name]);

                ImGui.TableNextColumn();
                DrawIcon(RequirementIcons[job], 0.75f);
                if (ImGui.IsItemHovered())
                {
                    ImGui.SetTooltip(Helper.ToTitleCaseExtended(job > 10000 ? currentGcName : ClassJobs.GetRow(job)!.Name));
                }

                ImGui.SameLine();
                ImGui.AlignTextToFramePadding();
                ImGui.TextUnformatted($"{monsterProgress.Killed} / {monster.Count.ToString()}");

                ImGui.TableNextColumn();
                if (monster.IsOpenWorld)
                {
                    if (Plugin.TeleportConsumer.IsAvailable)
                    {
                        if (ImGui.Button($"T##{job}{monster.Name}{monster.Icon.ToString()}{monster.Count}"))
                        {
                            Plugin.TeleportToNearestAetheryte(monsterLocation);
                            Plugin.SetMapMarker(monsterLocation.MapLink);
                        }
                        ImGui.SameLine();
                    }

                    if (ImGui.Selectable($"{monsterLocation.Name} {monsterLocation.MapLink.CoordinateString}##{job}{monster.Icon.ToString()}"))
                    {
                        Plugin.SetMapMarker(monsterLocation.MapLink);
                    }
                }
                else
                {
                    if (ImGui.Selectable($"{monsterLocation.DutyName}##{job}{monster.Icon.ToString()}"))
                    {
                        Plugin.OpenDutyFinder(monsterLocation.DutyKey);
                    }
                }

                ImGui.TableNextRow();
            }
        }
        ImGui.EndTable();
    }

    private static void DrawArrows(ref int selected, int length, int id)
    {
        ImGui.SameLine();
        if (selected == 0) ImGui.BeginDisabled();
        if (Dalamud.Interface.Components.ImGuiComponents.IconButton(id, FontAwesomeIcon.ArrowLeft)) selected--;
        if (selected == 0) ImGui.EndDisabled();

        ImGui.SameLine();
        if (selected + 1 == length) ImGui.BeginDisabled();
        if (Dalamud.Interface.Components.ImGuiComponents.IconButton(id+1, FontAwesomeIcon.ArrowRight)) selected++;
        if (selected + 1 == length) ImGui.EndDisabled();
    }

    private static void DrawIcon(uint iconId, float scale = 1)
    {
        var iconSize = size * ImGuiHelpers.GlobalScale * scale;
        var texture = Plugin.Texture.GetIcon(iconId);
        if (texture == null)
        {
            ImGui.Text($"Unknown icon {iconId}");
            return;
        }

        ImGui.Image(texture.ImGuiHandle, iconSize);
    }

    public void SetJobAndGc(uint job, string name, uint gc, string gcName)
    {
        currentJob = job;
        currentJobName = name;
        currentGc = gc;
        currentGcName = gcName;
    }

    public void SetTerritory(ushort territory)
    {
        currentTerritory = territory;
        // Plugin.Log.Debug($"Current territory: {territory}");
    }
}
