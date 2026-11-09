using System.Runtime.InteropServices;

namespace GammaKey.Vibrance;

internal static class NvApiNative
{
    private const uint IdInitialize = 0x0150E828;
    private const uint IdEnumNvidiaDisplayHandle = 0x9ABDD40D;
    private const uint IdGetAssociatedNvidiaDisplayName = 0x22A78B05;
    private const uint IdGetDvcInfo = 0x4085DE45;
    private const uint IdSetDvcLevel = 0x172409B4;

    public const int NvApiOk = 0;
    public const int NvApiEndEnumeration = -7;
    public const int NvApiHandleInvalidated = -8;
    public const int NvApiExpectedDisplayHandle = -13;

    [StructLayout(LayoutKind.Sequential)]
    public struct DvcInfo
    {
        public uint Version;
        public int CurrentLevel;
        public int MinLevel;
        public int MaxLevel;

        public static DvcInfo Create() => new() { Version = (uint)Marshal.SizeOf<DvcInfo>() | (1u << 16) };
    }

    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [DllImport("nvapi64.dll", EntryPoint = "nvapi_QueryInterface", CallingConvention = CallingConvention.Cdecl)]
    private static extern IntPtr QueryInterface(uint id);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate int InitializeDelegate();

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate int EnumNvidiaDisplayHandleDelegate(int index, out IntPtr handle);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate int GetAssociatedNvidiaDisplayNameDelegate(IntPtr handle, byte[] name);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate int GetDvcInfoDelegate(IntPtr handle, uint outputId, ref DvcInfo info);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate int SetDvcLevelDelegate(IntPtr handle, uint outputId, int level);

    public static InitializeDelegate? Initialize => Resolve<InitializeDelegate>(IdInitialize);
    public static EnumNvidiaDisplayHandleDelegate? EnumNvidiaDisplayHandle => Resolve<EnumNvidiaDisplayHandleDelegate>(IdEnumNvidiaDisplayHandle);
    public static GetAssociatedNvidiaDisplayNameDelegate? GetAssociatedNvidiaDisplayName => Resolve<GetAssociatedNvidiaDisplayNameDelegate>(IdGetAssociatedNvidiaDisplayName);
    public static GetDvcInfoDelegate? GetDvcInfo => Resolve<GetDvcInfoDelegate>(IdGetDvcInfo);
    public static SetDvcLevelDelegate? SetDvcLevel => Resolve<SetDvcLevelDelegate>(IdSetDvcLevel);

    private static T? Resolve<T>(uint id) where T : Delegate
    {
        var ptr = QueryInterface(id);
        return ptr == IntPtr.Zero ? null : Marshal.GetDelegateForFunctionPointer<T>(ptr);
    }
}
