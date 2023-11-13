﻿using System;
using System.IO.MemoryMappedFiles;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.Command;
using Dalamud.Interface.Windowing;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game.Control;

namespace Linkpearl;

public class Dalamud {
    public static void Initialize(DalamudPluginInterface pluginInterface) => pluginInterface.Create<Dalamud>();

    [PluginService, RequiredVersion("1.0")] public static IFramework Framework { get; private set; } = null!;
    [PluginService, RequiredVersion("1.0")] public static IClientState ClientState { get; private set; } = null!;
    [PluginService, RequiredVersion("1.0")] public static IChatGui ChatGui { get; private set; } = null!;
    [PluginService, RequiredVersion("1.0")] public static ICommandManager CommandManager { get; private set; } = null!;
    [PluginService, RequiredVersion("1.0")] public static ICondition Condition { get; private set; } = null!;
    [PluginService, RequiredVersion("1.0")] public static IPluginLog PluginLog { get; private set; } = null!;
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

    public Plugin(DalamudPluginInterface pluginInterface) {
        Dalamud.Initialize(pluginInterface);

        this.Config = pluginInterface.GetPluginConfig() as Config ?? new Config();
        this.Config.Initialize(pluginInterface);
        this.ConfigWindow = new ConfigWindow(this);
        this.LinuxMode = this.Config.LinuxMode;

        this.WindowSystem.AddWindow(this.ConfigWindow);
        pluginInterface.UiBuilder.Draw += this.DrawUI;
        pluginInterface.UiBuilder.OpenConfigUi += this.DrawConfigUI;

        if (this.LinuxMode) {
            Dalamud.Framework.Update += this.FrameworkUpdateLinux;
        } else {
            Dalamud.Framework.Update += this.FrameworkUpdateWindows;
        }

        // Dalamud.CommandManager.AddHandler(CommandName, new CommandInfo(this.CommandHandler) {
        //     HelpMessage = "Debug command for Linkpearl.",
        //     ShowInHelp = false
        // });

        Dalamud.ChatGui.Print("LinkPearl Reloaded");



        try {
            if (this.LinuxMode) {
                this._memoryMappedFile = MemoryMappedFile.CreateFromFile("/dev/shm/MumbleLink." + this.Config.LinuxUID, System.IO.FileMode.OpenOrCreate, null, Marshal.SizeOf<MumbleAvatarLinux>());
            } else {
                this._memoryMappedFile = MemoryMappedFile.CreateOrOpen("MumbleLink", Marshal.SizeOf<MumbleAvatarWindows>());
            }
            this._memoryMappedViewAccessor = this._memoryMappedFile.CreateViewAccessor();
        } catch (Exception e) {
            Dalamud.PluginLog.Error(e, "Failed to create memory mapped file");
            Dalamud.ChatGui.PrintError(
                "[Linkpearl] Failed to connect to Mumble. Make sure Mumble is open and re-enable the plugin!");
        }
    }

    private void DrawUI() { this.WindowSystem.Draw(); }
    private void DrawConfigUI() { this.ConfigWindow.IsOpen = true; }

    public void Dispose() {
        if (this.LinuxMode) {
            Dalamud.Framework.Update -= this.FrameworkUpdateLinux;
        } else {
            Dalamud.Framework.Update -= this.FrameworkUpdateWindows;
        }

        this.WindowSystem.RemoveAllWindows();
        this.ConfigWindow.Dispose();

        Dalamud.CommandManager.RemoveHandler(CommandName);

        this._memoryMappedViewAccessor?.Dispose();
        this._memoryMappedFile?.Dispose();
    }

    // private void CommandHandler(string cmd, string args) {
    //     var mumbleAvatar = this.BuildAvatarWindows();
    //     Dalamud.PluginLog.Debug($"Avatar exists: {mumbleAvatar != null}");
    //     if (mumbleAvatar == null) return;

    //     var avatarPos = mumbleAvatar.Value.AvatarPosition;
    //     Dalamud.PluginLog.Debug($"Avatar position: {avatarPos[0]}, {avatarPos[1]}, {avatarPos[2]}");
    //     var avatarFront = mumbleAvatar.Value.AvatarFront;
    //     Dalamud.PluginLog.Debug($"Avatar front: {avatarFront[0]}, {avatarFront[1]}, {avatarFront[2]}");
    //     var avatarTop = mumbleAvatar.Value.AvatarTop;
    //     Dalamud.PluginLog.Debug($"Avatar top: {avatarTop[0]}, {avatarTop[1]}, {avatarTop[2]}");

    //     var cameraPos = mumbleAvatar.Value.CameraPosition;
    //     Dalamud.PluginLog.Debug($"Camera position: {cameraPos[0]}, {cameraPos[1]}, {cameraPos[2]}");
    //     var cameraFront = mumbleAvatar.Value.CameraFront;
    //     Dalamud.PluginLog.Debug($"Camera front: {cameraFront[0]}, {cameraFront[1]}, {cameraFront[2]}");
    //     var cameraTop = mumbleAvatar.Value.CameraTop;
    //     Dalamud.PluginLog.Debug($"Camera top: {cameraTop[0]}, {cameraTop[1]}, {cameraTop[2]}");

    //     var context = mumbleAvatar.Value.Context;
    //     Dalamud.PluginLog.Debug($"Context: {Encoding.UTF8.GetString(context)}");
    // }

