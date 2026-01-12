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

    public string DetectWeapon()
    {
        var weaponRegion = Regions.FirstOrDefault(r => r.Name == "WeaponText")?.Bounds;
        if (weaponRegion == null) return "main";

        //using var bmp = CaptureRegion(weaponRegion.Value);
        //if (bmp == null) return "main";

        // TODO: run OCR/template matching
        return "main"; // placeholder
    }

    private int _exclusionY = 0;
    private int _exclusionExpire = 0;
    WriteableBitmap bmpKill;
    byte[] _killPixels;
    int _lastScanTime;
    public bool DetectKill()
    {
        var region = Regions.Find(r => r.Name == "KillText");
        if (region == null) return false;

        int now = Environment.TickCount;
        if (unchecked(now - _lastScanTime) < 100)
            return false;
        _lastScanTime = now;
        if (unchecked(now - _exclusionExpire) > 0)
            _exclusionY = Math.Max(0, (int)region.Bounds.Top);

        bmpKill ??= CaptureRegion(region.Bounds);

        int stride = bmpKill.PixelWidth * 4;
        int needed = bmpKill.PixelHeight * stride;
        if (_killPixels == null || _killPixels.Length < needed)
            _killPixels = new byte[needed];
        bmpKill.CopyPixels(_killPixels, stride, 0);
        if (_killPixels[0] != 0)
            return true; // debug: always detect

        int hStep = 5; //horizontal pixel movement
        int vStep = 3; //vertical pixel movement

        _exclusionY = Math.Clamp(_exclusionY, 0, bmpKill.PixelHeight - 1);

        byte targetR = 231, targetG = 236, targetB = 119;
        int tolerance = 10;
        for (int x = 0; x < bmpKill.PixelWidth; x += hStep)
        {
            for (int y = _exclusionY; y < bmpKill.PixelHeight; y += vStep)
            {
                int idx = y * stride + x * 4;
                if ((uint)idx >= (uint)_killPixels.Length - 4) 
                    continue;
                byte b = _killPixels[idx + 0], g = _killPixels[idx + 1], r = _killPixels[idx + 2];

                if (Math.Abs(r - targetR) <= tolerance &&
                    Math.Abs(g - targetG) <= tolerance &&
                    Math.Abs(b - targetB) <= tolerance)
                {
                    //check downward strip for valid kill banner
                    int run = 1;
                    for (int dy = 1; dy < 20 && y + dy < bmpKill.PixelHeight; dy++)
                    {
                        int idx2 = (y + dy) * stride + x * 4;
                        if ((uint)idx2 >= (uint)_killPixels.Length - 4) 
                            break;
                        byte r2 = _killPixels[idx2 + 2], g2 = _killPixels[idx2 + 1], b2 = _killPixels[idx2 + 0];
                        if (Math.Abs(r2 - targetR) <= tolerance &&
                            Math.Abs(g2 - targetG) <= tolerance &&
                            Math.Abs(b2 - targetB) <= tolerance) 
                            {run++;}
                        else {
                            break;
                        }
                    }

                    if (run >= 20)
                    {
                        _exclusionY = y;
                        _exclusionExpire = now + 5000;
                        return true; // kill detected
                    }
                }
            }
        }
        return false;
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
