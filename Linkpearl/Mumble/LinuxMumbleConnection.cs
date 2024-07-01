using System;
using System.IO.MemoryMappedFiles;
using System.Runtime.InteropServices;
using System.Text;

namespace Linkpearl;

public class LinuxMumbleConnection : IMumbleConnection {
    private static int OutputDataSize = Marshal.SizeOf<MumbleAvatarLinux>();

    private MumbleAvatarLinux outputData;
    private nint structPtr;

    private readonly MemoryMappedFile memoryMappedFile;
    private readonly MemoryMappedViewAccessor memoryMappedViewAccessor;

    public LinuxMumbleConnection(string identity, int uid) {
        var identityBytes = new byte[256 * 4];
        Encoding.UTF32.GetBytes(identity, identityBytes);

        var name = new byte[256 * 4];
        Encoding.UTF32.GetBytes(Plugin.MumbleName, name);

        var description = new byte[2048 * 4];
        Encoding.UTF32.GetBytes(Plugin.MumbleDescription, description);

        this.outputData = new MumbleAvatarLinux {
            UiVersion = Plugin.MumbleVersion,
            Name = name,
            Description = description,
            Identity = identityBytes,

            AvatarTop = [0f, 1f, 0f],
            AvatarFront = [0f, 0f, 0f],
            AvatarPosition = [0f, 0f, 0f],

            CameraTop = [0f, 0f, 0f],
            CameraFront = [0f, 0f, 0f],
            CameraPosition = [0f, 0f, 0f],

            Context = new byte[256]
        };

        this.structPtr = Marshal.AllocHGlobal(OutputDataSize);
        this.memoryMappedFile = MemoryMappedFile.CreateFromFile(
            "/dev/shm/MumbleLink." + uid,
            System.IO.FileMode.OpenOrCreate, null, OutputDataSize,
            MemoryMappedFileAccess.ReadWrite);
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
