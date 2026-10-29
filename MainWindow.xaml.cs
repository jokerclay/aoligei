using Ookii.Dialogs.Wpf;
using SourceChord.FluentWPF;
using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using WpfAnimatedGif;

namespace aoligei;

public partial class MainWindow : Window
{
    private DispatcherTimer _countDownTimer;
    private readonly DispatcherTimer _LocalTimer;
    private TimeSpan _remaining;
    private string? _backgroundDir;

    private readonly MediaPlayer _effectPlayer = new MediaPlayer(); // 用于播放提示音（开始、结束）
    private readonly MediaPlayer _backgroundPlayer = new MediaPlayer(); // 用于播放背景音乐
    private static readonly Random _random = new Random();

    public enum AppThemeMode { Dark, Light, System }
    private AppThemeMode _currentTheme = AppThemeMode.Dark;

    enum Caller { Start, Background, End } // 枚举播放类型：开始音效、背景音乐、结束音效

    public MainWindow()
    {
        InitializeComponent();

        // 默认初始化深色主题
        ApplyTheme(AppThemeMode.Dark);

        // 监听系统主题变化
        Microsoft.Win32.SystemEvents.UserPreferenceChanged += (s, e) =>
        {
            if (_currentTheme == AppThemeMode.System)
            {
                Dispatcher.Invoke(() => ApplyTheme(AppThemeMode.System));
            }
        };

        _countDownTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _countDownTimer.Tick += TimerTick;
        _LocalTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _LocalTimer.Tick += UpdateTime;
        _LocalTimer.Start();
        UpdateTime(this, EventArgs.Empty);

        // 当背景音乐播放完一首后，继续播放下一首
        _backgroundPlayer.MediaEnded += (s, e) => PlayMusic(Caller.Background);
        SetGifImage();
    }

