using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;
using Hunty.Resources;
using Lumina.Excel.Sheets;

namespace Hunty.Windows;

public class CompanionWindow : Window, IDisposable
{
    private readonly Plugin Plugin;

    private int SelectedArea;
    private string LastArea = string.Empty;
    private uint LastTerritory;

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
    private static readonly Dictionary<uint, uint> ClassToJob = new() {
        { 1, 19 },
        { 2, 20 },
        { 3, 21 },
        { 4, 22 },
        { 5, 23 },
        { 6, 24 },
        { 7, 25 },
        { 26, 27 },
        { 29, 30 }
    };
    private readonly ReadOnlyDictionary<uint, sbyte> ClassJobToExpArrayIndex;

    public CompanionWindow(Plugin plugin) : base("Hunty Companion", ImGuiWindowFlags.NoScrollbar)
    {

        Plugin = plugin;
        ClassJobToExpArrayIndex = Plugin.Data.GetExcelSheet<ClassJob>()
            .Where(x => x is { RowId: > 0, ExpArrayIndex: >= 0 })
            .ToDictionary(x => x.RowId, x => x.ExpArrayIndex)
            .AsReadOnly();
    }

    public void Dispose() { }

    public unsafe void EquipGearset(byte id) => RaptureGearsetModule.Instance()->EquipGearset(id);

    internal unsafe ReadOnlyCollection<(byte Id, uint ClassJob, short Level, short ItemLevel)> GetJobGearSets()
    {
        List<(byte, uint, short, short)> jobs = [];

        var playerState = PlayerState.Instance();
        var gearsetModule = RaptureGearsetModule.Instance();
        if (playerState == null || gearsetModule == null)
            return jobs.AsReadOnly();

        for (var i = 0; i < 100; ++i)
        {
            var gearset = gearsetModule->GetGearset(i);
            if (gearset->Flags.HasFlag(RaptureGearsetModule.GearsetFlag.Exists))
            {
                var gearsetJob = gearset->ClassJob;
                if (!ClassToJob.ContainsKey(gearsetJob) && !ClassToJob.ContainsValue(gearsetJob))
                    continue;

                var level = playerState->ClassJobLevels[ClassJobToExpArrayIndex[gearsetJob]];
                if (level == 0)
                    continue;

                var itemLevel = gearset->ItemLevel;
                jobs.Add((gearset->Id, gearsetJob, level, itemLevel));
            }
        }

        return jobs.AsReadOnly();
    }

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

        if (currentAreas.Count == 0)
        {
            ImGuiHelpers.ScaledDummy(5);
            Helper.TextColored(ImGuiColors.HealerGreen, Language.ErrorLogDone);
            return;
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
        using var table = ImRaii.Table("##monsterTable", 5);
        if (table.Success)
        {
            ImGui.TableSetupColumn("##icon", ImGuiTableColumnFlags.WidthFixed, Helper.IconSize.X * ImGuiHelpers.GlobalScale);
            ImGui.TableSetupColumn(Language.TableLabelMonster);
            ImGui.TableSetupColumn("Rank", ImGuiTableColumnFlags.WidthFixed, 50f);
            ImGui.TableSetupColumn(Language.TableLabelDone, ImGuiTableColumnFlags.WidthFixed, 75f);
            ImGui.TableSetupColumn(Language.TableLabelCoords, ImGuiTableColumnFlags.WidthFixed, 100f);

            ImGui.TableHeadersRow();
            var playerPosition = Plugin.Objects[0]?.Position;
            if (playerPosition != null)
                playerPosition = new(playerPosition.Value.X, 0, playerPosition.Value.Z);
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
                if (ImGui.IsItemClicked())
                {
                    if (Plugin.CommandManager.Commands.ContainsKey("/esp"))
                        Plugin.CommandManager.ProcessCommand($"/esp sub {Plugin.GetMonsterNameLoc(monster.Id)}");
                    ShowLocation(monsterLocation);
                }
                if (ImGui.IsItemHovered())
                    ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);

                ImGui.TableNextColumn();
                ImGui.TextUnformatted($"{Plugin.GetRankFromMemory(job) + 1} (Lv{Plugin.GetRankFromMemory(job) * 10})");

                ImGui.TableNextColumn();
                Helper.DrawIcon(RequirementIcons[job], 0.75f);
                var gearset = GetJobGearSets().FirstOrNull(x => x.ClassJob == job ||
                    ClassToJob.TryGetValue(job, out var classToJob) && x.ClassJob == classToJob);
                var _job = gearset?.ClassJob ?? job;
                if (ImGui.IsItemClicked() && job <= 10000 && gearset != null)
                {
                    EquipGearset(gearset.Value.Id);
                }
                if (ImGui.IsItemHovered())
                {
                    if (job <= 10000 && gearset != null)
                        ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
                    Helper.Tooltip(Utils.ToTitleCaseExtended(job > 10000 ? Plugin.CurrentGcName : Sheets.ClassJobSheet.GetRow(_job).Name));
                }

                ImGui.SameLine();
                ImGui.AlignTextToFramePadding();
                ImGui.TextUnformatted($"{monsterProgress.Killed} / {monster.Count.ToString()}");

                ImGui.TableNextColumn();
                var distance = string.Empty;
                if (playerPosition != null && monsterLocation.Terri == Plugin.ClientState.TerritoryType)
                {
                    distance = $"{Vector3.Distance(monsterLocation.Position, playerPosition.Value):N0}y ";
                }
                if (monster.IsOpenWorld)
                {
                    if (Plugin.TeleportConsumer.IsAvailable)
                    {
                        using var pushedFont = ImRaii.PushFont(UiBuilder.IconFont);
                        if (ImGui.Button($"{FontAwesomeIcon.StreetView.ToIconString()}##{job}{monster.Name}{monster.Icon.ToString()}{monster.Count}"))
                        {
                            ShowLocation(monsterLocation, teleport: true);
                        }

                        ImGui.SameLine();
                    }

                    if (ImGui.Selectable($"{distance}{monsterLocation.Name} {monsterLocation.MapLink.CoordinateString}##{job}{monster.Icon.ToString()}"))
                        ShowLocation(monsterLocation);
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

    public void ShowLocation(HuntingMonsterLocation monsterLocation, bool teleport = false)
    {
        if (teleport)
            Plugin.TeleportToNearestAetheryte(monsterLocation);
        Plugin.SetMapMarker(monsterLocation.MapLink);
    }
}
