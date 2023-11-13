using System;
using System.Numerics;
using Dalamud.Interface.Internal.Windows.Settings.Widgets;
using Dalamud.Interface.Windowing;
using ImGuiNET;

namespace Linkpearl;
public class ConfigWindow : Window, IDisposable {

  private Config Config;

  public ConfigWindow(Plugin plugin) : base(
    "Linkpearl Config", 
    ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse
  ) {
    this.Size = new Vector2(380, 220);
    this.SizeCondition = ImGuiCond.Always;

    this.Config = plugin.Config;
  }

  public void Dispose() { }

  public override void Draw() {
    var linuxMode = this.Config.LinuxMode;
    ImGui.TextWrapped("If your Mumble client is running on Linux some of the data structures are different.  Please set this appropriately (leave unchecked if you are running Mumble in Windows)");
    if (ImGui.Checkbox("Linux Mode", ref linuxMode)) {
      this.Config.LinuxMode = linuxMode;
      this.Config.Save();
    }
    if (linuxMode) {
      ImGui.TextWrapped("What is the UID of the user Mumble is running under?");
      var linuxUID = this.Config.LinuxUID;
      if (ImGui.InputInt("User ID", ref linuxUID)) {
        this.Config.LinuxUID = linuxUID;
      }
    }
    ImGui.TextWrapped("After making a configuration change please disable and re-enable this plugin in order to retry the connection");

  }
}