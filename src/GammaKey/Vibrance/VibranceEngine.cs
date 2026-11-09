using System.Text;
using GammaKey.Models;
using GammaKey.Storage;

namespace GammaKey.Vibrance;

public sealed class VibranceEngine
{
    private readonly Dictionary<string, IntPtr> _handlesByDevice = new(StringComparer.OrdinalIgnoreCase);
    private NvApiNative.GetDvcInfoDelegate? _getInfo;
    private NvApiNative.SetDvcLevelDelegate? _setLevel;

    public bool IsAvailable { get; private set; }
    public string? UnavailableReason { get; private set; }

    public VibranceEngine()
    {
        try
        {
            Initialize();
        }
        catch (DllNotFoundException)
        {
            UnavailableReason = "NVIDIA driver not found";
        }
        catch (EntryPointNotFoundException)
        {
            UnavailableReason = "NVIDIA driver too old";
        }
    }

    private void Initialize()
    {
        var init = NvApiNative.Initialize;
        var enumHandle = NvApiNative.EnumNvidiaDisplayHandle;
        var getName = NvApiNative.GetAssociatedNvidiaDisplayName;
        _getInfo = NvApiNative.GetDvcInfo;
        _setLevel = NvApiNative.SetDvcLevel;

        if (init is null || enumHandle is null || getName is null || _getInfo is null || _setLevel is null)
        {
            UnavailableReason = "NVIDIA driver missing vibrance support";
            return;
        }
        if (init() != NvApiNative.NvApiOk)
        {
            UnavailableReason = "NVIDIA driver failed to start";
            return;
        }

        var nameBuffer = new byte[64];
        for (int i = 0; ; i++)
        {
            var status = enumHandle(i, out var handle);
            if (status != NvApiNative.NvApiOk) break;
            Array.Clear(nameBuffer);
            if (getName(handle, nameBuffer) != NvApiNative.NvApiOk) continue;
            var name = Encoding.ASCII.GetString(nameBuffer).TrimEnd('\0');
            if (name.Length > 0) _handlesByDevice[name] = handle;
        }

        IsAvailable = _handlesByDevice.Count > 0;
        if (!IsAvailable) UnavailableReason = "No monitor on an NVIDIA output";
    }

    public int? GetSlider(string deviceName)
    {
        var raw = GetRaw(deviceName, out var info);
        if (raw is null) return null;
        int span = Math.Max(1, info.MaxLevel - info.MinLevel);
        double fraction = (raw.Value - info.MinLevel) / (double)span;
        return (int)Math.Round(ColorValues.VibranceMin + fraction * (ColorValues.VibranceMax - ColorValues.VibranceMin));
    }

    public bool SetSlider(string deviceName, int slider)
    {
        if (GetRaw(deviceName, out var info) is null) return false;
        slider = Math.Clamp(slider, ColorValues.VibranceMin, ColorValues.VibranceMax);
        double fraction = (slider - ColorValues.VibranceMin) / (double)(ColorValues.VibranceMax - ColorValues.VibranceMin);
        int level = info.MinLevel + (int)Math.Round(fraction * (info.MaxLevel - info.MinLevel));
        return SetRaw(deviceName, level);
    }

    public void CaptureBaseline(Baseline into)
    {
        foreach (var device in _handlesByDevice.Keys)
        {
            var raw = GetRaw(device, out _);
            if (raw is not null) into.VibranceLevels[device] = raw.Value;
        }
    }

    public void RestoreBaseline(Baseline from)
    {
        foreach (var (device, level) in from.VibranceLevels)
            SetRaw(device, level);
    }

    private int? GetRaw(string deviceName, out NvApiNative.DvcInfo info)
    {
        info = NvApiNative.DvcInfo.Create();
        if (!IsAvailable || _getInfo is null || !_handlesByDevice.TryGetValue(deviceName, out var handle)) return null;
        return _getInfo(handle, 0, ref info) == NvApiNative.NvApiOk ? info.CurrentLevel : null;
    }

    private bool SetRaw(string deviceName, int level)
    {
        if (!IsAvailable || _setLevel is null || !_handlesByDevice.TryGetValue(deviceName, out var handle)) return false;
        return _setLevel(handle, 0, level) == NvApiNative.NvApiOk;
    }
}
