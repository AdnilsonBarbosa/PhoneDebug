using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;
using PhoneDebug.Core;
using PhoneDebug.Core.Diagnostics;
using PhoneDebug.Core.Models;
using PhoneDebug.Core.Services;
using PhoneDebug.Core.Tools;

namespace PhoneDebug.App;

/// <summary>
/// The whole user interface: status, device, three actions and a log.
/// It owns no ADB logic - everything comes from PhoneDebug.Core, exactly
/// like the command line does.
/// </summary>
internal sealed class MainForm : Form
{
    private const int MaxLogLines = 1500;

    private readonly Label _statusDot = new();
    private readonly Label _statusText = new();
    private readonly Label _deviceName = new();
    private readonly Label _deviceVersion = new();
    private readonly Label _banner = new();
    private readonly LinkLabel _recheck = new();
    private readonly ComboBox _deviceSelector = new();
    private readonly Button _mirrorButton = Theme.PrimaryButton("Open Screen");
    private readonly Button _installButton = Theme.SecondaryButton("Install APK");
    private readonly Button _screenshotButton = Theme.SecondaryButton("Screenshot");
    private readonly CheckBox _logcatToggle = new();
    private readonly RichTextBox _log = new();

    private TableLayoutPanel? _actions;
    private PhoneDebugContext? _core;
    private DeviceMonitor? _monitor;
    private MirrorSession? _mirror;
    private MirrorInput _mirrorInput = MirrorInput.Standard;
    private LogcatSession? _logcat;
    private AndroidDevice? _device;
    private int _logLines;
    private bool _busy;
    private bool _closing;

    public MainForm()
    {
        Text = AppInfo.Name;
        BackColor = Theme.Background;
        Font = Theme.Body;
        ForeColor = Theme.Text;
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(440, 620);
        MinimumSize = new Size(400, 520);
        Icon = LoadIcon();

        Controls.Add(BuildLayout());

        _mirrorButton.Click += OnMirrorClicked;
        _installButton.Click += OnInstallClicked;
        _screenshotButton.Click += OnScreenshotClicked;
        _logcatToggle.CheckedChanged += OnLogcatToggled;
        _deviceSelector.SelectedIndexChanged += OnDeviceSelected;
        _recheck.LinkClicked += async (_, _) => await StartAsync().ConfigureAwait(true);

        Load += async (_, _) =>
        {
            ApplyScaling();
            await StartAsync().ConfigureAwait(true);
        };
        FormClosing += OnFormClosing;
    }

    // ---------------------------------------------------------------- layout

    private Control BuildLayout()
    {
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            BackColor = Theme.Background,
            Padding = new Padding(24, 20, 24, 16),
        };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        var title = new Label
        {
            Text = AppInfo.Name,
            Font = Theme.Title,
            ForeColor = Theme.Text,
            AutoSize = true,
            Margin = new Padding(0, 0, 0, 14),
        };

        _statusDot.Text = "○";
        _statusDot.Font = Theme.Status;
        _statusDot.ForeColor = Theme.Muted;
        _statusDot.AutoSize = true;
        _statusDot.Margin = new Padding(0, 0, 6, 0);

        _statusText.Text = "Starting...";
        _statusText.Font = Theme.Status;
        _statusText.ForeColor = Theme.Muted;
        _statusText.AutoSize = true;
        _statusText.Margin = new Padding(0, 1, 0, 0);

        _recheck.Text = "Check again";
        _recheck.Font = Theme.Small;
        _recheck.AutoSize = true;
        _recheck.LinkColor = Theme.Accent;
        _recheck.ActiveLinkColor = Theme.AccentHover;
        _recheck.Margin = new Padding(12, 3, 0, 0);
        _recheck.Visible = false;

