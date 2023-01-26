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
using Dalamud.Game.Command;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using Dalamud.Interface.Windowing;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using Lumina.Excel.GeneratedSheets;
using Newtonsoft.Json;
using MapType = FFXIVClientStructs.FFXIV.Client.UI.Agent.MapType;

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

        public HuntingData HuntingData = null!;
        
        public Plugin(
            [RequiredVersion("1.0")] DalamudPluginInterface pluginInterface,
            [RequiredVersion("1.0")] Framework framework,
            [RequiredVersion("1.0")] GameGui gameGui,
            [RequiredVersion("1.0")] CommandManager commandManager)
        {
            PluginInterface = pluginInterface;
            Framework = framework;
            GameGui = gameGui;
            CommandManager = commandManager;
            
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
            // try
            // {
            //     PluginLog.Debug("Loading Monsters.");
            //     
            //     var path = Path.Combine(PluginInterface.AssemblyLocation.Directory?.FullName!, "monsters.json");
            //     var jsonString = File.ReadAllText(path);
            //     HuntingData = (HuntingData) JsonConvert.DeserializeObject(jsonString, new JsonSerializerSettings()
            //     {
            //         TypeNameAssemblyFormatHandling = TypeNameAssemblyFormatHandling.Simple,
            //         TypeNameHandling = TypeNameHandling.Objects
            //     });
            // }
            // catch (Exception e)
            // {
            //     PluginLog.Error("There was a problem building the HuntingData.");
            //     PluginLog.Error(e.Message);
            // }

            try
            {
                var Hunt = new HuntingData();
                
                PrintMonsterNote(Hunt);
                
                var l = JsonConvert.SerializeObject(Hunt, new JsonSerializerSettings()
                {
                    TypeNameAssemblyFormatHandling = TypeNameAssemblyFormatHandling.Simple,
                    TypeNameHandling = TypeNameHandling.All,
                    Formatting = Formatting.Indented,
                });
                
                PluginLog.Information(PluginInterface.AssemblyLocation.Directory?.FullName!, "monsters.json");
                var writePath = Path.Combine(PluginInterface.AssemblyLocation.Directory?.FullName!, "monsters.json");
                File.WriteAllText(writePath, l);
                
                var hunt = JsonConvert.DeserializeObject<HuntingData>(l, new JsonSerializerSettings()
                {
                    TypeNameAssemblyFormatHandling = TypeNameAssemblyFormatHandling.Simple,
                    TypeNameHandling = TypeNameHandling.All,
                });
            }
            catch (Exception e)
            {
                PluginLog.Error("There was a problem building the HuntingData.");
                PluginLog.Error(e.Message);
            }
            // var Hunt = new HuntingData();
            // PrintMonsterNote(Hunt);
            // var l = JsonConvert.SerializeObject(Hunt, new JsonSerializerSettings()
            // {
            //     TypeNameAssemblyFormatHandling = TypeNameAssemblyFormatHandling.Simple,
            //     TypeNameHandling = TypeNameHandling.Objects,
            //     Formatting = Formatting.Indented,
            // });
            //
            // PluginLog.Information(PluginInterface.AssemblyLocation.Directory?.FullName!, "monsters.json");
            // var writePath = Path.Combine(PluginInterface.AssemblyLocation.Directory?.FullName!, "monsters.json");
            // File.WriteAllText(writePath, l);
        }

        public void Dispose()
        {
            WindowSystem.RemoveAllWindows();
            CommandManager.RemoveHandler(CommandName);
        }
        
        private void OnCommand(string command, string args)
        {
            WindowSystem.GetWindow("Grimoire")!.IsOpen = true;
        }
        
        private void DrawUI()
        {
            WindowSystem.Draw();
        }

        private void DrawConfigUI()
        {
            WindowSystem.GetWindow("Configuration")!.IsOpen = true;
        }

        public unsafe void SetMapMarker(MapLinkPayload map)
        {
            var instance = AgentMap.Instance();
            if (instance != null)
            {
                instance->IsFlagMarkerSet = 0;
                AgentMap.Instance()->SetFlagMapMarker(map.Map.TerritoryType.Row, map.Map.RowId, map.RawX / 1000.0f, map.RawY / 1000.0f);
                instance->OpenMap(map.Map.RowId, map.Map.TerritoryType.Row, type: MapType.FlagMarker);
            }
        }
        
        private void PrintMonsterNote(HuntingData hunt)
        {
            var monster = Data.GetExcelSheet<MonsterNote>()!;
            var mapSheet = Data.GetExcelSheet<Map>();

            var tencounter = 0;
            var index = 0;
            foreach (var match in monster)
            {
                if (match.RowId == 0) continue;
                try
                {
                    var name = match.Name.ToString().Split(" ")[0];

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
                        
                        foreach (var placename in target.Value!.UnkData3)
                        {
                            var map = mapSheet.FirstOrDefault(row => row.PlaceName.Row == placename.PlaceNameZone);
                            if (map != null && map.TerritoryType.Row != 0)
                            {
                                newMonster.Locations.Add(new HuntingMonsterLocation(map.TerritoryType.Row, map.RowId));
                            }
                        }
                        
                        hunt.Classes[name].Monsters[index].Add(newMonster);
                    }
                    
                    tencounter++;
                    if (tencounter % 10 == 0)
                    {
                        index++;
                        hunt.Classes[name].Monsters.Add(new List<HuntingMonster>());
                    }
                    if (tencounter == 50)
                    {
                        tencounter = 0;
                        index = 0;
                    }
                }
                catch (Exception e)
                {
                    PluginLog.Error(e.Message);
                }
            }
        }  
        
        private void PrintTerris()
        {
            var mapSheet = Data.GetExcelSheet<TerritoryType>();
            var contentSheet = Data.GetExcelSheet<ContentFinderCondition>()!;
            foreach (var match in mapSheet)
            {
                if (match.Map.IsValueCreated && match.Map.Value!.PlaceName.Value!.Name != "")
                {
                    if (match.RowId == 0) continue;
                    PluginLog.Information("---------------");
                    PluginLog.Information(match.Map.Value!.PlaceName.Value!.Name);
                    PluginLog.Information($"TerriID: {match.RowId}");
                    PluginLog.Information($"MapID: {match.Map.Row}");

                    var content = contentSheet.FirstOrDefault(x => x.TerritoryType.Row == match.RowId);
                    if (content == null) continue;
                    if (Helper.ToTitleCaseExtended(content.Name, 0) == "") continue;
                    PluginLog.Information($"Duty: {Helper.ToTitleCaseExtended(content.Name, 0)}");
                }
            }
        }        
    }
}
