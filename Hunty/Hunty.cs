using Dalamud.IoC;
using Dalamud.Plugin;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using CheapLoc;
using Dalamud.Game;
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
using Lumina.Excel.Sheets;
using Newtonsoft.Json;

using GrandCompany = Lumina.Excel.Sheets.GrandCompany;
using Map = Lumina.Excel.Sheets.Map;

namespace Hunty
{
    public sealed class Plugin : IDalamudPlugin
    {
        [PluginService] public static IDalamudPluginInterface PluginInterface { get; set; } = null!;
        [PluginService] public static IDataManager Data { get; set; } = null!;
        [PluginService] public static IChatGui ChatGui { get; set; } = null!;
        [PluginService] public static IClientState ClientState { get; set; } = null!;
        [PluginService] public static ICommandManager CommandManager { get; set; } = null!;
        [PluginService] public static IGameGui GameGui { get; set; } = null!;
        [PluginService] public static IFramework Framework { get; set; } = null!;
        [PluginService] public static IPluginLog Log { get; set; } = null!;
        [PluginService] public static ITextureProvider Texture { get; set; } = null!;

        private const string CommandName = "/hunty";
        private const string CommandXL = "/huntyxl";
        private const string CommandCompanion = "/huntycompanion";

        public Configuration Configuration { get; init; }

        private Localization Localization = new();
        private WindowSystem WindowSystem = new("Hunty");
        private MainWindow MainWindow;
        private XLWindow XLWindow;
        private CompanionWindow CompanionWindow;

        public readonly HuntingData HuntingData = null!;
        private uint CurrentJobId;
        private ClassJob CurrentJobParent;

        public static TeleportConsumer TeleportConsumer = null!;

        private static ExcelSheet<BNpcName> BNpcName;

        private static ExcelSheet<Map> MapSheet;
        private static SubrowExcelSheet<MapMarker> MapMarkerSheet;
        private static ExcelSheet<Aetheryte> AetheryteSheet;

