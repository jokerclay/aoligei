using Ookii.Dialogs.Wpf;
using SourceChord.FluentWPF;
using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using WpfAnimatedGif;

namespace aoligei;

public partial class MainWindow : AcrylicWindow
{
    private DispatcherTimer _countDownTimer;
    private readonly DispatcherTimer _LocalTimer;
    private TimeSpan _remaining;
    private string? _backgroundDir;

    private readonly MediaPlayer _effectPlayer = new MediaPlayer(); // 用于播放提示音（开始、结束）
    private readonly MediaPlayer _backgroundPlayer = new MediaPlayer(); // 用于播放背景音乐
    private static readonly Random _random = new Random();

    enum Caller { Start, Background, End } // 枚举播放类型：开始音效、背景音乐、结束音效

    public MainWindow()
    {
        InitializeComponent();
        ResizeMode = ResizeMode.NoResize;

        // 初始化亚克力外观（去除噪点、加深暗色）
        UpdateAcrylicAppearance();

        // 监听系统主题变化（当用户选择“跟随系统”时实时联动）
        Microsoft.Win32.SystemEvents.UserPreferenceChanged += (s, e) =>
        {
            Dispatcher.Invoke(() =>
            {
                if (ThemeComboBox?.SelectedIndex == 0)
                {
                    UpdateAcrylicAppearance();
                }
            });
        };

        _countDownTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _countDownTimer.Tick += TimerTick;
        _LocalTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _LocalTimer.Tick += UpdateTime;
        _LocalTimer.Start();
        UpdateTime(this, EventArgs.Empty); // Set initial time immediately

        // 当背景音乐播放完一首后，继续播放下一首
        _backgroundPlayer.MediaEnded += (s, e) => PlayMusic(Caller.Background);
        SetGifImage();
    }

    // 主题切换（跟随系统 / 浅色 / 深色）
    private void ThemeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        UpdateAcrylicAppearance();
    }

    private void UpdateAcrylicAppearance()
    {
        if (ThemeComboBox == null) return;

        bool isDark;
        switch (ThemeComboBox.SelectedIndex)
        {
            case 1: // 浅色模式
                ResourceDictionaryEx.GlobalTheme = ElementTheme.Light;
                isDark = false;
                break;
            case 2: // 深色模式
                ResourceDictionaryEx.GlobalTheme = ElementTheme.Dark;
                isDark = true;
                break;
            default: // 0: 跟随系统
                ResourceDictionaryEx.GlobalTheme = ElementTheme.Default;
                isDark = IsSystemDarkMode();
                break;
        }

        ApplyAcrylicTint(isDark);
    }

    private void ApplyAcrylicTint(bool isDark)
    {
        // 彻底去除噪点纹理，消除画面颗粒感
        NoiseOpacity = 0.0;

        if (isDark)
        {
            // 加深暗黑底色，提高不透明度，防止桌面背景杂色透出显灰
            TintColor = Color.FromRgb(0x14, 0x14, 0x14);
            TintOpacity = 0.92;
            FallbackColor = Color.FromRgb(0x16, 0x16, 0x16);
        }
        else
        {
            // 纯净明亮的浅色
            TintColor = Color.FromRgb(0xF6, 0xF6, 0xF6);
            TintOpacity = 0.72;
            FallbackColor = Color.FromRgb(0xF0, 0xF0, 0xF0);
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
            // 默认返回 false (浅色)
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

    // 使用 Ookii.Dialogs.Wpf 的 VistaFolderBrowserDialog 实现 WPF 原生文件夹选择
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
        }
    }

    // 用户点击“开始专注”按钮
    private void StartButton_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_backgroundDir) || !Directory.Exists(_backgroundDir))
        {
            MessageBox.Show("请先选择有效的背景音乐文件夹。", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
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
            MessageBox.Show("请输入有效的分钟数。", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    // 每秒触发一次，用于更新倒计时显示
    private void TimerTick(object? sender, EventArgs e)
    {
        _remaining = _remaining.Subtract(TimeSpan.FromSeconds(1));
        TimerDisplay.Text = _remaining.ToString("mm\\:ss");

        if (_remaining <= TimeSpan.Zero)
        {
            _countDownTimer.Stop();
            _backgroundPlayer.Stop();
            PlayMusic(Caller.End); // 播放结束音效
            MessageBox.Show("时间到！休息一下吧。", "专注结束", MessageBoxButton.OK, MessageBoxImage.Information);
            StartButton.IsEnabled = true;
        }
    }

    // 用户点击“重置”按钮
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

    // 开始音效播放完后回调，开始播放背景音乐
    private void OnStartEffectEnded(object? s, EventArgs e)
    {
        _effectPlayer.MediaEnded -= OnStartEffectEnded;
        PlayMusic(Caller.Background);
    }

    // 根据 Caller 类型播放对应音乐：开始音效、背景音乐、结束音效
    private void PlayMusic(Caller c)
    {
        MediaPlayer player;
        string? musicDir = c == Caller.Background ? _backgroundDir :
            c == Caller.Start ? Helper.GetMusicStartDir() : Helper.GetMusicEndDir();
        player = c == Caller.Background ? _backgroundPlayer : _effectPlayer;

        if (string.IsNullOrWhiteSpace(musicDir) || !Directory.Exists(musicDir)) return;

        var supported = new[] { ".mp3", ".wav", ".wma", ".aac", ".m4a", ".flac" }; // 支持的音频格式
        var files = Directory.GetFiles(musicDir)
            .Where(f => supported.Contains(Path.GetExtension(f), StringComparer.OrdinalIgnoreCase))
            .ToArray();
        if (files.Length == 0) return;

        var selected = files[_random.Next(files.Length)]; // 随机选择一首播放
        player.Open(new Uri(selected, UriKind.Absolute));
        player.Play();
    }

    // 用户点击“下一首”按钮手动切换背景音乐
    private void NextTrackButton_Click(object sender, RoutedEventArgs e)
    {
        if (!string.IsNullOrWhiteSpace(_backgroundDir))
        {
            _backgroundPlayer.Stop(); // 停止当前背景音乐
            PlayMusic(Caller.Background); // 播放下一首
        }
    }

    // 支持窗口拖动
    private void TitleBar_MouseLeftButtonDown(object s, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed)
            DragMove();
    }

    // 音量滑块值变化时，更新背景音乐音量
    private void VolumeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_backgroundPlayer != null)
        {
            _backgroundPlayer.Volume = VolumeSlider.Value; // 设置背景音乐音量（范围：0.0 - 1.0）
        }
    }
}
