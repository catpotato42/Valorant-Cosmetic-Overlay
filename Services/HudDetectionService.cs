using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Drawing;
using System.Drawing.Imaging;

namespace VALORANT_Overlay.Services;
public class HudDetectionService
{
    public List<DetectionRegion> Regions { get; }

    public HudDetectionService(List<DetectionRegion> sharedRegions)
    {
        Regions = sharedRegions ?? throw new ArgumentNullException(nameof(sharedRegions));
    }

    /// <summary>
    /// Detect weapon type based on the WeaponText region
    /// </summary>
    public string DetectWeapon()
    {
        var weaponRegion = Regions.FirstOrDefault(r => r.Name == "WeaponText")?.Bounds;
        if (weaponRegion == null) return "main";

        using var bmp = CaptureRegion(weaponRegion.Value);
        if (bmp == null) return "main";

        // TODO: run OCR/template matching
        return "main"; // placeholder
    }

    /// <summary>
    /// Detect if a kill occurred based on the KillText region
    /// </summary>
    public bool DetectKill()
    {
        var killRegion = Regions.FirstOrDefault(r => r.Name == "KillText")?.Bounds;
        if (killRegion == null) return false;

        using var bmp = CaptureRegion(killRegion.Value);
        if (bmp == null) return false;

        // TODO: run OCR/template matching for kill text
        return false; // placeholder
    }

    /// <summary>
    /// Capture a region of the screen
    /// </summary>
    private Bitmap CaptureRegion(Rect bounds)
    {
        try
        {
            var bmp = new Bitmap((int)bounds.Width, (int)bounds.Height);
            using (var g = Graphics.FromImage(bmp))
            {
                g.CopyFromScreen((int)bounds.X, (int)bounds.Y, 0, 0, new System.Drawing.Size((int)bounds.Width, (int)bounds.Height));
            }
            return bmp;
        }
        catch
        {
            return null;
        }
    }
}
