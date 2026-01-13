using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Drawing;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Drawing.Imaging;

namespace VALORANT_Overlay.Services;
public class HudDetectionService
{
    public List<DetectionRegion> Regions { get; }

    public HudDetectionService(List<DetectionRegion> sharedRegions)
    {
        Regions = sharedRegions ?? throw new ArgumentNullException(nameof(sharedRegions));
    }
    private WriteableBitmap bmpWeapon;
    private byte[] _weaponPixels;
    private int _lastWeaponScanTime;

    public string DetectWeapon()
    {
        var region = Regions.Find(r => r.Name == "WeaponText");
        if (region == null) return "main";

        int now = Environment.TickCount;

        // throttle like DetectKill
        if (unchecked(now - _lastWeaponScanTime) < 100)
            return "main";
        _lastWeaponScanTime = now;

        bmpWeapon = CaptureRegion(region.Bounds);

        int width = bmpWeapon.PixelWidth;
        int height = bmpWeapon.PixelHeight;
        int stride = width * 4;
        int needed = height * stride;

        if (_weaponPixels == null || _weaponPixels.Length < needed)
            _weaponPixels = new byte[needed];

        bmpWeapon.CopyPixels(_weaponPixels, stride, 0);

        const int hStep = 2;
        const int minRun = 3;
        byte targetR = 210;
        byte targetG = 210;
        byte targetB = 210;
        int tolerance = 5;


        int topMostHitY = int.MaxValue;

        for (int x = 0; x < width; x += hStep)
        {
            int run = 0;

            for (int y = 0; y < height; y++)
            {
                int idx = y * stride + x * 4;
                if ((uint)idx >= (uint)_weaponPixels.Length - 4)
                    continue;

                byte b = _weaponPixels[idx + 0];
                byte g = _weaponPixels[idx + 1];
                byte r = _weaponPixels[idx + 2];

                bool isWhite =
                    Math.Abs(r - targetR) <= tolerance + 3 && //red tolerance slightly higher because often for melee it breaks
                    Math.Abs(g - targetG) <= tolerance &&
                    Math.Abs(b - targetB) <= tolerance;


                if (isWhite)
                {
                    run++;
                    if (run >= minRun)
                    {
                        int startY = y - run + 1;
                        if (startY < topMostHitY)
                            topMostHitY = startY;
                        break;
                    }
                }
                else
                {
                    run = 0;
                }
            }
        }

        if (topMostHitY == int.MaxValue)
            return "__none__";

        int third = height / 3;

        if (topMostHitY < third)
            return "knife";
        if (topMostHitY < third * 2)
            return "pistol";
        return "main";
    }

    private int _lastScanTime;
    private WriteableBitmap bmpKill;
    private byte[] _killPixels;
    private int _lastKillTime = 0; // tick count of last detected kill

    public bool DetectKill()
    {
        var region = Regions.Find(r => r.Name == "KillText");
        if (region == null) return false;

        int now = Environment.TickCount;

        // Global cooldown: only detect once every 5 seconds
        if (unchecked(now - _lastKillTime) < 5000)
            return false;

        // Throttle captures slightly
        if (unchecked(now - _lastScanTime) < 100)
            return false;
        _lastScanTime = now;

        bmpKill = CaptureRegion(region.Bounds);

        int stride = bmpKill.PixelWidth * 4;
        int needed = bmpKill.PixelHeight * stride;
        if (_killPixels == null || _killPixels.Length < needed)
            _killPixels = new byte[needed];
        bmpKill.CopyPixels(_killPixels, stride, 0);

        int hStep = 2; // horizontal pixel movement
        byte targetR = 231, targetG = 236, targetB = 119;
        const int minRun = 12;
        int tolerance = 20;

        for (int x = 0; x < bmpKill.PixelWidth; x += hStep)
        {
            for (int y = 0; y < bmpKill.PixelHeight; y++)
            {
                int idx = y * stride + x * 4;
                if ((uint)idx >= (uint)_killPixels.Length - 4)
                    continue;

                byte b = _killPixels[idx + 0];
                byte g = _killPixels[idx + 1];
                byte r = _killPixels[idx + 2];

                if (Math.Abs(r - targetR) <= tolerance &&
                    Math.Abs(g - targetG) <= tolerance &&
                    Math.Abs(b - targetB) <= tolerance)
                {
                    // downward run check with allowed misses
                    int run = 1, misses = 0;
                    int maxMisses = 2;
                    for (int dy = 1; dy < minRun && y + dy < bmpKill.PixelHeight; dy++)
                    {
                        int idx2 = (y + dy) * stride + x * 4;
                        if ((uint)idx2 >= (uint)_killPixels.Length - 4)
                            break;

                        byte r2 = _killPixels[idx2 + 2];
                        byte g2 = _killPixels[idx2 + 1];
                        byte b2 = _killPixels[idx2 + 0];

                        if (Math.Abs(r2 - targetR) <= tolerance &&
                            Math.Abs(g2 - targetG) <= tolerance &&
                            Math.Abs(b2 - targetB) <= tolerance)
                        {
                            run++;
                        }
                        else
                        {
                            misses++;
                            if (misses > maxMisses)
                                break;
                        }
                    }

                    if (run >= minRun)
                    {
                        _lastKillTime = now; // start cooldown
                        return true; // kill detected
                    }
                }
            }
        }

        return false;
    }

    private void DumpKillRegionPixels()
    {
        if (bmpKill == null) return;

        int stride = bmpKill.PixelWidth * 4;
        int needed = bmpKill.PixelHeight * stride;
        if (_killPixels == null || _killPixels.Length < needed)
            _killPixels = new byte[needed];
        bmpKill.CopyPixels(_killPixels, stride, 0);

        string path = Path.Combine(Environment.CurrentDirectory, "KillPixelsDump.txt");
        using (StreamWriter sw = new(path))
        {
            for (int x = 0; x < bmpKill.PixelWidth; x++)
            {
                sw.WriteLine($"Column {x}:");
                for (int y = 0; y < bmpKill.PixelHeight; y++)
                {
                    int idx = y * stride + x * 4;
                    if ((uint)idx >= (uint)_killPixels.Length - 4) continue;

                    byte b = _killPixels[idx + 0];
                    byte g = _killPixels[idx + 1];
                    byte r = _killPixels[idx + 2];
                    sw.WriteLine($"y={y} R={r} G={g} B={b}");
                }
                sw.WriteLine();
            }
        }
    }

    
    private WriteableBitmap CaptureRegion(Rect bounds)
    {
        Bitmap bmp = new Bitmap((int)bounds.Width, (int)bounds.Height, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        using (Graphics g = Graphics.FromImage(bmp))
        {
            g.CopyFromScreen((int)bounds.X, (int)bounds.Y, 0, 0, bmp.Size, CopyPixelOperation.SourceCopy);
        }

        // Convert to WPF WriteableBitmap
        var wb = new WriteableBitmap(bmp.Width, bmp.Height, 96, 96, System.Windows.Media.PixelFormats.Bgra32, null);
        var data = bmp.LockBits(new Rectangle(0, 0, bmp.Width, bmp.Height),
                                ImageLockMode.ReadOnly, bmp.PixelFormat);
        wb.WritePixels(new Int32Rect(0, 0, bmp.Width, bmp.Height), data.Scan0, data.Stride * bmp.Height, data.Stride);
        bmp.UnlockBits(data);
        bmp.Dispose();
        return wb;
    }
}
