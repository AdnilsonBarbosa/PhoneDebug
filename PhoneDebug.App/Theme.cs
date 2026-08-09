using System.Drawing;
using System.Windows.Forms;

namespace PhoneDebug.App;

/// <summary>One place for the few colours and fonts the window uses.</summary>
internal static class Theme
{
    public static readonly Color Background = Color.White;
    public static readonly Color Text = Color.FromArgb(17, 24, 39);
    public static readonly Color Muted = Color.FromArgb(107, 114, 128);
    public static readonly Color Border = Color.FromArgb(226, 232, 240);
    public static readonly Color Accent = Color.FromArgb(37, 99, 235);
    public static readonly Color AccentHover = Color.FromArgb(29, 78, 216);
    public static readonly Color Connected = Color.FromArgb(22, 163, 74);
    public static readonly Color Danger = Color.FromArgb(220, 38, 38);
    public static readonly Color LogBackground = Color.FromArgb(249, 250, 251);

    public static readonly Font Title = new("Segoe UI Semibold", 15f, FontStyle.Bold);
    public static readonly Font DeviceName = new("Segoe UI Semibold", 12f, FontStyle.Bold);
    public static readonly Font Body = new("Segoe UI", 9.75f);
    public static readonly Font Small = new("Segoe UI", 8.5f);
    public static readonly Font Status = new("Segoe UI", 10.5f);
    public static readonly Font Mono = new("Consolas", 8.5f);

    public static Button PrimaryButton(string text)
    {
        var button = BaseButton(text);
        button.BackColor = Accent;
        button.ForeColor = Color.White;
        button.FlatAppearance.MouseOverBackColor = AccentHover;
        button.Font = new Font("Segoe UI Semibold", 10f, FontStyle.Bold);
        return button;
    }

    public static Button SecondaryButton(string text)
    {
        var button = BaseButton(text);
        button.BackColor = Color.White;
        button.ForeColor = Text;
        button.FlatAppearance.BorderSize = 1;
        button.FlatAppearance.BorderColor = Color.FromArgb(209, 213, 219);
        button.FlatAppearance.MouseOverBackColor = Color.FromArgb(243, 244, 246);
        return button;
    }

    private static Button BaseButton(string text) => new()
    {
        Text = text,
        FlatStyle = FlatStyle.Flat,
        Font = Body,
        Cursor = Cursors.Hand,
        Dock = DockStyle.Fill,
        Margin = new Padding(0),
        UseVisualStyleBackColor = false,
        FlatAppearance = { BorderSize = 0 },
    };
}
