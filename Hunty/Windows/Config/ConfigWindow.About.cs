using System.Numerics;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using Hunty.Resources;
using ImGuiNET;

namespace Hunty.Windows.Config;

public partial class ConfigWindow
{
    private const float SeparatorPadding = 1.0f;
    private static float GetSeparatorPaddingHeight => SeparatorPadding * ImGuiHelpers.GlobalScale;

    private static void About()
    {
        using var tabItem = ImRaii.TabItem(Language.About);
        if (!tabItem.Success)
            return;

        var buttonHeight = ImGui.GetFrameHeightWithSpacing() + ImGui.GetStyle().WindowPadding.Y + GetSeparatorPaddingHeight;
        using (var contentChild = ImRaii.Child("AboutContent", new Vector2(0, -buttonHeight)))
        {
            if (contentChild)
            {
                ImGuiHelpers.ScaledDummy(5.0f);

                ImGui.TextUnformatted(Language.Author);
                ImGui.SameLine();
                ImGui.TextColored(ImGuiColors.ParsedGold, Plugin.PluginInterface.Manifest.Author);

                ImGui.TextUnformatted(Language.Discord);
                ImGui.SameLine();
                ImGui.TextColored(ImGuiColors.ParsedGold, "@infi");

                ImGui.TextUnformatted(Language.Version);
                ImGui.SameLine();
                ImGui.TextColored(ImGuiColors.ParsedOrange, Plugin.PluginInterface.Manifest.AssemblyVersion.ToString());
            }
        }

        ImGui.Separator();
        ImGuiHelpers.ScaledDummy(1.0f);

        using var bottomChild = ImRaii.Child("AboutBottomBar", new Vector2(0, 0), false, 0);
        if (bottomChild)
        {
            using (ImRaii.PushColor(ImGuiCol.Button, ImGuiColors.ParsedBlue))
            {
                if (ImGui.Button(Language.DiscordThread))
                    Dalamud.Utility.Util.OpenLink("https://discord.com/channels/581875019861328007/1073609754187931808");
            }

            ImGui.SameLine();

            using (ImRaii.PushColor(ImGuiCol.Button, ImGuiColors.DPSRed))
            {
                if (ImGui.Button(Language.GithubIssues))
                    Dalamud.Utility.Util.OpenLink("https://github.com/Infiziert90/Hunty/issues");
            }

            ImGui.SameLine();

            using (ImRaii.PushColor(ImGuiCol.Button, new Vector4(0.12549f, 0.74902f, 0.33333f, 0.6f)))
            {
                if (ImGui.Button(Language.KoFiTip))
                    Dalamud.Utility.Util.OpenLink("https://ko-fi.com/infiii");
            }
        }
    }
}
