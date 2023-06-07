using Dalamud.Game;
using Dalamud.Game.Gui;
using Dalamud.IoC;
using Dalamud.Logging;
using Dalamud.Plugin;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CheapLoc;
using Dalamud;
using Hunty.Windows;
using Dalamud.Data;
using Dalamud.Game.ClientState;
using Dalamud.Game.Command;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using Dalamud.Interface.Windowing;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using Hunty.Data;
using Lumina.Excel.GeneratedSheets;
using Newtonsoft.Json;
using GrandCompany = Lumina.Excel.GeneratedSheets.GrandCompany;
using Map = Lumina.Excel.GeneratedSheets.Map;

namespace Hunty
{
    public sealed class Plugin : IDalamudPlugin
    {
        [PluginService] public static DataManager Data { get; set; } = null!;

        public string Name => "Hunty";
        private const string CommandName = "/hunty";
        private const string CommandXL = "/huntyxl";

        public static DalamudPluginInterface PluginInterface { get; private set; } = null!;
        public readonly ClientState ClientState = null!;
        private Localization Localization = new();
        private Configuration Configuration { get; init; }
        private CommandManager CommandManager { get; init; }
        private Framework Framework { get; init; }
        private GameGui GameGui { get; init; }
        private WindowSystem WindowSystem = new("Hunty");
        private MainWindow MainWindow = null!;
        private XLWindow XLWindow = null!;

        public readonly HuntingData HuntingData = null!;
        private uint currentJobId;
        private ClassJob currentJobParent = new();

        public Plugin(
            [RequiredVersion("1.0")] DalamudPluginInterface pluginInterface,
            [RequiredVersion("1.0")] Framework framework,
            [RequiredVersion("1.0")] GameGui gameGui,
            [RequiredVersion("1.0")] CommandManager commandManager,
            [RequiredVersion("1.0")] ClientState clientState)
        {
            PluginInterface = pluginInterface;
            Framework = framework;
            GameGui = gameGui;
            CommandManager = commandManager;
            ClientState = clientState;

            Configuration = PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
            Configuration.Initialize(PluginInterface);

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
                PluginLog.Debug("Loading Monsters.");

                var path = Path.Combine(PluginInterface.AssemblyLocation.Directory?.FullName!, "monsters.json");
                var jsonString = File.ReadAllText(path);
                HuntingData = JsonConvert.DeserializeObject<HuntingData>(jsonString);
            }
            catch (Exception e)
            {
                PluginLog.Error("There was a problem building the HuntingData.");
                PluginLog.Error(e.Message);
            }

            MainWindow.Initialize();
            XLWindow.Initialize();
            Framework.Update += CheckJobChange;
            ClientState.Login += OnLogin;
        }

        private void OnLogin(object? sender, EventArgs e)
        {
            if (ClientState.LocalPlayer == null) return;
            var currentJobRes = ClientState.LocalPlayer.ClassJob;
            currentJobId = currentJobRes.Id;
            currentJobParent = currentJobRes.GameData!.ClassJobParent.Value!;

            var name = Helper.ToTitleCaseExtended(currentJobParent.Name);
            PluginLog.Debug($"Logging in on: {name}");
            MainWindow.SetJobAndGc(currentJobParent.RowId, name, GetGrandCompany(), GetCurrentGcName());
            XLWindow.SetJobAndGc(currentJobParent.RowId, name, GetGrandCompany(), GetCurrentGcName());
        }

        private void CheckJobChange(Framework framework)
        {
            if (ClientState.LocalPlayer == null) return;

            var currentJobRes = ClientState.LocalPlayer.ClassJob;
            if (currentJobRes.Id != currentJobId)
            {
                currentJobId = currentJobRes.Id;

                var parentJob = currentJobRes.GameData!.ClassJobParent;
                if (parentJob.Row != currentJobParent.RowId)
                {
                    currentJobParent = parentJob.Value!;
                    var name = Helper.ToTitleCaseExtended(currentJobParent.Name);
                    PluginLog.Debug($"Job switch: {name}");
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
                    PluginLog.Error(e.Message);
                }
            }

            var l = JsonConvert.SerializeObject(hunt, new JsonSerializerSettings { Formatting = Formatting.Indented,});

            var path = Path.Combine(PluginInterface.AssemblyLocation.Directory?.FullName!, "monstersTest.json");
            PluginLog.Information($"Writing monster json");
            PluginLog.Information(path);
            File.WriteAllText(path, l);
        }
        #endregion
    }
}
