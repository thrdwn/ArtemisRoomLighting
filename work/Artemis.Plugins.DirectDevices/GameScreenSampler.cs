using System.Runtime.InteropServices;

namespace Artemis.Plugins.DirectDevices;

internal sealed class GameScreenSampler : IDisposable
{
    private const int Srccopy = 0x00CC0020;
    private const int DibRgbColors = 0;
    private const int BiRgb = 0;
    private const int SmCxScreen = 0;
    private const int SmCyScreen = 1;

    private readonly int _width;
    private readonly int _height;
    private readonly IntPtr _screenDc;
    private readonly IntPtr _memoryDc;
    private readonly IntPtr _bitmap;
    private readonly IntPtr _previousObject;
    private readonly byte[] _pixels;
    private bool _captureValid;

    public GameScreenSampler(int width = 160, int height = 90)
    {
        _width = width;
        _height = height;
        _pixels = new byte[width * height * 4];
        _screenDc = GetDC(IntPtr.Zero);
        _memoryDc = CreateCompatibleDC(_screenDc);
        _bitmap = CreateCompatibleBitmap(_screenDc, width, height);
        _previousObject = SelectObject(_memoryDc, _bitmap);
        SetStretchBltMode(_memoryDc, 4);
    }

    public bool Capture()
    {
        int sourceWidth = Math.Max(1, GetSystemMetrics(SmCxScreen));
        int sourceHeight = Math.Max(1, GetSystemMetrics(SmCyScreen));
        StretchBlt(_memoryDc, 0, 0, _width, _height, _screenDc, 0, 0, sourceWidth, sourceHeight, Srccopy);

        BitmapInfo bitmapInfo = new()
        {
            Header = new BitmapInfoHeader
            {
                Size = Marshal.SizeOf<BitmapInfoHeader>(),
                Width = _width,
                Height = -_height,
                Planes = 1,
                BitCount = 32,
                Compression = BiRgb
            }
        };

        _captureValid = GetDIBits(
            _memoryDc,
            _bitmap,
            0,
            (uint)_height,
            _pixels,
            ref bitmapInfo,
            DibRgbColors) != 0;
        return _captureValid;
    }

    public ScreenRegionStats Sample(double startX, double endX, double startY, double endY)
    {
        if (!_captureValid)
            return default;

        int left = Math.Clamp((int)Math.Floor(startX * _width), 0, _width - 1);
        int right = Math.Clamp((int)Math.Ceiling(endX * _width), left + 1, _width);
        int top = Math.Clamp((int)Math.Floor(startY * _height), 0, _height - 1);
        int bottom = Math.Clamp((int)Math.Ceiling(endY * _height), top + 1, _height);

        long rTotal = 0;
        long gTotal = 0;
        long bTotal = 0;
        int redPixels = 0;
        int tealPixels = 0;
        int greenPixels = 0;
        int brightPixels = 0;
        int count = 0;

        for (int y = top; y < bottom; y++)
        {
            int rowOffset = y * _width * 4;
            for (int x = left; x < right; x++)
            {
                int offset = rowOffset + (x * 4);
                int b = _pixels[offset];
                int g = _pixels[offset + 1];
                int r = _pixels[offset + 2];
                int max = Math.Max(r, Math.Max(g, b));
                int min = Math.Min(r, Math.Min(g, b));

                rTotal += r;
                gTotal += g;
                bTotal += b;
                count++;

                if (r >= 105 && r > g * 1.35 && r > b * 1.20)
                    redPixels++;
                if (g >= 95 && b >= 85 && g > r * 1.18 && b > r * 1.10)
                    tealPixels++;
                if (g >= 100 && g > r * 1.28 && g > b * 1.15)
                    greenPixels++;
                if (max >= 170 && max - min >= 25)
                    brightPixels++;
            }
        }

        if (count == 0)
            return default;

        double rAverage = rTotal / (double)count;
        double gAverage = gTotal / (double)count;
        double bAverage = bTotal / (double)count;
        return new ScreenRegionStats(
            rAverage,
            gAverage,
            bAverage,
            ((rAverage * 0.2126) + (gAverage * 0.7152) + (bAverage * 0.0722)) / 255.0,
            redPixels / (double)count,
            tealPixels / (double)count,
            greenPixels / (double)count,
            brightPixels / (double)count);
    }

    public void Dispose()
    {
        SelectObject(_memoryDc, _previousObject);
        DeleteObject(_bitmap);
        DeleteDC(_memoryDc);
        ReleaseDC(IntPtr.Zero, _screenDc);
    }

    [DllImport("user32.dll")]
    private static extern IntPtr GetDC(IntPtr hwnd);

    [DllImport("user32.dll")]
    private static extern int ReleaseDC(IntPtr hwnd, IntPtr dc);

    [DllImport("user32.dll")]
    private static extern int GetSystemMetrics(int index);

    [DllImport("gdi32.dll")]
    private static extern IntPtr CreateCompatibleDC(IntPtr dc);

    [DllImport("gdi32.dll")]
    private static extern IntPtr CreateCompatibleBitmap(IntPtr dc, int width, int height);

    [DllImport("gdi32.dll")]
    private static extern IntPtr SelectObject(IntPtr dc, IntPtr obj);

    [DllImport("gdi32.dll")]
    private static extern bool DeleteObject(IntPtr obj);

    [DllImport("gdi32.dll")]
    private static extern bool DeleteDC(IntPtr dc);

    [DllImport("gdi32.dll")]
    private static extern bool SetStretchBltMode(IntPtr dc, int mode);

    [DllImport("gdi32.dll")]
    private static extern bool StretchBlt(
        IntPtr destDc,
        int destX,
        int destY,
        int destWidth,
        int destHeight,
        IntPtr sourceDc,
        int sourceX,
        int sourceY,
        int sourceWidth,
        int sourceHeight,
        int rasterOperation);

    [DllImport("gdi32.dll")]
    private static extern int GetDIBits(
        IntPtr dc,
        IntPtr bitmap,
        uint startScan,
        uint scanLines,
        byte[] bits,
        ref BitmapInfo bitmapInfo,
        int usage);

    [StructLayout(LayoutKind.Sequential)]
    private struct BitmapInfo
    {
        public BitmapInfoHeader Header;
        public uint Colors;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct BitmapInfoHeader
    {
        public int Size;
        public int Width;
        public int Height;
        public short Planes;
        public short BitCount;
        public int Compression;
        public int SizeImage;
        public int XPelsPerMeter;
        public int YPelsPerMeter;
        public int ClrUsed;
        public int ClrImportant;
    }
}

internal readonly record struct ScreenRegionStats(
    double R,
    double G,
    double B,
    double Luminance,
    double RedRatio,
    double TealRatio,
    double GreenRatio,
    double BrightRatio);
