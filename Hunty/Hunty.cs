using Dalamud.Game;
using Dalamud.Game.Gui;
using Dalamud.IoC;
using Dalamud.Logging;
using Dalamud.Plugin;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Hunty.Windows;
using Dalamud.Data;
using Dalamud.Game.ClientState;
using Dalamud.Game.Command;
using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using Dalamud.Interface.Windowing;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using Hunty.Logic;
using Lumina.Excel.GeneratedSheets;
using Newtonsoft.Json;
using Map = Lumina.Excel.GeneratedSheets.Map;

namespace Hunty
{
    public sealed class Plugin : IDalamudPlugin
    {
        [PluginService] public static DataManager Data { get; set; } = null!;
        
        public string Name => "Hunty";
        private const string CommandName = "/hunty";
        
        public static DalamudPluginInterface PluginInterface { get; private set; } = null!;
        public Configuration Configuration { get; init; }
        private CommandManager CommandManager { get; init; }
        private Framework Framework { get; init; }
        private GameGui GameGui { get; init; }
        private ChatGui Chat { get; init; }
        public WindowSystem WindowSystem = new("Hunty");
        public ClientState ClientState = null!;
        public MainWindow MainWindow = null!;
        public ulong LocalContentID = 0;
        
        public HuntingData HuntingData = null!;
        private ClassJob CurrentJob = new();

        private static List<string> GrandCompanies = new() {"No GC", "Maelstrom", "Twin Adder", "Immortal Flames"};
        
        public Plugin(
            [RequiredVersion("1.0")] DalamudPluginInterface pluginInterface,
            [RequiredVersion("1.0")] Framework framework,
            [RequiredVersion("1.0")] GameGui gameGui,
            [RequiredVersion("1.0")] CommandManager commandManager,
            [RequiredVersion("1.0")] ClientState clientState,
            [RequiredVersion("1.0")] ChatGui chat)
        {   
            PluginInterface = pluginInterface;
            Framework = framework;
            GameGui = gameGui;
            CommandManager = commandManager;
            ClientState = clientState;
            Chat = chat;
            
            Configuration = PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
            Configuration.Initialize(PluginInterface);

            MainWindow = new MainWindow(this);
            WindowSystem.AddWindow(MainWindow);
            
            CommandManager.AddHandler(CommandName, new CommandInfo(OnCommand)
            {
                HelpMessage = "Opens a small guide book"
            });
            
            PluginInterface.UiBuilder.Draw += DrawUI;
            
            TexturesCache.Initialize();

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
            Chat.ChatMessage += OnChatMessage;
            Framework.Update += CheckJobChange;
        }

        private void CheckJobChange(Framework framework)
        {
            if (ClientState.LocalPlayer == null) return;
            LocalContentID  = ClientState.LocalContentId;
            var job = ClientState.LocalPlayer.ClassJob.GameData!.ClassJobParent;

            if (job.Row != CurrentJob.RowId) {
                CurrentJob = job.Value;
                var name = Helper.ToTitleCaseExtended(job.Value!.Name, 0);
                PluginLog.Debug($"Job switch: {name}");
                MainWindow.SetJobAndGc(name, GetGrandCompany());
            }
        }
        
        public void Dispose()
        {
            Chat.ChatMessage -= OnChatMessage;
            Framework.Update -= CheckJobChange;
            
            TexturesCache.Instance?.Dispose();
            
            MainWindow.Dispose();
            WindowSystem.RemoveWindow(MainWindow);
            CommandManager.RemoveHandler(CommandName);
        }
        
        private void OnCommand(string command, string args) => MainWindow.IsOpen = true;
        private void DrawUI() => WindowSystem.Draw();
        
        public void SetMapMarker(MapLinkPayload map) => GameGui.OpenMapWithMapLink(map);
        public unsafe string GetGrandCompany() => GrandCompanies[UIState.Instance()->PlayerState.GrandCompany];
        
        private void OnChatMessage(XivChatType type, uint id, ref SeString sender, ref SeString message, ref bool handled)
        {
            if (type != XivChatType.SystemMessage) return;
            
            PluginLog.Debug($"Content: {message}");
            PluginLog.Debug($"Language: {ClientState.ClientLanguage}");

            var m = Reg.Match(message.ToString(), ClientState.ClientLanguage);
            if (!m.Success) return;

            var entry = new NewProgressEntry(m.Groups);
            var name = entry.Job != "" ? entry.Job : Helper.ToTitleCaseExtended(CurrentJob.Name, 0);
            
            if (GetGrandCompany() != "No GC")
            {
                if (HuntingData.Jobs[GetGrandCompany()].Monsters.Any(monsters => monsters.Any(monster => monster.Name == entry.Mob))) 
                    name = GetGrandCompany();
            }

            var progress = Configuration.Progress.GetOrCreate(LocalContentID).GetOrCreate(name).GetOrCreate(entry.Mob);
            progress.Done = entry.Done;
            progress.Killed = entry.Killed;
            
            Configuration.Save();
        }
        
        // If square ever decides to change the Hunting Log~
        #region internal
        private void WriteMonsterNote()
        {
            var hunt = new HuntingData();
            
            var monster = Data.GetExcelSheet<MonsterNote>()!;
            var mapSheet = Data.GetExcelSheet<Map>();

            var tencounter = 0;
            var index = 0;
            foreach (var match in monster)
            {
                if (match.RowId == 0) continue;
                try
                {
                    var parts = match.Name.ToString().Split(" ");
                    var name = string.Join(" ", parts.Take(parts.Length-1));
                    if (name == "Order of the Twin Adder") name = "Twin Adder";
                    
                    if (!hunt.Jobs.ContainsKey(name))
                    {
                        hunt.Jobs.Add(name, new HuntingRank());
                        hunt.Jobs[name].Monsters.Add(new List<HuntingMonster>());
                    }
                    
                    foreach (var (target, count) in match.MonsterNoteTarget.Zip(match.Count))
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
                            }
                        }
                        
                        hunt.Jobs[name].Monsters[index].Add(newMonster);
                    }
                    
                    tencounter++;
                    if (tencounter % 50 == 0)
                    {
                        index = 0;
                    } 
                    else if (tencounter % 10 == 0)
                    {
                        index++;
                        hunt.Jobs[name].Monsters.Add(new List<HuntingMonster>());
                    }
                }
                catch (Exception e)
                {
                    PluginLog.Error(e.Message);
                }
            }
            
            var l = JsonConvert.SerializeObject(hunt, new JsonSerializerSettings { Formatting = Formatting.Indented,});
            
            var path = Path.Combine(PluginInterface.AssemblyLocation.Directory?.FullName!, "monsters.json");
            PluginLog.Information($"Writing monster json");
            PluginLog.Information(path);
            File.WriteAllText(path, l);
        }
        #endregion
    }
}
