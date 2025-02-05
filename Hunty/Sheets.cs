using Lumina.Excel;
using Lumina.Excel.Sheets;

namespace Hunty;

public static class Sheets
{
    public static readonly ExcelSheet<Map> MapSheet;
    public static readonly ExcelSheet<BNpcName> BNpcName;
    public static readonly ExcelSheet<ClassJob> ClassJobSheet;
    public static readonly ExcelSheet<Aetheryte> AetheryteSheet;
    public static readonly ExcelSheet<GrandCompany> GrandCompanySheet;
    public static readonly SubrowExcelSheet<MapMarker> MapMarkerSheet;
    public static readonly ExcelSheet<TerritoryType> TerritoryTypeSheet;
    public static readonly ExcelSheet<ContentFinderCondition> ContentFinderConditionSheet;

    static Sheets()
    {
        MapSheet = Plugin.Data.GetExcelSheet<Map>()!;
        BNpcName = Plugin.Data.GetExcelSheet<BNpcName>()!;
        ClassJobSheet = Plugin.Data.GetExcelSheet<ClassJob>()!;
        AetheryteSheet = Plugin.Data.GetExcelSheet<Aetheryte>()!;
        GrandCompanySheet = Plugin.Data.GetExcelSheet<GrandCompany>()!;
        MapMarkerSheet = Plugin.Data.GetSubrowExcelSheet<MapMarker>()!;
        TerritoryTypeSheet = Plugin.Data.GetExcelSheet<TerritoryType>()!;
        ContentFinderConditionSheet = Plugin.Data.GetExcelSheet<ContentFinderCondition>()!;
    }
}
