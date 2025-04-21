using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Dalamud.Interface;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;
using Hunty.Resources;
using ImGuiNET;

namespace Hunty.Windows;

public class XLWindow : Window, IDisposable
{
    private readonly Plugin Plugin;

    private int SelectedArea;
    private string LastArea;

    public XLWindow(Plugin plugin) : base("Hunty XL")
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
        ImGui.TextUnformatted(Plugin.CurrentGcName);

        var skipText = Language.OptionSkipDone;
        var textLength = ImGui.GetTextLineHeight() + ImGui.CalcTextSize(skipText).X + ImGui.GetStyle().ItemInnerSpacing.X + ImGui.GetStyle().FramePadding.X;
        ImGui.SameLine(ImGui.GetContentRegionMax().X - textLength);

        if (ImGui.Checkbox(skipText, ref Plugin.Configuration.SkipDone))
            Plugin.Configuration.Save();

        ImGui.TextUnformatted(Plugin.CurrentJobName);
        if (!Plugin.HuntingData.JobRanks.TryGetValue(Plugin.CurrentJob, out _))
        {
            ImGuiHelpers.ScaledDummy(5);
            ImGui.Separator();
            ImGuiHelpers.ScaledDummy(5);

            Helper.TextColored(ImGuiColors.ParsedOrange, Language.ErrorNoLog);
            return;
        }

        var currentRank = Plugin.GetRankFromMemory(Plugin.CurrentJob);
        if (currentRank >= 5)
        {
            ImGuiHelpers.ScaledDummy(5);
            ImGui.Separator();
            ImGuiHelpers.ScaledDummy(5);
            Helper.TextColored(ImGuiColors.ParsedOrange, Language.ErrorLogDone);
            return;
        }

        var currentAreas = new Dictionary<string, List<HuntingMonster>>();
        foreach (var m in Plugin.HuntingData.JobRanks[Plugin.CurrentJob][currentRank].Tasks.SelectMany(a => a.Monsters))
        {
            var location = m.GetLocation;
            if (location.Name == string.Empty)
                location.InitLocation();

            currentAreas.GetOrCreate(location.Name).Add(m);
        }

        var areaList = currentAreas.Keys.ToArray();
        SelectedArea = areaList.Contains(LastArea) ? Array.IndexOf(areaList, LastArea) : 0;

        ImGui.Combo("##areaSelector", ref SelectedArea, areaList, areaList.Length);
        Helper.DrawArrows(ref SelectedArea, areaList.Length, 4);
        LastArea = areaList[SelectedArea];

        ImGuiHelpers.ScaledDummy(5);
        ImGui.Separator();
        ImGuiHelpers.ScaledDummy(5);

        foreach (var job in Plugin.JobArray)
        {
            var jobRank = Plugin.GetRankFromMemory(job);
            if (jobRank >= 5)
                continue;

            var area = new Dictionary<string, List<HuntingMonster>>();
            foreach (var m in Plugin.HuntingData.JobRanks[job][jobRank].Tasks.SelectMany(a => a.Monsters))
            {
                var location = m.GetLocation;
                if (location.Name == string.Empty)
                    location.InitLocation();

                area.GetOrCreate(location.Name).Add(m);
            }

            if (area.Count == 0)
                continue;

            if (!area.TryGetValue(areaList[SelectedArea], out var monsters))
                continue;

            Helper.TextColored(ImGuiColors.DalamudViolet, Utils.ToTitleCaseExtended(Sheets.ClassJobSheet.GetRow(job).Name));

            var memoryProgress = Plugin.GetMemoryProgress(job, jobRank);
            Helper.DrawProgressSymbol(monsters.All(x => memoryProgress[x.Name].Done));

            ImGuiHelpers.ScaledDummy(5);
            using var table = ImRaii.Table($"##monsterTable{job}", 4);
            if (table.Success)
            {
                ImGui.TableSetupColumn("##icon", ImGuiTableColumnFlags.WidthFixed, Helper.IconSize.X * ImGuiHelpers.GlobalScale);
                ImGui.TableSetupColumn(Language.TableLabelMonster);
                ImGui.TableSetupColumn(Language.TableLabelDone, ImGuiTableColumnFlags.None, 0.4f);
                ImGui.TableSetupColumn(Language.TableLabelCoords, ImGuiTableColumnFlags.None, 1.5f);

                ImGui.TableHeadersRow();
                foreach (var monster in monsters)
                {
                    var monsterProgress = memoryProgress[monster.Name];
                    if (Plugin.Configuration.SkipDone && monsterProgress.Done)
                        continue;

                    ImGui.TableNextColumn();
                    Helper.DrawIcon(monster.Icon);

                    ImGui.TableNextColumn();
                    ImGui.TextUnformatted(Plugin.GetMonsterNameLoc(monster.Id));

                    ImGui.TableNextColumn();
                    if (monsterProgress.Done)
                    {
                        using (ImRaii.PushFont(UiBuilder.IconFont))
                            ImGui.TextUnformatted(FontAwesomeIcon.Check.ToIconString());
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
                            using var pushedFont = ImRaii.PushFont(UiBuilder.IconFont);
                            if (ImGui.Button($"{FontAwesomeIcon.StreetView.ToIconString()}##{job}{monster.Name}{monster.Icon.ToString()}{monster.Count}"))
                            {
                                Plugin.TeleportToNearestAetheryte(monster.GetLocation);
                                Plugin.SetMapMarker(monster.GetLocation.MapLink);
                            }

                            ImGui.SameLine();
                        }

                        if (ImGui.Selectable($"{monster.GetLocation.Name} {monster.GetCoordinates}##{job}{monster.Icon.ToString()}"))
                            Plugin.SetMapMarker(monster.GetLocation.MapLink);
                    }
                    else
                    {
                        if (ImGui.Selectable($"{monster.GetLocation.DutyName}##{job}{monster.Icon.ToString()}"))
                            Plugin.OpenDutyFinder(monster.GetLocation.DutyKey);
                    }

                    ImGui.TableNextRow();
                }
            }
        }
    }
}
