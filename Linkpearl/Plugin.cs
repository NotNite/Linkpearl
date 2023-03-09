using System;
using System.IO.MemoryMappedFiles;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using Dalamud.Game;
using Dalamud.Game.ClientState;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.Command;
using Dalamud.Game.Gui;
using Dalamud.IoC;
using Dalamud.Logging;
using Dalamud.Plugin;
using FFXIVClientStructs.FFXIV.Client.Game.Control;

namespace Linkpearl;

public sealed class Plugin : IDalamudPlugin {
    public string Name => "Linkpearl";
    public const string CommandName = "/pearldebug";

    [PluginService] public static Framework Framework { get; set; } = null!;
    [PluginService] public static ClientState ClientState { get; set; } = null!;
    [PluginService] public static ChatGui ChatGui { get; set; } = null!;
    [PluginService] public static CommandManager CommandManager { get; set; } = null!;
    [PluginService] public static Condition Condition { get; set; } = null!;

    private readonly MemoryMappedFile? _memoryMappedFile;
    private readonly MemoryMappedViewAccessor? _memoryMappedViewAccessor;
    private uint _tickCount;

    public Plugin() {
        Framework.Update += this.FrameworkUpdate;

        CommandManager.AddHandler(CommandName, new CommandInfo(this.CommandHandler) {
            HelpMessage = "Debug command for Linkpearl.",
            ShowInHelp = false
        });

        try {
            this._memoryMappedFile = MemoryMappedFile.CreateOrOpen("MumbleLink", Marshal.SizeOf<MumbleAvatar>());
            this._memoryMappedViewAccessor = this._memoryMappedFile.CreateViewAccessor();
        } catch (Exception e) {
            PluginLog.Error(e, "Failed to create memory mapped file");
            ChatGui.PrintError(
                "[Linkpearl] Failed to connect to Mumble. Make sure Mumble is open and re-enable the plugin!");
        }
    }

    public void Dispose() {
        Framework.Update -= this.FrameworkUpdate;
        CommandManager.RemoveHandler(CommandName);
        this._memoryMappedViewAccessor?.Dispose();
        this._memoryMappedFile?.Dispose();
    }


    private void CommandHandler(string cmd, string args) {
        var mumbleAvatar = this.BuildAvatar();
        PluginLog.Debug($"Avatar exists: {mumbleAvatar != null}");
        if (mumbleAvatar == null) return;

        var avatarPos = mumbleAvatar.Value.AvatarPosition;
        PluginLog.Debug($"Avatar position: {avatarPos[0]}, {avatarPos[1]}, {avatarPos[2]}");
        var avatarFront = mumbleAvatar.Value.AvatarFront;
        PluginLog.Debug($"Avatar front: {avatarFront[0]}, {avatarFront[1]}, {avatarFront[2]}");
        var avatarTop = mumbleAvatar.Value.AvatarTop;
        PluginLog.Debug($"Avatar top: {avatarTop[0]}, {avatarTop[1]}, {avatarTop[2]}");

        var cameraPos = mumbleAvatar.Value.CameraPosition;
        PluginLog.Debug($"Camera position: {cameraPos[0]}, {cameraPos[1]}, {cameraPos[2]}");
        var cameraFront = mumbleAvatar.Value.CameraFront;
        PluginLog.Debug($"Camera front: {cameraFront[0]}, {cameraFront[1]}, {cameraFront[2]}");
        var cameraTop = mumbleAvatar.Value.CameraTop;
        PluginLog.Debug($"Camera top: {cameraTop[0]}, {cameraTop[1]}, {cameraTop[2]}");

        var context = mumbleAvatar.Value.Context;
        PluginLog.Debug($"Context: {Encoding.UTF8.GetString(context)}");
    }

    private unsafe MumbleAvatar? BuildAvatar() {
        if (ClientState.LocalPlayer == null) return null;
        if (this._memoryMappedFile == null) return null;
        if (this._memoryMappedViewAccessor == null) return null;

        var avatarPos = ClientState.LocalPlayer.Position;
        var avatarRot = ClientState.LocalPlayer.Rotation; // -pi to pi radians
        var avatarFront = new Vector3((float)Math.Cos(avatarRot), 0, (float)Math.Sin(avatarRot));
        var avatarTop = new Vector3(0, 1, 0);

        var camera = CameraManager.Instance->GetActiveCamera();
        if (camera == null) return null;

        var cameraPos = camera->CameraBase.SceneCamera.Object.Position;
        var cameraViewMatrix = camera->CameraBase.SceneCamera.ViewMatrix;
        var cameraFront = new Vector3(cameraViewMatrix.M13, cameraViewMatrix.M23, cameraViewMatrix.M33);
        var cameraTop = camera->CameraBase.SceneCamera.Vector_1;

        var contextId = ClientState.LocalPlayer.CurrentWorld.Id.ToString();
        var boundByDuty = Condition[ConditionFlag.BoundByDuty]
                          || Condition[ConditionFlag.BoundByDuty56]
                          || Condition[ConditionFlag.BoundByDuty95];

        if (boundByDuty) {
            contextId = "duty";
        }

        var context = contextId + "-" + ClientState.TerritoryType;
        var contextBytes = new byte[256];
        var contextBytesWritten = Encoding.UTF8.GetBytes(context, contextBytes);

        var cid = ClientState.LocalContentId.ToString("X8");
        var hash = SHA256.Create().ComputeHash(Encoding.UTF8.GetBytes(cid));
        var identity = BitConverter.ToString(hash).Replace("-", "").ToLower();

        return new MumbleAvatar {
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

    private void FrameworkUpdate(Framework onGodFr) {
        if (this._memoryMappedViewAccessor == null) return;

        var mumbleAvatar = this.BuildAvatar();
        if (mumbleAvatar == null) return;

        var size = Marshal.SizeOf<MumbleAvatar>();
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
}
