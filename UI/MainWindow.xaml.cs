using System;
using System.Windows;
using System.Runtime.InteropServices;
using System.Windows.Interop;
using System.Windows.Threading;
using System.Windows.Input;
using System.Windows.Controls;
using VALORANT_Overlay.Services;
using Microsoft.Win32;

namespace VALORANT_Overlay.UI;

public partial class MainWindow : Window
{
    private const int GWL_EXSTYLE = -20;
    private const int WS_EX_TRANSPARENT = 0x00000020;
    private const int WS_EX_TOOLWINDOW = 0x00000080;
    private const int WS_EX_NOACTIVATE = 0x08000000;
    private const int WS_EX_LAYERED = 0x00080000;
    private const int HOTKEY_ID = 9000;
    private const uint MOD_NONE = 0x0000;
    private const uint VK_F2 = 0x71;

    [DllImport("user32.dll")]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll")]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    [DllImport("user32.dll")]
    private static extern int GetWindowLong(IntPtr hwnd, int index);

    [DllImport("user32.dll")]
    private static extern int SetWindowLong(IntPtr hwnd, int index, int value);

    private AnimationController _animationController;
    private string _currentWeapon = "knife";
    private string _lastDetectedWeapon = "__none__"; //used to keep track of last tick weapon to reduce false positive switches
    private int _weaponConsistentCount = 0;
    private const int _requiredConsistentTicks = 2;

    private readonly DispatcherTimer _timer = new();
    private HudDetectionService _hudDetectionService;
    private SettingsModeController _settingsController;
    private readonly DispatcherTimer _valorantCheckTimer = new();
    private bool _isValoActive = false;
    private readonly DispatcherTimer _topmostTimer = new DispatcherTimer();


    public MainWindow()
    {
        InitializeComponent();

        _animationController = new AnimationController(AnimationImage);
        _animationController.LoadAnimations();

        _settingsController = new SettingsModeController(RootCanvas);
        _hudDetectionService = new HudDetectionService(_settingsController.Regions);

        _valorantCheckTimer.Interval = TimeSpan.FromSeconds(5);
        _valorantCheckTimer.Tick += ValorantCheck_Tick;
        _valorantCheckTimer.Start();

        _settingsController.SettingsModeChanged += SetOverlayInteractive;

        KeyDown += MainWindow_KeyDown; //testing only

        _timer.Interval = TimeSpan.FromMilliseconds(150);
        _timer.Tick += HudPollingTimer_Tick;
        _timer.Start();

        _topmostTimer.Interval = TimeSpan.FromSeconds(1);
        _topmostTimer.Tick += (s, e) => { if (_isValoActive) Topmost = true; };
        _topmostTimer.Start();
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);

        WindowState = WindowState.Normal;
        WindowStyle = WindowStyle.None;
        ResizeMode = ResizeMode.NoResize;

        Left = 0;
        Top = 0;
        Width = SystemParameters.PrimaryScreenWidth;
        Height = SystemParameters.PrimaryScreenHeight;

        MakeOverlay();
        SnapAnimationToRegion();
        //add for prod
        //RegisterRunAtStartup();

        var hwnd = new WindowInteropHelper(this).Handle;
        RegisterHotKey(hwnd, HOTKEY_ID, MOD_NONE, VK_F2);
        HwndSource.FromHwnd(hwnd)?.AddHook(WndProc);
    }

    protected override void OnClosed(EventArgs e)
    {
        base.OnClosed(e);
        var hwnd = new WindowInteropHelper(this).Handle;
        UnregisterHotKey(hwnd, HOTKEY_ID);
    }

    //c++ win32 showing up
    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        const int WM_HOTKEY = 0x0312;

        if (msg == WM_HOTKEY && wParam.ToInt32() == HOTKEY_ID)
        {
            _settingsController.ToggleSettingsMode();
            handled = true;
        }

        return IntPtr.Zero;
    }

    private void HudPollingTimer_Tick(object sender, EventArgs e)
    {
        if (_settingsController.IsInSettingsMode)
            return;
        string detectedWeapon = _hudDetectionService.DetectWeapon();
        if (detectedWeapon != "__none__")
        {
            if (detectedWeapon == _lastDetectedWeapon)
            {
                _weaponConsistentCount++;
            }
            else
            {
                _lastDetectedWeapon = detectedWeapon;
                _weaponConsistentCount = 1;
            }

            if (_weaponConsistentCount >= 2 && detectedWeapon != _currentWeapon)
            {
                _currentWeapon = detectedWeapon;

                AnimationType idleAnim = detectedWeapon switch
                {
                    "main" => AnimationType.IdleMain,
                    "pistol" => AnimationType.IdlePistol,
                    "knife" => AnimationType.IdleKnife,
                    _ => AnimationType.IdleMain
                };
                _animationController.SetIdleType(idleAnim);
                _animationController.Play(AnimationType.WeaponSwap);
            }
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

    private void SetOverlayInteractive(bool interactive)
    {
        SnapAnimationToRegion();

        var hwnd = new WindowInteropHelper(this).Handle;
        int exStyle = GetWindowLong(hwnd, GWL_EXSTYLE);

        if (interactive)
            exStyle &= ~WS_EX_TRANSPARENT;
        else
            exStyle |= WS_EX_TRANSPARENT;

        SetWindowLong(hwnd, GWL_EXSTYLE, exStyle);
    }


    //delete for prod
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
        }
    }

    private void ValorantCheck_Tick(object? sender, EventArgs e)
    {
        bool running = ValorantProcessService.IsValorantRunning();

        if (running && !_isValoActive)
            EnterActiveMode();
        else if (!running && _isValoActive)
            EnterDormantMode();
    }

    private void EnterActiveMode()
    {
        _isValoActive = true;

        Show();
        Topmost = true;

        _timer.Start();
    }

    private void EnterDormantMode()
    {
        _isValoActive = false;

        _timer.Stop();
        Hide();
    }

    private static void RegisterRunAtStartup()
    {
        using var key = Registry.CurrentUser.OpenSubKey(
            @"Software\Microsoft\Windows\CurrentVersion\Run", true);

        key?.SetValue(
            "ValorantCosmeticOverlay",
            $"\"{Environment.ProcessPath}\"");
    }
    
    private void SnapAnimationToRegion()
    {
        var region = _settingsController.Regions
            .First(r => r.Name == "Animation");

        // Ensure layout is up to date (important if frame size just changed)
        AnimationImage.UpdateLayout();

        double left = region.Bounds.X;
        double top = region.Bounds.Y + region.Bounds.Height - AnimationImage.ActualHeight;

        Canvas.SetLeft(AnimationImage, left);
        Canvas.SetTop(AnimationImage, top);
    }
}
