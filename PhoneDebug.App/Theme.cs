using System.Drawing;
using System.Windows.Forms;

namespace PhoneDebug.App;

/// <summary>One place for the colours and fonts the window uses.</summary>
internal static class Theme
{
    // Background shades: a very soft grey window behind white cards.
    public static readonly Color Background = Color.FromArgb(247, 248, 250);
    public static readonly Color Raised = Color.White;
    public static readonly Color Muted = Color.FromArgb(107, 114, 128);
    public static readonly Color Border = Color.FromArgb(229, 232, 236);

    // Text
    public static readonly Color Text = Color.FromArgb(24, 29, 38);
    public static readonly Color Subtle = Color.FromArgb(120, 127, 138);

    // Accent
    public static readonly Color Accent = Color.FromArgb(64, 48, 210);
    public static readonly Color AccentHover = Color.FromArgb(54, 38, 180);
    public static readonly Color AccentPressed = Color.FromArgb(44, 30, 150);

    // State
    public static readonly Color Connected = Color.FromArgb(23, 160, 90);
    public static readonly Color Danger = Color.FromArgb(220, 52, 69);
    public static readonly Color Warn = Color.FromArgb(217, 142, 20);

    // Log
    public static readonly Color LogBackground = Color.FromArgb(250, 251, 252);

    public static readonly Font Title = new("Segoe UI Semibold", 16f, FontStyle.Bold);
    public static readonly Font Section = new("Segoe UI Semibold", 10.5f, FontStyle.Bold);
    public static readonly Font DeviceName = new("Segoe UI Semibold", 12f, FontStyle.Bold);
    public static readonly Font Body = new("Segoe UI", 9.75f);
    public static readonly Font Small = new("Segoe UI", 8.5f);
    public static readonly Font Status = new("Segoe UI", 9.5f);
    public static readonly Font Mono = new("Consolas", 8.5f);

    public static RoundedButton PrimaryButton(string text) => new()
    {
        Text = text,
        BackColor = Accent,
        ForeColor = Color.White,
        HoverColor = AccentHover,
        PressedColor = AccentPressed,
        Font = new Font("Segoe UI Semibold", 10f, FontStyle.Bold),
        Cursor = Cursors.Hand,
        Dock = DockStyle.Fill,
        Margin = new Padding(0),
    };

    public static RoundedButton SecondaryButton(string text) => new()
    {
        Text = text,
        BackColor = Raised,
        ForeColor = Text,
        HoverColor = Color.FromArgb(244, 245, 248),
        PressedColor = Color.FromArgb(235, 237, 241),
        Font = Body,
        Cursor = Cursors.Hand,
        Dock = DockStyle.Fill,
        Margin = new Padding(0),
    };
}