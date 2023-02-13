using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Dalamud.Interface;
using Dalamud.Interface.Windowing;
using ImGuiNET;

namespace Hunty.Windows;

public class MainWindow : Window, IDisposable
{
    private Plugin Plugin;
    private int selectedClass = 0;
    private int selectedRank = 0;
    private int selectedArea = 0;
    
    private bool openGrandCompany = false;
    private bool isOpenWorld = false;
    
    private string currentJob = "";
    private string currentGC = "";
    
    private Dictionary<string, List<HuntingMonster>> currentAreas = new();
    
    private readonly Vector4 redColor = new(0.980f, 0.245f, 0.245f, 1.0f);
    private static Vector2 size = new(40, 40);
    private static string[] jobs;
    
    public MainWindow(Plugin plugin) : base("Hunty")
    {
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(600, 450),
            MaximumSize = new Vector2(float.MaxValue, float.MaxValue)
        };
            
        Plugin = plugin;
    }

    public void Initialize() => jobs = Plugin.HuntingData.JobRanks.Keys.ToArray()[..^3];
    public void Dispose() { }

    public override void Draw()
    {
        var oldRank = selectedRank;
        var oldClass = selectedClass;

        List<HuntingRank> selClass;
        if (openGrandCompany)
        {
            Plugin.HuntingData.JobRanks.TryGetValue(currentGC, out selClass);
            ImGui.TextUnformatted(currentGC);
        }
        else if (!Plugin.HuntingData.JobRanks.TryGetValue(currentJob, out selClass))
        {
            ImGui.Combo("##classSelector", ref selectedClass, jobs, jobs.Length);
            DrawArrows(ref selectedClass, jobs.Length, 0);

            selClass = Plugin.HuntingData.JobRanks[jobs[selectedClass]];
        }
        else
        {
            ImGui.TextUnformatted(currentJob);
        }

        var btnText = openGrandCompany ? "Jobs" : "Grand Company";
        var textLength = ImGui.CalcTextSize(btnText).X;
        var scrollBarSpacing = ImGui.GetScrollMaxY() == 0 ? 0.0f : 15.0f;
        ImGui.SameLine(ImGui.GetWindowWidth() - 15.0f - textLength - scrollBarSpacing);
        
        if (ImGui.Button(btnText))
        {
            openGrandCompany ^= true;
            Defaults();
            return;
        }
        
        if (openGrandCompany && currentGC == "No GC")
        {
            ImGui.TextColored(redColor,"This character has no Grand Company.");
            return;
        }
        
        ImGuiHelpers.ScaledDummy(5);
        ImGui.Separator();
        ImGuiHelpers.ScaledDummy(5);
        
        var rankList = selClass!.Select((_, i) => $"Rank {i+1}").ToArray();
        ImGui.Combo("##rankSelector", ref selectedRank, rankList, rankList.Length);
        DrawArrows(ref selectedRank, rankList.Length, 2);
        
        FillCurrentAreas(oldRank, oldClass, selClass);
        
        var areaList = currentAreas.Keys.ToArray();
        ImGui.Combo("##areaSelector", ref selectedArea, areaList, areaList.Length);
        DrawArrows(ref selectedArea, areaList.Length, 4);
        
        ImGuiHelpers.ScaledDummy(10);
        
        var monsters = currentAreas[areaList[selectedArea]];
        isOpenWorld = monsters.First().IsOpenWorld;

        var memoryProgress = Plugin.GetMemoryProgress(!openGrandCompany ? jobs[selectedClass] : currentGC, selectedRank);
        if (ImGui.BeginTable("##monsterTable", 4))
        {
            ImGui.TableSetupColumn("##icon", ImGuiTableColumnFlags.None, 0.2f);
            ImGui.TableSetupColumn("Monster");
            ImGui.TableSetupColumn("Needed", ImGuiTableColumnFlags.None, 0.4f);
            ImGui.TableSetupColumn(isOpenWorld ? "Coords" : "Duty", ImGuiTableColumnFlags.None, 1.5f);
            
            ImGui.TableHeadersRow();
            
            foreach (var monster in monsters)
            {
                ImGui.TableNextColumn();
                DrawIcon(monster.Icon);
                
                ImGui.TableNextColumn();
                ImGui.TextUnformatted(monster.Name);
                
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
                if (isOpenWorld)
                {
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

    private void Defaults()
    {
        selectedClass = 0;
        selectedRank = 0;
        selectedArea = 0;
        
        if (Plugin.HuntingData.JobRanks.ContainsKey(currentJob))
            selectedClass = Array.IndexOf(jobs, currentJob);
        
        currentAreas.Clear();
    }
    
    private static void DrawIcon(uint iconId)
    {
        var texture = TexturesCache.Instance!.GetTextureFromIconId(iconId);
        ImGui.Image(texture.ImGuiHandle, size);
    }

    public void SetJobAndGc(string job, string gc)
    {
        currentJob = job;
        currentGC = gc;
        
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
