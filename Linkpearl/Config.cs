using System;
using Dalamud.Configuration;
using Dalamud.Plugin;

namespace Linkpearl {
  [Serializable]
  public class Config : IPluginConfiguration {
    public int Version { get; set; } = 0;

    public bool LinuxMode { get; set; } = false;
    public int LinuxUID { get; set; } = 1000;
    public int RateMS { get; set; } = 55;

    // the below exist just to make saving less cumbersome
    [NonSerialized]
    private DalamudPluginInterface? PluginInterface;
    public void Initialize(DalamudPluginInterface pluginInterface) => this.PluginInterface = pluginInterface;
    public void Save() => this.PluginInterface!.SavePluginConfig(this);
  }
}