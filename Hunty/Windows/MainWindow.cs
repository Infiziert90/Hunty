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
    
    private string currentJob = "";
    private string currentGC = "";
    
    private Dictionary<string, List<HuntingMonster>> currentAreas = new();
    
    private static Vector2 size = new(40, 40);
    private const ImGuiWindowFlags flags = ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse;

    public MainWindow(Plugin plugin) : base("Hunty")
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
        var oldRank = selectedRank;
        var oldClass = selectedClass;

        HuntingRank selClass;
        var ok = Plugin.HuntingData.Classes.TryGetValue(currentJob, out selClass);
        if (!openGrandCompany && !ok)
        {
            var keyList = Plugin.HuntingData.Classes.Keys.ToArray()[..^3];
            ImGui.Combo("##classSelector", ref selectedClass, keyList, keyList.Length);
            DrawArrows(ref selectedClass, keyList.Length, 0);

            selClass = Plugin.HuntingData.Classes[keyList[selectedClass]];
        }
        else if (openGrandCompany && Plugin.HuntingData.Classes.TryGetValue(currentGC, out selClass))
        {
            ImGui.TextUnformatted(currentGC);
        }
        else
        {
            ImGui.TextUnformatted(currentJob);
        }

        var textLength = ImGui.CalcTextSize(openGrandCompany ? currentJob : currentGC).X;
        var scrollBarSpacing = ImGui.GetScrollMaxY() == 0 ? 0.0f : 15.0f;
        ImGui.SameLine(ImGui.GetWindowWidth() - 15.0f - textLength - scrollBarSpacing);

        if (!openGrandCompany)
        {
            if (ImGui.Button(currentGC))
            {
                openGrandCompany = true;
                Defaults();
                return;
            }
        }
        else
        {
            if (ImGui.Button(currentJob))
            {
                openGrandCompany = false;
                Defaults();
                return;
            }
        }
        
        ImGuiHelpers.ScaledDummy(5);
        ImGui.Separator();
        ImGuiHelpers.ScaledDummy(5);
        
        var rankList = selClass!.Monsters.Select((_, i) => $"Rank {i+1}").ToArray();
        ImGui.Combo("##rankSelector", ref selectedRank, rankList, rankList.Length);
        DrawArrows(ref selectedRank, rankList.Length, 2);
        
        var selRank = selClass.Monsters[selectedRank];

        if (selectedRank != oldRank || selectedClass != oldClass || currentAreas.Count == 0)
        {
            currentAreas.Clear();
            selectedArea = 0;
            
            foreach (var m in selRank)
            {
                var location = m.Locations[0];
                if (location.Name == string.Empty) location.InitLocation();

                var key = !location.IsDuty ? location.Name : location.DutyName;
                if (!currentAreas.ContainsKey(key)) currentAreas.Add(key, new List<HuntingMonster>());
                currentAreas[key].Add(m);
            }
        }
        
        var areaList = currentAreas.Keys.ToArray();
        ImGui.Combo("##areaSelector", ref selectedArea, areaList, areaList.Length);
        DrawArrows(ref selectedArea, areaList.Length, 4);
        
        ImGuiHelpers.ScaledDummy(10);
        
        var monster = currentAreas[areaList[selectedArea]];

        var isDuty = monster[0].Locations[0].IsDuty;
        if (ImGui.BeginTable("##monsterTable", !isDuty ? 4 : 3))
        {
            ImGui.TableSetupColumn("##icon", ImGuiTableColumnFlags.None, 0.2f);
            ImGui.TableSetupColumn("Monster");
            ImGui.TableSetupColumn("Needed", ImGuiTableColumnFlags.None, 0.4f);
            if (!isDuty) ImGui.TableSetupColumn("Coords", ImGuiTableColumnFlags.None, 1.5f);
            
            ImGui.TableHeadersRow();
            foreach (var m in monster)
            {
                ImGui.TableNextColumn();
                DrawIcon(m.Icon);
                
                ImGui.TableNextColumn();
                ImGui.TextUnformatted(m.Name);
                
                ImGui.TableNextColumn();
                ImGui.TextUnformatted(m.Count.ToString());

                if (!isDuty)
                {
                    ImGui.TableNextColumn();
                    if (ImGui.Selectable($"{m.GetLocation().Name} {m.GetLocation().MapLink.CoordinateString}##{m.Icon.ToString()}"))
                    {
                        Plugin.SetMapMarker(m.GetLocation().MapLink);
                    }
                }

                ImGui.TableNextRow();
            }
        }
        ImGui.EndTable();
    }

    public static void DrawArrows(ref int selected, int length, int id)
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

    public void Defaults()
    {
        selectedClass = 0;
        selectedRank = 0;
        selectedArea = 0;
        
        currentAreas.Clear();
    }
    
    public static void DrawIcon(uint iconId)
    {
        var texture = TexturesCache.Instance!.GetTextureFromIconId(iconId);
        ImGui.Image(texture.ImGuiHandle, size);
    }

    public void SetJobAndGc(string job, string gc)
    {
        currentJob = job;
        currentGC = gc;
    }
}
