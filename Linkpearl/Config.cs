using System;
using Dalamud.Configuration;

namespace Linkpearl;

[Serializable]
public class Config : IPluginConfiguration {
    public int Version { get; set; } = 0;
    public int LinuxUid { get; set; } = 1000;
    public void Save() => Services.DalamudPluginInterface.SavePluginConfig(this);
}
