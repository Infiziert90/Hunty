using System;
using System.Numerics;
using Dalamud.Interface.Windowing;
using ImGuiNET;

namespace Hunty.Windows;

public class MainWindow : Window, IDisposable
{
    private Plugin Plugin;
    private int selectedSpell = 1; // 0 is the first learned blu skill
    private static Vector2 size = new(80, 80);

    public MainWindow(Plugin plugin) : base("Hunty", ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse)
    {
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(370, 500),
            MaximumSize = new Vector2(float.MaxValue, float.MaxValue)
        };
            
        Plugin = plugin;
    }

    public void Dispose() { }

    public override void Draw()
    {
        foreach (var (key, value) in Plugin.HuntingData.Classes)
        {
            ImGui.TextUnformatted(key);
            ImGui.Indent();
            foreach (var monsters in value.Monsters)
            {
                ImGui.TextUnformatted("Rank:");
                ImGui.Indent();
                foreach (var monster in monsters)
                {
                    ImGui.TextUnformatted(monster.Name);
                    ImGui.TextUnformatted(monster.Count.ToString());
                }
                ImGui.Unindent();
            }
            
            ImGui.Unindent();
        }
        
        
        // var keyList = Sources.Keys.ToList();
        // var stringList = Sources.Select(x => $"{x.Key} - {x.Value.Name}").ToArray();
        // ImGui.Combo("##spellSelector", ref selectedSpell, stringList, stringList.Length);
        //
        // ImGui.SameLine();
        // if (selectedSpell == 0) ImGui.BeginDisabled();
        // if (Dalamud.Interface.Components.ImGuiComponents.IconButton(0, FontAwesomeIcon.ArrowLeft)) selectedSpell--;
        // if (selectedSpell == 0) ImGui.EndDisabled();
        //
        // ImGui.SameLine();
        // if (selectedSpell + 1 == stringList.Length) ImGui.BeginDisabled();
        // if (Dalamud.Interface.Components.ImGuiComponents.IconButton(1, FontAwesomeIcon.ArrowRight)) selectedSpell++;
        // if (selectedSpell + 1 == stringList.Length) ImGui.EndDisabled();
        //
        // ImGuiHelpers.ScaledDummy(10);
        // ImGui.Separator();
        // ImGuiHelpers.ScaledDummy(5);
        //
        // if (Sources.Any())
        // {
        //     var spell = Sources[keyList[selectedSpell]];
        //     ImGuiHelpers.ScaledDummy(10);
        //     
        //     ImGui.BeginChild("Content", new Vector2(0, -30), false, 0);
        //     if (spell.Source.Type != RegionType.Buy)
        //     {
        //         ImGui.TextUnformatted($"Min Lvl: {spell.Source.DutyMinLevel}");
        //     }
        //     if (spell.Source.Info != "")
        //     {
        //         ImGui.TextUnformatted($"{(spell.Source.Type != RegionType.Buy ? "Mob" : "Info")}: {spell.Source.Info}");
        //     }
        //     if (spell.Source.TerritoryType != null)
        //     {
        //         ImGui.TextUnformatted(!spell.Source.IsDuty
        //             ? $"Region: {spell.Source.PlaceName}"
        //             : $"Duty: {spell.Source.DutyName}");
        //     }
        //     if (spell.Source.MapLink != null)
        //     {
        //         if (ImGui.Selectable($"Coords: {spell.Source.MapLink.CoordinateString}##mapCoords"))
        //         {
        //             Plugin.SetMapMarker(spell.Source.MapLink);
        //         }
        //     }
        //
        //     if (spell.Source.TerritoryType != null && spell.Source.Type != RegionType.Buy)
        //     {
        //         var combos = Sources.Where(x => x.Key != keyList[selectedSpell] && spell.Source.CompareTerritory(x.Value.Source)).ToArray();
        //         if (combos.Any())
        //         {
        //             ImGuiHelpers.ScaledDummy(5);
        //             ImGui.Separator();
        //             ImGuiHelpers.ScaledDummy(5);
        //             ImGui.TextUnformatted("Same location:");
        //             foreach (var (key, value) in combos)
        //             {
        //                 if (ImGui.Selectable($"{key} - {value.Name}"))
        //                 {
        //                     selectedSpell = keyList.FindIndex(x => x == key);
        //                 }
        //             }
        //         }
        //     }
        //
        //     if (spell.Source.AcquiringTips != "")
        //     {
        //         ImGuiHelpers.ScaledDummy(5);
        //         ImGui.Separator();
        //         ImGuiHelpers.ScaledDummy(5);
        //         ImGui.TextUnformatted($"Acquisition Tips:");
        //         foreach (var tip in spell.Source.AcquiringTips.Split("\n"))
        //         {
        //             ImGui.Bullet();
        //             ImGui.PushTextWrapPos();
        //             ImGui.TextUnformatted(tip);
        //         }
        //     }
        //     ImGui.EndChild();
        //     
        //     ImGui.BeginChild("BottomBar", new Vector2(0,0), false, 0);
        //     ImGui.TextDisabled("Data sourced from ffxiv.consolegameswiki.com");
        //     ImGui.EndChild();
        // }
    }
}