    private unsafe MumbleAvatarWindows? BuildAvatarWindows() {
        if (Dalamud.ClientState.LocalPlayer == null) return null;
        if (this._memoryMappedFile == null) return null;
        if (this._memoryMappedViewAccessor == null) return null;

        var avatarPos = Dalamud.ClientState.LocalPlayer.Position;
        var avatarRot = Dalamud.ClientState.LocalPlayer.Rotation; // -pi to pi radians
        var avatarFront = new Vector3((float)Math.Cos(avatarRot), 0, (float)Math.Sin(avatarRot));
        var avatarTop = new Vector3(0, 1, 0);

        var camera = CameraManager.Instance()->GetActiveCamera();
        if (camera == null) return null;

        var cameraPos = camera->CameraBase.SceneCamera.Object.Position;
        var cameraViewMatrix = camera->CameraBase.SceneCamera.ViewMatrix;
        var cameraFront = new Vector3(cameraViewMatrix.M13, cameraViewMatrix.M23, cameraViewMatrix.M33);
        var cameraTop = camera->CameraBase.SceneCamera.Vector_1;

        var contextId = Dalamud.ClientState.LocalPlayer.CurrentWorld.Id.ToString();
        var boundByDuty = Dalamud.Condition[ConditionFlag.BoundByDuty]
                          || Dalamud.Condition[ConditionFlag.BoundByDuty56]
                          || Dalamud.Condition[ConditionFlag.BoundByDuty95];

        if (boundByDuty) {
            contextId = "duty";
        }

        var context = contextId + "-" + Dalamud.ClientState.TerritoryType;
        var contextBytes = new byte[256];
        var contextBytesWritten = Encoding.UTF8.GetBytes(context, contextBytes);

        var cid = Dalamud.ClientState.LocalContentId.ToString("X8");
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

    private unsafe MumbleAvatarLinux? BuildAvatarLinux() {
        if (Dalamud.ClientState.LocalPlayer == null) return null;
        if (this._memoryMappedFile == null) return null;
        if (this._memoryMappedViewAccessor == null) return null;

        var avatarPos = Dalamud.ClientState.LocalPlayer.Position;
        var avatarRot = Dalamud.ClientState.LocalPlayer.Rotation; // -pi to pi radians
        var avatarFront = new Vector3((float)Math.Cos(avatarRot), 0, (float)Math.Sin(avatarRot));
        var avatarTop = new Vector3(0, 1, 0);

        var camera = CameraManager.Instance()->GetActiveCamera();
        if (camera == null) return null;

        var cameraPos = camera->CameraBase.SceneCamera.Object.Position;
        var cameraViewMatrix = camera->CameraBase.SceneCamera.ViewMatrix;
        var cameraFront = new Vector3(cameraViewMatrix.M13, cameraViewMatrix.M23, cameraViewMatrix.M33);
        var cameraTop = camera->CameraBase.SceneCamera.Vector_1;

        var contextId = Dalamud.ClientState.LocalPlayer.CurrentWorld.Id.ToString();
        var boundByDuty = Dalamud.Condition[ConditionFlag.BoundByDuty]
                          || Dalamud.Condition[ConditionFlag.BoundByDuty56]
                          || Dalamud.Condition[ConditionFlag.BoundByDuty95];

        if (boundByDuty) {
            contextId = "duty";
        }

        var context = contextId + "-" + Dalamud.ClientState.TerritoryType;
        var contextBytes = new byte[256];
        var contextBytesWritten = Encoding.UTF8.GetBytes(context, contextBytes);

        var cid = Dalamud.ClientState.LocalContentId.ToString("X8");
        var hash = SHA256.Create().ComputeHash(Encoding.UTF8.GetBytes(cid));
        var identity = BitConverter.ToString(hash).Replace("-", "").ToLower();
        var identityBytes = new byte[256 * 4];
        Encoding.UTF32.GetBytes(identity, identityBytes);

        var name = new byte[256 * 4];
        Encoding.UTF32.GetBytes("Linkpearl", name);
        var description = new byte[2048 * 4];
        Encoding.UTF32.GetBytes("An actually updated Mumble positional audio plugin", description);

        return new MumbleAvatarLinux {
            UIVersion = 2,
            UITick = this._tickCount,

            Name = name,
            Description = description,

            Identity = identityBytes,
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

    private void FrameworkUpdate<T>(Func<T> action) {
        if (this._memoryMappedViewAccessor == null) return;

        var mumbleAvatar = action();
        if (mumbleAvatar == null) return;

        var size = Marshal.SizeOf<MumbleAvatarWindows>();
        var buffer = new byte[size];

        var ptr = IntPtr.Zero;
        try {
            ptr = Marshal.AllocHGlobal(size);
            Marshal.StructureToPtr(mumbleAvatar, ptr, true);
            Marshal.Copy(ptr, buffer, 0, size);

            this._memoryMappedViewAccessor.WriteArray(0, buffer, 0, size);
        } finally {
            Marshal.FreeHGlobal(ptr);
        }

        this._tickCount += 1;
    }

    private void FrameworkUpdateWindows(IFramework onGodFr) {
        this.FrameworkUpdate(this.BuildAvatarWindows);
    }
    private void FrameworkUpdateLinux(IFramework onGodFr) {
        this.FrameworkUpdate(this.BuildAvatarLinux);
    }
}