    // 拖拽窗口移动
    private void CircleBorder_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed)
        {
            DragMove();
        }
    }

    // 最小化与关闭
    private void MinimizeButton_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;
    private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();

    // 外观主题循环切换 (深色 -> 浅色 -> 跟随系统 -> 深色)
    private void ThemeButton_Click(object sender, RoutedEventArgs e)
    {
        _currentTheme = _currentTheme switch
        {
            AppThemeMode.Dark => AppThemeMode.Light,
            AppThemeMode.Light => AppThemeMode.System,
            _ => AppThemeMode.Dark
        };
        ApplyTheme(_currentTheme);
    }

    private void ApplyTheme(AppThemeMode mode)
    {
        bool isDark = mode switch
        {
            AppThemeMode.Light => false,
            AppThemeMode.Dark => true,
            _ => IsSystemDarkMode()
        };

        ThemeButton.Content = mode switch
        {
            AppThemeMode.Light => "☀️",
            AppThemeMode.Dark => "🌙",
            _ => "💻"
        };
        ThemeButton.ToolTip = $"当前模式: {(mode == AppThemeMode.System ? "跟随系统" : mode == AppThemeMode.Dark ? "深色模式" : "浅色模式")} (点击切换)";

        if (isDark)
        {
            // 暗黑模式画刷
            Resources["CircleBgBrush"] = new SolidColorBrush(Color.FromArgb(0xF2, 0x18, 0x18, 0x1A));
            Resources["CircleBorderBrush"] = new SolidColorBrush(Color.FromArgb(0x35, 0xFF, 0xFF, 0xFF));
            Resources["BarBgBrush"] = new SolidColorBrush(Color.FromArgb(0x40, 0x00, 0x00, 0x00));
            Resources["TextPrimaryBrush"] = new SolidColorBrush(Color.FromRgb(0xFF, 0xFF, 0xFF));
            Resources["TextSecondaryBrush"] = new SolidColorBrush(Color.FromRgb(0xA0, 0xA0, 0xA5));
            Resources["ButtonBgBrush"] = new SolidColorBrush(Color.FromArgb(0x60, 0x30, 0x30, 0x34));
            Resources["ButtonHoverBrush"] = new SolidColorBrush(Color.FromArgb(0x80, 0x48, 0x48, 0x50));
            Resources["BorderSubtleBrush"] = new SolidColorBrush(Color.FromArgb(0x28, 0xFF, 0xFF, 0xFF));
            Resources["AccentBrush"] = new SolidColorBrush(Color.FromRgb(0xFF, 0x95, 0x00));
            Resources["AccentTextBrush"] = new SolidColorBrush(Color.FromRgb(0xFF, 0xFF, 0xFF));
            ResourceDictionaryEx.GlobalTheme = ElementTheme.Dark;
        }
        else
        {
            // 浅色模式画刷
            Resources["CircleBgBrush"] = new SolidColorBrush(Color.FromArgb(0xF5, 0xF8, 0xF9, 0xFA));
            Resources["CircleBorderBrush"] = new SolidColorBrush(Color.FromArgb(0x25, 0x00, 0x00, 0x00));
            Resources["BarBgBrush"] = new SolidColorBrush(Color.FromArgb(0x20, 0x00, 0x00, 0x00));
            Resources["TextPrimaryBrush"] = new SolidColorBrush(Color.FromRgb(0x1C, 0x1C, 0x1E));
            Resources["TextSecondaryBrush"] = new SolidColorBrush(Color.FromRgb(0x6C, 0x6C, 0x70));
            Resources["ButtonBgBrush"] = new SolidColorBrush(Color.FromArgb(0x50, 0xEA, 0xEA, 0xEF));
            Resources["ButtonHoverBrush"] = new SolidColorBrush(Color.FromArgb(0x80, 0xDC, 0xDC, 0xE2));
            Resources["BorderSubtleBrush"] = new SolidColorBrush(Color.FromArgb(0x20, 0x00, 0x00, 0x00));
            Resources["AccentBrush"] = new SolidColorBrush(Color.FromRgb(0xFF, 0x3B, 0x30));
            Resources["AccentTextBrush"] = new SolidColorBrush(Color.FromRgb(0xFF, 0xFF, 0xFF));
            ResourceDictionaryEx.GlobalTheme = ElementTheme.Light;
        }
    }

    private static bool IsSystemDarkMode()
    {
        try
        {
            using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(
                @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            var val = key?.GetValue("AppsUseLightTheme");
            if (val is int intVal)
            {
                return intVal == 0;
            }
        }
        catch
        {
            // ignore
        }
        return false;
    }

    private void SetGifImage()
    {
        var gifFilePath = Helper.GetRunningGifFile1Path();
        if (gifFilePath is not null)
        {
            var image = new BitmapImage();
            image.BeginInit();
            image.UriSource = new Uri(gifFilePath);
            image.EndInit();
            ImageBehavior.SetAnimatedSource(GifImage1, image);
        }

        var gifFilePath2 = Helper.GetRunningGifFile2Path();
        if (gifFilePath2 is not null)
        {
            var image = new BitmapImage();
            image.BeginInit();
            image.UriSource = new Uri(gifFilePath2);
            image.EndInit();
            ImageBehavior.SetAnimatedSource(GifImage2, image);
        }
    }

    private void UpdateTime(object? sender, EventArgs e)
    {
        TimeTextBlock.Text = DateTime.Now.ToString("HH:mm:ss");
    }

    // 浏览背景音乐文件夹
    private void BrowseBackgroundFolder_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new VistaFolderBrowserDialog
        {
            Description = "请选择背景音乐文件夹",
            UseDescriptionForTitle = true
        };
        if (dialog.ShowDialog(this) == true)
        {
            _backgroundDir = dialog.SelectedPath;
            BackgroundDirBox.Text = _backgroundDir;
            var dirName = Path.GetFileName(_backgroundDir);
            BrowseFolderButton.Content = string.IsNullOrEmpty(dirName) ? "🎵 已选歌" : $"🎵 {dirName}";
            BrowseFolderButton.ToolTip = _backgroundDir;
        }
    }

    // 开始专注
    private void StartButton_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_backgroundDir) || !Directory.Exists(_backgroundDir))
        {
            MessageBox.Show("请先点击【📁 选歌】选择有效的背景音乐文件夹。", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (int.TryParse(MinutesBox.Text, out int minutes) && minutes > 0)
        {
            _remaining = TimeSpan.FromMinutes(minutes);
            TimerDisplay.Text = _remaining.ToString("mm\\:ss");
            PlayStartEffectThenBackground(); // 播放开始音效，播放完后开始背景音乐
            _countDownTimer.Start();
            StartButton.IsEnabled = false;
        }
        else
        {
            MessageBox.Show("请输入有效的专注分钟数。", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    // 倒计时每秒更新
    private void TimerTick(object? sender, EventArgs e)
    {
        _remaining = _remaining.Subtract(TimeSpan.FromSeconds(1));
        TimerDisplay.Text = _remaining.ToString("mm\\:ss");

        if (_remaining <= TimeSpan.Zero)
        {
            _countDownTimer.Stop();
            _backgroundPlayer.Stop();
            PlayMusic(Caller.End); // 播放结束音效
            MessageBox.Show("时间到！休息一下吧，你太棒了！", "专注结束", MessageBoxButton.OK, MessageBoxImage.Information);
            StartButton.IsEnabled = true;
        }
    }

    // 重置倒计时
    private void ResetButton_Click(object sender, RoutedEventArgs e)
    {
        _countDownTimer.Stop();
        _remaining = TimeSpan.Zero;
        TimerDisplay.Text = "00:00";
        _backgroundPlayer.Stop();
        _effectPlayer.Stop();
        StartButton.IsEnabled = true;
    }

    // 播放开始音效，播放完后自动播放背景音乐
    private void PlayStartEffectThenBackground()
    {
        _effectPlayer.MediaEnded -= OnStartEffectEnded;
        _effectPlayer.MediaEnded += OnStartEffectEnded;
        PlayMusic(Caller.Start);
    }

    private void OnStartEffectEnded(object? s, EventArgs e)
    {
        _effectPlayer.MediaEnded -= OnStartEffectEnded;
        PlayMusic(Caller.Background);
    }

    // 播放音乐
    private void PlayMusic(Caller c)
    {
        MediaPlayer player;
        string? musicDir = c == Caller.Background ? _backgroundDir :
            c == Caller.Start ? Helper.GetMusicStartDir() : Helper.GetMusicEndDir();
        player = c == Caller.Background ? _backgroundPlayer : _effectPlayer;

        if (string.IsNullOrWhiteSpace(musicDir) || !Directory.Exists(musicDir)) return;

        var supported = new[] { ".mp3", ".wav", ".wma", ".aac", ".m4a", ".flac" };
        var files = Directory.GetFiles(musicDir)
            .Where(f => supported.Contains(Path.GetExtension(f), StringComparer.OrdinalIgnoreCase))
            .ToArray();
        if (files.Length == 0) return;

        var selected = files[_random.Next(files.Length)];
        player.Open(new Uri(selected, UriKind.Absolute));
        player.Play();
    }

    // 切歌
    private void NextTrackButton_Click(object sender, RoutedEventArgs e)
    {
        if (!string.IsNullOrWhiteSpace(_backgroundDir))
        {
            _backgroundPlayer.Stop();
            PlayMusic(Caller.Background);
        }
    }

    // 音量调节
    private void VolumeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_backgroundPlayer != null)
        {
            _backgroundPlayer.Volume = VolumeSlider.Value;
        }
    }
}
