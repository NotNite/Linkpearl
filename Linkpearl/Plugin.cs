using System;
using System.IO.MemoryMappedFiles;
using System.Threading.Tasks;
using Dalamud.Interface.Windowing;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;

namespace Linkpearl;

public class D {
    public static void Initialize(DalamudPluginInterface pluginInterface) => pluginInterface.Create<D>();

    [PluginService, RequiredVersion("1.0")] public static IFramework Framework { get; private set; } = null!;
    [PluginService, RequiredVersion("1.0")] public static IClientState ClientState { get; private set; } = null!;
    [PluginService, RequiredVersion("1.0")] public static IChatGui ChatGui { get; private set; } = null!;
    [PluginService, RequiredVersion("1.0")] public static ICommandManager CommandManager { get; private set; } = null!;
    [PluginService, RequiredVersion("1.0")] public static ICondition Condition { get; private set; } = null!;
    [PluginService, RequiredVersion("1.0")] public static IPluginLog Log { get; private set; } = null!;
}

public sealed class Plugin : IDalamudPlugin {
    public string Name => "Linkpearl";
    public const string CommandName = "/pearldebug";


    public WindowSystem WindowSystem = new("Linkpearl");
    public Config Config { get; init; }
    public ConfigWindow ConfigWindow { get; init; }

    private readonly MemoryMappedFile? _memoryMappedFile;
    private readonly MemoryMappedViewAccessor? _memoryMappedViewAccessor;
    private uint _tickCount;

    private DataOperation? dataOperation;

    public Plugin(DalamudPluginInterface pluginInterface) {
        D.Initialize(pluginInterface);

        this.Config = pluginInterface.GetPluginConfig() as Config ?? new Config();
        this.Config.Initialize(pluginInterface);
        this.ConfigWindow = new ConfigWindow(this);

        this.WindowSystem.AddWindow(this.ConfigWindow);
        pluginInterface.UiBuilder.Draw += this.DrawUI;
        pluginInterface.UiBuilder.OpenConfigUi += this.DrawConfigUI;

        //this.ConfigWindow.IsOpen = true;

        DataStart();
        // LP_Dalamud.ChatGui.Print("LinkPearl Reloaded");
    }

    public void DataStart(bool paused = false) {
        try {
            if (Config.LinuxMode) {
                dataOperation = new DataOperationLinux(Config.RateMS, Config.LinuxUID);
            } else {
                dataOperation = new DataOperationWindows(Config.RateMS);
            }
            if (!paused) D.Framework.Update += dataOperation.RateLimitedUpdate;
        } catch (Exception e) {
            D.Log.Error(e, "Unable to create link to Mumble");
            D.ChatGui.PrintError("[Linkpearl] Failed to create link to Mumble");
        }
    }

    public void DataTest() {
        DataStop();
        DataStart(paused: true);

        if (dataOperation is null) return;
        D.Framework.Update += dataOperation.RateLimitedUpdate;

        Task.Factory.StartNew(() => {
            System.Threading.Thread.Sleep(1000);
            D.Framework.Update -= dataOperation.RateLimitedUpdate;
        });
    }

    public void DataStop() {
        if (dataOperation is null) return;

        D.Framework.Update -= dataOperation.RateLimitedUpdate;
        dataOperation.Dispose();
        dataOperation = null;
    }

    private void DrawUI() { this.WindowSystem.Draw(); }
    private void DrawConfigUI() { this.ConfigWindow.IsOpen = true; }

    public void Dispose() {
        DataStop();

        this.WindowSystem.RemoveAllWindows();
        this.ConfigWindow.Dispose();

        // D.CommandManager.RemoveHandler(CommandName);

        this._memoryMappedViewAccessor?.Dispose();
        this._memoryMappedFile?.Dispose();
    }

    public void ShowInformationInLog() {
        if (dataOperation == null){
            D.Log.Debug($"Avatar does not exist");
            return;
        } 

        dynamic mumbleAvatar = new { };
        if (dataOperation.GetType() == typeof(DataOperationLinux)) {
            mumbleAvatar = ((DataOperationLinux)dataOperation).outputData;
        } else if (dataOperation?.GetType() == typeof(DataOperationWindows)) {
            mumbleAvatar = ((DataOperationWindows)dataOperation).outputData;
        }

        var avatarPos = mumbleAvatar.AvatarPosition;
        D.Log.Debug($"Avatar position: {avatarPos[0]}, {avatarPos[1]}, {avatarPos[2]}");
        var avatarFront = mumbleAvatar.AvatarFront;
        D.Log.Debug($"Avatar front: {avatarFront[0]}, {avatarFront[1]}, {avatarFront[2]}");
        var avatarTop = mumbleAvatar.AvatarTop;
        D.Log.Debug($"Avatar top: {avatarTop[0]}, {avatarTop[1]}, {avatarTop[2]}");

        var cameraPos = mumbleAvatar.CameraPosition;
        D.Log.Debug($"Camera position: {cameraPos[0]}, {cameraPos[1]}, {cameraPos[2]}");
        var cameraFront = mumbleAvatar.CameraFront;
        D.Log.Debug($"Camera front: {cameraFront[0]}, {cameraFront[1]}, {cameraFront[2]}");
        var cameraTop = mumbleAvatar.CameraTop;
        D.Log.Debug($"Camera top: {cameraTop[0]}, {cameraTop[1]}, {cameraTop[2]}");

        var context = mumbleAvatar.Context;
        D.Log.Debug($"Context: {System.Text.Encoding.UTF8.GetString(context)}");
    }

}
