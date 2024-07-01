using System;
using Dalamud.Interface.Windowing;
using Dalamud.Utility;
using ImGuiNET;

namespace Linkpearl;

public class ConfigWindow(Plugin plugin) : Window("Linkpearl Config", ImGuiWindowFlags.AlwaysAutoResize) {
    private readonly string rateLimitWarning = string.Join(' ',
        "Mumble only reads positional data every",
        "20ms.  Sending data much faster than that is pointless.",
        " You may, however, find the CPU and memory overhead savings",
        "of slowing down the data rate to be helpful.",
        " Especially consider this if you have",
        "any crashing problems with this plugin.");

    public override void Draw() {
        if (Util.IsWine()) {
            ImGui.TextWrapped("What is the UID of the user Mumble is running under?");
            var linuxUid = plugin.Config.LinuxUid;
            if (ImGui.InputInt("User ID", ref linuxUid)) {
                plugin.Config.LinuxUid = linuxUid;
                plugin.Config.Save();
            }
        }

        ImGui.Separator();
        ImGui.TextWrapped(rateLimitWarning);

        var rate = plugin.Config.RateMs;
        ImGui.TextWrapped("Limit send rate to (in ms, higher is slower):");
        if (ImGui.DragInt("", ref rate, 1f, 15, 1000, null, ImGuiSliderFlags.AlwaysClamp)) {
            plugin.Config.RateMs = rate;
            plugin.Config.Save();
        }
    }
}
