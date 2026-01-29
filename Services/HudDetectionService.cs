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

        const int hStep = 1;
        const int minRun = 3;
        int tolerance = 5;
        
        // Three target RGB values for different weapons (melee, classic, main)
        (byte r, byte g, byte b)[] targetColors = new[]
        {
            ((byte)205, (byte)202, (byte)202), // melee
            ((byte)213, (byte)210, (byte)210), // classic
            ((byte)213, (byte)212, (byte)212)  // main
        };

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

                //red is slightly higher tolerance as it seems to cause the most problems
                bool isWhite = false;
                foreach (var (targetR, targetG, targetB) in targetColors)
                {
                    if (Math.Abs(r - targetR) <= tolerance+1 &&
                        Math.Abs(g - targetG) <= tolerance &&
                        Math.Abs(b - targetB) <= tolerance)
                    {
                        isWhite = true;
                        break;
                    }
                }

                if (isWhite)
                {
                    run++;
                    if (run >= minRun)
                    {
                        int startY = y - run + 1;
                        if (startY < topMostHitY)
                        {
                            topMostHitY = startY;
                        }
                        break;
                    }
                }
                else
                {
                    run = 0;
                }
            }
        }

        // Save debug image when NO hit is detected
        if (topMostHitY == int.MaxValue)
        {
            return "__none__";
        }

        int third = height / 3;
        
        // Save debug image when weapon is detected
        try
        {
            var encoder = new System.Windows.Media.Imaging.PngBitmapEncoder();
            encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(bmpWeapon));
            using (var fs = System.IO.File.Create("weapon_region_debug.png"))
            {
                encoder.Save(fs);
            }
        }
        catch { }

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

    
    public WriteableBitmap CaptureRegion(Rect bounds)
    {
        //Get the current DPI Scale factor from the MainWindow
        double scaleX = 1.0;
        double scaleY = 1.0;

        var source = PresentationSource.FromVisual(Application.Current.MainWindow);
        if (source != null && source.CompositionTarget != null)
        {
            scaleX = source.CompositionTarget.TransformToDevice.M11;
            scaleY = source.CompositionTarget.TransformToDevice.M22;
        }

        //Convert WPF Coordinates (DIPs) to Physical Pixels
        int x = (int)Math.Round(bounds.X * scaleX);
        int y = (int)Math.Round(bounds.Y * scaleY);
        int width = (int)Math.Round(bounds.Width * scaleX);
        int height = (int)Math.Round(bounds.Height * scaleY);

        //Capture using the calculated physical pixels
        using (Bitmap bmp = new Bitmap(width, height, System.Drawing.Imaging.PixelFormat.Format32bppArgb))
        {
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.CopyFromScreen(x, y, 0, 0, bmp.Size, CopyPixelOperation.SourceCopy);
            }
            var wb = new WriteableBitmap(width, height, 96 * scaleX, 96 * scaleY, System.Windows.Media.PixelFormats.Bgra32, null);
            
            var data = bmp.LockBits(new Rectangle(0, 0, width, height),
                                    ImageLockMode.ReadOnly, bmp.PixelFormat);
            
            wb.WritePixels(new Int32Rect(0, 0, width, height), data.Scan0, data.Stride * height, data.Stride);
            bmp.UnlockBits(data);
            
            return wb;
        }
    }
}
