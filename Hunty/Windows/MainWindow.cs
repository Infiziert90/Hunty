using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Dalamud.Interface;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;
using Hunty.Resources;
using ImGuiNET;

namespace Hunty.Windows;

public class MainWindow : Window, IDisposable
{
    private readonly Plugin Plugin;

    private int SelectedClass;
    private int SelectedRank;
    private int SelectedArea;

    private bool OpenGrandCompany;

    private static readonly string[] Jobs = new string[9];
    private readonly Dictionary<string, List<HuntingMonster>> CurrentAreas = new();

    public MainWindow(Plugin plugin) : base("Hunty")
    {
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(600, 450),
            MaximumSize = new Vector2(float.MaxValue, float.MaxValue)
        };

        Plugin = plugin;

        for (var i = 0; i < Plugin.JobArray.Length; i++)
            Jobs[i] = Utils.ToTitleCaseExtended(Sheets.ClassJobSheet.GetRow(Plugin.JobArray[i]).Name);
    }

    public void Dispose() { }

    public override void Draw()
    {
        var oldRank = SelectedRank;
        var oldClass = SelectedClass;

        ImGui.AlignTextToFramePadding();
        List<HuntingRank> selClass;
        if (OpenGrandCompany)
        {
            Plugin.HuntingData.JobRanks.TryGetValue(Plugin.CurrentGc, out selClass);
            ImGui.TextUnformatted(Plugin.CurrentGcName);
        }
        else if (!Plugin.HuntingData.JobRanks.TryGetValue(Plugin.CurrentJob, out selClass))
        {
            ImGui.Combo("##classSelector", ref SelectedClass, Jobs, Jobs.Length);
            Helper.DrawArrows(ref SelectedClass, Jobs.Length);

            selClass = Plugin.HuntingData.JobRanks[Plugin.JobArray[SelectedClass]];
        }
        else
        {
            ImGui.TextUnformatted(Plugin.CurrentJobName);
        }

        var btnText = OpenGrandCompany ? Language.ButtonJobs : Language.ButtonGrandCompany;
        var textLength = ImGui.CalcTextSize(btnText).X;
        ImGui.SameLine(ImGui.GetContentRegionAvail().X - textLength);

        if (ImGui.Button(btnText))
        {
            OpenGrandCompany ^= true;
            Defaults();
            return;
        }

        if (OpenGrandCompany && Plugin.CurrentGc == 10000)
        {
            Helper.TextColored(Helper.RedColor, Language.ErrorNoGrandCompany);
            return;
        }

        ImGuiHelpers.ScaledDummy(5);
        ImGui.Separator();
        ImGuiHelpers.ScaledDummy(5);

        var current = !OpenGrandCompany ? Plugin.JobArray[SelectedClass] : Plugin.CurrentGc;

        var rankList = selClass!.Select((_, i) => $"{Language.SelectorRank} {i+1}").ToArray();
        ImGui.Combo("##rankSelector", ref SelectedRank, rankList, rankList.Length);
        Helper.DrawArrows(ref SelectedRank, rankList.Length, 2);
        Helper.DrawProgressSymbol(SelectedRank < Plugin.GetRankFromMemory(current));

        FillCurrentAreas(oldRank, oldClass, selClass);

        var areaList = CurrentAreas.Keys.ToArray();
        ImGui.Combo("##areaSelector", ref SelectedArea, areaList, areaList.Length);
        Helper.DrawArrows(ref SelectedArea, areaList.Length, 4);

        var monsters = CurrentAreas[areaList[SelectedArea]];
        var memoryProgress = Plugin.GetMemoryProgress(current, SelectedRank);
        Helper.DrawProgressSymbol(monsters.All(x => memoryProgress[x.Name].Done));

        ImGuiHelpers.ScaledDummy(10);

        using var table = ImRaii.Table("##monsterTable", 4);
        if (table.Success)
        {
            ImGui.TableSetupColumn("##icon", ImGuiTableColumnFlags.WidthFixed, Helper.IconSize.X * ImGuiHelpers.GlobalScale);
            ImGui.TableSetupColumn(Language.TableLabelMonster);
            ImGui.TableSetupColumn(Language.TableLabelDone, ImGuiTableColumnFlags.None, 0.4f);
            ImGui.TableSetupColumn(monsters[0].IsOpenWorld ? Language.TableLabelCoords : Language.TableLabelDungeon, ImGuiTableColumnFlags.None, 1.5f);

            ImGui.TableHeadersRow();
            foreach (var monster in monsters)
            {
                ImGui.TableNextColumn();
                Helper.DrawIcon(monster.Icon);

                ImGui.TableNextColumn();
                ImGui.TextUnformatted(Plugin.GetMonsterNameLoc(monster.Id));

                ImGui.TableNextColumn();
                var monsterProgress = memoryProgress[monster.Name];
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
                        if (ImGui.Button($"{FontAwesomeIcon.StreetView.ToIconString()}##{monster.Name}{monster.Icon.ToString()}{monster.Count}"))
                        {
                            Plugin.TeleportToNearestAetheryte(monster.GetLocation);
                            Plugin.SetMapMarker(monster.GetLocation.MapLink);
                        }

                        ImGui.SameLine();
                    }

                    if (ImGui.Selectable($"{monster.GetLocation.Name} {monster.GetCoordinates}##{monster.Icon.ToString()}"))
                        Plugin.SetMapMarker(monster.GetLocation.MapLink);
                }
                else
                {
                    if (ImGui.Selectable($"{monster.GetLocation.DutyName}##{monster.Icon.ToString()}"))
                        Plugin.OpenDutyFinder(monster.GetLocation.DutyKey);
                }

                ImGui.TableNextRow();
            }
        }
    }

    public void Defaults()
    {
        SelectedClass = 0;
        SelectedRank = 0;
        SelectedArea = 0;

        if (Plugin.HuntingData.JobRanks.ContainsKey(Plugin.CurrentJob))
            SelectedClass = Array.IndexOf(Plugin.JobArray, Plugin.CurrentJob);

        CurrentAreas.Clear();
    }

    private void FillCurrentAreas(int oldRank, int oldClass, IReadOnlyList<HuntingRank> selClass)
    {
        if (SelectedRank == oldRank && SelectedClass == oldClass && CurrentAreas.Count != 0)
            return;

        SelectedArea = 0;
        CurrentAreas.Clear();
        foreach (var m in selClass![SelectedRank].Tasks.SelectMany(a => a.Monsters))
        {
            var location = m.Locations[0];
            if (location.Name == string.Empty)
                location.InitLocation();

            CurrentAreas.GetOrCreate(location.IsDuty ? location.DutyName: location.Name).Add(m);
        }
    }
}
