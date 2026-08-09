using System.Drawing;
using System.Windows.Forms;
using PhoneDebug.Core;
using PhoneDebug.Core.Diagnostics;
using PhoneDebug.Core.Services;
using PhoneDebug.Core.Tools;

namespace PhoneDebug.App;

/// <summary>
/// Shown when there is no phone: the USB steps, and a QR code the phone can
/// scan to pair over Wi-Fi. Closes itself as soon as a device turns up,
/// whichever way it arrived.
/// </summary>
internal sealed class ConnectForm : Form
{
    private readonly PhoneDebugContext _core;
    private readonly CancellationTokenSource _cts = new();
    private readonly System.Windows.Forms.Timer _poll = new() { Interval = 2000 };

    private readonly Label _status = new();
    private readonly PictureBox _qr = new();
    private readonly Panel _codePanel = new();
    private readonly TextBox _address = new();
    private readonly TextBox _code = new();
    private readonly LinkLabel _toggleCode = new();

    private PairingRequest? _request;
    private bool _closing;

    public ConnectForm(PhoneDebugContext core)
    {
        _core = core;

        Text = "Connect a phone";
        BackColor = Theme.Background;
        ForeColor = Theme.Text;
        Font = Theme.Body;
        FormBorderStyle = FormBorderStyle.Sizable;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowIcon = false;
        StartPosition = FormStartPosition.CenterParent;
        ClientSize = new Size(430, 700);
        MinimumSize = new Size(380, 420);
        AutoScroll = true;

        Controls.Add(BuildLayout());

        Load += async (_, _) => await StartAsync().ConfigureAwait(true);
        FormClosing += (_, _) =>
        {
            _closing = true;
            _poll.Stop();
            _cts.Cancel();
        };
    }

    private Control BuildLayout()
    {
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            AutoSize = true,
            Padding = new Padding(24, 20, 24, 20),
            BackColor = Theme.Background,
        };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        // Wi-Fi first: the QR code is the thing people came here for, and it
        // stays visible without scrolling.
        AddRow(root, Heading("Over Wi-Fi   (Android 11 and newer)"));
        AddRow(root, Body(
            "Developer options > Wireless debugging > Pair device with QR code,\n" +
            "then point the camera here:"));

        _qr.Size = new Size(220, 220);
        _qr.SizeMode = PictureBoxSizeMode.Zoom;
        _qr.BackColor = Color.White;
        _qr.Anchor = AnchorStyles.None;
        _qr.Margin = new Padding(0, 8, 0, 8);
        AddRow(root, _qr);

        _status.Text = "Preparing...";
        _status.ForeColor = Theme.Muted;
        _status.AutoSize = true;
        _status.Margin = new Padding(0, 0, 0, 8);
        AddRow(root, _status);

        _toggleCode.Text = "Type a pairing code instead";
        _toggleCode.Font = Theme.Small;
        _toggleCode.AutoSize = true;
        _toggleCode.LinkColor = Theme.Accent;
        _toggleCode.LinkClicked += (_, _) => _codePanel.Visible = !_codePanel.Visible;
        AddRow(root, _toggleCode);

        AddRow(root, BuildCodePanel());

        AddRow(root, Separator());

        AddRow(root, Heading("Over USB"));
        AddRow(root, Body(
            "1.  Settings > About phone - tap \"Build number\" seven times\n" +
            "2.  Developer options - turn on \"USB debugging\"\n" +
            "3.  Plug the phone in with a data cable\n" +
            "4.  Accept \"Allow USB debugging\" on the phone"));

