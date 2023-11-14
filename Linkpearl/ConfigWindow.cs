using System;
using System.Numerics;
using Dalamud.Interface.Internal.Windows.Settings.Widgets;
using Dalamud.Interface.Windowing;
using ImGuiNET;

namespace Linkpearl;
public class ConfigWindow : Window, IDisposable {

  private Config Config;

  private Action testRun;

  public ConfigWindow(Plugin plugin) : base(
    "Linkpearl Config",
    ImGuiWindowFlags.NoCollapse) {
    Size = new Vector2(380, 220);
    SizeCondition = ImGuiCond.Once;

    Config = plugin.Config;
    testRun = plugin.Start;
  }

  public void Dispose() { }

  public override void Draw() {
    var linuxMode = Config.LinuxMode;
    ImGui.TextWrapped("If your Mumble client is running on Linux some of the data structures are different.  Please set this appropriately (leave unchecked if you are running Mumble in Windows)");
    if (ImGui.Checkbox("Linux Mode", ref linuxMode)) {
      Config.LinuxMode = linuxMode;
      Config.Save();
    }
    if (linuxMode) {
      ImGui.TextWrapped("What is the UID of the user Mumble is running under?");
      var linuxUID = Config.LinuxUID;
      if (ImGui.InputInt("User ID", ref linuxUID)) {
        Config.LinuxUID = linuxUID;
        Config.Save();
      }
    }
    ImGui.TextWrapped("After making a configuration change please disable and re-enable this plugin in order to retry the connection");

    var rate = Config.RateMS;
    if (ImGui.DragInt("Send Rate (ms)", ref rate, 1f, 15, 1000)) {
      Config.RateMS = rate > 1000 ? 1000 : rate < 15 ? 15 : rate;
      Config.Save();
    }

    if (ImGui.Button("Test")) {
      testRun();
    }
  }
}