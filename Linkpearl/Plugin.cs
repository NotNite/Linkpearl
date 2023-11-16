using System;
using System.IO.MemoryMappedFiles;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.Command;
using Dalamud.Interface.Windowing;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game.Control;

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
    private bool LinuxMode { get; init; }

    private DataOperationLinux? dataOperation;

    public Plugin(DalamudPluginInterface pluginInterface) {
        D.Initialize(pluginInterface);

        this.Config = pluginInterface.GetPluginConfig() as Config ?? new Config();
        this.Config.Initialize(pluginInterface);
        this.ConfigWindow = new ConfigWindow(this);
        this.LinuxMode = this.Config.LinuxMode;

        this.WindowSystem.AddWindow(this.ConfigWindow);
        pluginInterface.UiBuilder.Draw += this.DrawUI;
        pluginInterface.UiBuilder.OpenConfigUi += this.DrawConfigUI;

        this.ConfigWindow.IsOpen = true;

        /*
        if (this.LinuxMode) {
            LP_Dalamud.Framework.Update += this.FrameworkUpdateLinux;
        } else {
            LP_Dalamud.Framework.Update += this.FrameworkUpdateWindows;
        }

        LP_Dalamud.CommandManager.AddHandler(CommandName, new CommandInfo(this.CommandHandler) {
            HelpMessage = "Debug command for Linkpearl.",
            ShowInHelp = false
        });

        LP_Dalamud.ChatGui.Print("LinkPearl Reloaded");

        try {
            if (this.LinuxMode) {
                this._memoryMappedFile = MemoryMappedFile.CreateFromFile("/dev/shm/MumbleLink." + this.Config.LinuxUID, System.IO.FileMode.OpenOrCreate, null, Marshal.SizeOf<MumbleAvatarLinux>());
            } else {
                this._memoryMappedFile = MemoryMappedFile.CreateOrOpen("MumbleLink", Marshal.SizeOf<MumbleAvatarWindows>());
            }
            this._memoryMappedViewAccessor = this._memoryMappedFile.CreateViewAccessor();
        } catch (Exception e) {
            LP_Dalamud.PluginLog.Error(e, "Failed to create memory mapped file");
            LP_Dalamud.ChatGui.PrintError(
                "[Linkpearl] Failed to connect to Mumble. Make sure Mumble is open and re-enable the plugin!");
        }
        */
    }

    public void DataStart(bool paused = false) {
        try {
            dataOperation = new DataOperationLinux(Config.RateMS, Config.LinuxUID);
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
        var mumbleAvatar = dataOperation?.outputData;
        D.Log.Debug($"Avatar exists: {mumbleAvatar != null}");
        if (mumbleAvatar == null) return;

        var avatarPos = mumbleAvatar.Value.AvatarPosition;
        D.Log.Debug($"Avatar position: {avatarPos[0]}, {avatarPos[1]}, {avatarPos[2]}");
        var avatarFront = mumbleAvatar.Value.AvatarFront;
        D.Log.Debug($"Avatar front: {avatarFront[0]}, {avatarFront[1]}, {avatarFront[2]}");
        var avatarTop = mumbleAvatar.Value.AvatarTop;
        D.Log.Debug($"Avatar top: {avatarTop[0]}, {avatarTop[1]}, {avatarTop[2]}");

        var cameraPos = mumbleAvatar.Value.CameraPosition;
        D.Log.Debug($"Camera position: {cameraPos[0]}, {cameraPos[1]}, {cameraPos[2]}");
        var cameraFront = mumbleAvatar.Value.CameraFront;
        D.Log.Debug($"Camera front: {cameraFront[0]}, {cameraFront[1]}, {cameraFront[2]}");
        var cameraTop = mumbleAvatar.Value.CameraTop;
        D.Log.Debug($"Camera top: {cameraTop[0]}, {cameraTop[1]}, {cameraTop[2]}");

        var context = mumbleAvatar.Value.Context;
        D.Log.Debug($"Context: {Encoding.UTF8.GetString(context)}");
    }

    private unsafe MumbleAvatarWindows? BuildAvatarWindows() {
        if (D.ClientState.LocalPlayer == null) return null;
        if (this._memoryMappedFile == null) return null;
        if (this._memoryMappedViewAccessor == null) return null;

        var avatarPos = D.ClientState.LocalPlayer.Position;
        var avatarRot = D.ClientState.LocalPlayer.Rotation; // -pi to pi radians
        var avatarFront = new Vector3((float)Math.Cos(avatarRot), 0, (float)Math.Sin(avatarRot));
        var avatarTop = new Vector3(0, 1, 0);

        var camera = CameraManager.Instance()->GetActiveCamera();
        if (camera == null) return null;

        var cameraPos = camera->CameraBase.SceneCamera.Object.Position;
        var cameraViewMatrix = camera->CameraBase.SceneCamera.ViewMatrix;
        var cameraFront = new Vector3(cameraViewMatrix.M13, cameraViewMatrix.M23, cameraViewMatrix.M33);
        var cameraTop = camera->CameraBase.SceneCamera.Vector_1;

        var contextId = D.ClientState.LocalPlayer.CurrentWorld.Id.ToString();
        var boundByDuty = D.Condition[ConditionFlag.BoundByDuty]
                          || D.Condition[ConditionFlag.BoundByDuty56]
                          || D.Condition[ConditionFlag.BoundByDuty95];

        if (boundByDuty) {
            contextId = "duty";
        }

        var context = contextId + "-" + D.ClientState.TerritoryType;
        var contextBytes = new byte[256];
        var contextBytesWritten = Encoding.UTF8.GetBytes(context, contextBytes);

        var cid = D.ClientState.LocalContentId.ToString("X8");
        var hash = SHA256.Create().ComputeHash(Encoding.UTF8.GetBytes(cid));
        var identity = BitConverter.ToString(hash).Replace("-", "").ToLower();

        return new MumbleAvatarWindows {
            UIVersion = 2,
            UITick = this._tickCount,

            Name = "Linkpearl",
            Description = "An actually updated Mumble positional audio plugin",

            Identity = identity,
            Context = contextBytes,
            ContextLength = (uint)contextBytesWritten,

            AvatarPosition = new[] {
                avatarPos.X,
                avatarPos.Y,
                avatarPos.Z
            },

            AvatarFront = new[] {
                avatarFront.X,
                avatarFront.Y,
                avatarFront.Z
            },

            AvatarTop = new[] {
                avatarTop.X,
                avatarTop.Y,
                avatarTop.Z
            },

            CameraPosition = new[] {
                cameraPos.X,
                cameraPos.Y,
                cameraPos.Z
            },

            CameraFront = new[] {
                cameraFront.X,
                cameraFront.Y,
                cameraFront.Z
            },

            CameraTop = new[] {
                cameraTop.X,
                cameraTop.Y,
                cameraTop.Z
            }
        };
    }
}
