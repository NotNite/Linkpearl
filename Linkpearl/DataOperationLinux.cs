using System;
using System.IO.MemoryMappedFiles;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game.Control;
using FFXIVClientStructs.FFXIV.Common.Math;
using Linkpearl;

public abstract class DataOperation {

    private uint clockTick = 0;
    protected uint updateTick = 0;
    private TimeSpan lastRun = TimeSpan.Zero;
    private TimeSpan rate;

    public DataOperation(int rateMS) {
        rate = new TimeSpan(0, 0, 0, 0, rateMS);
    }

    protected bool accumulateDeltaTime(TimeSpan delta) {
        lastRun = lastRun.Add(delta);
        if (lastRun.CompareTo(rate) > -1) {
            D.Log.Debug($"{++updateTick}/{++clockTick} - {delta.ToString()} - {this.lastRun} - work");
            lastRun = TimeSpan.Zero;
            return true;
        }
        D.Log.Debug($"{updateTick}/{++clockTick} - {delta.ToString()} - {this.lastRun} - skip");
        return false;
    }

    public void RateLimitedUpdate(IFramework framework) {
        if (!accumulateDeltaTime(framework.UpdateDelta)) return;
        performUpdate();
    }

    protected abstract void performUpdate();

}

public class DataOperationLinux : DataOperation {

    private readonly MemoryMappedFile? memoryMappedFile;
    private readonly MemoryMappedViewAccessor? memoryMappedViewAccessor;

    public MumbleAvatarLinux outputData;

    private int fileWriteBufferSize = Marshal.SizeOf<MumbleAvatarLinux>();
    private byte[] fileWriteBuffer;
    private nint writeStructPtr = IntPtr.Zero;

    public DataOperationLinux(int rateMS, int mumbleUserID) : base(rateMS) {
        var cid = D.ClientState.LocalContentId.ToString("X8");
        var hash = SHA256.Create().ComputeHash(Encoding.UTF8.GetBytes(cid));
        var identity = BitConverter.ToString(hash).Replace("-", "").ToLower();
        var identityBytes = new byte[256 * 4];
        Encoding.UTF32.GetBytes(identity, identityBytes);

        var name = new byte[256 * 4];
        Encoding.UTF32.GetBytes("Linkpearl", name);
        var description = new byte[2048 * 4];
        Encoding.UTF32.GetBytes("An actually updated Mumble positional audio plugin", description);

        outputData = new MumbleAvatarLinux {
            UIVersion = 2,
            Name = name,
            Description = description,
            Identity = identityBytes,

            AvatarTop = new[] { 0f, 1f, 0f },
            AvatarFront = new[] { 0f, 0f, 0f },
            AvatarPosition = new[] { 0f, 0f, 0f },

            CameraTop = new[] { 0f, 0f, 0f },
            CameraFront = new[] { 0f, 0f, 0f },
            CameraPosition = new[] { 0f, 0f, 0f },

            Context = new byte[256]
        };


        fileWriteBuffer = new byte[fileWriteBufferSize];
        try {
            writeStructPtr = Marshal.AllocHGlobal(fileWriteBufferSize);
        } catch (OutOfMemoryException) {
            writeStructPtr = IntPtr.Zero;
        }

        memoryMappedFile = MemoryMappedFile.CreateFromFile(
          "/dev/shm/MumbleLink." + mumbleUserID,
          System.IO.FileMode.OpenOrCreate, null, fileWriteBufferSize,
          MemoryMappedFileAccess.ReadWrite);
        memoryMappedViewAccessor = memoryMappedFile.CreateViewAccessor();

    }

    public void Dispose() {
        Marshal.FreeHGlobal(writeStructPtr);
        memoryMappedViewAccessor?.Dispose();
        memoryMappedFile?.Dispose();
    }

    protected override void performUpdate() {
        D.Log.Debug($"{updateTick} : work work");

        if (memoryMappedFile == null
         || memoryMappedViewAccessor == null
         || fileWriteBuffer == null
         || writeStructPtr == IntPtr.Zero
         || !refreshOuputData()) {
            // D.Log.Debug($"memoryMappedFile: {memoryMappedFile}");
            // D.Log.Debug($"memoryMappedViewAccessor: {memoryMappedViewAccessor}");
            // D.Log.Debug($"fileWriteBuffer: {fileWriteBuffer?.Length}");
            // D.Log.Debug($"writeStructPtr: {writeStructPtr}");
            return;
        }

        D.Log.Debug("continue to work work");

        Marshal.StructureToPtr(outputData, writeStructPtr, true);
        Marshal.Copy(writeStructPtr, fileWriteBuffer, 0, fileWriteBufferSize);
        memoryMappedViewAccessor.WriteArray(0, fileWriteBuffer, 0, fileWriteBufferSize);

    }

    private bool refreshOuputData() {
        if (D.ClientState.LocalPlayer == null) {
            D.Log.Debug("!! No Local Player");
            return false;
        }

        Vector3 cameraPos;
        Matrix4x4 cameraViewMatrix;
        Vector3 cameraTop;
        unsafe {
            var camera = CameraManager.Instance()->GetActiveCamera();
            if (camera == null) {
                D.Log.Debug("!! No Camera Available");
                return false;
            }

            cameraPos = camera->CameraBase.SceneCamera.Object.Position;
            cameraViewMatrix = camera->CameraBase.SceneCamera.ViewMatrix;
            cameraTop = camera->CameraBase.SceneCamera.Vector_1;
        }

        outputData.UITick = updateTick;

        outputData.CameraPosition[0] = cameraPos.X;
        outputData.CameraPosition[1] = cameraPos.Y;
        outputData.CameraPosition[2] = cameraPos.Z;

        // var cameraFront = new Vector3(cameraViewMatrix.M13, cameraViewMatrix.M23, cameraViewMatrix.M33);
        outputData.CameraFront[0] = cameraViewMatrix.M13;
        outputData.CameraFront[1] = cameraViewMatrix.M23;
        outputData.CameraFront[2] = cameraViewMatrix.M33;

        outputData.CameraTop[0] = cameraTop.X;
        outputData.CameraTop[1] = cameraTop.Y;
        outputData.CameraTop[2] = cameraTop.Z;

        var boundByDuty = D.Condition[ConditionFlag.BoundByDuty]
                       || D.Condition[ConditionFlag.BoundByDuty56]
                       || D.Condition[ConditionFlag.BoundByDuty95];
        var contextId = boundByDuty ? "duty" : D.ClientState.LocalPlayer.CurrentWorld.Id.ToString();
        var context = contextId + "-" + D.ClientState.TerritoryType;

        var contextBytesWritten = Encoding.UTF8.GetBytes(context, outputData.Context);
        outputData.ContextLength = (uint)contextBytesWritten;

        var avatarPos = D.ClientState.LocalPlayer.Position;
        outputData.AvatarPosition[0] = avatarPos.X;
        outputData.AvatarPosition[1] = avatarPos.Y;
        outputData.AvatarPosition[2] = avatarPos.Z;

        var avatarRot = D.ClientState.LocalPlayer.Rotation; // -pi to pi radians
                                                            //var avatarFront = new Vector3((float)Math.Cos(avatarRot), 0, (float)Math.Sin(avatarRot));
        outputData.AvatarFront[0] = (float)Math.Cos(avatarRot);
        //outputData.AvatarFront[1] = avatarFront.Y;
        outputData.AvatarFront[2] = (float)Math.Sin(avatarRot);

        return true;
    }
}