        return root;
    }

    private Control BuildCodePanel()
    {
        _codePanel.AutoSize = true;
        _codePanel.Visible = false;
        _codePanel.Margin = new Padding(0, 8, 0, 0);

        var layout = new TableLayoutPanel
        {
            ColumnCount = 1,
            AutoSize = true,
            Dock = DockStyle.Top,
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        var hint = Body("Tap \"Pair device with pairing code\" on the phone and copy what it shows.");

        _address.PlaceholderText = "IP address & port, e.g. 192.168.0.10:37123";
        _address.Width = 340;
        _address.Margin = new Padding(0, 6, 0, 4);

        _code.PlaceholderText = "6-digit pairing code";
        _code.Width = 340;
        _code.Margin = new Padding(0, 0, 0, 6);

        var pair = Theme.PrimaryButton("Pair");
        pair.Height = 34;
        pair.Width = 340;
        pair.Dock = DockStyle.None;
        pair.Click += async (_, _) => await PairWithCodeAsync().ConfigureAwait(true);

        layout.Controls.Add(hint);
        layout.Controls.Add(_address);
        layout.Controls.Add(_code);
        layout.Controls.Add(pair);

        _codePanel.Controls.Add(layout);
        return _codePanel;
    }

    // ---------------------------------------------------------------- flow

    private async Task StartAsync()
    {
        // A phone plugged in over USB should close this window too.
        _poll.Tick += (_, _) => CheckForDevice();
        _poll.Start();

        if (_core.Pairing is null)
        {
            _status.Text = "ADB is not available.";
            _qr.Visible = false;
            return;
        }

        try
        {
            _request = WirelessPairing.CreateRequest();
            _qr.Image = Render(QrCode.Encode(_request.QrPayload), _qr.Width);
            _status.Text = "Waiting for the phone to scan...";
        }
        catch (Exception ex)
        {
            Log.Error("Could not build the pairing QR code", ex);
            _qr.Visible = false;
            _status.Text = "The QR code could not be drawn. Use a pairing code instead.";
            _codePanel.Visible = true;
            return;
        }

        var progress = new Progress<string>(message =>
        {
            if (!_closing)
                _status.Text = message;
        });

        try
        {
            var result = await _core.Pairing
                .PairWithQrAsync(_request, progress, TimeSpan.FromMinutes(5), _cts.Token)
                .ConfigureAwait(true);

            if (_closing)
                return;

            if (result.Success)
            {
                Done();
                return;
            }

            _status.ForeColor = Theme.Danger;
            _status.Text = result.Message;
        }
        catch (OperationCanceledException)
        {
            // window closed
        }
        catch (Exception ex)
        {
            Log.Error("Wireless pairing failed", ex);
            if (!_closing)
            {
                _status.ForeColor = Theme.Danger;
                _status.Text = "Pairing failed. See the log file.";
            }
        }
    }

    private async Task PairWithCodeAsync()
    {
        if (_core.Pairing is null)
            return;

        _status.ForeColor = Theme.Muted;
        _status.Text = "Pairing...";

        var address = _address.Text;
        var code = _code.Text;

        var paired = await Task.Run(() => _core.Pairing.PairWithCode(address, code)).ConfigureAwait(true);
        if (_closing)
            return;

        if (!paired.Success)
        {
            _status.ForeColor = Theme.Danger;
            _status.Text = paired.Message;
            return;
        }

        _status.ForeColor = Theme.Muted;
        _status.Text = "Paired. Waiting for the device...";

        try
        {
            var connected = await _core.Pairing.WaitForDeviceAsync(null, _cts.Token).ConfigureAwait(true);
            if (_closing)
                return;

            if (connected.Success)
                Done();
            else
            {
                _status.ForeColor = Theme.Danger;
                _status.Text = connected.Message;
            }
        }
        catch (OperationCanceledException)
        {
            // window closed
        }
    }

    private void CheckForDevice()
    {
        if (_closing || _core.Devices is null)
            return;

        try
        {
            if (_core.Devices.ListDevices().Any(d => d.IsAuthorized))
                Done();
        }
        catch (Exception ex)
        {
            Log.Warn($"Device check failed while connecting: {ex.Message}");
        }
    }

    private void Done()
    {
        if (_closing)
            return;

        _closing = true;
        _poll.Stop();
        DialogResult = DialogResult.OK;
        Close();
    }

    // ---------------------------------------------------------------- drawing

    private static Bitmap Render(QrCode qr, int pixels)
    {
        var scale = Math.Max(2, pixels / qr.Size);
        var size = scale * qr.Size;

        var bitmap = new Bitmap(size, size);
        using var graphics = Graphics.FromImage(bitmap);
        graphics.Clear(Color.White);

        using var dark = new SolidBrush(Color.Black);
        for (var y = 0; y < qr.Size; y++)
        for (var x = 0; x < qr.Size; x++)
        {
            if (qr.IsDark(x, y))
                graphics.FillRectangle(dark, x * scale, y * scale, scale, scale);
        }

        return bitmap;
    }

    private static void AddRow(TableLayoutPanel table, Control control)
    {
        table.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        table.Controls.Add(control, 0, table.RowStyles.Count - 1);
        table.RowCount = table.RowStyles.Count;
    }

    private static Label Heading(string text) => new()
    {
        Text = text,
        Font = Theme.Section,
        ForeColor = Theme.Text,
        AutoSize = true,
        Margin = new Padding(0, 0, 0, 6),
    };

    private static Label Body(string text) => new()
    {
        Text = text,
        ForeColor = Theme.Subtle,
        AutoSize = true,
        Margin = new Padding(0, 0, 0, 10),
    };

    private static Control Separator() => new Panel
    {
        Height = 1,
        Dock = DockStyle.Top,
        BackColor = Theme.Border,
        Margin = new Padding(0, 6, 0, 12),
    };

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _cts.Dispose();
            _poll.Dispose();
            _qr.Image?.Dispose();
        }

        base.Dispose(disposing);
    }
}
