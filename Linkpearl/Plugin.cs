using System;
using System.Numerics;
using System.Security.Cryptography;
using System.Text;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using Dalamud.Utility;
using FFXIVClientStructs.FFXIV.Client.Game.Control;

namespace Linkpearl;

public sealed class Plugin : IDalamudPlugin {
    public const string MumbleName = "Linkpearl";
    public const string MumbleDescription = "An actually updated Mumble positional audio plugin";
    public const int MumbleVersion = 2;
    public const int RateMs = 50;

    public Config Config;

    private uint tickCount;
    private IMumbleConnection? connection;
    private DateTime lastSend = DateTime.MinValue;

    public Plugin(IDalamudPluginInterface pluginInterface) {
        pluginInterface.Create<Services>();

        this.Config = Services.DalamudPluginInterface.GetPluginConfig() as Config ?? new Config();

        Services.Framework.Update += this.Update;
        Services.ClientState.Login += this.Start;
        Services.ClientState.Logout += this.Stop;
        if (Services.ClientState.IsLoggedIn) this.Start();
    }

    private unsafe void Update(IFramework framework) {
        if (this.connection == null || Services.ClientState.LocalPlayer == null) return;
        if (framework.LastUpdate > this.lastSend.AddMilliseconds(RateMs)) {
            this.lastSend = framework.LastUpdate;
            this.tickCount++;

            var manager = CameraManager.Instance();
            if (manager == null) return;
            var camera = manager->GetActiveCamera();
            if (camera == null) return;
            var cameraPos = camera->CameraBase.SceneCamera.Object.Position;
            var cameraViewMatrix = camera->CameraBase.SceneCamera.ViewMatrix;
            var cameraTop = camera->CameraBase.SceneCamera.Vector_1;

            var boundByDuty = Services.Condition[ConditionFlag.BoundByDuty]
                              || Services.Condition[ConditionFlag.BoundByDuty56]
                              || Services.Condition[ConditionFlag.BoundByDuty95];
            var contextId = boundByDuty ? "duty" : Services.ClientState.LocalPlayer.CurrentWorld.RowId.ToString();
            var context = contextId + "-" + Services.ClientState.TerritoryType;

            var avatar = new MumbleAvatar {
                UiTick = this.tickCount,
                Context = context,

                AvatarPosition = Services.ClientState.LocalPlayer.Position,
                AvatarFront = Vector3.Zero,
                AvatarTop = new Vector3(0, 1, 0),

                CameraPosition = cameraPos,
                CameraFront = new Vector3(cameraViewMatrix.M13, cameraViewMatrix.M23, cameraViewMatrix.M33),
                CameraTop = cameraTop
            };

            // -pi to pi radians
            var avatarRot = Services.ClientState.LocalPlayer.Rotation;
            avatar.AvatarFront.X = (float) Math.Cos(avatarRot);
            avatar.AvatarFront.Z = (float) Math.Sin(avatarRot);

            this.connection.Update(avatar);
        }
    }

    public void Start() {
        var cid = Services.ClientState.LocalContentId.ToString("X8");
        var hash = SHA256.Create().ComputeHash(Encoding.UTF8.GetBytes(cid));
        var identity = BitConverter.ToString(hash).Replace("-", "").ToLower();

        this.connection = Util.IsWine()
                              ? new LinuxMumbleConnection(identity, this.Config.LinuxUid)
                              : new WindowsMumbleConnection(identity);
    }

    public void Stop(int type, int code) {
        this.connection?.Dispose();
        this.connection = null;
    }

    public void Dispose() {
        Services.Framework.Update -= this.Update;
        Services.ClientState.Login -= this.Start;
        Services.ClientState.Logout -= this.Stop;

        this.Config.Save();
        this.connection?.Dispose();
    }
}
