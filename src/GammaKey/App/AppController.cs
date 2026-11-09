using GammaKey.Display;
using GammaKey.Hotkeys;
using GammaKey.Models;
using GammaKey.Storage;
using GammaKey.Vibrance;

namespace GammaKey.App;

public sealed class AppController : IDisposable
{
    private const int ResetHotkeyId = 1;
    private const int FirstProfileHotkeyId = 100;
    private const int GuardIntervalMs = 3000;

    private readonly ProfileStore _profileStore;
    private readonly BaselineStore _baselineStore;
    private IHotkeyListener _hotkeys;
    private readonly System.Windows.Forms.Timer _guard = new() { Interval = GuardIntervalMs };
    private readonly Dictionary<int, string> _profileIdsByHotkeyId = new();
    private Baseline _baseline = new();

    public string BaseDirectory { get; }
    public string LockPath => Path.Combine(BaseDirectory, "running.lock");
    public AppSettings Settings { get; private set; }
    public int ArmedHotkeys { get; private set; }
    public DisplayEngine Display { get; } = new();
    public VibranceEngine Vibrance { get; } = new();

    public event Action<Profile, string?>? ProfileApplied;
    public event Action<string>? ResetApplied;
    public event Action? SettingsChanged;

    public AppController(string baseDirectory)
    {
        BaseDirectory = baseDirectory;
        _profileStore = new ProfileStore(Path.Combine(baseDirectory, "profiles.json"));
        _baselineStore = new BaselineStore(Path.Combine(baseDirectory, "baseline.json"));
        Settings = _profileStore.Load();
        _hotkeys = CreateListener(Settings.HotkeyPassthrough);
        // Memory only, no Save: the saved choice stays on so the next launch tries the hook
        // again, while the Settings checkbox shows the mode that is really running.
        if (Settings.HotkeyPassthrough && _hotkeys is not KeyboardHookListener)
            Settings.HotkeyPassthrough = false;
        _hotkeys.Pressed += OnHotkeyPressed;
        _guard.Tick += (_, _) => { if (Settings.GuardEnabled) Display.ReapplyIfCleared(); };
    }

    public void Start()
    {
        // A lock file left behind means the last run died with a curve applied. Put the
        // original back first so the fresh baseline is the real one, not the stale profile.
        var previous = _baselineStore.Load();
        bool recoveringFromCrash = File.Exists(LockPath);
        if (recoveringFromCrash && previous is not null)
        {
            Display.RestoreBaseline(previous);
            Vibrance.RestoreBaseline(previous);
        }
        else if (recoveringFromCrash)
        {
            // The crashed run left a profile curve on screen and no saved original; a flat
            // table is a safer "original" than capturing that stale curve.
            Display.ResetToIdentity();
        }

        _baseline = new Baseline();
        Display.CaptureBaseline(_baseline);
        Vibrance.CaptureBaseline(_baseline);
        _baselineStore.Save(_baseline);
        File.WriteAllText(LockPath, DateTime.Now.ToString("O"));

        if (!File.Exists(_profileStore.Path)) _profileStore.Save(Settings);

        RegisterHotkeys();
        _guard.Start();
    }

    public IReadOnlyList<string> RegisterHotkeys()
    {
        _hotkeys.UnregisterAll();
        _profileIdsByHotkeyId.Clear();
        var failed = new List<string>();

        int armed = 0;
        if (HotkeyCombo.TryParse(Settings.ResetHotkey, out var resetCombo))
        {
            if (_hotkeys.Register(ResetHotkeyId, resetCombo)) armed++;
            else failed.Add("Reset");
        }

        int nextId = FirstProfileHotkeyId;
        foreach (var profile in Settings.Profiles)
        {
            if (!HotkeyCombo.TryParse(profile.Hotkey, out var combo)) continue;
            if (_hotkeys.Register(nextId, combo)) { _profileIdsByHotkeyId[nextId] = profile.Id; armed++; }
            else failed.Add(profile.Name);
            nextId++;
        }
        ArmedHotkeys = armed;
        return failed;
    }

    public ApplyResult ApplyProfile(Profile profile)
    {
        var result = Preview(profile.Values, profile.Target);
        ProfileApplied?.Invoke(profile, result.Note);
        return result;
    }

    public ApplyResult Preview(ColorValues values, MonitorTarget target)
    {
        var monitors = Display.ResolveTargets(target, out var note);
        // A profile owns the whole desk: monitors it does not target go back to original, or they
        // would keep the last curve written to them for ever.
        var others = DisplayEngine.OtherDevices(Display.GetMonitors(), monitors);
        Display.RestoreBaseline(_baseline, others);
        if (Vibrance.IsAvailable) Vibrance.RestoreBaseline(_baseline, others);
        var result = Display.Apply(values, monitors);
        if (Vibrance.IsAvailable)
            foreach (var monitor in monitors) Vibrance.SetSlider(monitor.DeviceName, values.Vibrance);
        var combined = (result.Note, note) switch
        {
            (null, null) => null,
            (null, var n) => n,
            (var r, null) => r,
            (var r, var n) => $"{r}; {n}",
        };
        return result with { Note = combined };
    }

    public void Reset()
    {
        Display.RestoreBaseline(_baseline);
        Vibrance.RestoreBaseline(_baseline);
        ResetApplied?.Invoke("Reset to original");
    }

    public void Save()
    {
        _profileStore.Save(Settings);
        SettingsChanged?.Invoke();
    }

    public void SetStartWithWindows(bool on)
    {
        StartupRegistration.Set(on);
        Settings.StartWithWindows = on;
        Save();
    }

    private static IHotkeyListener CreateListener(bool passthrough)
    {
        if (!passthrough) return new HotkeyListener();
        var hook = new KeyboardHookListener();
        if (hook.IsInstalled) return hook;
        // A hook that failed to install would leave every hotkey dead, so use normal mode.
        hook.Dispose();
        return new HotkeyListener();
    }

    // Returns true when the requested mode is active afterwards.
    public bool SetHotkeyPassthrough(bool on)
    {
        if (Settings.HotkeyPassthrough == on) return true;
        _hotkeys.Pressed -= OnHotkeyPressed;
        _hotkeys.Dispose();
        _hotkeys = CreateListener(on);
        _hotkeys.Pressed += OnHotkeyPressed;
        bool isActive = (_hotkeys is KeyboardHookListener) == on;
        if (isActive)
        {
            Settings.HotkeyPassthrough = on;
            Save();
        }
        RegisterHotkeys();
        return isActive;
    }

    private bool _isShutDown;

    public void Shutdown()
    {
        if (_isShutDown) return;
        _isShutDown = true;
        _guard.Stop();
        _hotkeys.UnregisterAll();
        Display.RestoreBaseline(_baseline);
        Vibrance.RestoreBaseline(_baseline);
        if (File.Exists(LockPath)) File.Delete(LockPath);
    }

    private void OnHotkeyPressed(int id)
    {
        if (id == ResetHotkeyId)
        {
            Reset();
            return;
        }
        if (!_profileIdsByHotkeyId.TryGetValue(id, out var profileId)) return;
        var profile = Settings.Profiles.FirstOrDefault(p => p.Id == profileId);
        if (profile is not null) ApplyProfile(profile);
    }

    public void Dispose()
    {
        Shutdown();
        _guard.Dispose();
        _hotkeys.Dispose();
    }
}
