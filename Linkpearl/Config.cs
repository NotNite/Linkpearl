using System;
using Dalamud.Configuration;

namespace Linkpearl;

[Serializable]
public class Config : IPluginConfiguration {
    public int Version { get; set; } = 0;

    public int LinuxUid { get; set; } = 1000;
    public int RateMs { get; set; } = 55;

    public void Save() => Services.DalamudPluginInterface.SavePluginConfig(this);
}
