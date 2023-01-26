using System;
using System.Numerics;
using Dalamud.Interface.Windowing;
using ImGuiNET;

namespace Hunty.Windows;

public class ConfigWindow : Window, IDisposable
{
    private Configuration Configuration;

    public ConfigWindow(Plugin plugin) : base("Configuration", ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse)
    {
        Size = new Vector2(290, 90);
        SizeCondition = ImGuiCond.Always;

        Configuration = plugin.Configuration;
    }

    public void Dispose() { }

    public override void Draw()
    {
        
    }
}
