using Dalamud.IoC;
using Dalamud.Plugin;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using CheapLoc;
using Dalamud;
using Hunty.Windows;
using Dalamud.Game.Command;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using Hunty.Data;
using Hunty.IPC;
using Lumina.Excel;
using Lumina.Excel.GeneratedSheets;
using Newtonsoft.Json;

using GrandCompany = Lumina.Excel.GeneratedSheets.GrandCompany;
using Map = Lumina.Excel.GeneratedSheets.Map;

namespace Hunty
{
    public sealed class Plugin : IDalamudPlugin
    {
        [PluginService] public static DalamudPluginInterface PluginInterface { get; set; } = null!;
        [PluginService] public static IDataManager Data { get; set; } = null!;
        [PluginService] public static IChatGui ChatGui { get; set; } = null!;
        [PluginService] public static IClientState ClientState { get; set; } = null!;
        [PluginService] public static ICommandManager CommandManager { get; set; } = null!;
        [PluginService] public static IGameGui GameGui { get; set; } = null!;
        [PluginService] public static IFramework Framework { get; set; } = null!;
        [PluginService] public static IPluginLog Log { get; set; } = null!;

        private const string CommandName = "/hunty";
        private const string CommandXL = "/huntyxl";

        private Localization Localization = new();
        private Configuration Configuration { get; init; }
        private WindowSystem WindowSystem = new("Hunty");
        private MainWindow MainWindow = null!;
        private XLWindow XLWindow = null!;

        public readonly HuntingData HuntingData = null!;
        private uint CurrentJobId;
        private ClassJob CurrentJobParent = new();

        public static TeleportConsumer TeleportConsumer = null!;

        private static ExcelSheet<Map> MapSheet = null!;
        private static ExcelSheet<MapMarker> MapMarkerSheet = null!;
        private static ExcelSheet<Aetheryte> AetheryteSheet = null!;

        public Plugin()
        {
            Configuration = PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
            Configuration.Initialize(PluginInterface);

            MapSheet = Data.GetExcelSheet<Map>()!;
            MapMarkerSheet = Data.GetExcelSheet<MapMarker>()!;
            AetheryteSheet = Data.GetExcelSheet<Aetheryte>()!;

            TeleportConsumer = new TeleportConsumer();

            MainWindow = new MainWindow(this);
            XLWindow = new XLWindow(this);
            WindowSystem.AddWindow(MainWindow);
            WindowSystem.AddWindow(XLWindow);
            Localization.SetupWithLangCode(PluginInterface.UiLanguage);

            PluginInterface.UiBuilder.Draw += DrawUI;
            PluginInterface.UiBuilder.OpenConfigUi += OpenConfigWindow;
            PluginInterface.LanguageChanged += Localization.SetupWithLangCode;

            GetLocMonsterNames();
            TexturesCache.Initialize();

            CommandManager.AddHandler(CommandName, new CommandInfo(OnCommand)
            {
                HelpMessage = Loc.Localize("Help Message", "Opens a small guide book.")
            });

            CommandManager.AddHandler(CommandXL, new CommandInfo(OnXLCommand)
            {
                HelpMessage = Loc.Localize("Help Message 2", "Opens a big guide book.")
            });

            try
            {
                Log.Debug("Loading Monsters.");

                var path = Path.Combine(PluginInterface.AssemblyLocation.Directory?.FullName!, "monsters.json");
                var jsonString = File.ReadAllText(path);
                HuntingData = JsonConvert.DeserializeObject<HuntingData>(jsonString);
            }
            catch (Exception e)
            {
                Log.Error("There was a problem building the HuntingData.");
                Log.Error(e.Message);
            }

            MainWindow.Initialize();
            XLWindow.Initialize();
            Framework.Update += CheckJobChange;
            ClientState.Login += OnLogin;
        }

        private void OnLogin()
        {
            if (ClientState.LocalPlayer == null)
                return;

            var currentJobRes = ClientState.LocalPlayer.ClassJob;
            CurrentJobId = currentJobRes.Id;
            CurrentJobParent = currentJobRes.GameData!.ClassJobParent.Value!;

            var name = Helper.ToTitleCaseExtended(CurrentJobParent.Name);
            Log.Debug($"Logging in on: {name}");
            MainWindow.SetJobAndGc(CurrentJobParent.RowId, name, GetGrandCompany(), GetCurrentGcName());
            XLWindow.SetJobAndGc(CurrentJobParent.RowId, name, GetGrandCompany(), GetCurrentGcName());
        }

