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
        private bool _isResizing;
        private Size _startSize;
        private Point _startPosition;
        private const double ResizeHandleSize = 20;
        private readonly Canvas _canvas;
        private readonly string _configPath = "regions.json";
        public List<DetectionRegion> Regions { get; private set; } = new();

        //DO NOT ADD ANY OTHER PROPERTIES TO THE REGIONS. MUCH OF THIS CODE IS PREDICATED ON _canvas
        //having children in this order: Rectangle, TextBlock, Rectangle, TextBlock..., exitBtn with each
        //TextBlock being the child of the Rectangle before it. If you do add something, it must go *after*
        //the TextBlock and must be added in the foreach loop in DrawRegions.
        private Rectangle _activeRect;
        private Point _dragStart;
        private TextBlock _activeLabel;

        //the event makes mainwindow cleaner
        private bool _isActive = false;
        public event Action<bool>? SettingsModeChanged;
        //this was added later at the end of development
        public bool IsInSettingsMode => _isActive; 


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

            //remove rectangles and redraw if entering settings
            foreach (var child in _canvas.Children.OfType<UIElement>().ToList())
            {
                if (child is Rectangle || child is TextBlock)
                    _canvas.Children.Remove(child);
            }

            if (_isActive)
                DrawRegions();

            SettingsModeChanged?.Invoke(_isActive);
        }

        //called when toggling settings mode
        private void DrawRegions()
        {
            foreach (var region in Regions)
            {
                bool isKill = region.Name == "KillText";
                bool isWeapon = region.Name == "WeaponText";
                var rect = new Rectangle
                {
                    Stroke = Brushes.White,
                    StrokeThickness = 2,
                    Fill = isKill
                        ? new SolidColorBrush(Color.FromArgb(60, 255, 0, 0))     //red
                        : isWeapon
                            ? new SolidColorBrush(Color.FromArgb(60, 0, 255, 0)) //green
                            : new SolidColorBrush(Color.FromArgb(40, 255, 255, 255)),
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

                string label = isKill ? "Kill Banners" : isWeapon ? "Weapon Icons" : region.Name;
                var text = new TextBlock
                {
                    Text = label,
                    Foreground = Brushes.White,
                    FontWeight = FontWeights.Bold,
                    FontSize = 14
                };
                Canvas.SetLeft(text, region.Bounds.X + 4);
                Canvas.SetTop(text, region.Bounds.Y + 2);
                _canvas.Children.Add(text);

                //add new Children here.
            }
            //added at the end because I forgot to, it looks goofy ah lmao I changed it to comic sans
            var exitBtn = new TextBlock { Text = "exit", Foreground = Brushes.Red, FontFamily = new FontFamily("Comic Sans MS"), FontSize = 24, FontWeight = FontWeights.Bold, Cursor = Cursors.Hand };
            Canvas.SetRight(exitBtn, 10); Canvas.SetTop(exitBtn, 10);
            exitBtn.MouseLeftButtonDown += (s, e) => Environment.Exit(0);
            _canvas.Children.Add(exitBtn);
        }

        //for moving regions
        private void Rect_MouseDown(object sender, MouseButtonEventArgs e)
        {
            //sender is always a rectangle, Rect_MouseDown is called by event rect.MouseButtonDown (see DrawRegions in the same file)
            _activeRect = sender as Rectangle;
            _dragStart = e.GetPosition(_canvas);
            _startSize = new Size(_activeRect.Width, _activeRect.Height);
            _startPosition = new Point(Canvas.GetLeft(_activeRect), Canvas.GetTop(_activeRect));

            //find the text label (element after rect)
            int index = _canvas.Children.IndexOf(_activeRect);
            if (index != -1 && index + 1 < _canvas.Children.Count)
                _activeLabel = _canvas.Children[index + 1] as TextBlock;

            //check if bottom right corner was clicked (must be precise)
            Point clickPoint = e.GetPosition(_activeRect);
            _isResizing = clickPoint.X > _activeRect.Width - ResizeHandleSize && clickPoint.Y > _activeRect.Height - ResizeHandleSize;

            _activeRect.CaptureMouse();
        }

        private void Rect_MouseMove(object sender, MouseEventArgs e)
        {
            //cursor effect
            if (_activeRect == null)
            {
                if (sender is Rectangle rect)
                {
                    Point p = e.GetPosition(rect);
                    bool inCorner = p.X > rect.Width - ResizeHandleSize && p.Y > rect.Height - ResizeHandleSize;
                    rect.Cursor = inCorner ? Cursors.SizeNWSE : Cursors.SizeAll;
                }
                return;
            }

            if (!_activeRect.IsMouseCaptured) return;

            Vector delta = e.GetPosition(_canvas) - _dragStart;

            if (_isResizing)
            {
                //resize box
                _activeRect.Width = Math.Max(10, _startSize.Width + delta.X);
                _activeRect.Height = Math.Max(10, _startSize.Height + delta.Y);
            }
            else
            {
                //move box and label
                double newLeft = _startPosition.X + delta.X;
                double newTop = _startPosition.Y + delta.Y;

                Canvas.SetLeft(_activeRect, newLeft);
                Canvas.SetTop(_activeRect, newTop);

                if (_activeLabel != null)
                {
                    Canvas.SetLeft(_activeLabel, newLeft + 4);
                    Canvas.SetTop(_activeLabel, newTop + 2);
                }
            }
        }

        private void Rect_MouseUp(object sender, MouseButtonEventArgs e)
        {
            //I don't know if this if() actually works lmao cause activeRect shouldn't really be null
            if (_activeRect != null)
            {
                _activeRect.ReleaseMouseCapture();

                if (_activeRect.Tag is DetectionRegion region)
                {
                    //update region bounds
                    region.Bounds = new Rect(
                        Canvas.GetLeft(_activeRect),
                        Canvas.GetTop(_activeRect),
                        _activeRect.Width,
                        _activeRect.Height
                    );
                }

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

            //ensure default regions exist (if not adds them into the json)
            if (!Regions.Any(r => r.Name == "WeaponText"))
                Regions.Add(new DetectionRegion { Name = "WeaponText", Bounds = new Rect(0, 0, 100, 275) });

            if (!Regions.Any(r => r.Name == "KillText"))
                Regions.Add(new DetectionRegion { Name = "KillText", Bounds = new Rect(300, 0, 200, 400) });

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
