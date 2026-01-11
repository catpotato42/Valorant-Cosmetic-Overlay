using System;
using System.Windows;
using System.Runtime.InteropServices;
using System.Windows.Interop;
using System.Windows.Threading;
using System.Windows.Input;
using System.Windows.Controls;
using VALORANT_Overlay.Services;

namespace VALORANT_Overlay.UI;

public partial class MainWindow : Window
{
    private const int GWL_EXSTYLE = -20;
    private const int WS_EX_TRANSPARENT = 0x00000020;
    private const int WS_EX_TOOLWINDOW = 0x00000080;
    private const int WS_EX_NOACTIVATE = 0x08000000;
    private const int WS_EX_LAYERED = 0x00080000;

    [DllImport("user32.dll")]
    private static extern int GetWindowLong(IntPtr hwnd, int index);

    [DllImport("user32.dll")]
    private static extern int SetWindowLong(IntPtr hwnd, int index, int value);

    private AnimationController _animationController;
    private string _currentWeapon = "main";

    private readonly DispatcherTimer _timer = new();
    private HudDetectionService _hudDetectionService;
    private SettingsModeController _settingsController;

    public MainWindow()
    {
        InitializeComponent();
        Topmost = true;

        _animationController = new AnimationController(AnimationImage);
        _animationController.LoadAnimations();

        _settingsController = new SettingsModeController(RootCanvas);
        _hudDetectionService = new HudDetectionService(_settingsController.Regions);

        this.KeyDown += MainWindow_KeyDown;
        _timer.Interval = TimeSpan.FromMilliseconds(100);
        _timer.Tick += HudPollingTimer_Tick;
        _timer.Start();
    }

    private void HudPollingTimer_Tick(object sender, EventArgs e)
    {
        string detectedWeapon = _hudDetectionService.DetectWeapon();
        if (detectedWeapon != _currentWeapon)
        {
            _currentWeapon = detectedWeapon;
            // Map idle animation, update controller, trigger swap animation
        }

        bool gotKill = _hudDetectionService.DetectKill();
        if (gotKill)
        {
            _animationController.Play(AnimationType.Kill);
        }
    }

    private void MakeOverlay()
    {
        var hwnd = new WindowInteropHelper(this).Handle;
        int exStyle = GetWindowLong(hwnd, GWL_EXSTYLE);
        exStyle |= WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE | WS_EX_LAYERED | WS_EX_TRANSPARENT;
        SetWindowLong(hwnd, GWL_EXSTYLE, exStyle);
    }

    private void MainWindow_KeyDown(object sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.K:
                _animationController.Play(AnimationType.Kill);
                break;
            case Key.W:
                _animationController.Play(AnimationType.WeaponSwap);
                break;
            case Key.F2:
                _settingsController.ToggleSettingsMode();
                break;
        }
    }
}
