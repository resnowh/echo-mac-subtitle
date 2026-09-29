using Echo_Windows.Core;
using Echo_Windows.Services;
using Echo_Windows.ViewModels;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using Microsoft.Windows.Storage.Pickers;
using System.Diagnostics;
using System.Text.Json;

namespace Echo_Windows;

public sealed partial class MainPage : Page
{
    private readonly List<Border> waveform = [];
    private int waveformFrame;
    public MainPageViewModel ViewModel { get; } = new();
    public MainPage()
    {
        InitializeComponent();
        var c = ViewModel.Config;
        SonioxModel.Text = c.SonioxModel; DeepSeekModel.Text = c.DeepSeekModel;
        CorrectionTerms.Text = c.CorrectionTerms;
        SourceLanguage.Text = c.SourceLanguage; TargetLanguage.Text = c.TargetLanguage;
        Translate.IsOn = c.Translate; Speakers.IsOn = c.Speakers; Strict.IsOn = c.Strict;
        try { SonioxKey.Password = Preferences.Unprotect(c.SonioxSecret); DeepSeekKey.Password = Preferences.Unprotect(c.DeepSeekSecret); }
        catch { ViewModel.Status = "密钥无法解密，请重新输入并保存。"; }
        ThemeChoice.SelectedIndex = c.Theme == "Light" ? 1 : c.Theme == "Dark" ? 2 : 0;
        RequestedTheme = Enum.TryParse<ElementTheme>(c.Theme, out var theme) ? theme : ElementTheme.Default;
        Loaded += (_, _) => ApplyWindowTheme();
        ViewModel.Entries.CollectionChanged += (_, _) => EmptyHint.Visibility = ViewModel.Entries.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        EmptyHint.Visibility = ViewModel.Entries.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        for (int i = 0; i < 64; i++)
        {
            var bar = new Border { Width = 3, Height = 2, CornerRadius = new CornerRadius(2) };
            waveform.Add(bar);
            WaveformBars.Children.Add(bar);
        }
        ViewModel.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is nameof(ViewModel.Level) or nameof(ViewModel.IsRecording) or nameof(ViewModel.Status)) UpdateAudioDisplay();
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
    private void UpdateLanguageHeaders()
    {
        SourceHeading.Text = ViewModel.Config.SourceLanguage switch { "en" => "English", "zh" => "简体中文", "ja" => "日本語", "" => "原文", var code => code };
        TargetHeading.Text = ViewModel.Config.TargetLanguage switch { "zh" => "简体中文", "en" => "English", "ja" => "日本語", var code => code };
        TargetHeading.Visibility = ViewModel.Config.Translate ? Visibility.Visible : Visibility.Collapsed;
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
        RequestedTheme = Enum.Parse<ElementTheme>(ViewModel.Config.Theme);
        ApplyWindowTheme();
        UpdateAudioDisplay();
        ViewModel.Config.Save();
    }
    private void ApplyWindowTheme()
    {
        if (App.Window?.Content is FrameworkElement root) root.RequestedTheme = RequestedTheme;
    }
    private void Settings_Click(object sender, RoutedEventArgs e)
    {
        RecordingPanel.IsHitTestVisible = false;
        SettingsOverlay.Visibility = Visibility.Visible;
    }
    private void Back_Click(object sender, RoutedEventArgs e)
    {
        SettingsOverlay.Visibility = Visibility.Collapsed;
        RecordingPanel.IsHitTestVisible = true;
    }
    private void New_Click(object sender, RoutedEventArgs e) => ViewModel.NewArchive();
    private async void Start_Click(object sender, RoutedEventArgs e) => await ViewModel.StartAsync(Mode.SelectedIndex, (OutputDevice.SelectedItem as AudioDevice)?.Id, (InputDevice.SelectedItem as AudioDevice)?.Id);
    private async void Stop_Click(object sender, RoutedEventArgs e) => await ViewModel.StopAsync();
    private void Refresh_Click(object sender, RoutedEventArgs e) => ViewModel.RefreshDevices();
    private void Latest_Click(object sender, RoutedEventArgs e) { if (ViewModel.Entries.Count > 0) TranscriptList.ScrollIntoView(ViewModel.Entries.Last()); }
    private async void Summary_Click(object sender, RoutedEventArgs e) => await ViewModel.SummarizeAsync(SummaryScope.SelectedIndex);
    private async void Correction_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { DataContext: Subtitle entry }) return;
        var source = new TextBox { Header = "原文", Text = entry.English, AcceptsReturn = true, MinHeight = 88, TextWrapping = TextWrapping.Wrap };
        var translation = new TextBox { Header = "译文", Text = entry.Chinese, AcceptsReturn = true, MinHeight = 88, TextWrapping = TextWrapping.Wrap };
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
        actions.Children.Add(ai); actions.Children.Add(translate); actions.Children.Add(undo); actions.Children.Add(accept);
        body.Children.Add(actions); body.Children.Add(suggestionText);
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
            if (string.IsNullOrWhiteSpace(SonioxModel.Text) || (Translate.IsOn && string.IsNullOrWhiteSpace(TargetLanguage.Text))) throw new InvalidOperationException("请填写模型和翻译目标语言。");
            var c = ViewModel.Config;
            c.SonioxSecret = Preferences.Protect(SonioxKey.Password.Trim()); c.DeepSeekSecret = Preferences.Protect(DeepSeekKey.Password.Trim());
            c.CorrectionTerms = CorrectionTerms.Text.Trim();
            c.SonioxModel = SonioxModel.Text.Trim(); c.DeepSeekModel = DeepSeekModel.Text.Trim();
            c.SourceLanguage = SourceLanguage.Text.Trim(); c.TargetLanguage = TargetLanguage.Text.Trim();
            c.Translate = Translate.IsOn; c.Strict = Strict.IsOn; c.Speakers = Speakers.IsOn;
            c.Theme = ThemeChoice.SelectedIndex == 1 ? "Light" : ThemeChoice.SelectedIndex == 2 ? "Dark" : "Default";
            c.Save(); RequestedTheme = Enum.Parse<ElementTheme>(c.Theme);
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
                ViewModel.Import(await File.ReadAllTextAsync(file.Path));
            }
        }
        catch (Exception error) { ViewModel.Status = "导入失败：" + error.Message; }
    }
    private async Task ExportAsync(bool srt)
    {
        try
        {
            if (ViewModel.SelectedArchive is not { } archive) { ViewModel.Status = "请先选择或创建存档。"; return; }
            string text = srt ? TranscriptFiles.Srt(archive) : JsonSerializer.Serialize(archive, TranscriptFiles.Json);
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
