using System;
using System.IO.MemoryMappedFiles;
using System.Runtime.InteropServices;
using System.Text;

namespace Linkpearl;

public class WindowsMumbleConnection : IMumbleConnection {
    private static int OutputDataSize = Marshal.SizeOf<MumbleAvatarWindows>();

    private MumbleAvatarWindows outputData;
    private nint structPtr;

    private readonly MemoryMappedFile memoryMappedFile;
    private readonly MemoryMappedViewAccessor memoryMappedViewAccessor;

    public WindowsMumbleConnection(string identity) {
        this.outputData = new MumbleAvatarWindows {
            UiVersion = Plugin.MumbleVersion,
            Name = Plugin.MumbleName,
            Description = Plugin.MumbleDescription,
            Identity = identity,

            AvatarTop = [0f, 1f, 0f],
            AvatarFront = [0f, 0f, 0f],
            AvatarPosition = [0f, 0f, 0f],

            CameraTop = [0f, 0f, 0f],
            CameraFront = [0f, 0f, 0f],
            CameraPosition = [0f, 0f, 0f],

            Context = new byte[256]
        };

        this.structPtr = Marshal.AllocHGlobal(OutputDataSize);
        this.memoryMappedFile = MemoryMappedFile.CreateOrOpen("MumbleLink", OutputDataSize);
        this.memoryMappedViewAccessor = this.memoryMappedFile.CreateViewAccessor();
    }

    public void Dispose() {
        this.memoryMappedFile.Dispose();
        this.memoryMappedViewAccessor.Dispose();
    }

    public void Update(MumbleAvatar avatar) {
        this.outputData.UiTick = avatar.UiTick;

        this.outputData.AvatarPosition = [avatar.AvatarPosition.X, avatar.AvatarPosition.Y, avatar.AvatarPosition.Z];
        this.outputData.AvatarFront = [avatar.AvatarFront.X, avatar.AvatarFront.Y, avatar.AvatarFront.Z];
        this.outputData.AvatarTop = [avatar.AvatarTop.X, avatar.AvatarTop.Y, avatar.AvatarTop.Z];

        this.outputData.CameraPosition = [avatar.CameraPosition.X, avatar.CameraPosition.Y, avatar.CameraPosition.Z];
        this.outputData.CameraFront = [avatar.CameraFront.X, avatar.CameraFront.Y, avatar.CameraFront.Z];
        this.outputData.CameraTop = [avatar.CameraTop.X, avatar.CameraTop.Y, avatar.CameraTop.Z];

        this.outputData.ContextLength = (uint) Encoding.UTF8.GetBytes(avatar.Context, this.outputData.Context);

        unsafe {
            Marshal.StructureToPtr(this.outputData, this.structPtr, true);
            var span = new Span<byte>((void*) this.structPtr, OutputDataSize);
            this.memoryMappedViewAccessor.WriteArray(0, span.ToArray(), 0, OutputDataSize);
        }
    }
}
