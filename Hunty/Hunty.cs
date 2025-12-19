using Dalamud.IoC;
using Dalamud.Plugin;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Numerics;
using Dalamud.Game.ClientState.Objects.Enums;
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
using Hunty.Resources;
using Hunty.Windows.Config;
using Lumina.Excel.Sheets;
using Newtonsoft.Json;

namespace Hunty;

#pragma warning disable SeStringEvaluator
public sealed class Plugin : IDalamudPlugin
{
    [PluginService] public static IDalamudPluginInterface PluginInterface { get; set; } = null!;
    [PluginService] public static IDataManager Data { get; set; } = null!;
    [PluginService] public static IChatGui ChatGui { get; set; } = null!;
    [PluginService] public static IClientState ClientState { get; set; } = null!;
    [PluginService] public static IPlayerState PlayerState { get; set; } = null!;
    [PluginService] public static ICommandManager CommandManager { get; set; } = null!;
    [PluginService] public static IGameGui GameGui { get; set; } = null!;
    [PluginService] public static IFramework Framework { get; set; } = null!;
    [PluginService] public static IPluginLog Log { get; set; } = null!;
    [PluginService] public static ITextureProvider Texture { get; set; } = null!;
    [PluginService] public static ISeStringEvaluator Evaluator { get; set; } = null!;

    private const string CommandName = "/hunty";
    private const string CommandXL = "/huntyxl";
    private const string CommandCompanion = "/huntycompanion";

    public Configuration Configuration { get; init; }

    private WindowSystem WindowSystem = new("Hunty");
    private ConfigWindow ConfigWindow;
    private MainWindow MainWindow;
    private XLWindow XLWindow;
    private CompanionWindow CompanionWindow;

    public readonly HuntingData HuntingData = null!;
    private uint CurrentJobId;
    private ClassJob CurrentJobParent;

    public static TeleportConsumer TeleportConsumer = null!;

    public uint CurrentJob = 1;
    public string CurrentJobName = "";
    public uint CurrentGc = 1;
    public string CurrentGcName = "";

    public static readonly uint[] JobArray = [1, 2, 3, 4, 5, 6, 7, 26, 29];

    public Plugin()
    {
        Configuration = PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();

        LanguageChanged(PluginInterface.UiLanguage);

        TeleportConsumer = new TeleportConsumer();

        ConfigWindow = new ConfigWindow(this);
        MainWindow = new MainWindow(this);
        XLWindow = new XLWindow(this);
        CompanionWindow = new CompanionWindow(this);
        WindowSystem.AddWindow(ConfigWindow);
        WindowSystem.AddWindow(MainWindow);
        WindowSystem.AddWindow(XLWindow);
        WindowSystem.AddWindow(CompanionWindow);

        PluginInterface.UiBuilder.Draw += DrawUi;
        PluginInterface.UiBuilder.OpenMainUi += OpenMainWindow;
        PluginInterface.UiBuilder.OpenConfigUi += OpenConfigWindow;
        PluginInterface.LanguageChanged += LanguageChanged;

        CommandManager.AddHandler(CommandName, new CommandInfo(OnCommand)
        {
            HelpMessage = Language.HelpMessage
        });

        CommandManager.AddHandler(CommandXL, new CommandInfo(OnXLCommand)
        {
            HelpMessage = Language.HelpMessageXL
        });

        CommandManager.AddHandler(CommandCompanion, new CommandInfo(OnCompanionCommand)
        {
            HelpMessage = Language.HelpMessageCompanion
        });

        try
        {
            Log.Debug("Loading Monsters.");

            var path = Path.Combine(PluginInterface.AssemblyLocation.Directory!.FullName, "monsters.json");
            var jsonString = File.ReadAllText(path);
            HuntingData = JsonConvert.DeserializeObject<HuntingData>(jsonString)!;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "There was a problem building the HuntingData.");
        }

