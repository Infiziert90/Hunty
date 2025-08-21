using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Dalamud.Interface;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;
using Hunty.Resources;
using Dalamud.Bindings.ImGui;

namespace Hunty.Windows;

public class CompanionWindow : Window, IDisposable
{
    private readonly Plugin Plugin;

    private int SelectedArea;
    private string LastArea;
    private ushort LastTerritory;

    private static readonly Dictionary<uint, uint> RequirementIcons = new() {
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

    public CompanionWindow(Plugin plugin) : base("Hunty Companion")
    {
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(600, 450),
            MaximumSize = new Vector2(float.MaxValue, float.MaxValue)
        };

        Plugin = plugin;
    }

    public void Dispose() { }

    public override void Draw()
    {
        var territories = new Dictionary<string, uint>();
        var currentAreas = new Dictionary<string, List<(uint, HuntingMonster)>>();
        foreach (var job in Plugin.JobArray.Prepend(Plugin.CurrentGc))
        {
            // No GC, skip this build step
            if (job == 10000)
                continue;

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
                    territories.TryAdd(location.Name, location.Terri);
                }
            }
        }

        var areaList = currentAreas.Keys.OrderBy(name => territories[name]).ToArray();

        var areaSelected = false;
        if (LastTerritory != Plugin.ClientState.TerritoryType)
        {
            LastTerritory = Plugin.ClientState.TerritoryType;
            var territoryName = territories.FirstOrDefault(x => x.Value == Plugin.ClientState.TerritoryType).Key;
            if (territoryName != null)
            {
                areaSelected = true;
                SelectedArea = Array.IndexOf(areaList, territoryName);
            }
        }

        if (!areaSelected)
            SelectedArea = areaList.Contains(LastArea) ? Array.IndexOf(areaList, LastArea) : 0;

        float arrowButtonWidth;
        using (ImRaii.PushFont(UiBuilder.IconFont))
            arrowButtonWidth = ImGui.CalcTextSize(FontAwesomeIcon.ArrowLeft.ToIconString()).X + (ImGui.GetStyle().FramePadding.X * 2);

        var comboWidth = ImGui.GetContentRegionAvail().X - (2 * ImGui.GetStyle().ItemSpacing.X) - (arrowButtonWidth * 2);
        ImGui.PushItemWidth(comboWidth);
        ImGui.Combo("##areaSelector", ref SelectedArea, areaList, areaList.Length);
        Helper.DrawArrows(ref SelectedArea, areaList.Length, 4);
        LastArea = areaList[SelectedArea];

        ImGuiHelpers.ScaledDummy(5.0f);
        using var table = ImRaii.Table("##monsterTable", 4);
        if (table.Success)
        {
            ImGui.TableSetupColumn("##icon", ImGuiTableColumnFlags.WidthFixed, Helper.IconSize.X * ImGuiHelpers.GlobalScale);
            ImGui.TableSetupColumn(Language.TableLabelMonster);
            ImGui.TableSetupColumn(Language.TableLabelDone, ImGuiTableColumnFlags.None, 0.4f);
            ImGui.TableSetupColumn(Language.TableLabelCoords, ImGuiTableColumnFlags.None, 1.5f);

            ImGui.TableHeadersRow();
            foreach (var (job, monster) in currentAreas[LastArea].OrderBy(a => a.Item2.Name))
            {
                var index = monster.Locations.FindIndex(l => l.Name == LastArea);
                if (index < 0)
                    continue;

                var monsterLocation = monster.Locations[index];
                var monsterProgress = Plugin.GetMemoryProgress(job, Plugin.GetRankFromMemory(job))[monster.Name];
                if (monsterProgress.Done)
                    continue;

                ImGui.TableNextColumn();
                Helper.DrawIcon(monster.Icon);

                ImGui.TableNextColumn();
                ImGui.TextUnformatted(Plugin.GetMonsterNameLoc(monster.Id));

                ImGui.TableNextColumn();
                Helper.DrawIcon(RequirementIcons[job], 0.75f);
                if (ImGui.IsItemHovered())
                    Helper.Tooltip(Utils.ToTitleCaseExtended(job > 10000 ? Plugin.CurrentGcName : Sheets.ClassJobSheet.GetRow(job).Name));

                ImGui.SameLine();
                ImGui.AlignTextToFramePadding();
                ImGui.TextUnformatted($"{monsterProgress.Killed} / {monster.Count.ToString()}");

                ImGui.TableNextColumn();
                if (monster.IsOpenWorld)
                {
                    if (Plugin.TeleportConsumer.IsAvailable)
                    {
                        using var pushedFont = ImRaii.PushFont(UiBuilder.IconFont);
                        if (ImGui.Button($"{FontAwesomeIcon.StreetView.ToIconString()}##{job}{monster.Name}{monster.Icon.ToString()}{monster.Count}"))
                        {
                            Plugin.TeleportToNearestAetheryte(monsterLocation);
                            Plugin.SetMapMarker(monsterLocation.MapLink);
                        }

                        ImGui.SameLine();
                    }

                    if (ImGui.Selectable($"{monsterLocation.Name} {monsterLocation.MapLink.CoordinateString}##{job}{monster.Icon.ToString()}"))
                        Plugin.SetMapMarker(monsterLocation.MapLink);
                }
                else
                {
                    if (ImGui.Selectable($"{monsterLocation.DutyName}##{job}{monster.Icon.ToString()}"))
                        Plugin.OpenDutyFinder(monsterLocation.DutyKey);
                }

                ImGui.TableNextRow();
            }
        }
    }
}
