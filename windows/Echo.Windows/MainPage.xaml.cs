using Echo_Windows.Core;
using Echo_Windows.Services;
using Echo_Windows.ViewModels;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using Microsoft.Windows.Storage.Pickers;
using System.Diagnostics;
using System.Collections.Specialized;
using System.Text.Json;

namespace Echo_Windows;

public sealed record LanguageChoice(string? Code, string Title);

public sealed partial class MainPage : Page
{
    private readonly List<Border> waveform = [];
    private DesktopSubtitleOverlayWindow? subtitleOverlayWindow;
    private static readonly LanguageChoice[] SourceLanguages =
    [
        new(null, "自动识别"), new("en", "English"), new("zh", "简体中文"), new("ja", "日本語"),
        new("ko", "한국어"), new("es", "Español"), new("fr", "Français"), new("de", "Deutsch"),
        new("it", "Italiano"), new("pt", "Português"), new("ru", "Русский"), new("ar", "العربية"), new("hi", "हिन्दी")
    ];
    private static readonly LanguageChoice[] TargetLanguages =
    [
        new(null, "不翻译"), new("en", "English"), new("zh", "简体中文"), new("ja", "日本語"),
        new("ko", "한국어"), new("es", "Español"), new("fr", "Français"), new("de", "Deutsch"),
        new("it", "Italiano"), new("pt", "Português"), new("ru", "Русский"), new("ar", "العربية"), new("hi", "हिन्दी")
    ];
    private bool isAtTranscriptEnd = true;
    private readonly HashSet<Guid> observedSubtitleIds = [];
    private int waveformFrame;
    private bool initialized;
    private bool loadingSettings = true;
    public MainPageViewModel ViewModel { get; } = new();
    public MainPage()
    {
        InitializeComponent();
        var c = ViewModel.Config;
        SourceLanguageChoice.ItemsSource = SourceLanguages;
        TargetLanguageChoice.ItemsSource = TargetLanguages;
        SettingsSourceLanguage.ItemsSource = SourceLanguages;
        SettingsTargetLanguage.ItemsSource = TargetLanguages;
        CorrectionTerms.Text = c.CorrectionTerms;
        AutoSummary.IsOn = c.AutoSummaryEnabled;
        AutoCorrection.IsOn = c.AutoCorrectionEnabled;
        Translate.IsOn = c.Translate; Speakers.IsOn = c.Speakers; Strict.IsOn = c.Strict;
        try { SonioxKey.Password = Preferences.Unprotect(c.SonioxSecret); DeepSeekKey.Password = Preferences.Unprotect(c.DeepSeekSecret); }
        catch { ViewModel.Status = "密钥无法解密，请重新输入并保存。"; }
        ThemeChoice.SelectedIndex = c.Theme == "Light" ? 1 : c.Theme == "Dark" ? 2 : 0;
        TranscriptFolderPath.Text = TranscriptFiles.Root;
        SettingsSections.SelectedItem = SettingsGeneral;
        LoadSegmentationSettings(c.Segmentation);
        foreach (var slider in new[] { EndpointDelay, EndpointSensitivity, EndpointLatency, SilenceThreshold, SilenceWords, LongWords, LongDuration })
            slider.ValueChanged += SegmentationValueChanged;
        SilenceFallback.Toggled += (_, _) => UpdateSegmentationReadouts();
        LongFallback.Toggled += (_, _) => UpdateSegmentationReadouts();
        SettingsSourceLanguage.SelectedItem = SourceLanguages.FirstOrDefault(item => item.Code == (string.IsNullOrWhiteSpace(c.SourceLanguage) ? null : c.SourceLanguage)) ?? SourceLanguages[0];
        SettingsTargetLanguage.SelectedItem = TargetLanguages.FirstOrDefault(item => item.Code == (c.Translate ? c.TargetLanguage : null)) ?? TargetLanguages[0];
        SettingsTargetLanguage.IsEnabled = c.Translate;
        Translate.Toggled += (_, _) => SettingsTargetLanguage.IsEnabled = Translate.IsOn;
        loadingSettings = false;
        if (c.Theme is "Light" or "Dark") RequestedTheme = Enum.Parse<ElementTheme>(c.Theme);
        Loaded += async (_, _) =>
        {
            ApplyWindowTheme();
            if (ViewModel.Config.SubtitleOverlay.Enabled) ShowSubtitleOverlay();
            if (initialized) return;
            initialized = true;
            await ViewModel.LoadArchivesAsync();
        };
        App.Window.Closed += (_, _) => subtitleOverlayWindow?.CloseOverlay();
        ViewModel.Entries.CollectionChanged += Entries_CollectionChanged;
        EmptyHint.Visibility = ViewModel.Entries.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        TranscriptList.Loaded += (_, _) =>
        {
            var scrollViewer = FindDescendant<ScrollViewer>(TranscriptList);
            if (scrollViewer is not null)
                scrollViewer.ViewChanged += (_, _) =>
                {
                    bool atEnd = scrollViewer.ScrollableHeight - scrollViewer.VerticalOffset <= 32;
                    if (atEnd)
                    {
                        isAtTranscriptEnd = true;
                        NewContentButton.Visibility = Visibility.Collapsed;
                    }
                    else isAtTranscriptEnd = false;
                };
            if (ViewModel.Entries.Count > 0) TranscriptList.ScrollIntoView(ViewModel.Entries[^1]);
        };
        for (int i = 0; i < 64; i++)
        {
            var bar = new Border { Width = 3, Height = 2, CornerRadius = new CornerRadius(2) };
            waveform.Add(bar);
            WaveformBars.Children.Add(bar);
        }
        ViewModel.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is nameof(ViewModel.Level) or nameof(ViewModel.IsRecording) or nameof(ViewModel.Status)) UpdateAudioDisplay();
            if (e.PropertyName == nameof(ViewModel.IsRecording)) UpdateRecordingLanguageHint();
        };
        UpdateAudioDisplay();
        UpdateLanguageHeaders();
        UpdateOverlayMenuText();
        ArchiveTitle.Text = ViewModel.SelectedArchive?.Title ?? "新的录音";
        ViewModel.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(ViewModel.SelectedArchive))
            {
                ArchiveTitle.Text = ViewModel.SelectedArchive?.Title ?? "新的录音";
                ArchiveFlyout.Hide();
            }
        };
    }
    public static Visibility VisibleWhen(bool value) => value ? Visibility.Visible : Visibility.Collapsed;
    public static Visibility HiddenWhen(bool value) => value ? Visibility.Collapsed : Visibility.Visible;
    public static bool Not(bool value) => !value;
    public static string AccessibleText(string role, string? value) => string.IsNullOrWhiteSpace(value) ? string.Empty : $"{role}：{value}";
    public static string SubtitleEditAutomationId(Guid subtitleId) => $"EditSubtitle_{subtitleId:N}";
    private void UpdateLanguageHeaders()
    {
        SourceLanguageChoice.SelectedItem = SourceLanguages.FirstOrDefault(item => item.Code == (string.IsNullOrWhiteSpace(ViewModel.Config.SourceLanguage) ? null : ViewModel.Config.SourceLanguage)) ?? SourceLanguages[0];
        TargetLanguageChoice.SelectedItem = TargetLanguages.FirstOrDefault(item => item.Code == (ViewModel.Config.Translate ? ViewModel.Config.TargetLanguage : null)) ?? TargetLanguages[0];
        TargetLanguageChoice.Visibility = ViewModel.Config.Translate ? Visibility.Visible : Visibility.Collapsed;
        Strict.IsEnabled = !string.IsNullOrWhiteSpace(ViewModel.Config.SourceLanguage);
        SettingsSourceLanguage.SelectedItem = SourceLanguages.FirstOrDefault(item => item.Code == (string.IsNullOrWhiteSpace(ViewModel.Config.SourceLanguage) ? null : ViewModel.Config.SourceLanguage)) ?? SourceLanguages[0];
        SettingsTargetLanguage.SelectedItem = TargetLanguages.FirstOrDefault(item => item.Code == (ViewModel.Config.Translate ? ViewModel.Config.TargetLanguage : null)) ?? TargetLanguages[0];
        UpdateRecordingLanguageHint();
    }
    private void SettingsSections_SelectionChanged(SelectorBar sender, SelectorBarSelectionChangedEventArgs e)
    {
        GeneralSettingsPage.Visibility = sender.SelectedItem == SettingsGeneral ? Visibility.Visible : Visibility.Collapsed;
        RecognitionSettingsPage.Visibility = sender.SelectedItem == SettingsRecognition ? Visibility.Visible : Visibility.Collapsed;
        SegmentationSettingsPage.Visibility = sender.SelectedItem == SettingsSegmentation ? Visibility.Visible : Visibility.Collapsed;
        ServiceSettingsPage.Visibility = sender.SelectedItem == SettingsServices ? Visibility.Visible : Visibility.Collapsed;
    }
    private void LoadSegmentationSettings(TranscriptSegmentationSettings config)
    {
        EndpointDelay.Value = config.SonioxMaxEndpointDelayMilliseconds;
        EndpointSensitivity.Value = config.SonioxEndpointSensitivity;
        EndpointLatency.Value = config.SonioxEndpointLatencyAdjustmentLevel;
        SilenceFallback.IsOn = config.LocalSilenceFallbackEnabled;
        SilenceThreshold.Value = config.LocalSilenceThresholdSeconds;
        SilenceWords.Value = config.LocalSilenceMinimumWordCount;
        LongFallback.IsOn = config.LongSegmentFallbackEnabled;
        LongWords.Value = config.LongSegmentWordThreshold;
        LongDuration.Value = config.LongSegmentDurationThresholdSeconds;
        UpdateSegmentationReadouts();
    }
    private void UpdateSegmentationReadouts()
    {
        EndpointDelayValue.Text = $"最大端点延迟：{EndpointDelay.Value:0} ms";
        EndpointSensitivityValue.Text = $"端点灵敏度：{EndpointSensitivity.Value:0.0}";
        EndpointLatencyValue.Text = $"延迟调整等级：{EndpointLatency.Value:0} 级";
        SilenceThresholdValue.Text = $"静音阈值：{SilenceThreshold.Value:0.0} 秒";
        SilenceWordsValue.Text = $"静音兜底最少词数：{SilenceWords.Value:0} 词";
        LongWordsValue.Text = $"长段兜底词数门槛：{LongWords.Value:0} 词";
        LongDurationValue.Text = $"长段兜底时长门槛：{LongDuration.Value:0} 秒";
        SilenceThreshold.IsEnabled = SilenceWords.IsEnabled = SilenceFallback.IsOn;
        LongWords.IsEnabled = LongDuration.IsEnabled = LongFallback.IsOn;
    }
    private void SegmentationValueChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        if (loadingSettings) return;
        UpdateSegmentationReadouts();
    }
    private void ResetSegmentation_Click(object sender, RoutedEventArgs e) => LoadSegmentationSettings(new());
    private void UpdateRecordingLanguageHint() => LanguageNextSessionHint.Visibility = ViewModel.IsRecording ? Visibility.Visible : Visibility.Collapsed;
    private void Entries_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.NewItems is not null)
            foreach (Subtitle entry in e.NewItems)
                if (observedSubtitleIds.Add(entry.Id))
                    entry.PropertyChanged += (_, args) =>
                    {
                        if (ReferenceEquals(ViewModel.Entries.LastOrDefault(), entry)
                            && args.PropertyName is nameof(Subtitle.English) or nameof(Subtitle.Chinese))
                            TranscriptContentChanged();
                    };
        EmptyHint.Visibility = ViewModel.Entries.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        if (ViewModel.Entries.Count > 0) TranscriptContentChanged();
    }
    private void TranscriptContentChanged()
    {
        if (isAtTranscriptEnd)
        {
            if (ViewModel.Entries.Count > 0) TranscriptList.ScrollIntoView(ViewModel.Entries[^1]);
            NewContentButton.Visibility = Visibility.Collapsed;
        }
        else NewContentButton.Visibility = Visibility.Visible;
    }
    private static T? FindDescendant<T>(DependencyObject parent) where T : DependencyObject
    {
        int count = Microsoft.UI.Xaml.Media.VisualTreeHelper.GetChildrenCount(parent);
        for (int index = 0; index < count; index++)
        {
            var child = Microsoft.UI.Xaml.Media.VisualTreeHelper.GetChild(parent, index);
            if (child is T match) return match;
            if (FindDescendant<T>(child) is { } nested) return nested;
        }
        return null;
    }
    private void SourceLanguage_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (SourceLanguageChoice.SelectedItem is not LanguageChoice selected) return;
        ViewModel.Config.SourceLanguage = selected.Code ?? string.Empty;
        Strict.IsEnabled = selected.Code is not null;
        ViewModel.Config.Save();
        UpdateRecordingLanguageHint();
    }
    private void TargetLanguage_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (TargetLanguageChoice.SelectedItem is not LanguageChoice selected) return;
        ViewModel.Config.Translate = selected.Code is not null;
        if (selected.Code is not null) ViewModel.Config.TargetLanguage = selected.Code;
        Translate.IsOn = ViewModel.Config.Translate;
        TargetLanguageChoice.Visibility = ViewModel.Config.Translate ? Visibility.Visible : Visibility.Collapsed;
        ViewModel.Config.Save();
    }
    private void UpdateAudioDisplay()
    {
        if (waveform.Count == 0) return;
        bool active = ViewModel.IsRecording;
        double level = Math.Clamp(ViewModel.Level / 100, 0, 1);
        var accent = (Brush)Application.Current.Resources["EchoAccentBrush"];
        var muted = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"];
        foreach (var (bar, index) in waveform.Select((bar, index) => (bar, index)))
        {
            double rhythm = .28 + .72 * Math.Abs(Math.Sin(index * .47 + waveformFrame * .8));
            bar.Height = active ? Math.Max(2, 3 + level * rhythm * 42) : 2;
            bar.Background = active ? accent : muted;
        }
        waveformFrame++;
        AudioStatus.Text = active ? level > .035 ? "检测到声音" : "等待声音" : "未在录音";
        AudioDot.Fill = active && level > .035 ? accent : muted;
        ConnectionDot.Fill = active ? accent : muted;
    }
    private void CycleTheme_Click(object sender, RoutedEventArgs e)
    {
        ThemeChoice.SelectedIndex = (ThemeChoice.SelectedIndex + 1) % 3;
        ViewModel.Config.Theme = ThemeChoice.SelectedIndex == 1 ? "Light" : ThemeChoice.SelectedIndex == 2 ? "Dark" : "Default";
        ApplyWindowTheme();
        UpdateAudioDisplay();
        ViewModel.Config.Save();
    }
    private void ApplyWindowTheme()
    {
        if (ViewModel.Config.Theme == "Default")
        {
            ClearValue(FrameworkElement.RequestedThemeProperty);
            if (App.Window?.Content is FrameworkElement systemRoot) systemRoot.ClearValue(FrameworkElement.RequestedThemeProperty);
            return;
        }
        RequestedTheme = Enum.Parse<ElementTheme>(ViewModel.Config.Theme);
        if (App.Window?.Content is FrameworkElement root) root.RequestedTheme = RequestedTheme;
    }
    private void Settings_Click(object sender, RoutedEventArgs e)
    {
        RecordingPanel.Visibility = Visibility.Collapsed;
        SettingsOverlay.Visibility = Visibility.Visible;
        SettingsSections.Focus(FocusState.Programmatic);
    }
    private void Back_Click(object sender, RoutedEventArgs e)
    {
        SettingsOverlay.Visibility = Visibility.Collapsed;
        RecordingPanel.Visibility = Visibility.Visible;
        SettingsButton.Focus(FocusState.Programmatic);
    }
    private void New_Click(object sender, RoutedEventArgs e) => ViewModel.NewArchive();
    private async void DeleteArchive_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel.SelectedArchive is not { } archive) return;
        var dialog = new ContentDialog
        {
            Title = "将存档移入回收区？",
            Content = $"“{archive.Title}”及其中的字幕和总结会从列表移除，并保留在本地 Deleted 文件夹。可把 JSON 文件移回 Archives 文件夹恢复。",
            PrimaryButtonText = "移入回收区", CloseButtonText = "取消", DefaultButton = ContentDialogButton.Close, XamlRoot = XamlRoot
        };
        if (await dialog.ShowAsync() == ContentDialogResult.Primary) await ViewModel.MoveSelectedArchiveToDeletedAsync();
    }
    private async void SplitSegment_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new ContentDialog
        {
            Title = "拆出刚完成的录音段？",
            Content = "这会把刚完成的录音段移入一个新存档，并从原存档移除。原存档写入时会保留 .bak 备份。",
            PrimaryButtonText = "拆出本段", CloseButtonText = "取消", DefaultButton = ContentDialogButton.Close, XamlRoot = XamlRoot
        };
        if (await dialog.ShowAsync() == ContentDialogResult.Primary) await ViewModel.SplitCompletedSegmentAsync();
    }
    private async void RenameArchive_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel.SelectedArchive is not { } archive) { ViewModel.Status = "请先选择一个存档。"; return; }
        var name = new TextBox { Header = "存档名称", Text = archive.Title, MaxLength = 80 };
        AutomationProperties.SetName(name, "存档名称"); AutomationProperties.SetAutomationId(name, "RenameArchiveName");
        var dialog = new ContentDialog { Title = "重命名存档", Content = name, PrimaryButtonText = "保存", CloseButtonText = "取消", DefaultButton = ContentDialogButton.Primary, XamlRoot = XamlRoot };
        dialog.PrimaryButtonClick += (_, args) =>
        {
            if (string.IsNullOrWhiteSpace(name.Text)) { args.Cancel = true; ViewModel.Status = "存档名称不能为空。"; }
        };
        if (await dialog.ShowAsync() == ContentDialogResult.Primary && await ViewModel.RenameSelectedArchiveAsync(name.Text))
            ArchiveTitle.Text = ViewModel.SelectedArchive?.Title ?? "新的录音";
    }
    private async void Start_Click(object sender, RoutedEventArgs e) => await ViewModel.StartAsync(Mode.SelectedIndex, (OutputDevice.SelectedItem as AudioDevice)?.Id, (InputDevice.SelectedItem as AudioDevice)?.Id);
    private async void Stop_Click(object sender, RoutedEventArgs e) => await ViewModel.StopAsync();
    private async void SwitchAudio_Click(object sender, RoutedEventArgs e)
    {
        if (!ViewModel.IsRecording) return;
        ViewModel.RefreshDevices();
        var choices = new List<AudioDevice> { new("", "系统默认设备") };
        ComboBox? output = null, input = null;
        var content = new StackPanel { Spacing = 12, MinWidth = 360 };
        if (ViewModel.ActiveAudioMode is 0 or 2)
        {
            var outputChoices = new List<AudioDevice>(choices);
            outputChoices.AddRange(ViewModel.Outputs);
            output = new ComboBox { Header = "电脑音频来源", ItemsSource = outputChoices, DisplayMemberPath = nameof(AudioDevice.Name), HorizontalAlignment = HorizontalAlignment.Stretch };
            AutomationProperties.SetAutomationId(output, "SwitchOutputDevice");
            output.SelectedItem = outputChoices.FirstOrDefault(d => d.Id == ViewModel.ActiveOutputId) ?? outputChoices[0];
            content.Children.Add(output);
        }
        if (ViewModel.ActiveAudioMode is 1 or 2)
        {
            var inputChoices = new List<AudioDevice> { new("", "系统默认设备") };
            inputChoices.AddRange(ViewModel.Inputs);
            input = new ComboBox { Header = "麦克风", ItemsSource = inputChoices, DisplayMemberPath = nameof(AudioDevice.Name), HorizontalAlignment = HorizontalAlignment.Stretch };
            AutomationProperties.SetAutomationId(input, "SwitchInputDevice");
            input.SelectedItem = inputChoices.FirstOrDefault(d => d.Id == ViewModel.ActiveInputId) ?? inputChoices[0];
            content.Children.Add(input);
        }
        content.Children.Add(new TextBlock { Text = "切换期间转写连接保持不变；新设备无法启动时会尝试恢复原设备。", TextWrapping = TextWrapping.Wrap, Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"] });
        var dialog = new ContentDialog
        {
            Title = "切换录音设备", Content = content, PrimaryButtonText = "切换", CloseButtonText = "取消",
            DefaultButton = ContentDialogButton.Primary, XamlRoot = XamlRoot
        };
        if (await dialog.ShowAsync() == ContentDialogResult.Primary)
            await ViewModel.SwitchAudioDevicesAsync((output?.SelectedItem as AudioDevice)?.Id is { Length: > 0 } outId ? outId : null,
                (input?.SelectedItem as AudioDevice)?.Id is { Length: > 0 } inId ? inId : null);
    }
    private void Refresh_Click(object sender, RoutedEventArgs e) => ViewModel.RefreshDevices();
    private void ToggleOverlay_Click(object sender, RoutedEventArgs e)
    {
        var settings = ViewModel.Config.SubtitleOverlay;
        settings.Enabled = !settings.Enabled;
        SaveOverlaySettings(settings);
        if (settings.Enabled) ShowSubtitleOverlay(); else subtitleOverlayWindow?.HideOverlay();
        UpdateOverlayMenuText();
    }
    private void AdjustOverlay_Click(object sender, RoutedEventArgs e)
    {
        bool wasAdjusting = subtitleOverlayWindow?.IsAdjusting == true;
        var settings = ViewModel.Config.SubtitleOverlay;
        settings.Enabled = true;
        SaveOverlaySettings(settings);
        ShowSubtitleOverlay();
        subtitleOverlayWindow?.SetAdjusting(!wasAdjusting);
        UpdateOverlayMenuText();
    }
    private async void OverlaySettings_Click(object sender, RoutedEventArgs e) => await ShowOverlaySettingsAsync();
    private void ShowSubtitleOverlay()
    {
        subtitleOverlayWindow ??= new DesktopSubtitleOverlayWindow(
            ViewModel.SubtitleOverlayFeed, ViewModel.Config.SubtitleOverlay, PersistOverlayPlacement);
        subtitleOverlayWindow.ApplySettings(ViewModel.Config.SubtitleOverlay, reposition: true);
        subtitleOverlayWindow.ShowOverlay();
    }
    private void SaveOverlaySettings(DesktopSubtitleOverlaySettings settings)
    {
        ViewModel.Config.SubtitleOverlay = settings.Validate();
        ViewModel.Config.Save();
        subtitleOverlayWindow?.ApplySettings(ViewModel.Config.SubtitleOverlay);
        subtitleOverlayWindow?.SetAdjusting(!ViewModel.Config.SubtitleOverlay.PositionLocked);
        UpdateOverlayMenuText();
    }
    private void PersistOverlayPlacement(DesktopSubtitleOverlaySettings settings)
    {
        ViewModel.Config.SubtitleOverlay = settings;
        ViewModel.Config.Save();
        UpdateOverlayMenuText();
    }
    private void UpdateOverlayMenuText()
    {
        ToggleOverlayMenuItem.Text = ViewModel.Config.SubtitleOverlay.Enabled ? "关闭悬浮字幕" : "开启悬浮字幕";
        AdjustOverlayMenuItem.Text = subtitleOverlayWindow?.IsAdjusting == true ? "完成调整" : "调整位置和大小";
    }
    private async Task ShowOverlaySettingsAsync()
    {
        var current = ViewModel.Config.SubtitleOverlay;
        var enabled = new ToggleSwitch { Header = "启用悬浮字幕", IsOn = current.Enabled };
        var original = new ToggleSwitch { Header = "显示原文", IsOn = current.ShowOriginal };
        var translation = new ToggleSwitch { Header = "显示译文", IsOn = current.ShowTranslation };
        var clickThrough = new ToggleSwitch { Header = "点击穿透（不调整时）", IsOn = current.ClickThrough };
        var locked = new ToggleSwitch { Header = "锁定位置", IsOn = current.PositionLocked };
        AutomationProperties.SetAutomationId(enabled, "OverlayEnabled");
        AutomationProperties.SetAutomationId(original, "OverlayShowOriginal");
        AutomationProperties.SetAutomationId(translation, "OverlayShowTranslation");
        AutomationProperties.SetAutomationId(clickThrough, "OverlayClickThrough");
        AutomationProperties.SetAutomationId(locked, "OverlayPositionLocked");
        var content = new StackPanel { Spacing = 10, MaxWidth = 540 };
        content.Children.Add(new TextBlock { Text = "透明字幕浮在其他窗口上方，只读取当前识别结果，不会重新连接 Soniox。", TextWrapping = TextWrapping.Wrap });
        content.Children.Add(enabled); content.Children.Add(original); content.Children.Add(translation);
        Slider originalSize = AddOverlaySlider(content, "原文字号", current.OriginalFontSize, 16, 48, " pt");
        Slider translationSize = AddOverlaySlider(content, "译文字号", current.TranslationFontSize, 14, 44, " pt");
        Slider opacity = AddOverlaySlider(content, "字幕透明度", current.Opacity * 100, 35, 100, "%");
        Slider width = AddOverlaySlider(content, "最大宽度", current.WidthFraction * 100, 35, 95, "%");
        Slider retention = AddOverlaySlider(content, "定稿保留", current.RetentionSeconds, 1, 15, " 秒");
        Slider shadow = AddOverlaySlider(content, "文字阴影", current.ShadowStrength * 100, 0, 100, "%");
        content.Children.Add(clickThrough); content.Children.Add(locked);
        var resetPosition = new Button { Content = "重置字幕位置", HorizontalAlignment = HorizontalAlignment.Left };
        AutomationProperties.SetAutomationId(resetPosition, "ResetSubtitleOverlayPosition");
        resetPosition.Click += (_, _) =>
        {
            current.NormalizedX = .5; current.NormalizedBottom = .09;
            SaveOverlaySettings(current);
            subtitleOverlayWindow?.ApplySettings(current, reposition: true);
        };
        content.Children.Add(resetPosition);
        var dialog = new ContentDialog
        {
            Title = "悬浮字幕设置",
            Content = new ScrollViewer { Content = content, MaxHeight = 560, VerticalScrollBarVisibility = ScrollBarVisibility.Auto },
            PrimaryButtonText = "完成", SecondaryButtonText = "恢复默认", CloseButtonText = "取消",
            DefaultButton = ContentDialogButton.Primary, XamlRoot = XamlRoot
        };
        ContentDialogResult result = await dialog.ShowAsync();
        if (result == ContentDialogResult.Secondary)
        {
            SaveOverlaySettings(new DesktopSubtitleOverlaySettings { Enabled = current.Enabled,
                NormalizedX = current.NormalizedX, NormalizedBottom = current.NormalizedBottom });
            return;
        }
        if (result != ContentDialogResult.Primary) return;
        current.Enabled = enabled.IsOn;
        current.ShowOriginal = original.IsOn; current.ShowTranslation = translation.IsOn;
        current.OriginalFontSize = originalSize.Value; current.TranslationFontSize = translationSize.Value;
        current.Opacity = opacity.Value / 100; current.WidthFraction = width.Value / 100;
        current.RetentionSeconds = retention.Value; current.ShadowStrength = shadow.Value / 100;
        current.ClickThrough = clickThrough.IsOn; current.PositionLocked = locked.IsOn;
        SaveOverlaySettings(current);
        if (current.Enabled) ShowSubtitleOverlay(); else subtitleOverlayWindow?.HideOverlay();
    }
    private static Slider AddOverlaySlider(StackPanel content, string title, double value, double min, double max, string suffix)
    {
        var row = new StackPanel { Spacing = 3 };
        var label = new TextBlock { Text = $"{title} · {value:0}{suffix}", Style = (Style)Application.Current.Resources["CaptionTextBlockStyle"] };
        var slider = new Slider { Minimum = min, Maximum = max, Value = value, StepFrequency = 1 };
        AutomationProperties.SetName(slider, title);
        AutomationProperties.SetAutomationId(slider, $"Overlay{new string(title.Where(char.IsLetterOrDigit).ToArray())}");
        slider.ValueChanged += (_, args) => label.Text = $"{title} · {args.NewValue:0}{suffix}";
        row.Children.Add(label); row.Children.Add(slider); content.Children.Add(row);
        return slider;
    }
    private void Latest_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel.Entries.Count > 0) TranscriptList.ScrollIntoView(ViewModel.Entries.Last());
        isAtTranscriptEnd = true;
        NewContentButton.Visibility = Visibility.Collapsed;
    }
    private async void Summary_Click(object sender, RoutedEventArgs e) => await ViewModel.SummarizeAsync(SummaryScope.SelectedIndex);
    private async void Correction_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { DataContext: Subtitle entry }) return;
        string baselineSource = entry.English, baselineTranslation = entry.Chinese;
        var source = new TextBox { Header = "原文", Text = baselineSource, AcceptsReturn = true, MinHeight = 88, TextWrapping = TextWrapping.Wrap };
        var translation = new TextBox { Header = "译文", Text = baselineTranslation, AcceptsReturn = true, MinHeight = 88, TextWrapping = TextWrapping.Wrap };
        AutomationProperties.SetAutomationId(source, "CorrectionSource");
        AutomationProperties.SetAutomationId(translation, "CorrectionTranslation");
        var updateNotice = new TextBlock { Text = "本条识别稿仍在更新；保存只覆盖你编辑过的字段。", TextWrapping = TextWrapping.Wrap,
            Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"], Visibility = Visibility.Collapsed };
        var reload = new Button { Content = "载入最新识别稿", HorizontalAlignment = HorizontalAlignment.Left, Visibility = Visibility.Collapsed };
        var suggestionText = new TextBlock { TextWrapping = TextWrapping.Wrap, IsTextSelectionEnabled = true };
        var body = new StackPanel { Spacing = 10, MaxWidth = 620 };
        body.Children.Add(new TextBlock { Text = "录音会继续。只保存改动的字段；未编辑的内容继续接收识别结果。", TextWrapping = TextWrapping.Wrap,
            Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"] });
        body.Children.Add(updateNotice); body.Children.Add(reload); body.Children.Add(source); body.Children.Add(translation);
        if (entry.Correction is { } correction)
        {
            body.Children.Add(new TextBlock { Text = "识别稿（保留原始识别）", FontWeight = Microsoft.UI.Text.FontWeights.SemiBold });
            body.Children.Add(new TextBlock { Text = $"{correction.RawSource}\n{correction.RawTranslation}", TextWrapping = TextWrapping.Wrap, Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"] });
            if (correction.History.Count > 0)
                body.Children.Add(new TextBlock { Text = $"可撤销修改：{correction.History.Count} 次" });
        }
        var actions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        var ai = new Button { Content = "AI 校对" };
        var translate = new Button { Content = "重新翻译" };
        var undo = new Button { Content = "撤销上次纠正", IsEnabled = entry.CanUndoCorrection };
        var accept = new Button { Content = "应用建议", IsEnabled = false };
        AutomationProperties.SetAutomationId(ai, "RequestCorrection");
        AutomationProperties.SetAutomationId(translate, "RetranslateSubtitle");
        AutomationProperties.SetAutomationId(undo, "UndoCorrection");
        AutomationProperties.SetAutomationId(accept, "ApplyCorrectionSuggestion");
        actions.Children.Add(ai); actions.Children.Add(translate); actions.Children.Add(undo); actions.Children.Add(accept);
        body.Children.Add(actions); body.Children.Add(suggestionText);
        suggestionText.Text = ViewModel.GetCorrectionStatus(entry) ?? "";
        body.Children.Add(new TextBlock { Text = "AI 只读取文字。点击请求会把本句和相邻上下文发送给 DeepSeek，可能产生费用；建议需手动应用。", TextWrapping = TextWrapping.Wrap, Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"] });
        var root = new Grid();
        var bodyHost = new ContentControl { Content = body };
        root.Children.Add(bodyHost);
        var discardConfirmation = new Border
        {
            Width = 360, Padding = new Thickness(20), HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center,
            Background = (Brush)Application.Current.Resources["EchoCanvasBrush"], BorderBrush = (Brush)Application.Current.Resources["CardStrokeColorDefaultBrush"],
            BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(8), Visibility = Visibility.Collapsed
        };
        var discardActions = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Spacing = 8 };
        var discardButton = new Button { Content = "放弃修改" };
        var continueButton = new Button { Content = "继续编辑" };
        discardActions.Children.Add(discardButton); discardActions.Children.Add(continueButton);
        var discardContent = new StackPanel { Spacing = 16 };
        discardContent.Children.Add(new TextBlock { Text = "放弃尚未保存的修改？", FontWeight = Microsoft.UI.Text.FontWeights.SemiBold });
        discardContent.Children.Add(discardActions); discardConfirmation.Child = discardContent;
        root.Children.Add(discardConfirmation);
        AutomationProperties.SetAutomationId(discardButton, "DiscardCorrectionEdits");
        AutomationProperties.SetAutomationId(continueButton, "ContinueCorrectionEditing");
        string? sourceChange = null, translationChange = null;
        var dialog = new ContentDialog { Title = "纠正字幕", Content = root, PrimaryButtonText = "保存纠正", CloseButtonText = "关闭", DefaultButton = ContentDialogButton.Primary, XamlRoot = XamlRoot,
            IsPrimaryButtonEnabled = false };
        bool IsDirty() => source.Text != baselineSource || translation.Text != baselineTranslation;
        bool HasLiveUpdate() => entry.English != baselineSource || entry.Chinese != baselineTranslation;
        bool requesting = false, hasSuggestion = false, allowClose = false;
        void RefreshEditorState()
        {
            bool dirty = IsDirty();
            ai.IsEnabled = !dirty && !requesting;
            translate.IsEnabled = !dirty && !requesting && ViewModel.Config.Translate;
            undo.IsEnabled = !dirty && !requesting && entry.CanUndoCorrection;
            accept.IsEnabled = !dirty && !requesting && hasSuggestion;
            dialog.IsPrimaryButtonEnabled = dirty && !string.IsNullOrWhiteSpace(source.Text);
            updateNotice.Visibility = HasLiveUpdate() ? Visibility.Visible : Visibility.Collapsed;
            reload.Visibility = HasLiveUpdate() ? Visibility.Visible : Visibility.Collapsed;
            reload.IsEnabled = !dirty && !requesting;
        }
        source.TextChanged += (_, _) => RefreshEditorState();
        translation.TextChanged += (_, _) => RefreshEditorState();
        entry.PropertyChanged += Entry_PropertyChanged;
        void Entry_PropertyChanged(object? _, System.ComponentModel.PropertyChangedEventArgs args)
        {
            if (args.PropertyName is nameof(Subtitle.English) or nameof(Subtitle.Chinese)) RefreshEditorState();
        }
        reload.Click += (_, _) =>
        {
            source.Text = baselineSource = entry.English;
            translation.Text = baselineTranslation = entry.Chinese;
            suggestionText.Text = "已载入最新识别稿。";
            RefreshEditorState();
        };
        dialog.PrimaryButtonClick += (_, args) =>
        {
            if (string.IsNullOrWhiteSpace(source.Text)) { args.Cancel = true; ViewModel.Status = "原文不能为空。"; }
            else { sourceChange = source.Text != baselineSource ? source.Text : null; translationChange = translation.Text != baselineTranslation ? translation.Text : null; allowClose = true; }
        };
        dialog.Closing += (_, args) =>
        {
            if (allowClose || !IsDirty()) return;
            args.Cancel = true;
            bodyHost.IsEnabled = false;
            discardConfirmation.Visibility = Visibility.Visible;
        };
        continueButton.Click += (_, _) => { discardConfirmation.Visibility = Visibility.Collapsed; bodyHost.IsEnabled = true; };
        discardButton.Click += (_, _) => { allowClose = true; dialog.Hide(); };
        async Task Request(bool translateOnly)
        {
            requesting = true; RefreshEditorState();
            suggestionText.Text = "正在请求建议…";
            var result = await ViewModel.RequestCorrectionAsync(entry, translateOnly);
            if (result is not null)
            {
                suggestionText.Text = $"{(result.Uncertain ? "待确认建议（AI 无法确认原音）" : "AI 建议")}\n{result.Source}\n{result.Translation}\n{result.Reason}";
                accept.Tag = result;
                hasSuggestion = true;
            }
            else suggestionText.Text = ViewModel.Status;
            requesting = false; RefreshEditorState();
        }
        ai.Click += async (_, _) => await Request(false);
        translate.Click += async (_, _) => await Request(true);
        accept.Click += (_, _) =>
        {
            if (accept.Tag is CorrectionSuggestion result) { source.Text = result.Source; if (ViewModel.Config.Translate) translation.Text = result.Translation; }
        };
        undo.Click += (_, _) =>
        {
            ViewModel.UndoCorrection(entry); source.Text = baselineSource = entry.English; translation.Text = baselineTranslation = entry.Chinese;
            suggestionText.Text = "已撤销上次纠正；当前字幕保留为人工选择。"; RefreshEditorState();
        };
        if (ViewModel.GetSuggestion(entry) is { } prior)
        {
            suggestionText.Text = $"{(prior.Uncertain ? "待确认建议（AI 无法确认原音）" : "AI 建议")}\n{prior.Source}\n{prior.Translation}\n{prior.Reason}";
            hasSuggestion = true; accept.Tag = prior;
        }
        RefreshEditorState();
        ContentDialogResult result = await dialog.ShowAsync();
        entry.PropertyChanged -= Entry_PropertyChanged;
        if (result == ContentDialogResult.Primary) ViewModel.SaveCorrection(entry, sourceChange, translationChange);
    }
    private void Topmost_Toggled(object sender, RoutedEventArgs e)
    {
        if (App.Window?.AppWindow.Presenter is OverlappedPresenter presenter) presenter.IsAlwaysOnTop = ((Microsoft.UI.Xaml.Controls.Primitives.ToggleButton)sender).IsChecked == true;
    }
    private void SaveSettings_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (Translate.IsOn && string.IsNullOrWhiteSpace(ViewModel.Config.TargetLanguage)) throw new InvalidOperationException("请填写翻译目标语言。");
            var c = ViewModel.Config;
            c.SonioxSecret = Preferences.Protect(SonioxKey.Password.Trim()); c.DeepSeekSecret = Preferences.Protect(DeepSeekKey.Password.Trim());
            c.CorrectionTerms = CorrectionTerms.Text.Trim();
            c.Translate = Translate.IsOn; c.Strict = Strict.IsOn; c.Speakers = Speakers.IsOn;
            if (SettingsSourceLanguage.SelectedItem is LanguageChoice source) c.SourceLanguage = source.Code ?? string.Empty;
            if (SettingsTargetLanguage.SelectedItem is LanguageChoice target && target.Code is not null) c.TargetLanguage = target.Code;
            c.Segmentation = new TranscriptSegmentationSettings
            {
                SonioxMaxEndpointDelayMilliseconds = (int)Math.Round(EndpointDelay.Value),
                SonioxEndpointSensitivity = EndpointSensitivity.Value,
                SonioxEndpointLatencyAdjustmentLevel = (int)Math.Round(EndpointLatency.Value),
                LocalSilenceFallbackEnabled = SilenceFallback.IsOn,
                LocalSilenceThresholdSeconds = SilenceThreshold.Value,
                LocalSilenceMinimumWordCount = (int)Math.Round(SilenceWords.Value),
                LongSegmentFallbackEnabled = LongFallback.IsOn,
                LongSegmentWordThreshold = (int)Math.Round(LongWords.Value),
                LongSegmentDurationThresholdSeconds = LongDuration.Value
            }.Validate();
            c.AutoCorrectionEnabled = AutoCorrection.IsOn;
            c.AutoSummaryEnabled = AutoSummary.IsOn;
            c.Theme = ThemeChoice.SelectedIndex == 1 ? "Light" : ThemeChoice.SelectedIndex == 2 ? "Dark" : "Default";
            c.Save();
            loadingSettings = true;
            LoadSegmentationSettings(c.Segmentation);
            loadingSettings = false;
            ApplyWindowTheme();
            UpdateAudioDisplay();
            UpdateLanguageHeaders();
            ViewModel.Status = "设置已保存，Key 使用当前 Windows 用户加密。";
            Back_Click(sender, e);
        }
        catch (Exception error) { ViewModel.Status = "设置保存失败：" + error.Message; }
    }
    private void Folder_Click(object sender, RoutedEventArgs e)
    {
        try { Directory.CreateDirectory(TranscriptFiles.Root); Process.Start(new ProcessStartInfo("explorer.exe", TranscriptFiles.Root) { UseShellExecute = true }); }
        catch (Exception error) { ViewModel.Status = error.Message; }
    }
    private async void Import_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var picker = new FileOpenPicker(App.Window.AppWindow.Id); picker.FileTypeFilter.Add(".json");
            var file = await picker.PickSingleFileAsync();
            if (file is not null)
            {
                if (new FileInfo(file.Path).Length > 32 * 1024 * 1024) throw new InvalidOperationException("存档超过 32 MB，请先拆分存档。");
                await ViewModel.ImportAsync(await File.ReadAllTextAsync(file.Path));
            }
        }
        catch (Exception error) { ViewModel.Status = "导入失败：" + error.Message; }
    }
    private async Task ExportAsync(bool srt)
    {
        try
        {
            if (ViewModel.SelectedArchive is not { } archive) { ViewModel.Status = "请先选择或创建存档。"; return; }
            var snapshot = TranscriptFiles.Snapshot(archive);
            string text = await Task.Run(() => srt ? TranscriptFiles.Srt(snapshot) : JsonSerializer.Serialize(snapshot, TranscriptFiles.Json));
            var picker = new FileSavePicker(App.Window.AppWindow.Id) { SuggestedFileName = $"Echo-{DateTime.Now:yyyyMMdd-HHmmss}" };
            picker.FileTypeChoices.Add(srt ? "SRT 字幕" : "Echo 存档", new List<string> { srt ? ".srt" : ".json" });
            var file = await picker.PickSaveFileAsync();
            if (file is not null) { await File.WriteAllTextAsync(file.Path, text); ViewModel.Status = "已导出：" + file.Path; }
        }
        catch (Exception error) { ViewModel.Status = "导出失败：" + error.Message; }
    }
    private async void ExportSrt_Click(object sender, RoutedEventArgs e) => await ExportAsync(true);
    private async void ExportArchive_Click(object sender, RoutedEventArgs e) => await ExportAsync(false);
}