        Framework.Update += CheckJobChange;
        ClientState.Login += OnLogin;
    }

    public void Dispose()
    {
        Framework.Update -= CheckJobChange;
        PluginInterface.UiBuilder.Draw -= DrawUi;
        PluginInterface.UiBuilder.OpenMainUi -= OpenMainWindow;
        PluginInterface.UiBuilder.OpenConfigUi -= OpenConfigWindow;
        PluginInterface.LanguageChanged -= LanguageChanged;

        WindowSystem.RemoveAllWindows();
        ConfigWindow.Dispose();
        MainWindow.Dispose();
        XLWindow.Dispose();
        CompanionWindow.Dispose();

        CommandManager.RemoveHandler(CommandName);
        CommandManager.RemoveHandler(CommandXL);
        CommandManager.RemoveHandler(CommandCompanion);
    }

    private void LanguageChanged(string langCode)
    {
        Language.Culture = new CultureInfo(langCode);
    }

    private void OnLogin()
    {
        if (!PlayerState.IsLoaded)
            return;

        var currentJobRes = PlayerState.ClassJob;
        CurrentJobId = currentJobRes.RowId;
        CurrentJobParent = currentJobRes.Value.ClassJobParent.Value;

        var name = Utils.ToTitleCaseExtended(CurrentJobParent.Name);
        Log.Debug($"Logging in on: {name}");
        CurrentJob = CurrentJobParent.RowId;
        CurrentJobName = name;
        CurrentGc = GetGrandCompany();
        CurrentGcName = GetCurrentGcName();

        MainWindow.Defaults();
    }

    private void CheckJobChange(IFramework framework)
    {
        if (!PlayerState.IsLoaded)
            return;

        var currentJobRes = PlayerState.ClassJob;
        if (currentJobRes.RowId != CurrentJobId)
        {
            CurrentJobId = currentJobRes.RowId;

            var parentJob = currentJobRes.Value.ClassJobParent;
            if (parentJob.RowId != CurrentJobParent.RowId)
            {
                CurrentJobParent = parentJob.Value!;
                var name = Utils.ToTitleCaseExtended(CurrentJobParent.Name);
                Log.Debug($"Job switch: {name}");
                CurrentJob = parentJob.RowId;
                CurrentJobName = name;
                CurrentGc = GetGrandCompany();
                CurrentGcName = GetCurrentGcName();

                MainWindow.Defaults();
            }
        }
    }

    private void OnCommand(string command, string args) => MainWindow.Toggle();
    private void OnXLCommand(string command, string args) => XLWindow.Toggle();
    private void OnCompanionCommand(string command, string args) => CompanionWindow.Toggle();
    private void DrawUi() => WindowSystem.Draw();
    private void OpenMainWindow() => MainWindow.Toggle();
    private void OpenConfigWindow() => ConfigWindow.Toggle();

    public void SetMapMarker(MapLinkPayload map) => GameGui.OpenMapWithMapLink(map);
    public unsafe void OpenDutyFinder(uint key) => AgentContentsFinder.Instance()->OpenRegularDuty(key);
    public unsafe uint GetGrandCompany() => UIState.Instance()->PlayerState.GrandCompany + (uint) 10000;
    public string GetCurrentGcName() => Utils.ToTitleCaseExtended(Sheets.GrandCompanySheet.GetRow(GetGrandCompany() - 10000).Name);
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
                    currentProgress.Add(monster.Name, killed == monster.Count ? new MonsterProgress(true) : new MonsterProgress(killed));
                }
            }
        }

        return currentProgress;
    }

    // From: https://github.com/SheepGoMeh/HuntBuddy/blob/5a92e0e104839c30eaf398790dee32b793c3c53e/HuntBuddy/Location.cs#L520
    public static void TeleportToNearestAetheryte(HuntingMonsterLocation location)
    {
        var map = Sheets.MapSheet.GetRow(location.Map);
        var nearestAetheryteId = Sheets.MapMarkerSheet
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
                                       .MinBy(x => x.distance)?.rowId ?? 0;

        // Support the unique case of aetheryte not being in the same map
        var nearestAetheryte = location.Terri == 399
                                   ? map.TerritoryType.Value.Aetheryte.Value
                                   : Sheets.AetheryteSheet.FirstOrNull(x => x.IsAetheryte && x.Territory.RowId == location.Terri && x.RowId == nearestAetheryteId);

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
        var npcName = Sheets.BNpcName.GetRow(id);
        return Evaluator.EvaluateObjStr(ObjectKind.BattleNpc, npcName.RowId);
    }
}
