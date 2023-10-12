using System.Numerics;
using Dalamud.Interface;
using Dalamud.Interface.Utility;
using ImGuiNET;

namespace Hunty.Windows;

public static class Helper
{
    public static void DrawArrows(ref int selected, int length, int id)
    {
        ImGui.SameLine();
        if (selected == 0) ImGui.BeginDisabled();
        if (Dalamud.Interface.Components.ImGuiComponents.IconButton(id, FontAwesomeIcon.ArrowLeft)) selected--;
        if (selected == 0) ImGui.EndDisabled();

        ImGui.SameLine();
        if (selected + 1 == length) ImGui.BeginDisabled();
        if (Dalamud.Interface.Components.ImGuiComponents.IconButton(id+1, FontAwesomeIcon.ArrowRight)) selected++;
        if (selected + 1 == length) ImGui.EndDisabled();
    }

    public static void DrawIcon(uint iconId, Vector2 size, float scale = 1)
    {
        var iconSize = size * ImGuiHelpers.GlobalScale * scale;
        var texture = Plugin.Texture.GetIcon(iconId);
        if (texture == null)
        {
            ImGui.Text($"Unknown icon {iconId}");
            return;
        }

        ImGui.Image(texture.ImGuiHandle, iconSize);
    }

    public static void DrawProgressSymbol(bool done)
    {
        ImGui.SameLine();
        ImGui.PushFont(UiBuilder.IconFont);
        ImGui.TextUnformatted(done
            ? FontAwesomeIcon.Check.ToIconString()
            : FontAwesomeIcon.Times.ToIconString());
        ImGui.PopFont();
    }
}