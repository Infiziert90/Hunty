using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using CheapLoc;
using Dalamud.Interface;
using Dalamud.Interface.Windowing;
using Hunty.Data;
using ImGuiNET;
using Lumina.Excel.GeneratedSheets;

namespace Hunty.Windows;

public class MainWindow : Window, IDisposable
{
    private Plugin Plugin;
    private int selectedClass = 0;
    private int selectedRank = 0;
    private int selectedArea = 0;

    private bool openGrandCompany = false;

    private uint currentJob = 1;
    private string currentJobName = "";
    private uint currentGc = 1;
    private string currentGcName = "";

    private Dictionary<string, List<HuntingMonster>> currentAreas = new();

    private readonly Vector4 redColor = new(0.980f, 0.245f, 0.245f, 1.0f);
    private static Vector2 size = new(40, 40);
    private static string[] jobs = new string[9];
    private static readonly uint[] JobArray = { 1, 2, 3, 4, 5, 6, 7, 26, 29 };

    private static Dictionary<string, string> monsterLanguage;

    public MainWindow(Plugin plugin) : base("Hunty")
    {
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(600, 450),
            MaximumSize = new Vector2(float.MaxValue, float.MaxValue)
        };

        Plugin = plugin;
    }

    public void Initialize()
    {
        monsterLanguage = StaticData.MonsterNames[Plugin.ClientState.ClientLanguage];

        var classJobs = Plugin.Data.GetExcelSheet<ClassJob>()!;
        for (var i = 0; i < JobArray.Length; i++)
            jobs[i] = Helper.ToTitleCaseExtended(classJobs.GetRow(JobArray[i])!.Name);
    }

    public void Dispose() { }

    public override void Draw()
    {
        var oldRank = selectedRank;
        var oldClass = selectedClass;

        List<HuntingRank> selClass;
        if (openGrandCompany)
        {
            Plugin.HuntingData.JobRanks.TryGetValue(currentGc, out selClass);
            ImGui.TextUnformatted(currentGcName);
        }
        else if (!Plugin.HuntingData.JobRanks.TryGetValue(currentJob, out selClass))
        {
            ImGui.Combo("##classSelector", ref selectedClass, jobs, jobs.Length);
            DrawArrows(ref selectedClass, jobs.Length, 0);

            selClass = Plugin.HuntingData.JobRanks[JobArray[selectedClass]];
        }
        else
        {
            ImGui.TextUnformatted(currentJobName);
        }

        var btnText = openGrandCompany ? Loc.Localize("Button: Jobs", "Jobs") : Loc.Localize("Button: Grand Company", "Grand Company");
        var textLength = ImGui.CalcTextSize(btnText).X;
        var scrollBarSpacing = ImGui.GetScrollMaxY() == 0 ? 0.0f : 15.0f;
        ImGui.SameLine(ImGui.GetWindowWidth() - 15.0f - textLength - scrollBarSpacing);

        if (ImGui.Button(btnText))
        {
            openGrandCompany ^= true;
            Defaults();
            return;
        }

        if (openGrandCompany && currentGc == 10000)
        {
            ImGui.TextColored(redColor,Loc.Localize("Error: No Grand Company", "This character has no Grand Company."));
            return;
        }

        ImGuiHelpers.ScaledDummy(5);
        ImGui.Separator();
        ImGuiHelpers.ScaledDummy(5);

        var current = !openGrandCompany ? JobArray[selectedClass] : currentGc;

        var rankList = selClass!.Select((_, i) => $"{Loc.Localize("Selector: Rank", "Rank")} {i+1}").ToArray();
        ImGui.Combo("##rankSelector", ref selectedRank, rankList, rankList.Length);
        DrawArrows(ref selectedRank, rankList.Length, 2);
        DrawProgressSymbol(selectedRank < Plugin.GetRankFromMemory(current));

        FillCurrentAreas(oldRank, oldClass, selClass);

        var areaList = currentAreas.Keys.ToArray();
        ImGui.Combo("##areaSelector", ref selectedArea, areaList, areaList.Length);
        DrawArrows(ref selectedArea, areaList.Length, 4);

        var monsters = currentAreas[areaList[selectedArea]];
        var memoryProgress = Plugin.GetMemoryProgress(current, selectedRank);
        DrawProgressSymbol(monsters.All(x => memoryProgress[x.Name].Done));

        ImGuiHelpers.ScaledDummy(10);

        if (ImGui.BeginTable("##monsterTable", 4))
        {
            ImGui.TableSetupColumn("##icon", ImGuiTableColumnFlags.None, 0.2f);
            ImGui.TableSetupColumn(Loc.Localize("Table Label: Monster", "Monster"));
            ImGui.TableSetupColumn(Loc.Localize("Table Label: Done", "Done"), ImGuiTableColumnFlags.None, 0.4f);
            ImGui.TableSetupColumn(monsters.First().IsOpenWorld
                ? Loc.Localize("Table Label: Coords", "Coords")
                : Loc.Localize("Table Label: Dungeon", "Dungeon")
                , ImGuiTableColumnFlags.None, 1.5f);

            ImGui.TableHeadersRow();

            foreach (var monster in monsters)
            {
                ImGui.TableNextColumn();
                DrawIcon(monster.Icon);

                ImGui.TableNextColumn();
                ImGui.TextUnformatted(monsterLanguage == null ? Helper.ToTitleCaseExtended(monster.Name) : monsterLanguage[monster.Name]);

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
                        if (ImGui.Button($"T##{monster.Name}{monster.Icon.ToString()}{monster.Count}"))
                        {
                            Plugin.TeleportToNearestAetheryte(monster.GetLocation);
                            Plugin.SetMapMarker(monster.GetLocation.MapLink);
                        }
                        ImGui.SameLine();
                    }

                    if (ImGui.Selectable($"{monster.GetLocation.Name} {monster.GetCoordinates}##{monster.Icon.ToString()}"))
                    {
                        Plugin.SetMapMarker(monster.GetLocation.MapLink);
                    }
                }
                else
                {
                    if (ImGui.Selectable($"{monster.GetLocation.DutyName}##{monster.Icon.ToString()}"))
                    {
                        Plugin.OpenDutyFinder(monster.GetLocation.DutyKey);
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

    private void DrawProgressSymbol(bool done)
    {
        ImGui.SameLine();
        ImGui.PushFont(UiBuilder.IconFont);
        ImGui.TextUnformatted(done
            ? FontAwesomeIcon.Check.ToIconString()
            : FontAwesomeIcon.Times.ToIconString());
        ImGui.PopFont();
    }

    private void Defaults()
    {
        selectedClass = 0;
        selectedRank = 0;
        selectedArea = 0;

        if (Plugin.HuntingData.JobRanks.ContainsKey(currentJob))
            selectedClass = Array.IndexOf(JobArray, currentJob);

        currentAreas.Clear();
    }

    private static void DrawIcon(uint iconId)
    {
        var texture = TexturesCache.Instance!.GetTextureFromIconId(iconId);
        ImGui.Image(texture.ImGuiHandle, size);
    }

    public void SetJobAndGc(uint job, string name, uint gc, string gcName)
    {
        currentJob = job;
        currentJobName = name;
        currentGc = gc;
        currentGcName = gcName;

        Defaults();
    }

    private void FillCurrentAreas(int oldRank, int oldClass, IReadOnlyList<HuntingRank> selClass)
    {
        if (selectedRank == oldRank && selectedClass == oldClass && currentAreas.Any()) return;

        currentAreas.Clear();
        selectedArea = 0;

        foreach (var m in selClass![selectedRank].Tasks.SelectMany(a => a.Monsters))
        {
            var location = m.Locations[0];
            if (location.Name == string.Empty) location.InitLocation();

            var name = !location.IsDuty ? location.Name : location.DutyName;
            currentAreas.GetOrCreate(name).Add(m);
        }
    }
}
