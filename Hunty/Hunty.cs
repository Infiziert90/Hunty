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
using Dalamud.Game.Text.SeStringHandling.Payloads;
using Dalamud.Interface.Windowing;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
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
        public WindowSystem WindowSystem = new("Hunty");
        public ClientState ClientState = null!;

        public HuntingData HuntingData = null!;
        
        
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
        
            WindowSystem.AddWindow(new MainWindow(this));
            WindowSystem.AddWindow(new ConfigWindow(this));
            
            CommandManager.AddHandler(CommandName, new CommandInfo(OnCommand)
            {
                HelpMessage = "Opens a small guide book"
            });
            
            PluginInterface.UiBuilder.Draw += DrawUI;
            PluginInterface.UiBuilder.OpenConfigUi += DrawConfigUI;
            
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
        }

        public void Dispose()
        {
            TexturesCache.Instance?.Dispose();
            
            WindowSystem.RemoveAllWindows();
            CommandManager.RemoveHandler(CommandName);
        }
        
        private void OnCommand(string command, string args) => WindowSystem.GetWindow("Hunty")!.IsOpen = true;
        private void DrawUI() => WindowSystem.Draw();
        private void DrawConfigUI() => WindowSystem.GetWindow("Configuration")!.IsOpen = true;
        
        public void SetMapMarker(MapLinkPayload map) => GameGui.OpenMapWithMapLink(map);
        
        public string GetLocalPlayerJob()
        {
            var local = ClientState.LocalPlayer;
            if (local == null || local.ClassJob.GameData == null) 
                return "";
            return Helper.ToTitleCaseExtended(local.ClassJob.GameData.ClassJobParent.Value!.Name, 0);
        }

        private static List<string> GrandCompanies = new() {"No GC", "Maelstrom", "Twin Adder", "Immortal Flames"};
        
        public unsafe string GetGrandCompany()
        {
            return GrandCompanies[UIState.Instance()->PlayerState.GrandCompany];
        }
        
        // If square ever decides to change the Hunting Log~
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
                    
                    if (!hunt.Classes.ContainsKey(name))
                    {
                        hunt.Classes.Add(name, new HuntingRank());
                        hunt.Classes[name].Monsters.Add(new List<HuntingMonster>());
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
                        
                        hunt.Classes[name].Monsters[index].Add(newMonster);
                    }
                    
                    tencounter++;
                    if (tencounter % 50 == 0)
                    {
                        index = 0;
                    } 
                    else if (tencounter % 10 == 0)
                    {
                        index++;
                        hunt.Classes[name].Monsters.Add(new List<HuntingMonster>());
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
    }
}