        public Plugin()
        {
            Configuration = PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
            Configuration.Initialize(PluginInterface);
            Localization.SetupWithLangCode(PluginInterface.UiLanguage);

            BNpcName = Data.GetExcelSheet<BNpcName>()!;
            MapSheet = Data.GetExcelSheet<Map>()!;
            MapMarkerSheet = Data.GetSubrowExcelSheet<MapMarker>()!;
            AetheryteSheet = Data.GetExcelSheet<Aetheryte>()!;

            TeleportConsumer = new TeleportConsumer();

            MainWindow = new MainWindow(this);
            XLWindow = new XLWindow(this);
            CompanionWindow = new CompanionWindow(this);
            WindowSystem.AddWindow(MainWindow);
            WindowSystem.AddWindow(XLWindow);
            WindowSystem.AddWindow(CompanionWindow);

            PluginInterface.UiBuilder.Draw += DrawUI;
            PluginInterface.UiBuilder.OpenConfigUi += OpenConfigWindow;
            PluginInterface.LanguageChanged += Localization.SetupWithLangCode;

            CommandManager.AddHandler(CommandName, new CommandInfo(OnCommand)
            {
                HelpMessage = Loc.Localize("Help Message", "Opens a small guide book.")
            });

            CommandManager.AddHandler(CommandXL, new CommandInfo(OnXLCommand)
            {
                HelpMessage = Loc.Localize("Help Message 2", "Opens a big guide book.")
            });

            CommandManager.AddHandler(CommandCompanion, new CommandInfo(OnCompanionCommand)
            {
                HelpMessage = Loc.Localize("Help Message 3", "Opens a big guide book with hunts grouped by area.")
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

            Framework.Update += CheckJobChange;
            ClientState.Login += OnLogin;
            ClientState.TerritoryChanged += OnTerritoryChange;
        }

        private void OnLogin()
        {
            if (ClientState.LocalPlayer == null)
                return;

            var currentJobRes = ClientState.LocalPlayer.ClassJob;
            CurrentJobId = currentJobRes.RowId;
            CurrentJobParent = currentJobRes.Value.ClassJobParent.Value;

            var name = Utils.ToTitleCaseExtended(CurrentJobParent.Name);
            Log.Debug($"Logging in on: {name}");
            MainWindow.SetJobAndGc(CurrentJobParent.RowId, name, GetGrandCompany(), GetCurrentGcName());
            XLWindow.SetJobAndGc(CurrentJobParent.RowId, name, GetGrandCompany(), GetCurrentGcName());
            CompanionWindow.SetJobAndGc(CurrentJobParent.RowId, name, GetGrandCompany(), GetCurrentGcName());
            CompanionWindow.SetTerritory(ClientState.TerritoryType);
        }

        private void OnTerritoryChange(ushort territory)
        {
            CompanionWindow.SetTerritory(territory);
        }

        private void CheckJobChange(IFramework framework)
        {
            if (ClientState.LocalPlayer == null)
                return;

            var currentJobRes = ClientState.LocalPlayer.ClassJob;
            if (currentJobRes.RowId != CurrentJobId)
            {
                CurrentJobId = currentJobRes.RowId;

                var parentJob = currentJobRes.Value.ClassJobParent;
                if (parentJob.RowId != CurrentJobParent.RowId)
                {
                    CurrentJobParent = parentJob.Value!;
                    var name = Utils.ToTitleCaseExtended(CurrentJobParent.Name);
                    Log.Debug($"Job switch: {name}");
                    MainWindow.SetJobAndGc(parentJob.RowId, name, GetGrandCompany(), GetCurrentGcName());
                    XLWindow.SetJobAndGc(parentJob.RowId, name, GetGrandCompany(), GetCurrentGcName());
                    CompanionWindow.SetJobAndGc(parentJob.RowId, name, GetGrandCompany(), GetCurrentGcName());
                    CompanionWindow.SetTerritory(ClientState.TerritoryType);
                }
            }
        }

        public void Dispose()
        {
            Framework.Update -= CheckJobChange;
            PluginInterface.UiBuilder.Draw -= DrawUI;
            PluginInterface.UiBuilder.OpenConfigUi -= OpenConfigWindow;
            PluginInterface.LanguageChanged -= Localization.SetupWithLangCode;

            MainWindow.Dispose();
            XLWindow.Dispose();
            CompanionWindow.Dispose();
            WindowSystem.RemoveWindow(MainWindow);
            WindowSystem.RemoveWindow(XLWindow);
            WindowSystem.RemoveWindow(CompanionWindow);
            CommandManager.RemoveHandler(CommandName);
            CommandManager.RemoveHandler(CommandXL);
            CommandManager.RemoveHandler(CommandCompanion);
        }

        private void OnCommand(string command, string args) => MainWindow.IsOpen = true;
        private void OnXLCommand(string command, string args) => XLWindow.IsOpen = true;
        private void OnCompanionCommand(string command, string args) => CompanionWindow.IsOpen = true;
        private void DrawUI() => WindowSystem.Draw();
        private void OpenConfigWindow() => MainWindow.Toggle();

        public void SetMapMarker(MapLinkPayload map) => GameGui.OpenMapWithMapLink(map);
        public unsafe void OpenDutyFinder(uint key) => AgentContentsFinder.Instance()->OpenRegularDuty(key);
        public unsafe uint GetGrandCompany() => UIState.Instance()->PlayerState.GrandCompany + (uint) 10000;
        public string GetCurrentGcName() => Utils.ToTitleCaseExtended(Data.GetExcelSheet<GrandCompany>()!.GetRow(GetGrandCompany() - 10000)!.Name);
        public unsafe int GetRankFromMemory(uint job) => MonsterNoteManager.Instance()->RankData[StaticData.JobInMemory(job)].Rank;

        public unsafe Dictionary<string, MonsterProgress> GetMemoryProgress(uint job, int rank)
        {
            var currentProgress = new Dictionary<string, MonsterProgress>();

            var huntingRank = HuntingData.JobRanks[job][rank];
            var monsterNoteManager = MonsterNoteManager.Instance();
            var jobMemory = monsterNoteManager->RankData[StaticData.JobInMemory(job)];
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
                foreach (var (task, progress) in huntingRank.Tasks.Zip(jobMemory.RankData.ToArray()))
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

        // From: https://github.com/SheepGoMeh/HuntBuddy/blob/5a92e0e104839c30eaf398790dee32b793c3c53e/HuntBuddy/Location.cs#L520
        public static void TeleportToNearestAetheryte(HuntingMonsterLocation location)
        {
            var map = MapSheet.GetRow(location.Map)!;
            var nearestAetheryteId = MapMarkerSheet
                .SelectMany(x => x)
                .Where(x => x.DataType == 3 && x.RowId == map.MapMarkerRange)
                .Select(
                    marker => new
                    {
                        distance = Vector2.DistanceSquared(
                            location.Coords,
                            ConvertLocationToRaw(marker.X, marker.Y, map.SizeFactor)),
                        rowId = marker.DataKey.RowId
                    })
                .MinBy(x => x.distance).rowId;

            // Support the unique case of aetheryte not being in the same map
            var nearestAetheryte = location.Terri == 399
                ? map.TerritoryType.Value.Aetheryte.Value
                : AetheryteSheet.FirstOrNull(x => x.IsAetheryte && x.Territory.RowId == location.Terri && x.RowId == nearestAetheryteId);

            if (nearestAetheryte == null)
                return;

            TeleportConsumer.UseTeleport(nearestAetheryte.Value.RowId);
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

        public static string GetMonsterNameLoc(uint id)
        {
            var npc = BNpcName.GetRow(id)!;
            var correctedName = Utils.ToTitleCaseExtended(npc.Singular, npc.Article);
            if (ClientState.ClientLanguage == ClientLanguage.German)
                correctedName = Utils.CorrectGermanNames(correctedName, npc.Pronoun);

            return correctedName;
        }

        // Square decided to change duty arounds, so keep it
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
                if (task.RowId == 0)
                    continue;

                try
                {
                    var newTask = new HuntingTask { Name = task.Name.ToString() };

                    foreach (var (target, count) in task.MonsterNoteTarget.Zip(task.Count))
                    {
                        if (target.RowId == 0)
                            continue;

                        var newMonster = new HuntingMonster();
                        var bNpc = target.Value!.BNpcName.Value!;
                        newMonster.Name = Utils.ToTitleCaseExtended(bNpc.Singular, bNpc.Article);
                        newMonster.Count = count;
                        newMonster.Icon = (uint) target.Value.Icon;

                        var map = mapSheet.FirstOrNull(row => target.Value.PlaceNameZone.Where(z => z.RowId != 0).Any(z => z.Value.RowId == row.PlaceName.RowId));
                        if (map != null && map.Value.TerritoryType.RowId != 0)
                        {
                            var zone = mapSheet.FirstOrNull(row => target.Value.PlaceNameLocation.Where(z => z.RowId != 0).Any(l => l.RowId == row.PlaceName.RowId));
                            if (zone != null && zone.Value.TerritoryType.RowId != 0)
                            {
                                newMonster.Locations.Add(new HuntingMonsterLocation(map.Value.TerritoryType.RowId, map.Value.RowId, zone.Value.TerritoryType.RowId));
                            }
                            else
                            {
                                newMonster.Locations.Add(new HuntingMonsterLocation(map.Value.TerritoryType.RowId, map.Value.RowId));
                            }
                        }
                        Log.Information($"Note: {newTask.Name} - {newMonster.Locations.First().Name} Terri: {newMonster.Locations.First().Terri} Map: {newMonster.Locations.First().Map} Zone: {newMonster.Locations.First().Zone}");
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
