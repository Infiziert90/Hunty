using Dalamud.Configuration;
using System;

namespace Hunty;

[Serializable]
public class Configuration : IPluginConfiguration
{
    public int Version { get; set; } = 0;
    public bool SkipDone = true;

    public void Save()
    {
        Plugin.PluginInterface.SavePluginConfig(this);
    }
}
