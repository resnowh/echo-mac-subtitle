using Echo_Windows.Core;
using Echo_Windows.Services;
using Echo_Windows.ViewModels;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
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
    public MainPageViewModel ViewModel { get; } = new();
    public MainPage()
    {
        InitializeComponent();
        var c = ViewModel.Config;
        SourceLanguageChoice.ItemsSource = SourceLanguages;
        TargetLanguageChoice.ItemsSource = TargetLanguages;
        SonioxModel.Text = c.SonioxModel; DeepSeekModel.Text = c.DeepSeekModel;
        CorrectionTerms.Text = c.CorrectionTerms;
        AutoCorrection.IsOn = c.AutoCorrectionEnabled;
        Translate.IsOn = c.Translate; Speakers.IsOn = c.Speakers; Strict.IsOn = c.Strict;
        try { SonioxKey.Password = Preferences.Unprotect(c.SonioxSecret); DeepSeekKey.Password = Preferences.Unprotect(c.DeepSeekSecret); }
        catch { ViewModel.Status = "密钥无法解密，请重新输入并保存。"; }
        ThemeChoice.SelectedIndex = c.Theme == "Light" ? 1 : c.Theme == "Dark" ? 2 : 0;
        if (c.Theme is "Light" or "Dark") RequestedTheme = Enum.Parse<ElementTheme>(c.Theme);
        Loaded += async (_, _) =>
        {
            ApplyWindowTheme();
            if (initialized) return;
            initialized = true;
            await ViewModel.LoadArchivesAsync();
        };
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
        UpdateRecordingLanguageHint();
    }
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
        if (ViewModel.CanEdit) SonioxKey.Focus(FocusState.Programmatic);
        else BackSettingsButton.Focus(FocusState.Programmatic);
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
        var source = new TextBox { Header = "原文", Text = entry.English, AcceptsReturn = true, MinHeight = 88, TextWrapping = TextWrapping.Wrap };
        var translation = new TextBox { Header = "译文", Text = entry.Chinese, AcceptsReturn = true, MinHeight = 88, TextWrapping = TextWrapping.Wrap };
        AutomationProperties.SetAutomationId(source, "CorrectionSource");
        AutomationProperties.SetAutomationId(translation, "CorrectionTranslation");
        var suggestionText = new TextBlock { TextWrapping = TextWrapping.Wrap, IsTextSelectionEnabled = true };
        var body = new StackPanel { Spacing = 10, MaxWidth = 620 };
        body.Children.Add(source); body.Children.Add(translation);
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
        var dialog = new ContentDialog { Title = "纠正字幕", Content = body, PrimaryButtonText = "保存纠正", CloseButtonText = "取消", DefaultButton = ContentDialogButton.Primary, XamlRoot = XamlRoot };
        dialog.PrimaryButtonClick += (_, args) =>
        {
            if (string.IsNullOrWhiteSpace(source.Text)) { args.Cancel = true; ViewModel.Status = "原文不能为空。"; }
        };
        async Task Request(bool translateOnly)
        {
            ai.IsEnabled = translate.IsEnabled = accept.IsEnabled = false;
            suggestionText.Text = "正在请求建议…";
            var result = await ViewModel.RequestCorrectionAsync(entry, translateOnly);
            if (result is not null)
            {
                suggestionText.Text = $"{(result.Uncertain ? "待确认建议（AI 无法确认原音）" : "AI 建议")}\n{result.Source}\n{result.Translation}\n{result.Reason}";
                accept.IsEnabled = true;
                accept.Tag = result;
            }
            else suggestionText.Text = ViewModel.Status;
            ai.IsEnabled = translate.IsEnabled = true;
        }
        ai.Click += async (_, _) => await Request(false);
        translate.Click += async (_, _) => await Request(true);
        accept.Click += (_, _) =>
        {
            if (accept.Tag is CorrectionSuggestion result) { source.Text = result.Source; if (ViewModel.Config.Translate) translation.Text = result.Translation; }
        };
        undo.Click += (_, _) =>
        {
            ViewModel.UndoCorrection(entry); source.Text = entry.English; translation.Text = entry.Chinese;
            undo.IsEnabled = entry.CanUndoCorrection; suggestionText.Text = "已撤销上次纠正；当前字幕保留为人工选择。";
        };
        if (ViewModel.GetSuggestion(entry) is { } prior)
        {
            suggestionText.Text = $"{(prior.Uncertain ? "待确认建议（AI 无法确认原音）" : "AI 建议")}\n{prior.Source}\n{prior.Translation}\n{prior.Reason}";
            accept.IsEnabled = true; accept.Tag = prior;
        }
        if (await dialog.ShowAsync() == ContentDialogResult.Primary) ViewModel.SaveCorrection(entry, source.Text, translation.Text);
    }
    private void Topmost_Toggled(object sender, RoutedEventArgs e)
    {
        if (App.Window?.AppWindow.Presenter is OverlappedPresenter presenter) presenter.IsAlwaysOnTop = ((Microsoft.UI.Xaml.Controls.Primitives.ToggleButton)sender).IsChecked == true;
    }
    private void SaveSettings_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(SonioxModel.Text) || (Translate.IsOn && string.IsNullOrWhiteSpace(ViewModel.Config.TargetLanguage))) throw new InvalidOperationException("请填写模型和翻译目标语言。");
            var c = ViewModel.Config;
            c.SonioxSecret = Preferences.Protect(SonioxKey.Password.Trim()); c.DeepSeekSecret = Preferences.Protect(DeepSeekKey.Password.Trim());
            c.CorrectionTerms = CorrectionTerms.Text.Trim();
            c.SonioxModel = SonioxModel.Text.Trim(); c.DeepSeekModel = DeepSeekModel.Text.Trim();
            c.Translate = Translate.IsOn; c.Strict = Strict.IsOn; c.Speakers = Speakers.IsOn;
            c.AutoCorrectionEnabled = AutoCorrection.IsOn;
            c.Theme = ThemeChoice.SelectedIndex == 1 ? "Light" : ThemeChoice.SelectedIndex == 2 ? "Dark" : "Default";
            c.Save();
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
