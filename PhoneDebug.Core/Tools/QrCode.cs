using QRCoder;

namespace PhoneDebug.Core.Tools;

/// <summary>
/// A QR code as a plain grid of dark/light modules. Core does the encoding so
/// the terminal and the window can each draw it their own way.
/// </summary>
public sealed class QrCode
{
    private readonly bool[,] _modules;

    private QrCode(bool[,] modules, int size)
    {
        _modules = modules;
        Size = size;
    }

    /// <summary>Width and height in modules, quiet zone included.</summary>
    public int Size { get; }

    public bool IsDark(int x, int y) =>
        x >= 0 && y >= 0 && x < Size && y < Size && _modules[x, y];

    public static QrCode Encode(string text)
    {
        using var generator = new QRCodeGenerator();
        var data = generator.CreateQrCode(text, QRCodeGenerator.ECCLevel.M);

        var rows = data.ModuleMatrix;
        var size = rows.Count;
        var modules = new bool[size, size];

        for (var y = 0; y < size; y++)
        {
            var row = rows[y];
            for (var x = 0; x < size && x < row.Length; x++)
                modules[x, y] = row[x];
        }

        return new QrCode(modules, size);
    }
}
