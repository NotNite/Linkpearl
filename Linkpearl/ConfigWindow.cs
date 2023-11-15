using System;
using System.Numerics;
using Dalamud.Interface.Internal.Windows.Settings.Widgets;
using Dalamud.Interface.Windowing;
using ImGuiNET;

namespace Linkpearl;
public class ConfigWindow : Window, IDisposable {

  private Config Config;

  private Action testRun;
  private Action applyChanges;

  private readonly string linuxWarning = String.Join(' ',
      "If your Mumble client is running on Linux some",
      "of the communication methods are different.  Please",
      "set this appropriately (leave unchecked if you are",
      "running Mumble in Windows).");
  private readonly string rateLimitWarning = String.Join(' ',
      "Mumble only reads positional data every",
      "20ms.  Sending data much faster than that is pointless.",
      " You may, however, find the CPU and memory overhead savings",
      "of slowing down the data rate to be helpful.",
      " Especially consider this if you have",
      "any crashing problems with this plugin.");

  public ConfigWindow(Plugin plugin) : base(
    "Linkpearl Config",
    ImGuiWindowFlags.None // | ImGuiWindowFlags.AlwaysAutoResize
    ) {
    Size = new Vector2(475, 350);
    SizeCondition = ImGuiCond.Once;

    Config = plugin.Config;
    testRun = plugin.DataTest;
    applyChanges = () => {
      plugin.DataStop();
      plugin.DataStart();
    };
  }

  public void Dispose() { }

  public override void Draw() {
    var linuxMode = Config.LinuxMode;
    ImGui.TextWrapped(linuxWarning);
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
    ImGui.Separator();
    ImGui.TextWrapped(rateLimitWarning);

    var rate = Config.RateMS;
    ImGui.TextWrapped("Limit send rate to (in ms, higher is slower):");
    if (ImGui.DragInt("", ref rate, 1f, 15, 1000, null, ImGuiSliderFlags.AlwaysClamp)) {
      Config.RateMS = rate;
      Config.Save();
    }

    if (ImGui.Button("Apply Changes")) applyChanges();
    ImGui.SameLine();
    if (ImGui.Button("Test")) testRun();
  }
}