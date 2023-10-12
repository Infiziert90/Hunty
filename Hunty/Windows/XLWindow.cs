using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using CheapLoc;
using Dalamud.Interface;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Windowing;
using Hunty.Data;
using ImGuiNET;
using Lumina.Excel;
using Lumina.Excel.GeneratedSheets;

namespace Hunty.Windows;

public class XLWindow : Window, IDisposable
{
    private Plugin Plugin;
    private int selectedArea = 0;
    private string lastArea;

    private uint currentJob = 1;
    private string currentJobName = "";
    private uint currentGc = 1;
    private string currentGcName = "";

    private static Vector2 size = new(40, 40);
    private static readonly uint[] JobArray = { 1, 2, 3, 4, 5, 6, 7, 26, 29 };

    private static Dictionary<string, string> monsterLanguage;
    private static ExcelSheet<ClassJob> ClassJobs = null!;

    public XLWindow(Plugin plugin) : base("Hunty XL")
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
        ImGui.TextUnformatted(currentGcName);
        ImGui.TextUnformatted(currentJobName);

        Plugin.HuntingData.JobRanks.TryGetValue(currentGc, out var gc);

        if (!Plugin.HuntingData.JobRanks.TryGetValue(currentJob, out var selClass))
        {
            ImGuiHelpers.ScaledDummy(5);
            ImGui.Separator();
            ImGuiHelpers.ScaledDummy(5);
            ImGui.TextColored(ImGuiColors.ParsedOrange,Loc.Localize("Error: No Hunting Job", "This job has no hunting log."));
            return;
        }

        var currentRank = Plugin.GetRankFromMemory(currentJob);
        if (currentRank >= 5)
        {
            ImGuiHelpers.ScaledDummy(5);
            ImGui.Separator();
            ImGuiHelpers.ScaledDummy(5);
            ImGui.TextColored(ImGuiColors.ParsedOrange,Loc.Localize("Error: Hunting Log finished", "This job is done with the hunting log."));
            return;
        }

        var jobRanks = Plugin.HuntingData.JobRanks[currentJob];
        var currentAreas = new Dictionary<string, List<HuntingMonster>>();
        foreach (var m in jobRanks[currentRank].Tasks.SelectMany(a => a.Monsters))
        {
            var location = m.GetLocation;
            if (location.Name == string.Empty)
                location.InitLocation();

            var name = location.Name;
            currentAreas.GetOrCreate(name).Add(m);
        }

        var areaList = currentAreas.Keys.ToArray();
        selectedArea = areaList.Contains(lastArea)
            ? Array.IndexOf(areaList, lastArea)
            : 0;

        ImGui.Combo("##areaSelector", ref selectedArea, areaList, areaList.Length);
        Helper.DrawArrows(ref selectedArea, areaList.Length, 4);
        lastArea = areaList[selectedArea];

        ImGuiHelpers.ScaledDummy(5);
        ImGui.Separator();
        ImGuiHelpers.ScaledDummy(5);

        foreach (var job in JobArray)
        {
            var jobRank = Plugin.GetRankFromMemory(job);
            if (jobRank >= 5)
                continue;

            var area = new Dictionary<string, List<HuntingMonster>>();
            var ranks = Plugin.HuntingData.JobRanks[job];
            foreach (var m in ranks[jobRank].Tasks.SelectMany(a => a.Monsters))
            {
                var location = m.GetLocation;
                if (location.Name == string.Empty)
                    location.InitLocation();

                var name = location.Name;
                area.GetOrCreate(name).Add(m);
            }

            if (!area.Any())
                continue;

            if (!area.TryGetValue(areaList[selectedArea], out var monsters))
                continue;

            ImGui.TextColored(ImGuiColors.DalamudViolet, Utils.ToTitleCaseExtended(ClassJobs.GetRow(job)!.Name));
            var memoryProgress = Plugin.GetMemoryProgress(job, jobRank);
            Helper.DrawProgressSymbol(monsters.All(x => memoryProgress[x.Name].Done));

            ImGuiHelpers.ScaledDummy(5);

            if (ImGui.BeginTable($"##monsterTable{job}", 4))
            {
                ImGui.TableSetupColumn("##icon", ImGuiTableColumnFlags.None, 0.2f);
                ImGui.TableSetupColumn(Loc.Localize("Table Label: Monster", "Monster"));
                ImGui.TableSetupColumn(Loc.Localize("Table Label: Done", "Done"), ImGuiTableColumnFlags.None, 0.4f);
                ImGui.TableSetupColumn(Loc.Localize("Table Label: Coords", "Coords"), ImGuiTableColumnFlags.None, 1.5f);

                ImGui.TableHeadersRow();

                foreach (var monster in monsters)
                {
                    ImGui.TableNextColumn();
                    Helper.DrawIcon(monster.Icon, size);

                    ImGui.TableNextColumn();
                    ImGui.TextUnformatted(monsterLanguage == null ? Utils.ToTitleCaseExtended(monster.Name) : monsterLanguage[monster.Name]);

                    ImGui.TableNextColumn();
                    var monsterProgress = memoryProgress[monster.Name];
                    if (monsterProgress.Done)
                    {
                        ImGui.PushFont(UiBuilder.IconFont);
                        ImGui.TextUnformatted(FontAwesomeIcon.Check.ToIconString());
                        ImGui.PopFont();
                    }
                    else
                    {
                        ImGui.TextUnformatted($"{monsterProgress.Killed} / {monster.Count.ToString()}");
                    }

                    ImGui.TableNextColumn();
                    if (monster.IsOpenWorld)
                    {
                        if (Plugin.TeleportConsumer.IsAvailable)
                        {
                            if (ImGui.Button($"T##{job}{monster.Name}{monster.Icon.ToString()}{monster.Count}"))
                            {
                                Plugin.TeleportToNearestAetheryte(monster.GetLocation);
                                Plugin.SetMapMarker(monster.GetLocation.MapLink);
                            }
                            ImGui.SameLine();
                        }

                        if (ImGui.Selectable($"{monster.GetLocation.Name} {monster.GetCoordinates}##{job}{monster.Icon.ToString()}"))
                        {
                            Plugin.SetMapMarker(monster.GetLocation.MapLink);
                        }
                    }
                    else
                    {
                        if (ImGui.Selectable($"{monster.GetLocation.DutyName}##{job}{monster.Icon.ToString()}"))
                        {
                            Plugin.OpenDutyFinder(monster.GetLocation.DutyKey);
                        }
                    }

                    ImGui.TableNextRow();
                }
            }
            ImGui.EndTable();
        }
    }

    public void SetJobAndGc(uint job, string name, uint gc, string gcName)
    {
        currentJob = job;
        currentJobName = name;
        currentGc = gc;
        currentGcName = gcName;
    }
}