        private void CheckJobChange(IFramework framework)
        {
            if (ClientState.LocalPlayer == null)
                return;

            var currentJobRes = ClientState.LocalPlayer.ClassJob;
            if (currentJobRes.Id != CurrentJobId)
            {
                CurrentJobId = currentJobRes.Id;

                var parentJob = currentJobRes.GameData!.ClassJobParent;
                if (parentJob.Row != CurrentJobParent.RowId)
                {
                    CurrentJobParent = parentJob.Value!;
                    var name = Helper.ToTitleCaseExtended(CurrentJobParent.Name);
                    Log.Debug($"Job switch: {name}");
                    MainWindow.SetJobAndGc(parentJob.Row, name, GetGrandCompany(), GetCurrentGcName());
                    XLWindow.SetJobAndGc(parentJob.Row, name, GetGrandCompany(), GetCurrentGcName());
                }
            }
        }

        public void Dispose()
        {
            Framework.Update -= CheckJobChange;
            PluginInterface.UiBuilder.Draw -= DrawUI;
            PluginInterface.UiBuilder.OpenConfigUi -= OpenConfigWindow;
            PluginInterface.LanguageChanged -= Localization.SetupWithLangCode;

            TexturesCache.Instance?.Dispose();

            MainWindow.Dispose();
            XLWindow.Dispose();
            WindowSystem.RemoveWindow(MainWindow);
            WindowSystem.RemoveWindow(XLWindow);
            CommandManager.RemoveHandler(CommandName);
            CommandManager.RemoveHandler(CommandXL);
        }

        private void OnCommand(string command, string args) => MainWindow.IsOpen = true;
        private void OnXLCommand(string command, string args) => XLWindow.IsOpen = true;
        private void DrawUI() => WindowSystem.Draw();
        private void OpenConfigWindow() => MainWindow.Toggle();

        public void SetMapMarker(MapLinkPayload map) => GameGui.OpenMapWithMapLink(map);
        public unsafe void OpenDutyFinder(uint key) => AgentContentsFinder.Instance()->OpenRegularDuty(key);
        public unsafe uint GetGrandCompany() => UIState.Instance()->PlayerState.GrandCompany + (uint) 10000;
        public string GetCurrentGcName() => Helper.ToTitleCaseExtended(Data.GetExcelSheet<GrandCompany>()!.GetRow(GetGrandCompany() - 10000)!.Name);
        public unsafe int GetRankFromMemory(uint job) => MonsterNoteManager.Instance()->RankDataArraySpan[StaticData.JobInMemory(job)].Rank;

        public unsafe Dictionary<string, MonsterProgress> GetMemoryProgress(uint job, int rank)
        {
            var currentProgress = new Dictionary<string, MonsterProgress>();

            var huntingRank = HuntingData.JobRanks[job][rank];
            var monsterNoteManager = MonsterNoteManager.Instance();
            var jobMemory = monsterNoteManager->RankDataArraySpan[StaticData.JobInMemory(job)];
            var progressRank = jobMemory.Rank;

            if (progressRank > rank) // Rank is already finished, all monsters are done
            {
                foreach (var monster in huntingRank.Tasks.SelectMany(a => a.Monsters))
                    currentProgress.Add(monster.Name, new MonsterProgress(true));

            }
            else if (progressRank < rank) // Rank not yet reached, kills must be zero
            {
                foreach (var monster in huntingRank.Tasks.SelectMany(a => a.Monsters))
                    currentProgress.Add(monster.Name, new MonsterProgress(0));
            }
            else
            {
                foreach (var (task, progress) in huntingRank.Tasks.Zip(jobMemory.RankDataArraySpan.ToArray()))
                {
                    foreach (var (monster, idx) in task.Monsters.Select((monster, i) => ( monster, i )))
                    {
                        var killed = progress.Counts[idx];
                        currentProgress.Add(monster.Name,
                            killed == monster.Count ? new MonsterProgress(true) : new MonsterProgress(killed));
                    }
                }
            }

            return currentProgress;
        }

        private void GetLocMonsterNames()
        {
            var currentLanguage = ClientState.ClientLanguage;
            if (currentLanguage == ClientLanguage.English) return;

            var monsterNotes = Data.GetExcelSheet<MonsterNote>()!;
            var bNpcNamesEnglish = Data.GetExcelSheet<BNpcName>(ClientLanguage.English)!;

            var fill = StaticData.MonsterNames[currentLanguage];
            foreach (var currentMonster in monsterNotes
                         .Where(monsterNote => monsterNote.RowId != 0)
                         .SelectMany(monsterNote => monsterNote.MonsterNoteTarget
                             .Where(monster => monster.Row != 0)
                             .Select(monster => monster.Value!.BNpcName.Value!)))
            {
                var bNpcEnglish = bNpcNamesEnglish.GetRow(currentMonster.RowId)!;

                var correctedName = Helper.ToTitleCaseExtended(currentMonster.Singular, currentMonster.Article);
                if (ClientState.ClientLanguage == ClientLanguage.German)
                    correctedName = Helper.CorrectGermanNames(correctedName, currentMonster.Pronoun);

                fill[Helper.ToTitleCaseExtended(bNpcEnglish.Singular, bNpcEnglish.Article)] = correctedName;
            }

        }