        var statusRow = new FlowLayoutPanel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            Margin = new Padding(0, 0, 0, 12),
        };
        statusRow.Controls.AddRange([_statusDot, _statusText, _recheck]);

        _deviceName.Text = "";
        _deviceName.Font = Theme.DeviceName;
        _deviceName.ForeColor = Theme.Text;
        _deviceName.AutoSize = true;
        _deviceName.Margin = new Padding(0, 0, 0, 2);

        _deviceVersion.Text = "";
        _deviceVersion.Font = Theme.Body;
        _deviceVersion.ForeColor = Theme.Muted;
        _deviceVersion.AutoSize = true;
        _deviceVersion.Margin = new Padding(0, 0, 0, 10);

        _deviceSelector.DropDownStyle = ComboBoxStyle.DropDownList;
        _deviceSelector.Font = Theme.Body;
        _deviceSelector.Dock = DockStyle.Fill;
        _deviceSelector.Margin = new Padding(0, 0, 0, 10);
        _deviceSelector.Visible = false;

        _banner.Font = Theme.Body;
        _banner.ForeColor = Theme.Danger;
        _banner.AutoSize = true;
        _banner.Margin = new Padding(0, 0, 0, 10);
        _banner.Visible = false;

        var actions = new TableLayoutPanel
        {
            ColumnCount = 2,
            RowCount = 1,
            Dock = DockStyle.Fill,
            Margin = new Padding(0, 10, 0, 0),
        };
        actions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        actions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        _installButton.Margin = new Padding(0, 0, 6, 0);
        _screenshotButton.Margin = new Padding(6, 0, 0, 0);
        actions.Controls.Add(_installButton, 0, 0);
        actions.Controls.Add(_screenshotButton, 1, 0);

        var logsHeader = new TableLayoutPanel
        {
            ColumnCount = 2,
            Dock = DockStyle.Fill,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Margin = new Padding(0, 16, 0, 4),
        };
        logsHeader.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        logsHeader.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

        var logsLabel = new Label
        {
            Text = "Logs",
            Font = new Font("Segoe UI Semibold", 9.75f, FontStyle.Bold),
            ForeColor = Theme.Text,
            AutoSize = true,
            Margin = new Padding(0, 4, 0, 0),
        };

        _logcatToggle.Text = "Device logcat";
        _logcatToggle.Font = Theme.Small;
        _logcatToggle.ForeColor = Theme.Muted;
        _logcatToggle.AutoSize = true;
        _logcatToggle.Margin = new Padding(0, 4, 0, 0);
        _logcatToggle.Enabled = false;

        logsHeader.Controls.Add(logsLabel, 0, 0);
        logsHeader.Controls.Add(_logcatToggle, 1, 0);

        _log.ReadOnly = true;
        _log.BorderStyle = BorderStyle.None;
        _log.BackColor = Theme.LogBackground;
        _log.ForeColor = Theme.Text;
        _log.Font = Theme.Mono;
        _log.Dock = DockStyle.Fill;
        _log.Margin = new Padding(0);
        _log.WordWrap = false;
        _log.ScrollBars = RichTextBoxScrollBars.Both;
        _log.DetectUrls = false;

        var logHost = new Panel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(8),
            BackColor = Theme.LogBackground,
            Margin = new Padding(0, 0, 0, 10),
        };
        logHost.Controls.Add(_log);
        logHost.Paint += (_, e) => ControlPaint.DrawBorder(
            e.Graphics, logHost.ClientRectangle, Theme.Border, ButtonBorderStyle.Solid);

        var footer = new TableLayoutPanel
        {
            ColumnCount = 2,
            Dock = DockStyle.Fill,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Margin = new Padding(0),
        };
        footer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        footer.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

        var version = new Label
        {
            Text = AppInfo.Title,
            Font = Theme.Small,
            ForeColor = Theme.Muted,
            AutoSize = true,
            Margin = new Padding(0),
        };

        var logLink = new LinkLabel
        {
            Text = "Log file",
            Font = Theme.Small,
            AutoSize = true,
            LinkColor = Theme.Muted,
            ActiveLinkColor = Theme.Accent,
            Margin = new Padding(0),
        };
        logLink.LinkClicked += (_, _) => OpenLogFolder();

        footer.Controls.Add(version, 0, 0);
        footer.Controls.Add(logLink, 1, 0);

        // Every row sizes itself, so nothing clips on high-DPI screens and
        // hidden rows (the device picker, the warning banner) take no space.
        AddRow(root, title, SizeType.AutoSize);
        AddRow(root, statusRow, SizeType.AutoSize);
        AddRow(root, _deviceName, SizeType.AutoSize);
        AddRow(root, _deviceVersion, SizeType.AutoSize);
        AddRow(root, _deviceSelector, SizeType.AutoSize);
        AddRow(root, _banner, SizeType.AutoSize);
        AddRow(root, _mirrorButton, SizeType.AutoSize);
        AddRow(root, actions, SizeType.AutoSize);
        AddRow(root, logsHeader, SizeType.AutoSize);
        AddRow(root, logHost, SizeType.Percent, 100);
        AddRow(root, footer, SizeType.AutoSize);

        root.RowCount = root.RowStyles.Count;
        _actions = actions;
        return root;
    }

    /// <summary>
    /// Button heights are the only fixed sizes left, and they are scaled to
    /// the monitor the window is actually on.
    /// </summary>
    private void ApplyScaling()
    {
        var height = Scaled(40);
        _mirrorButton.Height = Scaled(44);
        _installButton.Height = height;
        _screenshotButton.Height = height;

        if (_actions is not null)
            _actions.Height = height;
    }

    private int Scaled(int value) => (int)Math.Round(value * DeviceDpi / 96.0);

    protected override void OnDpiChangedAfterParent(EventArgs e)
    {
        base.OnDpiChangedAfterParent(e);
        ApplyScaling();
    }

    private static void AddRow(TableLayoutPanel table, Control control, SizeType type, float size = 0)
    {
        table.RowStyles.Add(new RowStyle(type, size));
        table.Controls.Add(control, 0, table.RowStyles.Count - 1);
    }

    private static Icon? LoadIcon()
    {
        try
        {
            return Icon.ExtractAssociatedIcon(Environment.ProcessPath ?? Application.ExecutablePath);
        }
        catch (Exception ex)
        {
            Log.Warn($"Could not load the window icon: {ex.Message}");
            return null;
        }
    }

    // ---------------------------------------------------------------- startup

    private async Task StartAsync()
    {
        await StopMonitorAsync().ConfigureAwait(true);

        SetStatus("○", "Checking...", Theme.Muted);
        _recheck.Visible = false;
        _banner.Visible = false;

        var core = await Task.Run(() => PhoneDebugContext.Create()).ConfigureAwait(true);
        core = await EnsureToolsAsync(core).ConfigureAwait(true);
        _core = core;

        if (!core.CanUseDevices)
        {
            ShowToolProblem(core.Tools.Adb);
            return;
        }

        if (!core.Tools.Scrcpy.Found)
            ShowToolProblem(core.Tools.Scrcpy, fatal: false);
        else
            Append($"Ready. {core.Tools.Adb.Version}");

        _monitor = core.CreateMonitor();
        _monitor.Changed += OnMonitorChanged;
        _monitor.Start();
    }

    /// <summary>
    /// A fresh portable install has no adb/scrcpy next to PhoneDebug.exe (their
    /// licences prevent PhoneDebug from shipping them). When one is missing, it
    /// is fetched from its official source once and then re-detected.
    /// </summary>
    private async Task<PhoneDebugContext> EnsureToolsAsync(PhoneDebugContext core)
    {
        var needAdb = !core.Tools.Adb.Found;
        var needScrcpy = !core.Tools.Scrcpy.Found;
        if (!needAdb && !needScrcpy)
            return core;

        // Locked-down machines may not want a first-run download.
        if (Environment.GetEnvironmentVariable("PHONEDEBUG_NO_DOWNLOAD") == "1")
            return core;

        var what = needAdb && needScrcpy ? "adb and scrcpy"
            : needAdb ? "adb"
            : "scrcpy";

        SetStatus("○", "Downloading tools...", Theme.Accent);
        Append($"Phone Debug is ready to go, but {what} is missing. Downloading it now...");

        var progress = new Progress<string>(message => RunOnUi(() => Append(message, raw: true)));
        var outcome = await Task.Run(
            () => ToolDownloader.DownloadMissingAsync(progress)).ConfigureAwait(true);

        if (outcome.AdbDownloaded)
            Append("ADB downloaded from Google.");
        if (outcome.ScrcpyDownloaded)
            Append("scrcpy downloaded from GitHub.");

        return await Task.Run(() => PhoneDebugContext.Create(probeVersions: false)).ConfigureAwait(true);
    }

    private void ShowToolProblem(ToolStatus tool, bool fatal = true)
    {
        SetStatus("×", $"{tool.Name} not found", Theme.Danger);

        _banner.Text = string.Join(
            Environment.NewLine,
            ToolEnvironment.HowToInstall(tool.Name).Where(l => l.Length > 0).Take(2));
        _banner.Visible = true;
        _recheck.Visible = true;

        Append($"{tool.Name} not found.");
        foreach (var line in ToolEnvironment.HowToInstall(tool.Name))
            Append(line);

        if (fatal)
            UpdateButtons();
    }

    // ---------------------------------------------------------------- monitor

    private void OnMonitorChanged(object? sender, DeviceMonitorEventArgs e) => RunOnUi(() => Apply(e));

    private void Apply(DeviceMonitorEventArgs e)
    {
        _device = e.Device;

        switch (e.State)
        {
            case DeviceMonitorState.Connected when e.Device is not null:
                SetStatus("●", "Connected", Theme.Connected);
                _deviceName.Text = e.Device.Name;
                _deviceName.ForeColor = Theme.Text;
                _deviceName.Font = Theme.DeviceName;
                _deviceVersion.Text = e.Device.AndroidLabel ?? "";
                Append($"{e.Device.Name} connected ({e.Device.Serial})");
                break;

            case DeviceMonitorState.Unauthorized:
                SetStatus("○", "Not authorized", Theme.Danger);
                SetHint("Unlock your phone and accept \"Allow USB debugging\".");
                StopMirror();
                Append("Device detected but not authorized.");
                break;

            case DeviceMonitorState.Offline:
                SetStatus("○", "Device offline", Theme.Danger);
                SetHint("Unplug and reconnect the cable.");
                StopMirror();
                break;

            case DeviceMonitorState.MultipleDevices:
                SetStatus("●", "Several devices", Theme.Accent);
                SetHint("Choose which phone to use.");
                break;

            case DeviceMonitorState.AdbError:
                SetStatus("×", "ADB error", Theme.Danger);
                SetHint("ADB is not responding. Retrying...");
                Append($"ADB error: {e.Detail}");
                break;

            default:
                SetStatus("○", "No device", Theme.Muted);
                SetHint("Waiting for Android device...");
                StopMirror();
                break;
        }

        UpdateDeviceSelector(e.Devices);
        UpdateButtons();
    }

    private void UpdateDeviceSelector(IReadOnlyList<AndroidDevice> devices)
    {
        var authorized = devices.Where(d => d.IsAuthorized).ToList();
        if (authorized.Count < 2)
        {
            _deviceSelector.Visible = false;
            return;
        }

        foreach (var device in authorized.Where(d => d.Model is null))
            _core?.Devices?.Adb.FillDetails(device);

        _deviceSelector.SelectedIndexChanged -= OnDeviceSelected;
        _deviceSelector.Items.Clear();
        foreach (var device in authorized)
            _deviceSelector.Items.Add(new DeviceChoice(device));

        var current = _monitor?.PreferredSerial ?? _device?.Serial;
        var index = authorized.FindIndex(d => d.Serial == current);
        _deviceSelector.SelectedIndex = index >= 0 ? index : 0;
        _deviceSelector.Visible = true;
        _deviceSelector.SelectedIndexChanged += OnDeviceSelected;
    }

    private void OnDeviceSelected(object? sender, EventArgs e)
    {
        if (_deviceSelector.SelectedItem is not DeviceChoice choice || _monitor is null)
            return;

        if (_monitor.PreferredSerial == choice.Serial)
            return;

        _monitor.PreferredSerial = choice.Serial;
        _monitor.RefreshNow();
        StopMirror();
        Append($"Using {choice}");
    }

    private sealed record DeviceChoice(AndroidDevice Device)
    {
        public string Serial => Device.Serial;

        public override string ToString() => Device.DisplayName;
    }

    // ---------------------------------------------------------------- actions

    private void OnMirrorClicked(object? sender, EventArgs e)
    {
        // With no phone attached, the button is the way to attach one.
        if (_device is null)
        {
            ShowConnectDialog();
            return;
        }

        if (_mirror is { IsRunning: true })
        {
            StopMirror();
            return;
        }

        if (_core?.Scrcpy is null)
            return;

        Append("Opening screen...");
        StartMirror(UserPreferences.NeedsEmulatedInput(MirrorPreferences.KeyFor(_device))
            ? MirrorInput.Emulated
            : MirrorInput.Standard);
    }

    private void StartMirror(MirrorInput input)
    {
        if (_device is null || _core?.Scrcpy is null)
            return;

        _mirrorInput = input;
        _mirror = _core.Scrcpy.StartMirror(_device, input);

        if (_mirror is null)
        {
            Append("The screen could not be opened. See the log file.");
            MessageBox.Show(this,
                "The screen could not be opened.\n\nSee the log file for details.",
                AppInfo.Name, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        if (input == MirrorInput.Emulated)
        {
            Append(MirrorPreferences.EmulatedInputNote);
            Append(MirrorPreferences.ReleaseMouseNote);
        }

        _mirror.ProblemDetected += (_, problem) => RunOnUi(() => ShowProblem(problem));

        _mirror.Exited += (s, _) => RunOnUi(() =>
        {
            if (s is MirrorSession session && session.ExitCode != 0)
                Append($"The screen closed unexpectedly. {session.RecentOutput}".Trim());
            else
                Append("Screen closed.");

            UpdateButtons();
        });

        UpdateButtons();
    }

    private async void OnInstallClicked(object? sender, EventArgs e)
    {
        if (_device is null || _core?.Devices is null)
            return;

        using var dialog = new OpenFileDialog
        {
            Title = "Choose an APK",
            Filter = "Android package (*.apk)|*.apk|All files (*.*)|*.*",
            CheckFileExists = true,
        };

        if (dialog.ShowDialog(this) != DialogResult.OK)
            return;

        var serial = _device.Serial;
        var apk = dialog.FileName;

        Busy(true);
        Append($"Installing {Path.GetFileName(apk)}...");

        var result = await Task.Run(() => _core.Devices.InstallApk(serial, apk)).ConfigureAwait(true);

        Append(result.Success ? result.Message : $"Install failed: {result.Message}");
        Busy(false);

        if (!result.Success)
            MessageBox.Show(this, result.Message, "Install failed",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
    }

    private async void OnScreenshotClicked(object? sender, EventArgs e)
    {
        if (_device is null || _core?.Devices is null)
            return;

        Busy(true);
        Append("Capturing screenshot...");

        var result = await _core.Devices.CaptureScreenshotAsync(_device.Serial, null).ConfigureAwait(true);

        Append(result.Success ? result.Message : $"Screenshot failed: {result.Message}");
        Busy(false);

        // The path is shown in the log rather than opening Explorer, so taking
        // several captures in a row does not throw windows at the user.
        if (!result.Success)
            MessageBox.Show(this, result.Message, "Screenshot failed",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
    }

    private void OnLogcatToggled(object? sender, EventArgs e)
    {
        if (_logcatToggle.Checked)
            StartLogcat();
        else
            StopLogcat();
    }

    private void StartLogcat()
    {
        if (_device is null || _core?.Devices is null || _logcat is not null)
            return;

        _logcat = _core.Devices.StartLogcat(_device.Serial);
        if (_logcat is null)
        {
            Append("Could not start logcat.");
            _logcatToggle.Checked = false;
            return;
        }

        Append("--- device logcat ---");
        _logcat.LineReceived += (_, line) => RunOnUi(() => Append(line.Text, raw: true));
    }

    private void StopLogcat()
    {
        if (_logcat is null)
            return;

        _logcat.Dispose();
        _logcat = null;
        Append("--- logcat stopped ---");
    }

    private void StopMirror()
    {
        if (_mirror is null)
            return;

        _mirror.Dispose();
        _mirror = null;
        UpdateButtons();
    }

    private void ShowConnectDialog()
    {
        if (_core is null)
            return;

        using var dialog = new ConnectForm(_core);
        var result = dialog.ShowDialog(this);

        _monitor?.RefreshNow();

        if (result == DialogResult.OK)
            Append("Phone connected.");
    }

    /// <summary>A phone-side problem, such as a device that mirrors but refuses control.</summary>
    private void ShowProblem(MirrorProblem problem)
    {
        // A phone that blocks injected input can still be driven by pretending
        // to be a real USB keyboard and mouse, so try that before complaining.
        if (problem.Key == MirrorDiagnostics.ControlBlocked
            && _mirrorInput == MirrorInput.Standard
            && _device is not null
            && _core?.Scrcpy?.SupportsEmulatedInput == true)
        {
            Append(problem.Summary);
            Append("Switching to an emulated keyboard and mouse...");
            UserPreferences.RememberEmulatedInput(MirrorPreferences.KeyFor(_device));

            StopMirror();
            StartMirror(MirrorInput.Emulated);
            UpdateButtons();
            return;
        }

        Append(problem.Summary);
        foreach (var step in problem.Steps.Where(s => s.Length > 0))
            Append(step, raw: true);

        _banner.Text = problem.Summary;
        _banner.ForeColor = Theme.Danger;
        _banner.Visible = true;
    }

    // ---------------------------------------------------------------- helpers

    private void SetStatus(string dot, string text, Color color)
    {
        _statusDot.Text = dot;
        _statusDot.ForeColor = color;
        _statusText.Text = text;
        _statusText.ForeColor = color == Theme.Muted ? Theme.Muted : Theme.Text;
    }

    private void SetHint(string hint)
    {
        _deviceName.Text = hint;
        _deviceName.ForeColor = Theme.Muted;
        _deviceName.Font = Theme.Body;
        _deviceVersion.Text = "";
    }

    private void Busy(bool busy)
    {
        _busy = busy;
        Cursor = busy ? Cursors.WaitCursor : Cursors.Default;
        UpdateButtons();
    }

    private void UpdateButtons()
    {
        var connected = _device is not null;
        var mirroring = _mirror is { IsRunning: true };
        var canConnect = _core?.Devices is not null;

        _mirrorButton.Text = !connected ? "Connect a phone"
            : mirroring ? "Close Screen"
            : "Open Screen";

        _mirrorButton.Enabled = !_busy && (connected ? _core?.Scrcpy is not null : canConnect);
        _mirrorButton.BackColor = _mirrorButton.Enabled ? Theme.Accent : Color.FromArgb(191, 210, 250);

        _installButton.Enabled = connected && !_busy;
        _screenshotButton.Enabled = connected && !_busy;
        _logcatToggle.Enabled = connected && !_busy;

        if (!connected && _logcatToggle.Checked)
            _logcatToggle.Checked = false;
    }

    private void Append(string text, bool raw = false)
    {
        if (_closing || string.IsNullOrWhiteSpace(text))
            return;

        if (_logLines >= MaxLogLines)
        {
            _log.Clear();
            _logLines = 0;
            _log.AppendText("(earlier lines trimmed)" + Environment.NewLine);
        }

        var line = raw ? text : $"{DateTime.Now:HH:mm:ss}  {text}";
        _log.AppendText(line + Environment.NewLine);
        _logLines++;

        _log.SelectionStart = _log.TextLength;
        _log.ScrollToCaret();
    }

    private void RunOnUi(Action action)
    {
        if (_closing || !IsHandleCreated)
            return;

        try
        {
            if (InvokeRequired)
                BeginInvoke(action);
            else
                action();
        }
        catch (ObjectDisposedException)
        {
            // The window went away while a background task was finishing.
        }
        catch (InvalidOperationException)
        {
            // Handle destroyed between the check and the call.
        }
    }

    private void OpenLogFolder()
    {
        try
        {
            Directory.CreateDirectory(AppInfo.LogDirectory);
            Process.Start(new ProcessStartInfo(AppInfo.LogDirectory) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            Log.Error("Could not open the log folder", ex);
        }
    }

    private async Task StopMonitorAsync()
    {
        if (_monitor is null)
            return;

        _monitor.Changed -= OnMonitorChanged;
        await _monitor.StopAsync().ConfigureAwait(true);
        _monitor.Dispose();
        _monitor = null;
    }

    private async void OnFormClosing(object? sender, FormClosingEventArgs e)
    {
        _closing = true;
        StopLogcat();
        StopMirror();
        await StopMonitorAsync().ConfigureAwait(true);
    }
}
