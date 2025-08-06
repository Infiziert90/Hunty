using System.Numerics;
using Dalamud.Interface;
using Dalamud.Interface.Components;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Bindings.ImGui;

namespace Hunty.Windows;

public static class Helper
{
    public static readonly Vector2 IconSize = new(40, 40);
    public static readonly Vector4 RedColor = new(0.980f, 0.245f, 0.245f, 1.0f);

    /// <summary>
    /// An unformatted version for ImGui.TextColored
    /// </summary>
    /// <param name="color">color to be used</param>
    /// <param name="text">text to display</param>
    public static void TextColored(Vector4 color, string text)
    {
        using (ImRaii.PushColor(ImGuiCol.Text, color))
            ImGui.TextUnformatted(text);
    }

    /// <summary>
    /// An unformatted version for ImGui.SetTooltip
    /// </summary>
    /// <param name="tooltip">tooltip to display</param>
    public static void Tooltip(string tooltip)
    {
        using (ImRaii.Tooltip())
        using (ImRaii.TextWrapPos(ImGui.GetFontSize() * 35.0f))
            ImGui.TextUnformatted(tooltip);
    }

    public static bool DrawArrows(ref int selected, int length, int id = 0)
    {
        var changed = false;

        // Prevents changing values from triggering EndDisable
        var isMin = selected == 0;
        var isMax = selected + 1 == length;

        ImGui.SameLine();
        using (ImRaii.Disabled(isMin))
        {
            if (ImGuiComponents.IconButton(id, FontAwesomeIcon.ArrowLeft))
            {
                selected--;
                changed = true;
            }
        }

        ImGui.SameLine();
        using (ImRaii.Disabled(isMax))
        {
            if (ImGuiComponents.IconButton(id + 1, FontAwesomeIcon.ArrowRight))
            {
                selected++;
                changed = true;
            }
        }

        return changed;
    }

    public static void DrawIcon(uint iconId, float scale = 1)
    {
        var iconSize = IconSize * ImGuiHelpers.GlobalScale * scale;
        var texture = Plugin.Texture.GetFromGameIcon(iconId).GetWrapOrEmpty();
        ImGui.Image(texture.Handle, iconSize);
    }

    public static void DrawProgressSymbol(bool done)
    {
        ImGui.SameLine();
        using (ImRaii.PushFont(UiBuilder.IconFont))
            ImGui.TextUnformatted((done ? FontAwesomeIcon.Check : FontAwesomeIcon.Times).ToIconString());
    }
}
