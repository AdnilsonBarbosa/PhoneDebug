using PhoneDebug.Core;

namespace PhoneDebug.Cli.Commands;

internal static class HelpCommand
{
    public static void Show()
    {
        Ui.Blank();
        Ui.Line(AppInfo.Title);
        Ui.Blank();
        ShowUsage();
    }

    public static void ShowUsage()
    {
        Ui.Line("Usage:");
        Ui.Line("  phone-debug [command] [options]");
        Ui.Blank();
        Ui.Line("Commands:");
        Ui.Line("  (none)             Wait for a device and open the screen");
        Ui.Line("  connect            Connect a phone: USB steps, or Wi-Fi with a QR code");
        Ui.Line("  mirror             Mirror and control the Android device");
        Ui.Line("  devices            List connected Android devices");
        Ui.Line("  info               Show device information");
        Ui.Line("  install <apk>      Install an APK");
        Ui.Line("  logs               Show Android logs");
        Ui.Line("  screenshot [path]  Capture a device screenshot");
        Ui.Line("  reboot             Reboot the device");
        Ui.Blank();
        Ui.Line("Connecting:");
        Ui.Line("  connect usb             Show the USB steps and wait for the phone");
        Ui.Line("  connect qr              Pair over Wi-Fi with a QR code");
        Ui.Line("  connect <ip:port>       Connect to an already paired phone");
        Ui.Line("  pair <ip:port> <code>   Pair using the code the phone shows");
        Ui.Blank();
        Ui.Line("Options:");
        Ui.Line("  --light            Smaller, smoother picture - for Wi-Fi");
        Ui.Line("  --emulated         Control the phone as a USB keyboard and mouse");
        Ui.Line("  --standard         Control it the normal way (undoes --emulated)");
        Ui.Line("  -h, --help         Show this help");
        Ui.Line("  -v, --version      Show the version");
        Ui.Blank();
        Ui.Hint("In emulated mode the mouse is captured by the mirror window;");
        Ui.Hint("press left Alt to give it back to Windows.");
        Ui.Blank();
    }
}