        // From: https://github.com/SheepGoMeh/HuntBuddy/blob/5a92e0e104839c30eaf398790dee32b793c3c53e/HuntBuddy/Location.cs#L520
        public static void TeleportToNearestAetheryte(HuntingMonsterLocation location)
        {
            var map = MapSheet.GetRow(location.Map)!;
            var nearestAetheryteId = MapMarkerSheet
                .Where(x => x.DataType == 3 && x.RowId == map.MapMarkerRange)
                .Select(
                    marker => new
                    {
                        distance = Vector2.DistanceSquared(
                            location.Coords,
                            ConvertLocationToRaw(marker.X, marker.Y, map.SizeFactor)),
                        rowId = marker.DataKey
                    })
                .MinBy(x => x.distance).rowId;

            // Support the unique case of aetheryte not being in the same map
            var nearestAetheryte = location.Terri == 399
                ? map.TerritoryType?.Value?.Aetheryte.Value
                : AetheryteSheet.FirstOrDefault(x => x.IsAetheryte && x.Territory.Row == location.Terri && x.RowId == nearestAetheryteId);

            if (nearestAetheryte == null)
                return;

            TeleportConsumer.UseTeleport(nearestAetheryte.RowId);
        }

        private static Vector2 ConvertLocationToRaw(int x, int y, float scale)
        {
            var num = scale / 100f;
            return new Vector2(ConvertRawToMap((int)((x - 1024) * num * 1000f), scale), ConvertRawToMap((int)((y - 1024) * num * 1000f), scale));
        }

        private static float ConvertRawToMap(int pos, float scale)
        {
            var num1 = scale / 100f;
            var num2 = (float)(pos * (double)num1 / 1000.0f);
            return (40.96f / num1 * ((num2 + 1024.0f) / 2048.0f)) + 1.0f;
        }

        // If square ever decides to change the Hunting Log~
        #region internal
        private void WriteMonsterNote()
        {
            var hunt = new HuntingData();

            var monsterNotes = Data.GetExcelSheet<MonsterNote>()!;
            var mapSheet = Data.GetExcelSheet<Map>();

            var tencounter = 0;
            var index = 0;
            foreach (var task in monsterNotes)
            {
                if (task.RowId == 0) continue;
                try
                {
                    // TODO Make uint compatible
                    // var parts = task.Name.ToString().Split(" ");
                    // var name = string.Join(" ", parts.Take(parts.Length-1));
                    // if (name == "Order of the Twin Adder") name = "Twin Adder";

                    // if (!hunt.JobRanks.ContainsKey(name))
                    // {
                    //     hunt.JobRanks.Add(name, new List<HuntingRank>() {new HuntingRank()});
                    // }

                    var newTask = new HuntingTask
                    {
                        Name = task.Name.ToString()
                    };
                    foreach (var (target, count) in task.MonsterNoteTarget.Zip(task.Count))
                    {
                        if (target.Row == 0) continue;

                        var newMonster = new HuntingMonster();
                        var bNpc = target.Value!.BNpcName.Value!;
                        newMonster.Name = Helper.ToTitleCaseExtended(bNpc.Singular, bNpc.Article);
                        newMonster.Count = count;
                        newMonster.Icon = (uint) target.Value.Icon;

                        foreach (var placename in target.Value!.UnkData3)
                        {
                            var map = mapSheet.FirstOrDefault(row => row.PlaceName.Row == placename.PlaceNameZone);
                            if (map != null && map.TerritoryType.Row != 0)
                            {
                                var zone = mapSheet.FirstOrDefault(row => row.PlaceName.Row == placename.PlaceNameLocation);
                                if (zone != null && zone.TerritoryType.Row != 0)
                                {
                                    newMonster.Locations.Add(new HuntingMonsterLocation(map.TerritoryType.Row, map.RowId, zone.TerritoryType.Row));
                                }
                                else
                                {
                                    newMonster.Locations.Add(new HuntingMonsterLocation(map.TerritoryType.Row, map.RowId));
                                }

                                break;
                            }
                        }

                        newTask.Monsters.Add(newMonster);
                    }

                    // if (newTask.Monsters.Any()) hunt.JobRanks[name][index].Tasks.Add(newTask);

                    tencounter++;
                    if (tencounter % 50 == 0)
                    {
                        index = 0;
                    }
                    else if (tencounter % 10 == 0)
                    {
                        index++;
                        // hunt.JobRanks[name].Add(new HuntingRank());
                    }
                }
                catch (Exception e)
                {
                    Log.Error(e.Message);
                }
            }

            var l = JsonConvert.SerializeObject(hunt, new JsonSerializerSettings { Formatting = Formatting.Indented,});

            var path = Path.Combine(PluginInterface.AssemblyLocation.Directory?.FullName!, "monstersTest.json");
            Log.Information($"Writing monster json");
            Log.Information(path);
            File.WriteAllText(path, l);
        }
        #endregion
    }
}
