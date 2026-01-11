using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;

namespace VALORANT_Overlay.Services
{
    public class DetectionRegion
    {
        public string Name { get; set; } = "Region";
        public Rect Bounds { get; set; } = new Rect(0, 0, 200, 50);
    }

    public class SettingsModeController
    {
        private readonly Canvas _canvas;
        private readonly string _configPath = "regions.json";
        public List<DetectionRegion> Regions { get; private set; } = new();

        private Rectangle _activeRect;
        private Point _dragStart;

        private bool _isActive = false;

        public SettingsModeController(Canvas canvas)
        {
            _canvas = canvas;
            LoadRegions();
        }

        public void ToggleSettingsMode()
        {
            _isActive = !_isActive;

            if (!_isActive)
            {
                SaveRegions();
            }

            // Remove rectangles and redraw if entering settings
            foreach (var child in _canvas.Children.OfType<Rectangle>().ToList())
                _canvas.Children.Remove(child);

            if (_isActive)
                DrawRegions();
        }

        private void DrawRegions()
        {
            foreach (var region in Regions)
            {
                var rect = new Rectangle
                {
                    Stroke = Brushes.White,
                    StrokeThickness = 2,
                    Fill = new SolidColorBrush(Color.FromArgb(40, 255, 255, 255)),
                    Width = region.Bounds.Width,
                    Height = region.Bounds.Height,
                    Tag = region
                };

                Canvas.SetLeft(rect, region.Bounds.X);
                Canvas.SetTop(rect, region.Bounds.Y);

                rect.MouseLeftButtonDown += Rect_MouseDown;
                rect.MouseMove += Rect_MouseMove;
                rect.MouseLeftButtonUp += Rect_MouseUp;

                _canvas.Children.Add(rect);
            }
        }

        private void Rect_MouseDown(object sender, MouseButtonEventArgs e)
        {
            _activeRect = sender as Rectangle;
            _dragStart = e.GetPosition(_canvas);
            _activeRect.CaptureMouse();
        }

        private void Rect_MouseMove(object sender, MouseEventArgs e)
        {
            if (_activeRect == null || !_activeRect.IsMouseCaptured) return;

            Point pos = e.GetPosition(_canvas);
            double dx = pos.X - _dragStart.X;
            double dy = pos.Y - _dragStart.Y;

            Canvas.SetLeft(_activeRect, Canvas.GetLeft(_activeRect) + dx);
            Canvas.SetTop(_activeRect, Canvas.GetTop(_activeRect) + dy);

            if (_activeRect.Tag is DetectionRegion region)
            {
                region.Bounds = new Rect(Canvas.GetLeft(_activeRect), Canvas.GetTop(_activeRect),
                                         _activeRect.Width, _activeRect.Height);
            }

            _dragStart = pos;
        }

        private void Rect_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (_activeRect != null)
            {
                _activeRect.ReleaseMouseCapture();
                _activeRect = null;
            }
        }

        public void LoadRegions()
        {
            if (File.Exists(_configPath))
            {
                string json = File.ReadAllText(_configPath);
                Regions = JsonSerializer.Deserialize<List<DetectionRegion>>(json) ?? new List<DetectionRegion>();
            }
            else
            {
                Regions = new List<DetectionRegion>();
            }

            // Ensure default regions exist
            if (!Regions.Any(r => r.Name == "WeaponText"))
                Regions.Add(new DetectionRegion { Name = "WeaponText", Bounds = new Rect(0, 0, 200, 500) });

            if (!Regions.Any(r => r.Name == "KillText"))
                Regions.Add(new DetectionRegion { Name = "KillText", Bounds = new Rect(300, 0, 300, 400) });

            if (!Regions.Any(r => r.Name == "Animation"))
                Regions.Add(new DetectionRegion { Name = "Animation", Bounds = new Rect(100, 100, 200, 200) });
        }

        public void SaveRegions()
        {
            foreach (var child in _canvas.Children.OfType<Rectangle>())
            {
                if (child.Tag is DetectionRegion region)
                {
                    region.Bounds = new Rect(Canvas.GetLeft(child), Canvas.GetTop(child),
                                             child.Width, child.Height);
                }
            }

            string json = JsonSerializer.Serialize(Regions, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_configPath, json);
        }
    }
}
