using System.Runtime.InteropServices;

namespace Linkpearl;

[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
public struct MumbleAvatar {
    public uint UIVersion;
    public uint UITick;

    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 3)]
    public float[] AvatarPosition;

    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 3)]
    public float[] AvatarFront;

    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 3)]
    public float[] AvatarTop;

    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
    public string Name;

    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 3)]
    public float[] CameraPosition;

    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 3)]
    public float[] CameraFront;

    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 3)]
    public float[] CameraTop;

    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
    public string Identity;

    public uint ContextLength;

    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 256)]
    public byte[] Context;

    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 2048)]
    public string Description;
}
