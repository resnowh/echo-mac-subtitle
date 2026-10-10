using System.Text.Json;
using System.Collections.Concurrent;
using System.Globalization;
using System.Diagnostics;
using System.Reflection;
using Echo_Windows.Core;
using Echo_Windows.Services;
using NAudio.CoreAudioApi;
using NAudio.Wave;
using System.Net;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Text;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Runtime.InteropServices;
using System.Xml.Linq;

if (args.Length == 4 && args[0] == "--atomic-write-crash-child")
{
    TranscriptFiles.AtomicWrite(args[1], args[2], () =>
    {
        File.WriteAllText(args[3], "flush-complete");
        Thread.Sleep(Timeout.Infinite);
    });
    return;
}

int passed = 0;
void Check(bool condition, string name) { if (!condition) throw new Exception("FAIL: " + name); Console.WriteLine("PASS: " + name); passed++; }
void RunIcacls(string path, params string[] arguments)
{
    var startInfo = new ProcessStartInfo("icacls.exe") { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
    startInfo.ArgumentList.Add(path);
    foreach (string argument in arguments) startInfo.ArgumentList.Add(argument);
    using var process = Process.Start(startInfo) ?? throw new InvalidOperationException("Unable to start icacls for the isolated ACL check.");
    string output = process.StandardOutput.ReadToEnd() + process.StandardError.ReadToEnd();
    process.WaitForExit();
    if (process.ExitCode != 0) throw new InvalidOperationException($"icacls failed with exit code {process.ExitCode}: {output}");
}
var segment = new Segment { StartedAt = 800000000 };
var assembly = new TokenAssembler(segment, _ => { });
var mainWindowMinimum = MainWindowSizePolicy.Minimum(1920, 1080, 1);
var mainWindowInitial = MainWindowSizePolicy.Initial(1920, 1080, 1);
var mainWindowAt150Percent = MainWindowSizePolicy.Initial(1920, 1080, 1.5);
var mainWindowConstrained = MainWindowSizePolicy.Initial(700, 500, 1);
Check(mainWindowMinimum == new MainWindowPixelSize(680, 520)
    && mainWindowInitial == new MainWindowPixelSize(820, 650)
    && mainWindowAt150Percent == new MainWindowPixelSize(1230, 975)
    && mainWindowConstrained == new MainWindowPixelSize(700, 500),
    "main-window minimum and ideal sizes match Mac in DIPs, scale with DPI, and fit a smaller work area");
Check(new Preferences().Theme == "Dark" && JsonSerializer.Deserialize<Preferences>("{}", TranscriptFiles.Json)?.Theme == "Dark",
    "new and legacy preferences default to the Mac dark theme when no explicit theme is stored");
var migratedPreferredLanguage = new Preferences { SourceLanguage = "ja" }.Validate();
var retainedPreferredLanguage = new Preferences { SourceLanguage = "", PreferredSourceLanguage = "fr" }.Validate();
Check(migratedPreferredLanguage.PreferredSourceLanguage == "ja"
    && retainedPreferredLanguage.SourceLanguage == "" && retainedPreferredLanguage.PreferredSourceLanguage == "fr",
    "recognition mode keeps the last preferred language when switching to automatic and migrates legacy settings");
var legacyAudioPreferences = JsonSerializer.Deserialize<Preferences>("{}", TranscriptFiles.Json);
var audioModeRoundTrip = JsonSerializer.Deserialize<Preferences>(JsonSerializer.Serialize(new Preferences { AudioInputMode = 2 }, TranscriptFiles.Json), TranscriptFiles.Json);
Check(new Preferences().AudioInputMode == 1 && legacyAudioPreferences?.AudioInputMode == 1
    && audioModeRoundTrip?.AudioInputMode == 2 && new Preferences { AudioInputMode = 9 }.Validate().AudioInputMode == 1,
    "audio input mode defaults to Mac microphone, persists, and repairs unsupported values");
var correctionPreferences = new Preferences { SourceLanguage = "en", TargetLanguage = "zh", Translate = true, Strict = false, Speakers = true };
var correctionRecognitionSnapshot = CorrectionRecognitionSnapshot.Capture(correctionPreferences);
Check(correctionRecognitionSnapshot.Matches(correctionPreferences)
    && !correctionRecognitionSnapshot.Matches(new Preferences { SourceLanguage = "", TargetLanguage = "zh", Translate = true, Strict = false, Speakers = true })
    && !correctionRecognitionSnapshot.Matches(new Preferences { SourceLanguage = "en", TargetLanguage = "ja", Translate = true, Strict = false, Speakers = true })
    && !correctionRecognitionSnapshot.Matches(new Preferences { SourceLanguage = "en", TargetLanguage = "zh", Translate = false, Strict = false, Speakers = true })
    && !correctionRecognitionSnapshot.Matches(new Preferences { SourceLanguage = "en", TargetLanguage = "zh", Translate = true, Strict = true, Speakers = true })
    && !correctionRecognitionSnapshot.Matches(new Preferences { SourceLanguage = "en", TargetLanguage = "zh", Translate = true, Strict = false, Speakers = false }),
    "AI correction recognition snapshots become stale when any Mac-equivalent language or speaker option changes");
var mainPageViewModelSource = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "MainPageViewModel.cs"));
var audioCaptureSource = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "AudioCapture.cs"));
Check(audioCaptureSource.Contains("flags.HasFlag(AudioClientBufferFlags.Silent)", StringComparison.Ordinal)
    && audioCaptureSource.Contains("buffer.AddSamples(data)", StringComparison.Ordinal)
    && audioCaptureSource.Contains("source.FirstConvertedFrame.TrySetResult()", StringComparison.Ordinal),
    "WASAPI microphone readiness accepts silent packets and marks converted output only after valid samples are buffered");
Check(mainPageViewModelSource.Contains("CorrectionRecognitionSnapshot.Capture(Config)", StringComparison.Ordinal)
    && mainPageViewModelSource.Contains("!recognitionSnapshot.Matches(Config)", StringComparison.Ordinal)
    && mainPageViewModelSource.Contains("旧建议已忽略", StringComparison.Ordinal),
    "AI correction results are discarded when recognition settings change while the request is in flight");
Check(mainPageViewModelSource.Contains("newContentStartSegmentIndex = SelectedArchive.Segments.Count", StringComparison.Ordinal)
    && mainPageViewModelSource.Contains("Select(requestedArchive, scope, newContentStartSegmentIndex)", StringComparison.Ordinal),
    "new-content summaries are limited to segments created since the current recording session began");
Check(mainPageViewModelSource.Contains("TranscriptTextChunks.Create(source, archive: requestedArchive)", StringComparison.Ordinal)
    && mainPageViewModelSource.Contains("TranscriptSummaryPrompt.Build(scope, chunks[i])", StringComparison.Ordinal)
    && mainPageViewModelSource.Contains("你是一个专业的会议和演讲总结助手。请用简体中文回答。", StringComparison.Ordinal),
    "AI summaries submit archive-aware bilingual transcript chunks with scope-specific Mac prompts");
var computerOnlySelection = AudioInputModeSelection.ForSwitch(0, "speaker-id", "mic-id");
var microphoneOnlySelection = AudioInputModeSelection.ForSwitch(1, "speaker-id", "mic-id");
var mixedSelection = AudioInputModeSelection.ForSwitch(2, "speaker-id", "mic-id");
Check(computerOnlySelection == new AudioInputModeSelection(0, "speaker-id", null)
    && microphoneOnlySelection == new AudioInputModeSelection(1, null, "mic-id")
    && mixedSelection == new AudioInputModeSelection(2, "speaker-id", "mic-id"),
    "live audio mode switching keeps only the capture routes required by the selected mode");
var audioModePageXaml = XDocument.Load(Path.Combine(AppContext.BaseDirectory, "Fixtures", "MainPage.xaml"));
XNamespace mainPageXamlNamespace = "http://schemas.microsoft.com/winfx/2006/xaml";
var audioModeControl = audioModePageXaml.Descendants(XName.Get("ComboBox", "http://schemas.microsoft.com/winfx/2006/xaml/presentation"))
    .FirstOrDefault(element => element.Attribute(mainPageXamlNamespace + "Name")?.Value == "Mode");
Check(audioModeControl?.Attribute("SelectionChanged")?.Value == "AudioMode_SelectionChanged"
    && audioModeControl.Attribute("IsEnabled")?.Value.Contains("CanChangeAudioMode", StringComparison.Ordinal) == true,
    "audio input mode remains available during recording and routes selections through the live switch handler");
string correctionEditorSource = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "MainPage.xaml.cs"));
Check(correctionEditorSource.Contains("var historyExpander = new Expander { Header = \"识别稿与修改前版本\"", StringComparison.Ordinal)
    && correctionEditorSource.Contains("SetAutomationId(historyExpander, \"CorrectionHistory\")", StringComparison.Ordinal)
    && correctionEditorSource.Contains("correction.RawSource", StringComparison.Ordinal)
    && correctionEditorSource.Contains("correction.RawTranslation", StringComparison.Ordinal)
    && correctionEditorSource.Contains("foreach (var version in correction.History)", StringComparison.Ordinal)
    && correctionEditorSource.Contains("version.Date.ToLocalTime().ToString(\"t\")", StringComparison.Ordinal)
    && correctionEditorSource.Contains("version.Source", StringComparison.Ordinal)
    && correctionEditorSource.Contains("version.Translation", StringComparison.Ordinal)
    && correctionEditorSource.Contains("var bodyHost = new ScrollViewer", StringComparison.Ordinal)
    && correctionEditorSource.Contains("VerticalScrollBarVisibility = ScrollBarVisibility.Auto", StringComparison.Ordinal)
    && correctionEditorSource.Contains("MaxHeight = 480", StringComparison.Ordinal)
    && correctionEditorSource.Contains("SetAutomationId(termInput, \"CorrectionTerm\")", StringComparison.Ordinal)
    && correctionEditorSource.Contains("SetAutomationId(addTerm, \"AddCorrectionTerm\")", StringComparison.Ordinal)
    && correctionEditorSource.Contains("ViewModel.AddCorrectionTerm(termInput.Text)", StringComparison.Ordinal)
    && correctionEditorSource.Contains("CorrectionTerms.Text = ViewModel.Config.CorrectionTerms", StringComparison.Ordinal),
    "subtitle correction editor exposes dated history, bounded scrolling, and inline term addition like Mac");
Check(ThemePreference.IndexFor("Light") == 0 && ThemePreference.IndexFor("Dark") == 1 && ThemePreference.IndexFor("Default") == 2
    && ThemePreference.Next("Dark") == "Default" && ThemePreference.Next("Default") == "Light" && ThemePreference.Next("Light") == "Dark",
    "theme choices and main-window cycling follow the Mac light, dark, system order");
var overlaySettings = new DesktopSubtitleOverlaySettings();
Check(overlaySettings.OriginalFontSize == 26 && overlaySettings.TranslationFontSize == 24 && overlaySettings.WidthFraction == .75 && overlaySettings.RetentionSeconds == 5,
    "desktop subtitle overlay defaults match the Mac visual baseline");
var overlayAt100 = DesktopSubtitleOverlayPlacement.Calculate(0, 0, 1920, 1080, 1, .75, .5, .09);
var overlayAt150 = DesktopSubtitleOverlayPlacement.Calculate(-2560, 0, 2560, 1440, 1.5, .75, .5, .09);
var overlayAt200 = DesktopSubtitleOverlayPlacement.Calculate(1920, -200, 1280, 1024, 2, .75, 1, 1);
Check(overlayAt100 == new OverlayPlacement(240, 833, 1440, 150)
    && overlayAt150 == new OverlayPlacement(-2240, 1086, 1920, 225)
    && overlayAt200 == new OverlayPlacement(2240, -200, 960, 300),
    "overlay placement scales with DPI, supports negative multi-monitor origins, and clamps to short work areas");
string nativeOverlaySource = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "NativeDesktopSubtitleOverlayWindow.cs"));
string normalizedNativeOverlaySource = nativeOverlaySource.Replace("\r\n", "\n", StringComparison.Ordinal);
Check(normalizedNativeOverlaySource.Contains("case WmSettingChange when wParam == SpiSetWorkArea:\n                OnDisplayConfigurationChanged();\n                return 0;", StringComparison.Ordinal)
    && normalizedNativeOverlaySource.Contains("case WmDisplayChange:\n                OnDisplayConfigurationChanged();\n                return 0;", StringComparison.Ordinal),
    "native overlay recomputes its placement when Windows reports display or work-area changes");
Check(normalizedNativeOverlaySource.Contains("public void HideOverlay()\n    {\n        if (adjusting) SetAdjusting(false);", StringComparison.Ordinal),
    "hiding the native overlay exits adjustment mode as Mac hide does");
Check(normalizedNativeOverlaySource.Contains("0, 0, GetModuleHandle(null), 0);", StringComparison.Ordinal)
    && normalizedNativeOverlaySource.Contains("ShowWindow(hwnd, SwShowNoActivate);", StringComparison.Ordinal)
    && normalizedNativeOverlaySource.Contains("SetWindowPos(hwnd, HwndTopMost", StringComparison.Ordinal),
    "native subtitle overlay is an unowned, topmost window shown without activating or following main-window minimization");
string mainWindowSource = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "MainWindow.xaml.cs"));
Check(mainWindowSource.Contains("if (RootFrame.Content is MainPage page) page.CloseSubtitleOverlay();", StringComparison.Ordinal)
    && normalizedNativeOverlaySource.Contains("expiryTimer.Stop();\n        publishTimer.Stop();", StringComparison.Ordinal)
    && normalizedNativeOverlaySource.Contains("feed.PropertyChanged -= Feed_PropertyChanged;", StringComparison.Ordinal)
    && normalizedNativeOverlaySource.Contains("DestroyWindow(hwnd);", StringComparison.Ordinal),
    "closing the main window disposes the overlay timers, feed subscription, and native HWND");
var savedDisplaySettings = JsonSerializer.Deserialize<DesktopSubtitleOverlaySettings>(JsonSerializer.Serialize(new DesktopSubtitleOverlaySettings { DisplayId = 42, DisplayDeviceName = @"\\.\DISPLAY2" }, TranscriptFiles.Json), TranscriptFiles.Json);
var legacyOverlaySettings = JsonSerializer.Deserialize<DesktopSubtitleOverlaySettings>("{}", TranscriptFiles.Json);
Check(savedDisplaySettings is not null && savedDisplaySettings.DisplayId == 42 && savedDisplaySettings.DisplayDeviceName == @"\\.\DISPLAY2"
    && legacyOverlaySettings is not null && legacyOverlaySettings.DisplayId is null && legacyOverlaySettings.DisplayDeviceName is null,
    "overlay monitor identity round-trips while older settings remain compatible");
overlaySettings.ShowOriginal = false; overlaySettings.ShowTranslation = false; overlaySettings.Opacity = double.NaN; overlaySettings.Validate();
Check(overlaySettings.ShowOriginal && overlaySettings.Opacity == 1, "overlay settings retain a visible language and repair invalid persisted values");
var translationOnlySettings = new DesktopSubtitleOverlaySettings { ShowOriginal = false, ShowTranslation = true };
var originalOnlyState = new DesktopSubtitleOverlayState(Guid.NewGuid(), "Original", "", false, false, Guid.NewGuid(), null);
var translatedState = originalOnlyState with { Translation = "译文", TranslationEnabled = true };
var noLanguageSettings = new DesktopSubtitleOverlaySettings { ShowOriginal = false, ShowTranslation = false }.Validate();
Check(DesktopSubtitleOverlayPresentation.ShouldShowOriginal(originalOnlyState, translationOnlySettings)
    && !DesktopSubtitleOverlayPresentation.ShouldShowTranslation(originalOnlyState, translationOnlySettings)
    && !DesktopSubtitleOverlayPresentation.ShouldShowOriginal(translatedState, translationOnlySettings)
    && DesktopSubtitleOverlayPresentation.ShouldShowTranslation(translatedState, translationOnlySettings)
    && DesktopSubtitleOverlayPresentation.ShouldShowOriginal(translatedState, noLanguageSettings),
    "overlay visibility matches Mac when translation is disabled, when only translation is selected, and when both language switches are off");
XNamespace presentationNamespace = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
XNamespace xamlNamespace = "http://schemas.microsoft.com/winfx/2006/xaml";
Check(normalizedNativeOverlaySource.Contains("new CanvasTextLayout(drawing, text, format, width, maxLayoutHeight)", StringComparison.Ordinal)
    && normalizedNativeOverlaySource.Contains("TrimmingGranularity = CanvasTextTrimmingGranularity.Character", StringComparison.Ordinal)
    && normalizedNativeOverlaySource.Contains("TrimmingSign = CanvasTrimmingSign.Ellipsis", StringComparison.Ordinal)
    && normalizedNativeOverlaySource.Contains("drawing.DrawTextLayout(layout", StringComparison.Ordinal),
    "native DirectWrite overlay uses a bounded two-line layout with Mac-equivalent tail ellipsis for text and shadows");
Check(normalizedNativeOverlaySource.Contains("private const int MaxSubtitleLines = 2;", StringComparison.Ordinal)
    && normalizedNativeOverlaySource.Contains("float maxLayoutHeight = Math.Min(height, fontSize * 1.175f * MaxSubtitleLines);", StringComparison.Ordinal),
    "native overlay constrains each subtitle line to at most two DirectWrite lines");
var mainPageXaml = XDocument.Load(Path.Combine(AppContext.BaseDirectory, "Fixtures", "MainPage.xaml"));
var startRecordingControl = mainPageXaml.Descendants(presentationNamespace + "Button")
    .Single(element => element.Attribute("AutomationProperties.AutomationId")?.Value == "StartRecording");
var stopRecordingControl = mainPageXaml.Descendants(presentationNamespace + "Button")
    .Single(element => element.Attribute("AutomationProperties.AutomationId")?.Value == "StopRecording");
var archiveListControl = mainPageXaml.Descendants(presentationNamespace + "ListView")
    .Single(element => element.Attribute("AutomationProperties.AutomationId")?.Value == "ArchiveList");
var recordingStatusControl = mainPageXaml.Descendants(presentationNamespace + "TextBlock")
    .Single(element => element.Attribute("AutomationProperties.AutomationId")?.Value == "Status");
Check(startRecordingControl.Attribute("AutomationProperties.Name")?.Value == "开始录音"
    && stopRecordingControl.Attribute("AutomationProperties.Name")?.Value == "停止录音"
    && archiveListControl.Attribute("AutomationProperties.Name")?.Value == "本地存档"
    && recordingStatusControl.Attribute("AutomationProperties.Name")?.Value.Contains("AccessibleText('录音状态', ViewModel.Status)", StringComparison.Ordinal) == true
    && recordingStatusControl.Attribute("AutomationProperties.LiveSetting")?.Value == "Polite",
    "recording actions, archive picker, and live status expose contextual Narrator names");
var recognitionModeSelector = mainPageXaml.Descendants(presentationNamespace + "SelectorBar")
    .FirstOrDefault(element => element.Attribute(xamlNamespace + "Name")?.Value == "SettingsSourceMode");
var preferredSourceGroup = mainPageXaml.Descendants(presentationNamespace + "StackPanel")
    .FirstOrDefault(element => element.Attribute(xamlNamespace + "Name")?.Value == "PreferredSourceSettings");
var recognitionModeLabels = recognitionModeSelector?.Elements(presentationNamespace + "SelectorBarItem")
    .Select(element => element.Attribute("Text")?.Value).ToArray() ?? [];
Check(recognitionModeLabels.SequenceEqual(new[] { "自动识别", "优先语言" })
    && preferredSourceGroup?.Elements(presentationNamespace + "ComboBox").Any(element => element.Attribute(xamlNamespace + "Name")?.Value == "SettingsSourceLanguage") == true
    && preferredSourceGroup.Elements(presentationNamespace + "ToggleSwitch").Any(element => element.Attribute(xamlNamespace + "Name")?.Value == "Strict") == true,
    "recognition settings expose Mac-equivalent automatic/preferred modes and group preferred-language controls");
var alwaysVisibleReturnButton = mainPageXaml.Descendants(presentationNamespace + "Button")
    .Any(element => element.Attribute("AutomationProperties.AutomationId")?.Value == "ReturnToLatest");
Check(!alwaysVisibleReturnButton,
    "the main toolbar does not show a permanent return-to-latest button when Mac only shows it for unread content");
var mainPageCode = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "MainPage.xaml.cs"));
Check(mainPageCode.Contains("PreferredSourceLanguage = source.Code", StringComparison.Ordinal)
    && mainPageCode.Contains("UpdatePreferredSourceSettingsVisibility", StringComparison.Ordinal)
    && mainPageCode.Contains("c.SourceLanguage = SettingsSourceMode.SelectedItem == AutomaticSourceMode ? string.Empty", StringComparison.Ordinal),
    "recognition mode persists the preferred language while automatic mode omits hints and hides strict-language controls");
var overlayLockMenuItem = mainPageXaml.Descendants(presentationNamespace + "MenuFlyoutItem")
    .FirstOrDefault(element => element.Attribute(xamlNamespace + "Name")?.Value == "LockOverlayMenuItem");
int saveOverlayStart = mainPageCode.IndexOf("private void SaveOverlaySettings", StringComparison.Ordinal);
int persistOverlayStart = mainPageCode.IndexOf("private void PersistOverlayPlacement", StringComparison.Ordinal);
string saveOverlayBody = saveOverlayStart >= 0 && persistOverlayStart > saveOverlayStart
    ? mainPageCode[saveOverlayStart..persistOverlayStart] : "";
Check(overlayLockMenuItem?.Attribute("Click")?.Value == "ToggleOverlayLock_Click"
    && overlayLockMenuItem.Attribute("AutomationProperties.AutomationId")?.Value == "ToggleSubtitleOverlayLock"
    && mainPageCode.Contains("LockOverlayMenuItem.Text = ViewModel.Config.SubtitleOverlay.PositionLocked ? \"解锁位置\" : \"锁定位置\"", StringComparison.Ordinal)
    && mainPageCode.Contains("bool positionLockChanged = current.PositionLocked != wasPositionLocked", StringComparison.Ordinal)
    && mainPageCode.Contains("if (positionLockChanged && current.Enabled) subtitleOverlayWindow?.SetAdjusting(!current.PositionLocked)", StringComparison.Ordinal)
    && mainPageCode.Contains("if (settings.Enabled) subtitleOverlayWindow?.SetAdjusting(unlock)", StringComparison.Ordinal)
    && !saveOverlayBody.Contains("SetAdjusting", StringComparison.Ordinal),
    "overlay menu exposes Mac-equivalent lock/unlock and appearance saves preserve the current adjustment mode");
Check(mainPageCode.Contains("DirectManipulationStarted") && mainPageCode.Contains("DirectManipulationCompleted")
    && mainPageCode.Contains("PointerWheelChanged") && mainPageCode.Contains("PreviewKeyDown")
    && mainPageCode.Contains("transcriptFollowState.ViewChanged"),
    "transcript follow state tracks direct, wheel and keyboard scrolling separately from content layout changes");
Check(mainPageCode.Contains("Preferences.UpdateProtectedSecret") && mainPageCode.Contains("sonioxSecretUnreadable")
    && mainPageCode.Contains("deepSeekSecretUnreadable") && mainPageCode.Contains("密文已保留"),
    "settings preserve an unreadable DPAPI secret until the user replaces it");
var transcriptFollow = new TranscriptFollowState();
Check(transcriptFollow.ContentChanged() && transcriptFollow.IsAtEnd && !transcriptFollow.HasNewContent,
    "new transcript content stays followed when the user is at the live end");
transcriptFollow.ViewChanged(isAtEnd: false, isUserScrolling: true);
transcriptFollow.ViewChanged(isAtEnd: false, isUserScrolling: false);
Check(!transcriptFollow.IsAtEnd && !transcriptFollow.ContentChanged() && transcriptFollow.HasNewContent,
    "content growth and passive view changes do not resume following after the user scrolls into history");
transcriptFollow.ViewChanged(isAtEnd: true, isUserScrolling: true);
Check(transcriptFollow.IsAtEnd && !transcriptFollow.HasNewContent && transcriptFollow.ContentChanged(),
    "returning to the live end clears the new-content hint and resumes following");
var settingsOverlay = mainPageXaml.Descendants(presentationNamespace + "Grid")
    .Single(element => element.Attribute(xamlNamespace + "Name")?.Value == "SettingsOverlay");
var settingsPanelBorder = settingsOverlay.Element(presentationNamespace + "Border");
var settingsScrollViewer = settingsPanelBorder?.Descendants(presentationNamespace + "ScrollViewer")
    .SingleOrDefault(element => element.Attribute("AutomationProperties.AutomationId")?.Value == "SettingsScrollViewer");
var settingsScrollGrid = settingsScrollViewer?.Parent;
var settingsScrollGridRows = settingsScrollGrid?.Element(presentationNamespace + "Grid.RowDefinitions")?
    .Elements(presentationNamespace + "RowDefinition").Select(element => element.Attribute("Height")?.Value).ToArray();
Check(settingsPanelBorder?.Attribute("Width") is null && settingsPanelBorder?.Attribute("Height") is null
    && settingsPanelBorder?.Attribute("MaxWidth")?.Value == "520" && settingsPanelBorder?.Attribute("MaxHeight")?.Value == "560"
    && settingsPanelBorder.Attribute("HorizontalAlignment")?.Value == "Stretch"
    && settingsPanelBorder.Attribute("VerticalAlignment")?.Value == "Stretch"
    && settingsScrollViewer is not null && settingsScrollGrid?.Name == presentationNamespace + "Grid"
    && settingsScrollViewer.Attribute("Grid.Row")?.Value == "1"
    && settingsScrollGridRows is ["Auto", "*"],
    "settings panel keeps the Mac 520x560 DIP target, fits short work areas, and constrains its accessible scroll viewer to a star-sized row");
var overlayFeed = new DesktopSubtitleOverlayFeed();
var overlayEntry = new Subtitle { English = "Live caption", Chinese = "实时字幕" };
var overlayAt = DateTimeOffset.Parse("2026-10-09T00:00:00Z");
overlayFeed.Update(overlayEntry, true, overlayAt);
Check(overlayFeed.Current is { IsVisible: true, IsFinal: false, TranslationEnabled: true }, "overlay feed presents a provisional bilingual subtitle");
overlayFeed.Finalize(overlayEntry, true, overlayAt.AddSeconds(1));
var overlayFinal = overlayFeed.Current!;
overlayFeed.Update(overlayEntry, true, overlayAt.AddSeconds(3));
Check(overlayFeed.Current!.FinalizedAt == overlayFinal.FinalizedAt && overlayFeed.Current.RemainsVisible(overlayAt.AddSeconds(5), 5),
    "identical updates do not extend the final subtitle retention window");
overlayEntry.Chinese = "延迟到达的译文";
overlayFeed.Update(overlayEntry, true, overlayAt.AddSeconds(4));
Check(overlayFeed.Current!.FinalizedAt == overlayAt.AddSeconds(4) && overlayFeed.Current.RemainsVisible(overlayAt.AddSeconds(8), 5),
    "meaningful late translation resets the final subtitle retention window");
overlayFeed.Clear();
Check(overlayFeed.Current is null, "overlay feed clears stale subtitle state");
var segmentation = new TranscriptSegmentationSettings();
var summaryBlocks = TranscriptSummaryMarkdown.Parse("## 课程总结\r\n\r\n概览第一行\r\n第二行\r\n\r\n### 要点\r\n- 第一项\r\n* 第二项\r\n");
Check(!SummaryPanelPresentation.ShouldShow(false, false, false)
    && SummaryPanelPresentation.ShouldShow(true, false, false)
    && SummaryPanelPresentation.ShouldShow(false, true, false)
    && SummaryPanelPresentation.ShouldShow(false, false, true),
    "AI summary panel visibility matches Mac auto-summary, existing-summary and status conditions");
Check(summaryBlocks.Select(block => (block.Kind, block.Text)).SequenceEqual([
    (SummaryMarkdownBlockKind.Title, "课程总结"),
    (SummaryMarkdownBlockKind.Paragraph, "概览第一行\n第二行"),
    (SummaryMarkdownBlockKind.Heading, "要点"),
    (SummaryMarkdownBlockKind.Bullet, "第一项"),
    (SummaryMarkdownBlockKind.Bullet, "第二项")
]), "summary markdown blocks match the Mac title, heading, bullet, and paragraph rules across Windows line endings");
Check(segmentation.SonioxMaxEndpointDelayMilliseconds == 3000 && segmentation.SonioxEndpointSensitivity == -.3
    && segmentation.LocalSilenceThresholdSeconds == 4.5 && segmentation.LocalSilenceMinimumWordCount == 5
    && segmentation.LongSegmentWordThreshold == 80 && segmentation.LongSegmentDurationThresholdSeconds == 90,
    "segmentation defaults match the Mac settings baseline");
var segmentationRoundTrip = JsonSerializer.Deserialize<Preferences>(JsonSerializer.Serialize(new Preferences { Segmentation = new() { LocalSilenceThresholdSeconds = 7.5 } }, TranscriptFiles.Json), TranscriptFiles.Json);
Check(segmentationRoundTrip?.Segmentation.LocalSilenceThresholdSeconds == 7.5, "segmentation settings persist in the existing Windows preferences file");
double dateBeforeMidnight = (new DateTimeOffset(2026, 1, 1, 23, 59, 0, TimeSpan.Zero) - Archive.AppleEpoch).TotalSeconds;
double dateAfterMidnight = (new DateTimeOffset(2026, 1, 2, 0, 1, 0, TimeSpan.Zero) - Archive.AppleEpoch).TotalSeconds;
Check(TranscriptPresentation.DaySeparatorLabel(dateBeforeMidnight, dateAfterMidnight, TimeZoneInfo.Utc) == "2026年1月2日"
    && TranscriptPresentation.DaySeparatorLabel(dateBeforeMidnight, dateBeforeMidnight + 30, TimeZoneInfo.Utc) is null
    && TranscriptPresentation.DaySeparatorLabel(null, dateAfterMidnight, TimeZoneInfo.Utc) is null,
    "transcript day headers match the Mac cross-day rule and require adjacent wall-clock dates");
segmentation.SonioxMaxEndpointDelayMilliseconds = 4000; segmentation.SonioxEndpointSensitivity = double.NaN;
segmentation.LocalSilenceThresholdSeconds = -1; segmentation.Validate();
Check(segmentation.SonioxMaxEndpointDelayMilliseconds == 3000 && segmentation.SonioxEndpointSensitivity == -.3
    && segmentation.LocalSilenceThresholdSeconds == 4.5, "invalid segmentation preferences recover to safe defaults");
var requestPreferences = new Preferences { SourceLanguage = "", Strict = true, Translate = false, SonioxModel = "user-selected-model" };
var requestSegmentation = new TranscriptSegmentationSettings { SonioxMaxEndpointDelayMilliseconds = 1750, SonioxEndpointSensitivity = .2, SonioxEndpointLatencyAdjustmentLevel = 2 };
var sonioxRequest = SonioxRequestBuilder.Build(requestPreferences, requestSegmentation);
Check((int)sonioxRequest["max_endpoint_delay_ms"] == 1750 && (double)sonioxRequest["endpoint_sensitivity"] == .2
    && (int)sonioxRequest["endpoint_latency_adjustment_level"] == 2 && !sonioxRequest.ContainsKey("language_hints")
    && !sonioxRequest.ContainsKey("language_hints_strict") && !sonioxRequest.ContainsKey("translation")
    && (string)sonioxRequest["model"] == EchoServiceModels.SonioxRealtime,
    "Soniox automatic-language request removes both hints and strict mode, carries endpoint settings, and omits disabled translation");
requestPreferences.SourceLanguage = "en";
var strictLanguageRequest = SonioxRequestBuilder.Build(requestPreferences, requestSegmentation);
requestPreferences.SourceLanguage = "";
var automaticAfterStrictRequest = SonioxRequestBuilder.Build(requestPreferences, requestSegmentation);
Check(((string[])strictLanguageRequest["language_hints"])[0] == "en"
    && (bool)strictLanguageRequest["language_hints_strict"]
    && !automaticAfterStrictRequest.ContainsKey("language_hints")
    && !automaticAfterStrictRequest.ContainsKey("language_hints_strict"),
    "source-language selection and strict preference match Mac specified/automatic transitions without applying strict restriction to auto mode");
var sonioxContext = (Dictionary<string, object>)sonioxRequest["context"];
var sonioxGeneral = (Dictionary<string, string>[])sonioxContext["general"];
var sonioxTerms = (string[])sonioxContext["terms"];
var sonioxTranslationTerms = (Dictionary<string, string>[])sonioxContext["translation_terms"];
Check(sonioxGeneral.Length == 2 && sonioxGeneral[0]["value"] == "economics and finance"
    && sonioxGeneral[1]["value"] == "economic research, markets, and financial analysis"
    && sonioxTerms.Length == 39 && sonioxTranslationTerms.Length == 26
    && ((string)sonioxContext["text"]).Contains("must not be confused with duty or duties", StringComparison.Ordinal),
    "Soniox request preserves the Mac economics context, vocabulary, and translation pairs");
var customSonioxRequest = SonioxRequestBuilder.Build(new Preferences { CorrectionTerms = "  Acme  \r\n\n" + new string('x', 90) + "\n" + string.Join("\n", Enumerable.Range(0, 101).Select(i => $"Term{i}")) }, new());
var customSonioxContext = (Dictionary<string, object>)customSonioxRequest["context"];
var customSonioxTerms = (string[])customSonioxContext["terms"];
Check(customSonioxTerms.Length == 139 && customSonioxTerms[39] == "Acme"
    && customSonioxTerms[40] == new string('x', 80) && customSonioxTerms[^1] == "Term97"
    && JsonDocument.Parse(JsonSerializer.Serialize(customSonioxRequest)).RootElement.GetProperty("context").GetProperty("translation_terms").GetArrayLength() == 26,
    "Soniox request appends normalized user terms with Mac line, length, and count limits and serializes valid context JSON");
string combiningTerm = string.Concat(Enumerable.Repeat("e\u0301", 81));
string emojiTerm = string.Concat(Enumerable.Repeat("👩‍🏫", 81));
var unicodeSonioxTerms = SonioxRequestBuilder.NormalizeCorrectionTerms(combiningTerm + "\n" + emojiTerm);
Check(unicodeSonioxTerms.Length == 2 && StringInfo.ParseCombiningCharacters(unicodeSonioxTerms[0]).Length == 80
    && StringInfo.ParseCombiningCharacters(unicodeSonioxTerms[1]).Length == 80
    && unicodeSonioxTerms[0] == string.Concat(Enumerable.Repeat("e\u0301", 80))
    && unicodeSonioxTerms[1] == string.Concat(Enumerable.Repeat("👩‍🏫", 80)),
    "Soniox custom terms truncate by Unicode text elements like Swift Character without splitting combining marks or ZWJ emoji");
var addedCorrectionTerms = "Acme\r\nExisting\n\n";
bool termAdded = CorrectionTermList.TryAdd(addedCorrectionTerms, "  New term  ", out string normalizedCorrectionTerms);
Check(termAdded && normalizedCorrectionTerms == "Acme\nExisting\nNew term"
    && !CorrectionTermList.TryAdd(normalizedCorrectionTerms, "new TERM", out _)
    && !CorrectionTermList.TryAdd(normalizedCorrectionTerms, "   ", out _)
    && !CorrectionTermList.TryAdd(string.Join('\n', Enumerable.Range(0, 100).Select(i => $"Term{i}")), "Extra", out _),
    "inline correction terms trim input, normalize blank lines, reject case-insensitive duplicates and enforce the 100-term Mac limit");
string eightyOneEmoji = string.Concat(Enumerable.Repeat("👩‍🏫", 81));
Check(CorrectionTermList.HasValidCandidateLength(string.Concat(Enumerable.Repeat("👩‍🏫", 80)))
    && !CorrectionTermList.HasValidCandidateLength(eightyOneEmoji)
    && !CorrectionTermList.TryAdd("", eightyOneEmoji, out _),
    "inline correction term field uses the Mac 80-character limit measured in Unicode text elements");
var policySettings = new TranscriptSegmentationSettings();
Check(Math.Abs(AudioLevelHistory.MeasureRms([.1f, -.1f, 1f], 2) - .75) < .0001
    && AudioLevelHistory.MeasureRms([1f, 1f], 0) == 0,
    "audio meter computes Mac-scaled RMS over only valid captured samples");
var audioHistory = new AudioLevelHistory();
Check(audioHistory.TryRecord(.4, 1000, 1000) && Math.Abs(audioHistory.Current - .3) < .0001
    && !audioHistory.TryRecord(.8, 1049, 1000)
    && audioHistory.TryRecord(.8, 1050, 1000) && Math.Abs(audioHistory.Current - .675) < .0001
    && audioHistory.TryRecord(.4, 1100, 1000) && Math.Abs(audioHistory.Current - .6255) < .0001,
    "audio meter samples at 20 Hz, attacks quickly and decays slowly like Mac");
var expectedAudioHistory = new List<double>();
for (int i = 0; i < 60; i++)
{
    if (audioHistory.TryRecord(i % 2, 1200 + (i * 50), 1000)) expectedAudioHistory.Add(audioHistory.Current);
}
Check(audioHistory.Samples.Count == AudioLevelHistory.Capacity
    && audioHistory.Samples.SequenceEqual(expectedAudioHistory.TakeLast(AudioLevelHistory.Capacity)),
    "audio waveform retains the newest 48 samples in order");
audioHistory.Reset();
Check(audioHistory.Current == 0 && audioHistory.Samples.Count == 48 && audioHistory.Samples.All(sample => sample == 0),
    "audio waveform history resets cleanly between recording sessions");
using var emptySonioxResponse = JsonDocument.Parse("""{"tokens":[]}""");
using var endpointOnlySonioxResponse = JsonDocument.Parse("""{"tokens":[{"text":"<end>","is_final":true}]}""");
using var finishedSonioxResponse = JsonDocument.Parse("""{"finished":true,"tokens":[]}""");
using var errorSonioxResponse = JsonDocument.Parse("""{"error_message":"unavailable"}""");
Check(SonioxResponseActivity.ShouldResetQuietTimer(emptySonioxResponse.RootElement)
    && SonioxResponseActivity.ShouldResetQuietTimer(endpointOnlySonioxResponse.RootElement)
    && !SonioxResponseActivity.ShouldResetQuietTimer(finishedSonioxResponse.RootElement)
    && !SonioxResponseActivity.ShouldResetQuietTimer(errorSonioxResponse.RootElement),
    "quiet-time tracking treats empty and endpoint responses as activity like Mac, but excludes finished and error responses");
using var malformedFinishedSonioxResponse = JsonDocument.Parse("""{"finished":"true"}""");
using var codeOnlySonioxErrorResponse = JsonDocument.Parse("""{"error_code":503}""");
using var malformedErrorSonioxResponse = JsonDocument.Parse("""{"error_message":503}""");
Check(SonioxResponseControl.IsFinished(finishedSonioxResponse.RootElement)
    && !SonioxResponseControl.IsFinished(malformedFinishedSonioxResponse.RootElement)
    && SonioxResponseControl.IsServiceError(errorSonioxResponse.RootElement)
    && SonioxResponseControl.IsServiceError(codeOnlySonioxErrorResponse.RootElement)
    && !SonioxResponseControl.IsServiceError(malformedErrorSonioxResponse.RootElement),
    "Soniox control parsing accepts only boolean finished and string error messages while retaining error-code frames");
Check(TranscriptSegmentationPolicy.Trigger("one two three four five", 1, 4.5, policySettings) == TranscriptSegmentationTrigger.Silence
    && TranscriptSegmentationPolicy.Trigger("one two three four", 1, 10, policySettings) is null
    && TranscriptSegmentationPolicy.Trigger(new string('w', 1), 90, 0,
        new TranscriptSegmentationSettings { LongSegmentWordThreshold = 1 }, translationEnabled: true, translationReady: false) is null
    && TranscriptSegmentationPolicy.Trigger("", 0, 0, policySettings, endpointReached: true) == TranscriptSegmentationTrigger.Endpoint,
    "segmentation policy requires its thresholds, waits for enabled translation, and gives semantic endpoints priority");
using var segmentationFixtureDocument = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "segmentation-policy-parity.json")));
var segmentationCases = segmentationFixtureDocument.RootElement.GetProperty("cases").EnumerateArray().ToArray();
var windowsSegmentationResults = new List<(string Id, string Trigger)>();
bool segmentationFixtureMatches = true;
foreach (JsonElement testCase in segmentationCases)
{
    string id = testCase.GetProperty("id").GetString()!;
    string text = testCase.TryGetProperty("text", out JsonElement literalText)
        ? literalText.GetString()!
        : string.Join(' ', Enumerable.Repeat(testCase.GetProperty("repeatWord").GetString()!, testCase.GetProperty("repeatCount").GetInt32()));
    bool ReadBoolean(string name, bool fallback) => testCase.TryGetProperty(name, out JsonElement value) ? value.GetBoolean() : fallback;
    int ReadInteger(string name, int fallback) => testCase.TryGetProperty(name, out JsonElement value) ? value.GetInt32() : fallback;
    double ReadNumber(string name, double fallback) => testCase.TryGetProperty(name, out JsonElement value) ? value.GetDouble() : fallback;
    var caseSettings = new TranscriptSegmentationSettings
    {
        LocalSilenceFallbackEnabled = ReadBoolean("localSilenceFallbackEnabled", true),
        LocalSilenceThresholdSeconds = ReadNumber("localSilenceThresholdSeconds", 4.5),
        LocalSilenceMinimumWordCount = ReadInteger("localSilenceMinimumWordCount", 5),
        LongSegmentFallbackEnabled = ReadBoolean("longSegmentFallbackEnabled", true),
        LongSegmentWordThreshold = ReadInteger("longSegmentWordThreshold", 80),
        LongSegmentDurationThresholdSeconds = ReadNumber("longSegmentDurationThresholdSeconds", 90)
    };
    TranscriptSegmentationTrigger? trigger = TranscriptSegmentationPolicy.Trigger(
        text,
        ReadNumber("elapsedSeconds", 0),
        ReadNumber("quietSeconds", 0),
        caseSettings,
        endpointReached: ReadBoolean("endpointReached", false),
        translationEnabled: ReadBoolean("translationEnabled", false),
        translationReady: ReadBoolean("translationReady", true));
    string triggerName = trigger switch
    {
        TranscriptSegmentationTrigger.Endpoint => "endpoint",
        TranscriptSegmentationTrigger.Silence => "silence",
        TranscriptSegmentationTrigger.LongSegment => "longSegment",
        _ => "none"
    };
    windowsSegmentationResults.Add((id, triggerName));
    segmentationFixtureMatches &= triggerName == testCase.GetProperty("expectedTrigger").GetString();
}

bool macSegmentationArtifactMatches = true;
if (Environment.GetEnvironmentVariable("ECHO_MAC_SEGMENTATION_FIXTURE") is { Length: > 0 } macSegmentationPath)
{
    using var macSegmentationDocument = JsonDocument.Parse(File.ReadAllText(macSegmentationPath));
    JsonElement macRoot = macSegmentationDocument.RootElement;
    string? macSourceCommit = macRoot.GetProperty("macSourceCommit").GetString();
    string? transcriptModelsSha256 = macRoot.GetProperty("transcriptModelsSha256").GetString();
    macSegmentationArtifactMatches = macSourceCommit is { Length: > 0 } and not "unknown"
        && transcriptModelsSha256 is { Length: > 0 } and not "unknown";
    JsonElement[] macCases = macRoot.GetProperty("cases").EnumerateArray().ToArray();
    macSegmentationArtifactMatches &= macCases.Length == windowsSegmentationResults.Count;
    if (macCases.Length == windowsSegmentationResults.Count)
    {
        for (int i = 0; i < macCases.Length; i++)
        {
            macSegmentationArtifactMatches &= macCases[i].GetProperty("id").GetString() == windowsSegmentationResults[i].Id
                && macCases[i].GetProperty("trigger").GetString() == windowsSegmentationResults[i].Trigger
                && macCases[i].GetProperty("trigger").GetString() == segmentationCases[i].GetProperty("expectedTrigger").GetString();
        }
    }
}
Check(segmentationFixtureMatches && macSegmentationArtifactMatches,
    $"Windows segmentation policy matches {windowsSegmentationResults.Count} Mac-source cases for endpoint precedence, silence/long thresholds, translation wait, and Unicode whitespace");
using var segmentationSessionFixture = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "segmentation-session-parity.json")));
JsonElement segmentationSessionRoot = segmentationSessionFixture.RootElement;
JsonElement[] segmentationSessionEvents = segmentationSessionRoot.GetProperty("events").EnumerateArray().ToArray();
JsonElement segmentationSessionConfig = segmentationSessionRoot.GetProperty("config");
double sessionStartedAtUnixSeconds = segmentationSessionRoot.GetProperty("sessionStartedAtUnixSeconds").GetDouble();
double sessionStartedAtAppleSeconds = (DateTimeOffset.FromUnixTimeSeconds((long)sessionStartedAtUnixSeconds) - Archive.AppleEpoch).TotalSeconds;
var segmentationSessionSegment = new Segment { StartedAt = sessionStartedAtAppleSeconds };
int segmentationSessionFinalizations = 0;
var segmentationSessionAssembler = new TokenAssembler(segmentationSessionSegment, _ => { }, _ => segmentationSessionFinalizations++);
var segmentationSessionSettings = new TranscriptSegmentationSettings
{
    LocalSilenceFallbackEnabled = segmentationSessionConfig.GetProperty("localSilenceFallbackEnabled").GetBoolean(),
    LocalSilenceThresholdSeconds = segmentationSessionConfig.GetProperty("localSilenceThresholdSeconds").GetDouble(),
    LocalSilenceMinimumWordCount = segmentationSessionConfig.GetProperty("localSilenceMinimumWordCount").GetInt32(),
    LongSegmentFallbackEnabled = segmentationSessionConfig.GetProperty("longSegmentFallbackEnabled").GetBoolean(),
    LongSegmentWordThreshold = segmentationSessionConfig.GetProperty("longSegmentWordThreshold").GetInt32(),
    LongSegmentDurationThresholdSeconds = segmentationSessionConfig.GetProperty("longSegmentDurationThresholdSeconds").GetDouble()
}.Validate();
int[] expectedSessionEntryCounts = [1, 1, 1, 1, 2, 2, 2, 2, 3];
int[] expectedSessionFinalizations = [0, 0, 0, 1, 1, 1, 1, 2, 3];
bool sessionMacArtifactPresent = Environment.GetEnvironmentVariable("ECHO_MAC_SEGMENTATION_SESSION_FIXTURE") is { Length: > 0 };
bool sessionMacArtifactMatches = !sessionMacArtifactPresent;
JsonElement[] macSessionEvents = [];
if (sessionMacArtifactPresent)
{
    using var macSessionDocument = JsonDocument.Parse(File.ReadAllText(Environment.GetEnvironmentVariable("ECHO_MAC_SEGMENTATION_SESSION_FIXTURE")!));
    JsonElement macSessionRoot = macSessionDocument.RootElement;
    string? macSessionSourceHash = macSessionRoot.GetProperty("speechViewModelSha256").GetString();
    sessionMacArtifactMatches = macSessionRoot.GetProperty("sourceCommit").GetString() is { Length: > 0 } and not "unknown"
        && macSessionSourceHash is { Length: 64 } && macSessionSourceHash.All(Uri.IsHexDigit)
        && macSessionRoot.GetProperty("events").GetArrayLength() == segmentationSessionEvents.Length;
    macSessionEvents = macSessionRoot.GetProperty("events").EnumerateArray().ToArray();
}
for (int i = 0; i < segmentationSessionEvents.Length; i++)
{
    JsonElement currentEvent = segmentationSessionEvents[i];
    if (currentEvent.GetProperty("kind").GetString() == "response")
    {
        using var responseDocument = JsonDocument.Parse(currentEvent.GetProperty("message").GetRawText());
        segmentationSessionAssembler.Apply(responseDocument.RootElement);
    }
    else
    {
        TranscriptSegmentationRuntime.TryFinalizeCurrent(
            segmentationSessionSegment,
            segmentationSessionAssembler,
            segmentationSessionSettings,
            currentEvent.GetProperty("elapsedSeconds").GetDouble(),
            currentEvent.GetProperty("quietSeconds").GetDouble(),
            segmentationSessionConfig.GetProperty("translationEnabled").GetBoolean());
    }

    bool localSessionMatches = segmentationSessionSegment.Entries.Count == expectedSessionEntryCounts[i]
        && segmentationSessionFinalizations == expectedSessionFinalizations[i];
    if (i == 3)
        localSessionMatches &= segmentationSessionSegment.Entries[0].English == "we should start the class today"
            && segmentationSessionSegment.Entries[0].Chinese == "我们今天开始上课"
            && segmentationSessionSegment.Entries[0].Start == 1
            && segmentationSessionSegment.Entries[0].End == 2.5;
    if (i == 7)
        localSessionMatches &= segmentationSessionSegment.Entries[1].English == string.Join(' ', Enumerable.Repeat("lecture", 80))
            && segmentationSessionSegment.Entries[1].Chinese == "长段译文"
            && segmentationSessionSegment.Entries[1].Start == 20
            && segmentationSessionSegment.Entries[1].End == 21;
    if (i == 8)
        localSessionMatches &= segmentationSessionSegment.Entries[2].English == "the next class starts now"
            && segmentationSessionSegment.Entries[2].Chinese == "下一节课现在开始"
            && segmentationSessionSegment.Entries[2].Start == 120
            && segmentationSessionSegment.Entries[2].End == 121
            && segmentationSessionSegment.Entries[2].Speaker == "Speaker 2"
            && segmentationSessionSegment.Entries[2].Language == "en";

    bool macEventMatches = !sessionMacArtifactPresent;
    if (sessionMacArtifactPresent && macSessionEvents.Length == segmentationSessionEvents.Length)
    {
        JsonElement macEvent = macSessionEvents[i];
        JsonElement expectedRows = macEvent.GetProperty("expected");
        macEventMatches = macEvent.GetProperty("kind").GetString() == currentEvent.GetProperty("kind").GetString()
            && macEvent.GetProperty("finalizationCount").GetInt32() == segmentationSessionFinalizations
            && expectedRows.GetArrayLength() == segmentationSessionSegment.Entries.Count;
        if (macEventMatches)
        {
            int rowIndex = 0;
            foreach (JsonElement expectedRow in expectedRows.EnumerateArray())
            {
                Subtitle actualRow = segmentationSessionSegment.Entries[rowIndex++];
                double expectedRecordedAtUnix = expectedRow.GetProperty("recordedAtUnixSeconds").GetDouble();
                double actualRecordedAtUnix = actualRow.RecordedAt is double recordedAt
                    ? Archive.AppleEpoch.AddSeconds(recordedAt).ToUnixTimeMilliseconds() / 1000d : double.NaN;
                macEventMatches &= expectedRow.GetProperty("english").GetString() == actualRow.English
                    && expectedRow.GetProperty("chinese").GetString() == actualRow.Chinese
                    && Math.Abs(expectedRow.GetProperty("start").GetDouble() - actualRow.Start) < .001
                    && Math.Abs(expectedRow.GetProperty("end").GetDouble() - actualRow.End) < .001
                    && Math.Abs(expectedRecordedAtUnix - actualRecordedAtUnix) < .001
                    && (expectedRow.TryGetProperty("speaker", out JsonElement expectedSpeaker) ? expectedSpeaker.GetString() : null) == actualRow.Speaker
                    && (expectedRow.TryGetProperty("language", out JsonElement expectedLanguage) ? expectedLanguage.GetString() : null) == actualRow.Language;
            }
        }
    }
    Check(localSessionMatches && macEventMatches,
        $"segmentation token/session event {i + 1} preserves the Mac final rows, text, timestamps, metadata, and fallback timing");
    sessionMacArtifactMatches &= macEventMatches;
}
Check(sessionMacArtifactMatches,
    $"Windows token assembler and production session finalizer match {segmentationSessionEvents.Length} Mac production session snapshots");
int localFinalized = 0; var localSegment = new Segment(); var localAssembler = new TokenAssembler(localSegment, _ => { }, _ => localFinalized++);
using (var localPartial = JsonDocument.Parse("""{"tokens":[{"text":"A completed local phrase","is_final":true}]}""")) localAssembler.Apply(localPartial.RootElement);
Check(localAssembler.FinalizeCurrent() && localFinalized == 1, "local segmentation finalizes the active subtitle through the shared finalization path");
using (var afterLocal = JsonDocument.Parse("""{"tokens":[{"text":"Next phrase","is_final":true}]}""")) localAssembler.Apply(afterLocal.RootElement);
Check(localSegment.Entries.Count == 2 && localSegment.Entries[1].English == "Next phrase", "speech after a local fallback begins a fresh subtitle without duplicating the prior phrase");
void Apply(string text) { using var doc = JsonDocument.Parse(text); assembly.Apply(doc.RootElement); }
Apply("""{"tokens":[{"text":"Hel","is_final":false,"start_ms":0,"end_ms":200}]}""");
Apply("""{"tokens":[{"text":"Hello","is_final":true,"start_ms":0,"end_ms":400},{"text":" world","is_final":false,"start_ms":400,"end_ms":700}]}""");
Check(segment.Entries[0].English == "Hello world", "provisional replacement without duplicated prefix");
Apply("""{"tokens":[{"text":" there.","is_final":true,"start_ms":400,"end_ms":800}]}""");
Apply("""{"tokens":[{"text":"你好。","is_final":true,"translation_status":"translation"}]}""");
Apply("""{"tokens":[{"text":"<end>","is_final":true}]}""");
Apply("""{"tokens":[{"text":"Next.","is_final":true,"start_ms":1500,"end_ms":2000}]}""");
Check(segment.Entries.Count == 2 && segment.Entries[0].English == "Hello there." && segment.Entries[0].Chinese == "你好。"
    && segment.Entries[1].English == "Next.", "translation arriving before the endpoint stays paired, and the next response starts a new row");
using var translationOnlyRequest = SubtitleCorrectionService.CreateRequest("test-key", "Exact source", "旧译文", "nearby context", "term", "zh", translationOnly: true);
using var translationOnlyBody = JsonDocument.Parse(await translationOnlyRequest.Content!.ReadAsStringAsync());
var translationOnlyMessages = translationOnlyBody.RootElement.GetProperty("messages");
using var translationOnlyPayload = JsonDocument.Parse(translationOnlyMessages[1].GetProperty("content").GetString()!);
using var correctionRequest = SubtitleCorrectionService.CreateRequest("test-key", "Source to review", "译文", "previous\ncurrent\nnext", "term", "zh", translationOnly: false);
using var correctionBody = JsonDocument.Parse(await correctionRequest.Content!.ReadAsStringAsync());
Check(translationOnlyRequest.Headers.Authorization?.Scheme == "Bearer"
    && translationOnlyBody.RootElement.GetProperty("model").GetString() == EchoServiceModels.DeepSeek
    && translationOnlyBody.RootElement.GetProperty("thinking").GetProperty("type").GetString() == "disabled"
    && translationOnlyMessages[0].GetProperty("content").GetString()!.Contains("source 必须逐字保持输入原文", StringComparison.Ordinal)
    && translationOnlyPayload.RootElement.GetProperty("source").GetString() == "Exact source"
    && correctionBody.RootElement.GetProperty("messages")[0].GetProperty("content").GetString()!.Contains("前后文仅用于判断，不并入当前句", StringComparison.Ordinal),
    "DeepSeek correction requests match Mac thinking and distinguish translation-only from contextual proofreading instructions");
var priorityCorrectionQueue = new PriorityWorkQueue<string>(3);
bool priorityQueueContract = priorityCorrectionQueue.Enqueue("auto-1", false, item => item == "auto-1") == PriorityWorkQueueInsertResult.Added
    && priorityCorrectionQueue.Enqueue("auto-2", false, item => item == "auto-2") == PriorityWorkQueueInsertResult.Added
    && priorityCorrectionQueue.Promote(item => item == "auto-2")
    && priorityCorrectionQueue.Enqueue("manual", true, item => item == "manual") == PriorityWorkQueueInsertResult.Added
    && priorityCorrectionQueue.Enqueue("auto-1", false, item => item == "auto-1") == PriorityWorkQueueInsertResult.Duplicate
    && priorityCorrectionQueue.Enqueue("auto-3", false, item => item == "auto-3") == PriorityWorkQueueInsertResult.Full
    && priorityCorrectionQueue.Dequeue() == "manual" && priorityCorrectionQueue.Dequeue() == "auto-2"
    && priorityCorrectionQueue.Dequeue() == "auto-1" && priorityCorrectionQueue.Count == 0;
Check(priorityQueueContract, "manual correction jobs jump ahead of pending automatic jobs, including promotion of a matching queued job");
bool missingUncertainRejected = false;
try { JsonSerializer.Deserialize<CorrectionSuggestion>("""{"source":"s","translation":"t","reason":"r"}""", TranscriptFiles.Json); }
catch (JsonException) { missingUncertainRejected = true; }
Check(missingUncertainRejected, "DeepSeek correction response requires the Mac Codable uncertain field instead of silently defaulting to false");
var taggedEndpointSegment = new Segment(); var taggedEndpointAssembler = new TokenAssembler(taggedEndpointSegment, _ => { });
using (var taggedEndpointTurn = JsonDocument.Parse("""{"tokens":[{"text":"One","is_final":true,"translation_status":"original"},{"text":"一","is_final":true,"translation_status":"translation"},{"text":"<end>","is_final":true,"translation_status":"original"},{"text":"Two","is_final":true,"translation_status":"original"},{"text":"二","is_final":true,"translation_status":"translation"},{"text":"<end>","is_final":true}]}""")) taggedEndpointAssembler.Apply(taggedEndpointTurn.RootElement);
Check(taggedEndpointSegment.Entries.Count == 1 && taggedEndpointSegment.Entries[0].English == "OneTwo" && taggedEndpointSegment.Entries[0].Chinese == "一二",
    "multiple endpoint markers in one response finalize once after the complete bilingual response, matching Mac");
var emptyEndpointSegment = new Segment(); var emptyEndpointAssembler = new TokenAssembler(emptyEndpointSegment, _ => { });
using (var emptyEndpointTurn = JsonDocument.Parse("""{"tokens":[{"text":"<end>","is_final":true},{"text":"<fin>","is_final":true},{"text":"Real first entry","is_final":true}]}""")) emptyEndpointAssembler.Apply(emptyEndpointTurn.RootElement);
Check(emptyEndpointSegment.Entries.Count == 1 && emptyEndpointSegment.Entries[0].English == "Real first entry",
    "empty endpoint markers do not advance the cursor or create blank transcript rows");
string microCorrected = TranscriptRecognitionCorrections.CorrectEnglish("micro economic theory and macroeconomic model");
string macroCorrected = TranscriptRecognitionCorrections.CorrectEnglish("macroeconomic growth with a microeconomic class");
Check(microCorrected == "microeconomic theory and microeconomic model"
    && macroCorrected == "macroeconomic growth with a macroeconomic class",
    "economics terminology normalization follows the Mac strong-context rules without changing ambiguous contexts");
Check(TranscriptRecognitionCorrections.CorrectEnglish("The first duty and duty function") == "The first derivative and derivative function"
    && TranscriptRecognitionCorrections.CorrectEnglish("The duty is owed by Han") == "The duty is owed by Han"
    && TranscriptRecognitionCorrections.CorrectEnglish("on the other han") == "on the other hand",
    "calculus and truncated-hand recognition corrections apply only in the Mac phrase contexts");
var economicsSegment = new Segment(); var economicsAssembler = new TokenAssembler(economicsSegment, _ => { });
using (var economicsTurn = JsonDocument.Parse("""{"tokens":[{"text":"first duty","is_final":true},{"text":"关税","is_final":true,"translation_status":"translation"}]}""")) economicsAssembler.Apply(economicsTurn.RootElement);
Check(economicsSegment.Entries[0].English == "first derivative" && economicsSegment.Entries[0].Chinese == "导数"
    && TranscriptRecognitionCorrections.CorrectChineseTranslation("税收关税", "The tax is due") == "税收关税",
    "recognized transcript and its translation apply paired Mac calculus corrections while preserving ordinary tax text");
var fieldEdit = new Subtitle { English = "Initial source", Chinese = "初始译文" };
fieldEdit.ApplyRecognition("最新识别原文", "最新识别译文");
fieldEdit.Edit("人工编辑原文", null);
fieldEdit.ApplyRecognition("更晚识别原文", "更晚识别译文");
Check(fieldEdit.English == "人工编辑原文" && fieldEdit.Chinese == "更晚识别译文"
    && fieldEdit.Correction is { SourceLocked: true, TranslationLocked: false, RawSource: "更晚识别原文", RawTranslation: "更晚识别译文" },
    "saving only the edited field preserves the current translation and leaves it open to live updates");
Check(fieldEdit.UndoCorrection() && fieldEdit.English == "最新识别原文" && fieldEdit.Chinese == "最新识别译文"
    && fieldEdit.Correction is { SourceLocked: true, TranslationLocked: true },
    "field-level undo restores the exact pre-edit pair and locks both restored values");
var editedSegment = new Segment(); var editedAssembler = new TokenAssembler(editedSegment, _ => { });
void ApplyEdited(string text) { using var doc = JsonDocument.Parse(text); editedAssembler.Apply(doc.RootElement); }
ApplyEdited("""{"tokens":[{"text":"Recognized","is_final":false}]}""");
var edited = editedSegment.Entries[0]; edited.Edit("人工纠正", "");
ApplyEdited("""{"tokens":[{"text":"Recognized text","is_final":false}]}""");
ApplyEdited("""{"tokens":[{"text":"Recognized text","is_final":false},{"text":"识别译文","is_final":false,"translation_status":"translation"}]}""");
Check(edited.English == "人工纠正" && edited.Chinese == "识别译文" && edited.Correction?.RawSource == "Recognized text", "manual source lock preserves correction while live recognition updates raw text and unlocked translation");
var correctionSnapshot = JsonSerializer.Serialize(new Archive { Segments = [editedSegment] }, TranscriptFiles.Json);
Check(edited.UndoCorrection() && edited.English == "Recognized" && edited.Correction?.SourceLocked == true, "undo restores prior subtitle and locks the restored human choice");
ApplyEdited("""{"tokens":[{"text":"Late recognition","is_final":false}]}""");
Check(edited.English == "Recognized" && edited.Correction?.RawSource == "Late recognition", "late recognition cannot overwrite text after undo");
var bilingual = new Segment(); var bilingualAssembler = new TokenAssembler(bilingual, _ => { });
using (var turn = JsonDocument.Parse("""{"tokens":[{"text":"First","is_final":true},{"text":"一","is_final":true,"translation_status":"translation"},{"text":"<end>","is_final":true}]}""")) bilingualAssembler.Apply(turn.RootElement);
using (var nextTurn = JsonDocument.Parse("""{"tokens":[{"text":"Second","is_final":true},{"text":"二","is_final":true,"translation_status":"translation"}]}""")) bilingualAssembler.Apply(nextTurn.RootElement);
Check(bilingual.Entries.Count == 2 && bilingual.Entries[0].English == "First" && bilingual.Entries[0].Chinese == "一"
    && bilingual.Entries[1].English == "Second" && bilingual.Entries[1].Chinese == "二",
    "one response-level endpoint advances both transcript and translation before the next response");
int finalizedCount = 0; var finalizedSegment = new Segment(); var finalizedAssembler = new TokenAssembler(finalizedSegment, _ => { }, _ => finalizedCount++);
using (var finalTurn = JsonDocument.Parse("""{"tokens":[{"text":"finished sentence","is_final":true},{"text":"<end>","is_final":true}]}""")) finalizedAssembler.Apply(finalTurn.RootElement);
Check(finalizedCount == 1, "transcript final boundary triggers a single opt-in correction job");
int speakerFinalized = 0; var speakerSegment = new Segment(); var speakerAssembler = new TokenAssembler(speakerSegment, _ => { }, _ => speakerFinalized++);
using (var firstSpeakerTurn = JsonDocument.Parse("""{"tokens":[{"text":"first speaker","is_final":true,"speaker":"1","language":"en","start_ms":0,"end_ms":500}]}""")) speakerAssembler.Apply(firstSpeakerTurn.RootElement);
using (var nextSpeakerTurn = JsonDocument.Parse("""{"tokens":[{"text":"second speaker","is_final":true,"speaker":"2","language":"ja","start_ms":600,"end_ms":1000},{"text":"<end>","is_final":true}]}""")) speakerAssembler.Apply(nextSpeakerTurn.RootElement);
Check(speakerSegment.Entries.Count == 2 && speakerSegment.Entries[0].English == "first speaker" && speakerSegment.Entries[0].Speaker == "Speaker 1" && speakerSegment.Entries[0].Language == "en" && speakerSegment.Entries[1].English == "second speaker" && speakerSegment.Entries[1].Speaker == "Speaker 2" && speakerSegment.Entries[1].Language == "ja" && speakerFinalized == 2, "final speaker change across responses splits rows, retains language and finalizes each turn once like Mac");
var languageSegment = new Segment(); var languageAssembler = new TokenAssembler(languageSegment, _ => { });
void ApplyLanguage(string text) { using var doc = JsonDocument.Parse(text); languageAssembler.Apply(doc.RootElement); }
ApplyLanguage("""{"tokens":[{"text":"hello","is_final":false,"language":"en"}]}""");
ApplyLanguage("""{"tokens":[{"text":"hello","is_final":false,"language":null}]}""");
ApplyLanguage("""{"tokens":[{"text":"こんにちは","is_final":false,"language":"ja"}]}""");
bool provisionalLanguageStable = languageSegment.Entries[0].Language == "en";
ApplyLanguage("""{"tokens":[{"text":"こんにちは","is_final":true,"language":"ja"}]}""");
var invalidMetadataSegment = new Segment(); var invalidMetadataAssembler = new TokenAssembler(invalidMetadataSegment, _ => { });
using (var invalidMetadata = JsonDocument.Parse("""{"tokens":[{"text":"metadata","is_final":true,"speaker":1,"language":7}]}""")) invalidMetadataAssembler.Apply(invalidMetadata.RootElement);
Check(provisionalLanguageStable && languageSegment.Entries[0].Language == "ja"
    && invalidMetadataSegment.Entries[0].Speaker is null && invalidMetadataSegment.Entries[0].Language is null,
    "provisional language stays stable until final, null metadata is ignored, and non-string speaker/language values are rejected");
var provisionalSnapshotSegment = new Segment(); var provisionalSnapshotAssembler = new TokenAssembler(provisionalSnapshotSegment, _ => { });
void ApplyProvisionalSnapshot(string text) { using var doc = JsonDocument.Parse(text); provisionalSnapshotAssembler.Apply(doc.RootElement); }
ApplyProvisionalSnapshot("""{"tokens":[{"text":"Hello","is_final":false},{"text":"你好","is_final":false,"translation_status":"translation"}]}""");
ApplyProvisionalSnapshot("""{"tokens":[{"text":"Hello there","is_final":false}]}""");
bool previousTranslationCleared = provisionalSnapshotSegment.Entries[0].English == "Hello there" && provisionalSnapshotSegment.Entries[0].Chinese == "";
ApplyProvisionalSnapshot("""{"tokens":[{"text":"Hello there!","is_final":false},{"text":"你好！","is_final":false,"translation_status":"translation"}]}""");
Check(previousTranslationCleared && provisionalSnapshotSegment.Entries[0].English == "Hello there!" && provisionalSnapshotSegment.Entries[0].Chinese == "你好！",
    "each Mac-style response replaces provisional source and translation as one snapshot, preventing stale opposite-lane text");
var sonioxFixturePath = Environment.GetEnvironmentVariable("ECHO_MAC_SONIOX_FIXTURE");
if (string.IsNullOrWhiteSpace(sonioxFixturePath) || !File.Exists(sonioxFixturePath))
    sonioxFixturePath = Path.Combine(AppContext.BaseDirectory, "Fixtures", "soniox-mac-parity-sequence.json");
using (var sonioxFixture = JsonDocument.Parse(File.ReadAllText(sonioxFixturePath)))
{
    int fixtureFinalized = 0;
    int fixtureEventIndex = 0;
    var fixtureSegment = new Segment { StartedAt = 800000000 };
    var fixtureAssembler = new TokenAssembler(fixtureSegment, _ => { }, _ => fixtureFinalized++);
    foreach (var step in sonioxFixture.RootElement.GetProperty("events").EnumerateArray())
    {
        using var response = JsonDocument.Parse(step.GetProperty("message").GetRawText());
        fixtureAssembler.Apply(response.RootElement);
        var expected = step.GetProperty("expected").EnumerateArray().ToArray();
        bool stateMatches = expected.Length == fixtureSegment.Entries.Count
            && expected.Select((entry, index) =>
            {
                var actual = fixtureSegment.Entries[index];
                string? expectedString(string key) => entry.TryGetProperty(key, out var value) && value.ValueKind != JsonValueKind.Null ? value.GetString() : null;
                return actual.English == expectedString("english")
                    && actual.Chinese == expectedString("chinese")
                    && actual.Speaker == expectedString("speaker")
                    && actual.Language == expectedString("language")
                    && Math.Abs(actual.Start - entry.GetProperty("start").GetDouble()) < 0.001
                    && Math.Abs(actual.End - entry.GetProperty("end").GetDouble()) < 0.001;
            }).All(matches => matches)
            && fixtureFinalized == step.GetProperty("expectedFinalizations").GetInt32();
        Check(stateMatches, $"Mac production Soniox fixture response {++fixtureEventIndex} matches snapshots, metadata, timestamps, and finalization callbacks");
    }
}
var archive = new Archive { CreatedAt = 800000000, Summary = "已保存总结", SummarizedEntries = { [segment.Entries[0].Id] = TranscriptFiles.SummarySignature(segment.Entries[0].English) }, Segments = [segment, new Segment { StartedAt = 800000010, Entries = [new Subtitle { Start = 0, End = 1, English = "Again" }] }] };
var archiveOrdering = ArchiveOrdering.NewestFirst([
    new Archive { UpdatedAt = 10, Title = "older" },
    new Archive { UpdatedAt = 30, Title = "newest" },
    new Archive { UpdatedAt = 20, Title = "middle" }
]);
Check(archiveOrdering.Select(item => item.Title).SequenceEqual(["newest", "middle", "older"]),
    "archive selection order follows Mac updatedAt descending semantics");
Check(Archive.NewTitle(new DateTime(2026, 10, 10, 9, 5, 0)) == "课程 10-10 09:05",
    "new Windows archive title follows the Mac Chinese title and local month-day time format");
var snapshot = TranscriptFiles.Snapshot(archive);
archive.Segments[0].Entries[0].English = "后续编辑";
archive.Segments[0].Entries[0].Correction = new SubtitleCorrection { RawSource = "后来补充" };
Check(snapshot.Segments[0].Entries[0].English == "Hello there." && snapshot.Segments[0].Entries[0].Correction is null && snapshot.SummarizedEntries.Count == 1, "background persistence snapshot is isolated from later edits and keeps archive metadata");
string srt = TranscriptFiles.Srt(archive);
Check(srt.Contains("00:00:10,000 --> 00:00:11,000") && srt.Contains("你好。"), "SRT preserves segment wall-clock gap and translation");
var overlapExport = new Archive { Segments =
[
    new Segment { StartedAt = 800000002, Entries = [new Subtitle { Start = 0, End = 3, English = "later segment" }] },
    new Segment { StartedAt = 800000000, Entries = [new Subtitle { Start = 0, End = 5, English = "earlier segment" }] }
] };
string overlapSrt = TranscriptFiles.Srt(overlapExport);
Check(overlapSrt.IndexOf("earlier segment", StringComparison.Ordinal) < overlapSrt.IndexOf("later segment", StringComparison.Ordinal)
    && overlapSrt.Contains("00:00:00,000 --> 00:00:05,000") && overlapSrt.Contains("00:00:05,000 --> 00:00:08,000"),
    "archive SRT export sorts unordered segments by wall clock and clamps overlaps so cue times never move backward");
var metadataArchive = new Archive { Segments = [new Segment { StartedAt = 800000000, Entries = [new Subtitle { Start = 0, End = 1, English = "bonjour", Chinese = "你好", Speaker = "Speaker 2", Language = "fr" }] }] };
string metadataJson = JsonSerializer.Serialize(metadataArchive, TranscriptFiles.Json);
var metadataLoaded = TranscriptFiles.Parse(metadataJson);
string metadataSrt = TranscriptFiles.Srt(metadataLoaded);
string normalizedMetadataSrt = metadataSrt.Replace("\r\n", "\n");
Check(metadataLoaded.Segments[0].Entries[0].Speaker == "Speaker 2" && metadataLoaded.Segments[0].Entries[0].Language == "fr"
    && normalizedMetadataSrt.Contains("[Speaker 2]\nbonjour") && !normalizedMetadataSrt.Contains("[fr]")
    && metadataArchive.Segments[0].Entries[0].HasLanguage,
    "anonymous speaker and detected language survive archive round trip, while SRT keeps the Mac speaker-line format");
string json = JsonSerializer.Serialize(archive, TranscriptFiles.Json);
var loaded = TranscriptFiles.Parse(json);
Check(loaded.Id == archive.Id && loaded.Segments[0].StartedAt == 800000000 && loaded.Summary == "已保存总结" && loaded.SummarizedEntries.GetValueOrDefault(segment.Entries[0].Id) == TranscriptFiles.SummarySignature("Hello there.") && json.Contains("\"english\""), "archive summary, incremental signatures, legacy JSON fields and Apple reference date round trip");
var summaryUnchanged = new Subtitle { English = "unchanged" };
var summaryEdited = new Subtitle { English = "edited" };
var summaryNew = new Subtitle { English = "new" };
var summaryLast = new Subtitle { English = "last segment" };
var summaryArchive = new Archive { Segments = [
    new Segment { Entries = [summaryUnchanged, summaryEdited, summaryNew] },
    new Segment { Entries = [summaryLast] }
] };
summaryArchive.SummarizedEntries[summaryUnchanged.Id] = TranscriptFiles.SummarySignature(summaryUnchanged.English);
summaryArchive.SummarizedEntries[summaryEdited.Id] = TranscriptFiles.SummarySignature("before edit");
summaryArchive.SummarizedEntries[summaryLast.Id] = TranscriptFiles.SummarySignature(summaryLast.English);
var incrementalSummary = TranscriptSummarySelection.Select(summaryArchive, 0);
var currentSegmentSummary = TranscriptSummarySelection.Select(summaryArchive, 1);
var completeSummary = TranscriptSummarySelection.Select(summaryArchive, 2);
Check(incrementalSummary.Select(e => e.English).SequenceEqual(["edited", "new"])
    && currentSegmentSummary.Select(e => e.English).SequenceEqual(["last segment"])
    && completeSummary.Count == 4,
    "summary selection excludes unchanged entries incrementally while current-segment and whole-archive scopes stay distinct");
var historicalUnsubmitted = new Subtitle { English = "old unsummarized session" };
var currentUnsubmitted = new Subtitle { English = "new session content" };
var sessionScopedArchive = new Archive { Segments = [
    new Segment { Entries = [historicalUnsubmitted] },
    new Segment { Entries = [currentUnsubmitted] }
] };
var sessionIncrementalSummary = TranscriptSummarySelection.Select(sessionScopedArchive, 0, newContentStartSegmentIndex: 1);
Check(sessionIncrementalSummary.Count == 1 && sessionIncrementalSummary[0].Id == currentUnsubmitted.Id
    && TranscriptSummarySelection.Select(sessionScopedArchive, 2, newContentStartSegmentIndex: 1).Count == 2,
    "new-content summaries exclude unsummarized historical segments while whole-archive summaries retain them");
var legacyId = Guid.NewGuid(); var legacySegmentId = Guid.NewGuid(); var legacyEntryId = Guid.NewGuid();
double legacyCreated = (DateTimeOffset.Parse("2024-01-01T00:00:00Z") - Archive.AppleEpoch).TotalSeconds;
double legacySegmentStart = (DateTimeOffset.Parse("2024-01-01T23:59:59Z") - Archive.AppleEpoch).TotalSeconds;
string legacyJson = JsonSerializer.Serialize(new
{
    id = legacyId, title = "旧格式跨日存档", createdAt = legacyCreated, updatedAt = legacyCreated,
    segments = new[] { new { id = legacySegmentId, startedAt = legacySegmentStart, updatedAt = legacySegmentStart,
        entries = new[] { new { id = legacyEntryId, start = 2d, end = 3d, english = "跨日旧档", chinese = "跨日字幕" } } } }
}, TranscriptFiles.Json);
var legacyArchive = TranscriptFiles.Parse(legacyJson); var legacySubtitle = legacyArchive.Segments[0].Entries[0];
string legacySrt = TranscriptFiles.Srt(legacyArchive);
Check(legacySubtitle.Language is null && legacySubtitle.Speaker is null && legacyArchive.Segments[0].StartedAt == legacySegmentStart
    && legacyArchive.Segments[0].StartedAt + legacySubtitle.Start == (DateTimeOffset.Parse("2024-01-02T00:00:01Z") - Archive.AppleEpoch).TotalSeconds
    && legacySrt.Contains("00:00:00,000 --> 00:00:01,000") && !legacySrt.Contains("[en]"),
    "legacy archive without language or speaker loads without inferred metadata and retains its UTC cross-midnight timestamp through SRT export");
const string macArchiveFixture = """
{"id":"11111111-1111-1111-1111-111111111111","title":"Mac Codable fixture","createdAt":0,"updatedAt":1,"segments":[{"id":"22222222-2222-2222-2222-222222222222","startedAt":0,"updatedAt":1,"entries":[{"id":"33333333-3333-3333-3333-333333333333","start":0,"end":1,"recordedAt":0,"english":"","chinese":"译文先到","speaker":null,"language":null,"correction":null},{"id":"44444444-4444-4444-4444-444444444444","start":2.3456,"end":3.5801,"recordedAt":2.3456,"english":"Hello","chinese":"你好","speaker":"Speaker 1","language":"en","correction":{"rawSource":"Recognized","rawTranslation":"识别译文","sourceLocked":true,"translationLocked":false,"revision":"55555555-5555-5555-5555-555555555555","history":[{"source":"Earlier","translation":"之前","date":0}]}}]}]}
""";
var macParsed = TranscriptFiles.Parse(macArchiveFixture);
string macWindowsSrt = TranscriptFiles.Srt(macParsed).Replace("\r\n", "\n");
var macRoundTrip = TranscriptFiles.Parse(JsonSerializer.Serialize(TranscriptFiles.Snapshot(macParsed), TranscriptFiles.Json));
string macReferenceSrt = "1\n00:00:02,345 --> 00:00:03,580\n[Speaker 1]\nHello\n你好\n";
Check(macParsed.CreatedAt == 0 && macParsed.Segments[0].StartedAt == 0
    && macRoundTrip.Id == Guid.Parse("11111111-1111-1111-1111-111111111111")
    && macRoundTrip.Segments[0].Entries[1].RecordedAt == 2.3456
    && macRoundTrip.Segments[0].Entries[1].Correction?.History[0].Date == Archive.AppleEpoch
    && macWindowsSrt == macReferenceSrt,
    "Swift Codable-shaped archive preserves Apple epoch and correction history, and Windows SRT matches Mac speaker, offset, and millisecond formatting");
const string multiSegmentMacArchiveFixture = """
{"id":"aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa","title":"Mac multi-segment fixture","createdAt":100,"updatedAt":201,"segments":[{"id":"bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb","startedAt":100,"updatedAt":101,"entries":[{"id":"cccccccc-cccc-cccc-cccc-cccccccccccc","start":0,"end":1,"recordedAt":100,"english":"第一段","chinese":"First segment"}]},{"id":"dddddddd-dddd-dddd-dddd-dddddddddddd","startedAt":200,"updatedAt":201,"entries":[{"id":"eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee","start":0,"end":1,"recordedAt":200,"english":"第二段","chinese":"Second segment","speaker":"Speaker 2","language":"zh"}]}]}
""";
var multiSegmentArchive = TranscriptFiles.Parse(multiSegmentMacArchiveFixture);
var multiSegmentRoundTrip = TranscriptFiles.Parse(JsonSerializer.Serialize(TranscriptFiles.Snapshot(multiSegmentArchive), TranscriptFiles.Json));
string multiSegmentSrt = TranscriptFiles.Srt(multiSegmentRoundTrip).Replace("\r\n", "\n");
Check(multiSegmentRoundTrip.Segments.Count == 2
    && multiSegmentRoundTrip.Segments.Select(s => s.Id).SequenceEqual([Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"), Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd")])
    && multiSegmentRoundTrip.Segments[1].Entries[0].RecordedAt == 200
    && multiSegmentRoundTrip.Segments[1].Entries[0].Speaker == "Speaker 2"
    && multiSegmentSrt == "1\n00:00:00,000 --> 00:00:01,000\n第一段\nFirst segment\n\n2\n00:01:40,000 --> 00:01:41,000\n[Speaker 2]\n第二段\nSecond segment\n",
    "Swift-shaped multi-segment archive round trip preserves segment IDs, wall-clock gaps, metadata, and cross-segment SRT order");
string? runtimeMacFixturePath = Environment.GetEnvironmentVariable("ECHO_MAC_ARCHIVE_FIXTURE");
if (!string.IsNullOrWhiteSpace(runtimeMacFixturePath))
{
    runtimeMacFixturePath = Path.GetFullPath(runtimeMacFixturePath);
    var runtimeMacArchive = TranscriptFiles.Parse(File.ReadAllText(runtimeMacFixturePath));
    string runtimeMacSrt = TranscriptFiles.Srt(runtimeMacArchive).Replace("\r\n", "\n");
    string runtimeMacExpectedSrt = File.ReadAllText(Path.ChangeExtension(runtimeMacFixturePath, ".srt")).Replace("\r\n", "\n");
    string runtimeWindowsJson = JsonSerializer.Serialize(TranscriptFiles.Snapshot(runtimeMacArchive), TranscriptFiles.Json);
    var runtimeWindowsArchive = TranscriptFiles.Parse(runtimeWindowsJson);
    string windowsRoundTripPath = Path.Combine(Path.GetDirectoryName(runtimeMacFixturePath)!, "windows-roundtrip.json");
    TranscriptFiles.AtomicWrite(windowsRoundTripPath, runtimeWindowsJson);
    Check(runtimeMacArchive.Id == Guid.Parse("11111111-1111-1111-1111-111111111111")
        && runtimeMacArchive.CreatedAt == 0
        && runtimeMacArchive.Segments.Count == 2
        && runtimeMacArchive.Segments[0].Entries[1].RecordedAt == 2.3456
        && runtimeWindowsArchive.Segments[0].Entries[1].Correction?.History[0].Date == Archive.AppleEpoch
        && runtimeWindowsArchive.Segments[1].Id == Guid.Parse("66666666-6666-6666-6666-666666666666")
        && runtimeWindowsArchive.Segments[1].StartedAt == 100
        && runtimeWindowsArchive.Segments[1].Entries[0].RecordedAt == 100
        && runtimeWindowsArchive.Segments[1].Entries[0].Language == "zh"
        && runtimeMacSrt == runtimeMacExpectedSrt,
        "Windows imports a Mac production multi-segment Archive, preserves correction/date/metadata fields, and matches Mac production SRT byte-for-byte");
    Console.WriteLine($"A17 Windows round-trip fixture: {new FileInfo(windowsRoundTripPath).Length} bytes");
}
var legacyIsoRevisionDate = JsonSerializer.Deserialize<DateTimeOffset>("\"2026-09-30T00:00:00+00:00\"", TranscriptFiles.Json);
Check(legacyIsoRevisionDate == new DateTimeOffset(2026, 9, 30, 0, 0, 0, TimeSpan.Zero),
    "Windows correction history still reads existing ISO-8601 revision dates after enabling Apple epoch dates");
string atomicTestRoot = Path.Combine(Path.GetTempPath(), "Echo-AtomicWrite-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(atomicTestRoot);
try
{
    string atomicPath = Path.Combine(atomicTestRoot, "archive.json");
    TranscriptFiles.AtomicWrite(atomicPath, "旧存档");
    TranscriptFiles.AtomicWrite(atomicPath, "新存档");
    bool replacedSafely = File.ReadAllText(atomicPath) == "新存档" && File.ReadAllText(atomicPath + ".bak") == "旧存档"
        && !Directory.EnumerateFiles(atomicTestRoot, "*.tmp", SearchOption.TopDirectoryOnly).Any();
    string blockedPath = Path.Combine(atomicTestRoot, "blocked.json"); Directory.CreateDirectory(blockedPath);
    bool failedSafely = false;
    try { TranscriptFiles.AtomicWrite(blockedPath, "不应覆盖目录"); }
    catch { failedSafely = Directory.Exists(blockedPath) && !Directory.EnumerateFiles(atomicTestRoot, ".blocked.json.*.tmp").Any(); }
    string protectedPath = Path.Combine(atomicTestRoot, "protected.json");
    TranscriptFiles.AtomicWrite(protectedPath, "必须保留的旧存档");
    Directory.CreateDirectory(protectedPath + ".bak");
    bool replacementFailureSafe = false;
    try { TranscriptFiles.AtomicWrite(protectedPath, "不能替换的新存档"); }
    catch
    {
        replacementFailureSafe = File.ReadAllText(protectedPath) == "必须保留的旧存档"
            && Directory.Exists(protectedPath + ".bak")
            && !Directory.EnumerateFiles(atomicTestRoot, ".protected.json.*.tmp").Any();
    }
    Check(replacedSafely && failedSafely && replacementFailureSafe, "atomic archive replacement flushes data, preserves the last good file and backup on replace failure, and cleans temporary writes");
}
finally { if (Directory.Exists(atomicTestRoot)) Directory.Delete(atomicTestRoot, recursive: true); }
string diskFullTestRoot = Path.Combine(Path.GetTempPath(), "Echo-DiskFullWrite-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(diskFullTestRoot);
try
{
    string diskFullArchive = Path.Combine(diskFullTestRoot, "archive.json");
    TranscriptFiles.AtomicWrite(diskFullArchive, "磁盘耗尽前的完整存档");
    IOException? diskFull = null;
    try
    {
        TranscriptFiles.AtomicWrite(diskFullArchive, new string('新', 8192), (stream, bytes) =>
        {
            stream.Write(bytes, 0, Math.Min(64, bytes.Length));
            throw new IOException("simulated disk full", unchecked((int)0x80070070));
        });
    }
    catch (IOException error) { diskFull = error; }
    bool diskFullRecovery = diskFull is not null && (diskFull.HResult & 0xFFFF) == 112
        && File.ReadAllText(diskFullArchive) == "磁盘耗尽前的完整存档"
        && !File.Exists(diskFullArchive + ".bak")
        && !Directory.EnumerateFiles(diskFullTestRoot, ".archive.json.*.tmp", SearchOption.TopDirectoryOnly).Any()
        && TranscriptFiles.SaveFailureMessage(diskFull!) == "磁盘空间不足，存档未保存。请释放磁盘空间后重试。";
    Check(diskFullRecovery,
        "simulated disk-full during a partial temp write preserves the previous archive, removes the temp file and reports actionable guidance");
}
finally { if (Directory.Exists(diskFullTestRoot)) Directory.Delete(diskFullTestRoot, recursive: true); }
string crashTestRoot = Path.Combine(Path.GetTempPath(), "Echo-CrashWrite-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(crashTestRoot);
try
{
    string crashArchive = Path.Combine(crashTestRoot, "crash.json"), readyMarker = Path.Combine(crashTestRoot, "ready.marker");
    TranscriptFiles.AtomicWrite(crashArchive, "强退前的完整存档");
    string processPath = Environment.ProcessPath ?? throw new InvalidOperationException("Current process path is unavailable.");
    var childInfo = new ProcessStartInfo(processPath) { UseShellExecute = false, CreateNoWindow = true, WindowStyle = ProcessWindowStyle.Hidden };
    if (Path.GetFileNameWithoutExtension(processPath).Equals("dotnet", StringComparison.OrdinalIgnoreCase))
        childInfo.ArgumentList.Add(Assembly.GetEntryAssembly()?.Location ?? throw new InvalidOperationException("Entry assembly path is unavailable."));
    childInfo.ArgumentList.Add("--atomic-write-crash-child");
    childInfo.ArgumentList.Add(crashArchive);
    childInfo.ArgumentList.Add("替换后内容");
    childInfo.ArgumentList.Add(readyMarker);
    using var child = Process.Start(childInfo) ?? throw new InvalidOperationException("Unable to start the CoreChecks crash helper.");
    try
    {
        DateTime deadline = DateTime.UtcNow.AddSeconds(10);
        while (!File.Exists(readyMarker) && !child.HasExited && DateTime.UtcNow < deadline) await Task.Delay(20);
    }
    finally
    {
        if (!child.HasExited) { child.Kill(entireProcessTree: true); child.WaitForExit(10_000); }
    }
    bool reachedFlushBoundary = File.Exists(readyMarker);
    bool previousSnapshotSurvived = File.ReadAllText(crashArchive) == "强退前的完整存档";
    bool orphanedWriteExists = Directory.EnumerateFiles(crashTestRoot, ".crash.json.*.tmp", SearchOption.TopDirectoryOnly).Any();
    TranscriptFiles.AtomicWrite(crashArchive, "恢复后的完整存档");
    bool recoveredWithoutOrphan = File.ReadAllText(crashArchive) == "恢复后的完整存档"
        && File.ReadAllText(crashArchive + ".bak") == "强退前的完整存档"
        && !Directory.EnumerateFiles(crashTestRoot, ".crash.json.*.tmp", SearchOption.TopDirectoryOnly).Any();
    Check(reachedFlushBoundary && previousSnapshotSurvived && orphanedWriteExists && recoveredWithoutOrphan,
        "forced writer exit before atomic replace preserves the previous archive and the next save removes only the dead process temp file");
}
finally { if (Directory.Exists(crashTestRoot)) Directory.Delete(crashTestRoot, recursive: true); }
string aclTestRoot = Path.Combine(Path.GetTempPath(), "Echo-AclWrite-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(aclTestRoot);
try
{
    var aclDirectory = new DirectoryInfo(aclTestRoot);
    string aclArchive = Path.Combine(aclTestRoot, "denied.json");
    TranscriptFiles.AtomicWrite(aclArchive, "权限拒绝前的完整存档");
    DirectorySecurity originalSecurity = aclDirectory.GetAccessControl();
    try
    {
        var denyCreate = new FileSystemAccessRule(WindowsIdentity.GetCurrent().User!, FileSystemRights.CreateFiles,
            AccessControlType.Deny);
        var deniedSecurity = aclDirectory.GetAccessControl();
        deniedSecurity.AddAccessRule(denyCreate);
        aclDirectory.SetAccessControl(deniedSecurity);
        bool replacementDeniedSafely = false;
        try { TranscriptFiles.AtomicWrite(aclArchive, "不应替换的新存档"); }
        catch (UnauthorizedAccessException)
        {
            replacementDeniedSafely = File.ReadAllText(aclArchive) == "权限拒绝前的完整存档"
                && !Directory.EnumerateFiles(aclTestRoot, ".denied.json.*.tmp", SearchOption.TopDirectoryOnly).Any();
        }
        Check(replacementDeniedSafely, "ACL denial while creating an archive temp file preserves the previous archive without leaving a partial temp file");
    }
    finally { aclDirectory.SetAccessControl(originalSecurity); }
}
finally { if (Directory.Exists(aclTestRoot)) Directory.Delete(aclTestRoot, recursive: true); }
string aclReplaceRoot = Path.Combine(Path.GetTempPath(), "Echo-AclReplace-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(aclReplaceRoot);
try
{
    string aclArchive = Path.Combine(aclReplaceRoot, "replace-denied.json");
    TranscriptFiles.AtomicWrite(aclArchive, "拒绝替换前的完整存档");
    bool reachedReplaceBoundary = false, replacementDenied = false;
    string currentUserSid = WindowsIdentity.GetCurrent().User!.Value;
    bool denyRuleApplied = false;
    try
    {
        RunIcacls(aclArchive, "/deny", $"*{currentUserSid}:(D)");
        denyRuleApplied = true;
        try
        {
            TranscriptFiles.AtomicWrite(aclArchive, "不应替换的新存档", () => reachedReplaceBoundary = true);
        }
        catch (Exception error) when (error is UnauthorizedAccessException or IOException)
        {
            replacementDenied = !File.Exists(aclArchive + ".bak");
        }
    }
    finally { if (denyRuleApplied) RunIcacls(aclArchive, "/remove:d", $"*{currentUserSid}"); }
    bool cleanAfterPermissionsRestored = File.ReadAllText(aclArchive) == "拒绝替换前的完整存档"
        && !File.Exists(aclArchive + ".bak")
        && !Directory.EnumerateFiles(aclReplaceRoot, ".replace-denied.json.*.tmp", SearchOption.TopDirectoryOnly).Any();
    Check(reachedReplaceBoundary && replacementDenied && cleanAfterPermissionsRestored,
        "ACL denial at atomic replace preserves the previous archive; restoring the test ACL allows orphan cleanup");
}
finally { if (Directory.Exists(aclReplaceRoot)) Directory.Delete(aclReplaceRoot, recursive: true); }
var firstSaveStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
var releaseFirstSave = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
var saveAttempts = new List<(int Revision, int TranscriptEntries)>();
var durableSaves = new List<(int Revision, int TranscriptEntries)>();
async Task PersistCheckpoint((int Revision, int TranscriptEntries) snapshot)
{
    saveAttempts.Add(snapshot);
    if (snapshot.Revision == 1)
    {
        firstSaveStarted.TrySetResult();
        await releaseFirstSave.Task;
        throw new IOException("simulated disk write failure");
    }
    durableSaves.Add(snapshot);
}
var saveQueue = new OrderedPersistenceQueue<(int Revision, int TranscriptEntries)>(PersistCheckpoint);
Task failedCheckpoint = saveQueue.Enqueue((1, 1));
await firstSaveStarted.Task;
Task? recoveredCheckpoint = null;
bool coalescedBurst = true;
for (int checkpoint = 2; checkpoint <= 10000; checkpoint++)
{
    var queued = saveQueue.Enqueue((checkpoint, checkpoint));
    if (recoveredCheckpoint is null) recoveredCheckpoint = queued;
    else coalescedBurst &= ReferenceEquals(recoveredCheckpoint, queued);
}
Task latestCheckpoint = saveQueue.FlushAsync();
bool laterCheckpointWaited = saveAttempts.Count == 1 && saveAttempts[0] == (1, 1);
releaseFirstSave.TrySetResult();
bool firstCheckpointFailed = false;
try { await failedCheckpoint; } catch (IOException) { firstCheckpointFailed = true; }
await recoveredCheckpoint!;
await saveQueue.FlushAsync();
Check(laterCheckpointWaited && firstCheckpointFailed && coalescedBurst && ReferenceEquals(latestCheckpoint, recoveredCheckpoint)
    && saveAttempts.Count == 2 && saveAttempts[0] == (1, 1) && saveAttempts[1] == (10000, 10000)
    && durableSaves.Count == 1 && durableSaves[0] == (10000, 10000),
    "a slow archive writer keeps one newest pending full snapshot, coalesces 9,999 checkpoints and flushes all 10,000 entries after failure");
var splitSource = new Archive { Title = "课程", Segments = [new Segment(), new Segment { StartedAt = 800000123, Entries = [new Subtitle { English = "拆分字幕" }] }] };
splitSource.SummarizedEntries[splitSource.Segments[1].Entries[0].Id] = TranscriptFiles.SummarySignature("拆分字幕");
var splitResult = ArchiveOperations.SplitSegment(splitSource, splitSource.Segments[1].Id)!;
Check(splitSource.Segments.Count == 1 && splitResult.ExtractedArchive.Title == "课程 - 本段" && splitResult.ExtractedArchive.Segments[0].Entries[0].English == "拆分字幕" && splitSource.SummarizedEntries.Count == 0, "splitting moves a completed segment and preserves its text in a separate archive");
ArchiveOperations.RestoreSplit(splitSource, splitResult);
Check(splitSource.Segments.Count == 2 && splitSource.Segments[1].Entries[0].English == "拆分字幕" && splitSource.SummarizedEntries.Count == 1, "split rollback restores the original segment order and incremental summary state");
string trashTestRoot = Path.Combine(Path.GetTempPath(), "Echo-CoreChecks-" + Guid.NewGuid().ToString("N"));
string trashArchives = Path.Combine(trashTestRoot, "Archives"), trashDeleted = Path.Combine(trashTestRoot, "Deleted");
Directory.CreateDirectory(trashArchives);
var trashArchive = new Archive(); string trashSource = Path.Combine(trashArchives, $"{trashArchive.Id}.json");
File.WriteAllText(trashSource, "archive contents"); File.WriteAllText(trashSource + ".bak", "previous archive");
try
{
    string movedArchive = TranscriptFiles.MoveToDeleted(trashArchive, trashArchives, trashDeleted);
    Check(!File.Exists(trashSource) && File.ReadAllText(movedArchive) == "archive contents" && File.ReadAllText(movedArchive + ".bak") == "previous archive", "moving an archive to recycle area preserves both JSON and backup contents");
}
finally { if (Directory.Exists(trashTestRoot)) Directory.Delete(trashTestRoot, recursive: true); }
var correctionLoaded = TranscriptFiles.Parse(correctionSnapshot).Segments[0].Entries[0].Correction;
Check(correctionLoaded?.RawSource == "Recognized text" && correctionLoaded.SourceLocked && correctionLoaded.History.Count == 1 && correctionSnapshot.Contains("\"rawSource\""), "correction source, lock and undo history survive Mac-compatible archive round trip");
var chunkInput = new Subtitle { English = string.Concat(Enumerable.Repeat("汉", 300)) };
var chunks = TranscriptTextChunks.Create([chunkInput], 128);
Check(chunks.Count > 1 && chunks.All(c => c.Length <= 128) && chunks.Sum(c => c.Count(ch => ch == '汉')) == 300, "long transcript chunks stay bounded without dropping Unicode text");
DateTime firstLocalClock = new(2024, 1, 1, 23, 59, 59, DateTimeKind.Unspecified);
TimeSpan localOffset = TimeZoneInfo.Local.GetUtcOffset(firstLocalClock);
DateTimeOffset firstLocal = new(firstLocalClock, localOffset);
double AppleSeconds(DateTimeOffset local) => (local.ToUniversalTime() - Archive.AppleEpoch).TotalSeconds;
var timestampEntries = new[]
{
    new Subtitle { RecordedAt = AppleSeconds(firstLocal), English = " first English ", Chinese = " 第一条中文 ", Speaker = "speaker_1" },
    new Subtitle { RecordedAt = AppleSeconds(firstLocal.AddSeconds(2)), English = "second English", Chinese = "第二条中文" },
    new Subtitle { RecordedAt = AppleSeconds(firstLocal.AddSeconds(4)), English = "third English", Chinese = "第三条中文" }
};
var timestampTranscript = TranscriptTextChunks.Create(timestampEntries).Single();
var nextDayLocal = firstLocal.AddSeconds(2);
var sameDayLocal = firstLocal.AddSeconds(4);
Check(timestampTranscript.Contains($"1. [{firstLocal.ToString("MM-dd HH:mm:ss", CultureInfo.InvariantCulture)}]", StringComparison.Ordinal)
    && timestampTranscript.Contains($"2. [{nextDayLocal.ToString("MM-dd HH:mm:ss", CultureInfo.InvariantCulture)}]", StringComparison.Ordinal)
    && timestampTranscript.Contains($"3. [{sameDayLocal.ToString("HH:mm:ss", CultureInfo.InvariantCulture)}]", StringComparison.Ordinal)
    && timestampTranscript.Contains("英文：first English\n中文：第一条中文", StringComparison.Ordinal)
    && timestampTranscript.Contains("英文：second English\n中文：第二条中文", StringComparison.Ordinal)
    && !timestampTranscript.Contains("speaker_1", StringComparison.Ordinal),
    "AI summary transcript preserves numbered bilingual text and Mac local timestamp formatting across midnight");
var archiveTimedEntries = new[]
{
    new Subtitle { Start = 0, English = "archive first", Chinese = "存档第一条" },
    new Subtitle { Start = 2, English = "archive next day", Chinese = "存档跨日" }
};
var archiveTimedTranscript = TranscriptTextChunks.Create(archiveTimedEntries, archive: new Archive
{
    Segments = [new Segment { StartedAt = AppleSeconds(firstLocal), Entries = archiveTimedEntries.ToList() }]
}).Single();
Check(archiveTimedTranscript.Contains($"1. [{firstLocal.ToString("MM-dd HH:mm:ss", CultureInfo.InvariantCulture)}]", StringComparison.Ordinal)
    && archiveTimedTranscript.Contains($"2. [{nextDayLocal.ToString("MM-dd HH:mm:ss", CultureInfo.InvariantCulture)}]", StringComparison.Ordinal),
    "AI summary transcript derives Mac wall-clock times from archive segment start when entry timestamps are absent");
string incrementalPrompt = TranscriptSummaryPrompt.Build(0, timestampTranscript);
string sessionPrompt = TranscriptSummaryPrompt.Build(1, timestampTranscript);
string archivePrompt = TranscriptSummaryPrompt.Build(2, timestampTranscript);
Check(incrementalPrompt.Contains("### 新增内容", StringComparison.Ordinal)
    && incrementalPrompt.Contains("新增文字稿：", StringComparison.Ordinal)
    && sessionPrompt.Contains("请根据下面的英文实时文字稿", StringComparison.Ordinal)
    && archivePrompt.Contains("只返回总结正文", StringComparison.Ordinal)
    && sessionPrompt.Contains("中文翻译只作为辅助参考", StringComparison.Ordinal)
    && sessionPrompt.Contains(timestampTranscript, StringComparison.Ordinal),
    "incremental and full-summary prompts match Mac scope guidance and include the bilingual transcript");
var twoHourArchive = new Archive { Segments = [new Segment { StartedAt = 800000000,
    Entries = Enumerable.Range(0, 7200).Select(i => new Subtitle { Start = i, End = i + 1, English = $"Synthetic line {i}", Chinese = $"合成字幕 {i}" }).ToList() }] };
var archiveStressTimer = Stopwatch.StartNew();
var twoHourSnapshot = TranscriptFiles.Snapshot(twoHourArchive);
string twoHourJson = JsonSerializer.Serialize(twoHourSnapshot, TranscriptFiles.Json);
var twoHourLoaded = TranscriptFiles.Parse(twoHourJson);
string twoHourSrt = TranscriptFiles.Srt(twoHourLoaded);
archiveStressTimer.Stop();
Console.WriteLine($"A16 synthetic 2-hour archive: entries={twoHourLoaded.Segments[0].Entries.Count}, JSON={Encoding.UTF8.GetByteCount(twoHourJson)} bytes, SRT={Encoding.UTF8.GetByteCount(twoHourSrt)} bytes, snapshot+JSON+parse+SRT={archiveStressTimer.ElapsedMilliseconds} ms");
Check(twoHourLoaded.Segments[0].Entries.Count == 7200
    && twoHourLoaded.Segments[0].Entries[0].English == "Synthetic line 0"
    && twoHourLoaded.Segments[0].Entries[^1].English == "Synthetic line 7199"
    && twoHourSrt.Contains("02:00:00,000") && twoHourSrt.Contains("Synthetic line 7199"),
    "two-hour synthetic archive snapshot, JSON round trip and SRT export preserve all 7200 entries and the final timestamp");
string diskStressRoot = Path.Combine(Path.GetTempPath(), "Echo-CoreChecks-A16-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(diskStressRoot);
string diskStressPath = Path.Combine(diskStressRoot, "two-hour.json");
try
{
    var priorCheckpoint = new Archive { Id = twoHourLoaded.Id, Segments = [new Segment { StartedAt = 800000000,
        Entries = [new Subtitle { Start = 0, End = 1, English = "Prior checkpoint" }] }] };
    TranscriptFiles.AtomicWrite(diskStressPath, JsonSerializer.Serialize(TranscriptFiles.Snapshot(priorCheckpoint), TranscriptFiles.Json));
    var diskWriteTimer = Stopwatch.StartNew();
    TranscriptFiles.AtomicWrite(diskStressPath, twoHourJson);
    var diskReloaded = TranscriptFiles.Parse(File.ReadAllText(diskStressPath));
    var diskBackup = TranscriptFiles.Parse(File.ReadAllText(diskStressPath + ".bak"));
    diskWriteTimer.Stop();
    long diskBytes = new FileInfo(diskStressPath).Length;
    Console.WriteLine($"A16 atomic disk save: JSON={diskBytes} bytes, write+flush+replace+reload={diskWriteTimer.ElapsedMilliseconds} ms, backupEntries={diskBackup.Segments[0].Entries.Count}");
    Check(diskBytes == Encoding.UTF8.GetByteCount(twoHourJson)
        && diskReloaded.Segments[0].Entries.Count == 7200
        && diskReloaded.Segments[0].Entries[^1].English == "Synthetic line 7199"
        && diskBackup.Segments[0].Entries.Single().English == "Prior checkpoint"
        && !Directory.EnumerateFiles(diskStressRoot, "*.tmp", SearchOption.TopDirectoryOnly).Any(),
        "two-hour synthetic archive is durably replaced and reloaded while the previous checkpoint remains in backup");
}
finally { if (Directory.Exists(diskStressRoot)) Directory.Delete(diskStressRoot, recursive: true); }
int streamedRowsAdded = 0, streamedRowsFinalized = 0;
var tokenStressSegment = new Segment { StartedAt = 800000000 };
var tokenStressAssembler = new TokenAssembler(tokenStressSegment, _ => streamedRowsAdded++, _ => streamedRowsFinalized++);
var tokenStressTimer = Stopwatch.StartNew();
for (int i = 0; i < 7200; i++)
{
    int startMs = i * 1000, endMs = startMs + 1000;
    using (var provisional = JsonDocument.Parse($"{{\"tokens\":[{{\"text\":\"Partial {i}\",\"is_final\":false,\"start_ms\":{startMs},\"end_ms\":{endMs}}}]}}"))
        tokenStressAssembler.Apply(provisional.RootElement);
    using (var final = JsonDocument.Parse($"{{\"tokens\":[{{\"text\":\"Lecture point {i}.\",\"is_final\":true,\"start_ms\":{startMs},\"end_ms\":{endMs},\"speaker\":\"1\",\"language\":\"en\"}},{{\"text\":\"课程点 {i}。\",\"is_final\":true,\"translation_status\":\"translation\"}},{{\"text\":\"<end>\",\"is_final\":true}}]}}"))
        tokenStressAssembler.Apply(final.RootElement);
}
tokenStressTimer.Stop();
var tokenStressArchive = new Archive { Segments = [tokenStressSegment] };
string tokenStressJson = JsonSerializer.Serialize(TranscriptFiles.Snapshot(tokenStressArchive), TranscriptFiles.Json);
var tokenStressLoaded = TranscriptFiles.Parse(tokenStressJson);
string tokenStressSrt = TranscriptFiles.Srt(tokenStressLoaded);
var tokenStressEntries = tokenStressLoaded.Segments.Single().Entries;
Console.WriteLine($"A67 synthetic 2-hour Soniox stream: responses=14400, rows={tokenStressEntries.Count}, JSON={Encoding.UTF8.GetByteCount(tokenStressJson)} bytes, SRT={Encoding.UTF8.GetByteCount(tokenStressSrt)} bytes, stream+roundtrip+SRT={tokenStressTimer.ElapsedMilliseconds} ms");
Check(streamedRowsAdded == 7200 && streamedRowsFinalized == 7200 && tokenStressEntries.Count == 7200
    && tokenStressEntries.Select((entry, i) => entry.English == $"Lecture point {i}."
        && entry.Chinese == $"课程点 {i}。" && entry.Start == i && entry.End == i + 1
        && entry.RecordedAt == tokenStressSegment.StartedAt + i && entry.Speaker == "Speaker 1" && entry.Language == "en").All(matches => matches)
    && tokenStressSrt.Contains("02:00:00,000", StringComparison.Ordinal)
    && tokenStressSrt.Contains("Lecture point 7199.", StringComparison.Ordinal),
    "two-hour synthetic Soniox provisional/final stream preserves 7200 bilingual rows, timestamps, metadata, and SRT output");
string? macStressFixturePath = Environment.GetEnvironmentVariable("ECHO_MAC_SONIOX_STRESS_FIXTURE");
if (!string.IsNullOrWhiteSpace(macStressFixturePath))
{
    using var macStressFixture = JsonDocument.Parse(File.ReadAllText(macStressFixturePath));
    var macStressRun = macStressFixture.RootElement.GetProperty("events")[0];
    var macStressRows = macStressRun.GetProperty("expected").EnumerateArray().ToArray();
    Check(macStressFixture.RootElement.GetProperty("mode").GetString() == "two-hour-stress"
        && macStressRun.GetProperty("expectedFinalizations").GetInt32() == 7200
        && macStressRows.Length == tokenStressEntries.Count
        && macStressRows.Select((row, i) => row.GetProperty("english").GetString() == tokenStressEntries[i].English
            && row.GetProperty("chinese").GetString() == tokenStressEntries[i].Chinese
            && row.GetProperty("start").GetDouble() == tokenStressEntries[i].Start
            && row.GetProperty("end").GetDouble() == tokenStressEntries[i].End
            && row.GetProperty("speaker").GetString() == tokenStressEntries[i].Speaker
            && row.GetProperty("language").GetString() == tokenStressEntries[i].Language).All(matches => matches),
        "two-hour synthetic Soniox stream matches all 7200 rows and finalizations from the Mac production handler");
}
Check(Archive.AppleEpoch.AddSeconds(0).Year == 2001, "Apple date reference is not Unix time");
Check(SpeechRetryPolicy.MaxRetries == 2 && SpeechRetryPolicy.Delay(1) == TimeSpan.FromSeconds(1) && SpeechRetryPolicy.Delay(2) == TimeSpan.FromSeconds(3)
    && SpeechRetryPolicy.IsTransient(new System.Net.WebSockets.WebSocketException())
    && SpeechRetryPolicy.IsTransient(new SpeechServiceException("temporary 503", retryable: true))
    && !SpeechRetryPolicy.IsTransient(new SpeechServiceException("401 or quota"))
    && !SpeechRetryPolicy.IsTransient(new AudioCaptureFailureException("device failed", new IOException())), "network retry policy is bounded and excludes service/device errors");
string secret = "synthetic-local-test-not-an-api-key";
Check(Preferences.Unprotect(Preferences.Protect(secret)) == secret, "current-user DPAPI credential round trip");
Check(Preferences.UpdateProtectedSecret("unreadable-ciphertext", "", currentSecretCannotBeUnprotected: true) == "unreadable-ciphertext",
    "saving unrelated settings preserves a DPAPI secret that could not be decrypted");
string replacementSecret = Preferences.UpdateProtectedSecret("unreadable-ciphertext", " replacement-key ", currentSecretCannotBeUnprotected: true);
Check(Preferences.Unprotect(replacementSecret) == "replacement-key",
    "a replacement API key encrypts normally after an unreadable stored value");
Check(Preferences.UpdateProtectedSecret(Preferences.Protect("existing-key"), "", currentSecretCannotBeUnprotected: false) == "",
    "clearing a readable API key keeps the normal empty-field behavior");
bool rejected = false; try { TranscriptFiles.Parse("{}"); } catch { rejected = true; }
Check(rejected, "malformed archive is rejected rather than silently imported");
rejected = false;
try { TranscriptFiles.Parse(json.Replace("800000000", "1e100")); } catch { rejected = true; }
Check(rejected, "out-of-range archive dates are rejected before UI formatting");
var duplicate = new Archive { Segments = [new Segment { Entries = [segment.Entries[0], segment.Entries[0]] }] };
rejected = false; try { TranscriptFiles.Parse(JsonSerializer.Serialize(duplicate, TranscriptFiles.Json)); } catch { rejected = true; }
Check(rejected, "duplicate subtitle IDs are rejected before summary processing");
async Task<bool> CheckRejectedHandshakeAsync(int statusCode)
{
    var probe = new TcpListener(IPAddress.Loopback, 0); probe.Start();
    int statusPort = ((IPEndPoint)probe.LocalEndpoint).Port; probe.Stop();
    using var rejectedListener = new HttpListener(); rejectedListener.Prefixes.Add($"http://127.0.0.1:{statusPort}/"); rejectedListener.Start();
    using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
    var reject = Task.Run(async () =>
    {
        var context = await rejectedListener.GetContextAsync().WaitAsync(timeout.Token);
        bool hasBearerKey = context.Request.Headers["Authorization"] == "Bearer synthetic";
        context.Response.StatusCode = statusCode; context.Response.Close();
        return hasBearerKey;
    }, timeout.Token);
    await using var rejectedSession = new SpeechSession(new Uri($"ws://127.0.0.1:{statusPort}/"), captureEnabled: false);
    try { await rejectedSession.StartAsync(new Preferences(), "synthetic", 0, null, null); }
    catch (SpeechServiceException error)
    {
        bool hasBearerKey = await reject;
        return hasBearerKey && error.StatusCode == statusCode && error.Retryable == (statusCode >= 500)
            && (statusCode != 401 || error.Message.Contains("更新 Key"));
    }
    catch { await reject; return false; }
    return false;
}
Check(await CheckRejectedHandshakeAsync(401), "HTTP 401 handshake failure is not retried");
Check(await CheckRejectedHandshakeAsync(503), "HTTP 503 handshake failure uses the bounded retry policy");
async Task<(bool BearerHeader, bool ConfigOmitsKey, SpeechServiceException? Error)> CheckServiceErrorFrameAsync(
    int statusCode, string errorType, string serviceMessage, string expectedGuidance, bool retryable)
{
    var portProbe = new TcpListener(IPAddress.Loopback, 0); portProbe.Start();
    int port = ((IPEndPoint)portProbe.LocalEndpoint).Port; portProbe.Stop();
    using var listener = new HttpListener(); listener.Prefixes.Add($"http://127.0.0.1:{port}/"); listener.Start();
    using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
    var server = Task.Run(async () =>
    {
        var context = await listener.GetContextAsync().WaitAsync(timeout.Token);
        bool bearerHeader = context.Request.Headers["Authorization"] == "Bearer synthetic";
        using var socket = (await context.AcceptWebSocketAsync(null)).WebSocket;
        var packet = new byte[65536];
        var configResult = await socket.ReceiveAsync(new ArraySegment<byte>(packet), timeout.Token);
        using var config = JsonDocument.Parse(packet.AsMemory(0, configResult.Count));
        bool configOmitsKey = !config.RootElement.TryGetProperty("api_key", out _)
            && config.RootElement.GetProperty("sample_rate").GetInt32() == 16000;
        byte[] error = JsonSerializer.SerializeToUtf8Bytes(new
        {
            tokens = Array.Empty<object>(), error_code = statusCode, error_type = errorType,
            error_message = serviceMessage, request_id = "synthetic-request-id"
        });
        await socket.SendAsync(error.AsMemory(), WebSocketMessageType.Text, true, timeout.Token);
        return (bearerHeader, configOmitsKey);
    }, timeout.Token);
    var failure = new TaskCompletionSource<Exception>(TaskCreationOptions.RunContinuationsAsynchronously);
    await using (var session = new SpeechSession(new Uri($"ws://127.0.0.1:{port}/"), captureEnabled: false))
    {
        session.Failure += error => failure.TrySetResult(error);
        await session.StartAsync(new Preferences(), "synthetic", 0, null, null);
        var error = await failure.Task.WaitAsync(timeout.Token);
        var serverResult = await server;
        listener.Stop();
        var serviceError = error as SpeechServiceException;
        bool actionable = serviceError is not null
            && serviceError.StatusCode == statusCode && serviceError.ErrorType == errorType
            && serviceError.RequestId == "synthetic-request-id" && serviceError.Retryable == retryable
            && serviceError.Message.Contains(expectedGuidance) && serviceError.Message.Contains("synthetic-request-id");
        return (serverResult.bearerHeader, serverResult.configOmitsKey, actionable ? serviceError : null);
    }
}
var serviceErrorCases = new[]
{
    (401, "unauthenticated", "Incorrect API key", "更新 Key", false),
    (402, "organization_balance_exhausted", "Organization balance exhausted", "余额或月度预算", false),
    (403, "permission_denied", "Realtime STT permission missing", "产品权限", false),
    (429, "limit_exceeded", "Concurrent requests limit reached", "限额", false),
    (503, "service_unavailable", "Backend temporarily unavailable", "有限次数重试", true),
    (413, "max_duration_reached", "Maximum session duration reached", "达到时长上限", true)
};
bool serviceErrorsClassified = true;
foreach (var (statusCode, errorType, serviceMessage, expectedGuidance, retryable) in serviceErrorCases)
{
    var result = await CheckServiceErrorFrameAsync(statusCode, errorType, serviceMessage, expectedGuidance, retryable);
    serviceErrorsClassified &= result.BearerHeader && result.ConfigOmitsKey && result.Error is not null;
}
Check(serviceErrorsClassified,
    "Soniox WebSocket uses Authorization Bearer without placing the API key in config and gives actionable, non-retryable auth/quota errors");
async Task<bool> CheckMessageOnlyServiceErrorFrameAsync()
{
    var portProbe = new TcpListener(IPAddress.Loopback, 0); portProbe.Start();
    int port = ((IPEndPoint)portProbe.LocalEndpoint).Port; portProbe.Stop();
    using var listener = new HttpListener(); listener.Prefixes.Add($"http://127.0.0.1:{port}/"); listener.Start();
    using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
    var server = Task.Run(async () =>
    {
        var context = await listener.GetContextAsync().WaitAsync(timeout.Token);
        using var socket = (await context.AcceptWebSocketAsync(null)).WebSocket;
        var packet = new byte[65536];
        var config = await socket.ReceiveAsync(new ArraySegment<byte>(packet), timeout.Token);
        var frame = JsonSerializer.SerializeToUtf8Bytes(new Dictionary<string, object?>
        {
            ["error_message"] = "Synthetic service failure",
            ["request_id"] = "message-only-request"
        });
        await socket.SendAsync(frame.AsMemory(), WebSocketMessageType.Text, true, timeout.Token);
    }, timeout.Token);
    var failure = new TaskCompletionSource<Exception>(TaskCreationOptions.RunContinuationsAsynchronously);
    bool deliveredAsTranscript = false;
    await using var session = new SpeechSession(new Uri($"ws://127.0.0.1:{port}/"), captureEnabled: false);
    session.Failure += error => failure.TrySetResult(error);
    session.Message += _ => deliveredAsTranscript = true;
    await session.StartAsync(new Preferences(), "synthetic", 0, null, null);
    var received = await failure.Task.WaitAsync(timeout.Token);
    await server; listener.Stop();
    return received is SpeechServiceException serviceError && serviceError.StatusCode is null
        && serviceError.RequestId == "message-only-request" && !serviceError.Retryable
        && serviceError.Message.Contains("Synthetic service failure") && !deliveredAsTranscript;
}
Check(await CheckMessageOnlyServiceErrorFrameAsync(),
    "Soniox string error_message without error_code is surfaced as a service error and never reaches transcript parsing");
async Task<bool> CheckMalformedFinishedFrameAsync()
{
    var portProbe = new TcpListener(IPAddress.Loopback, 0); portProbe.Start();
    int port = ((IPEndPoint)portProbe.LocalEndpoint).Port; portProbe.Stop();
    using var listener = new HttpListener(); listener.Prefixes.Add($"http://127.0.0.1:{port}/"); listener.Start();
    using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
    var server = Task.Run(async () =>
    {
        var context = await listener.GetContextAsync().WaitAsync(timeout.Token);
        using var socket = (await context.AcceptWebSocketAsync(null)).WebSocket;
        var packet = new byte[65536];
        await socket.ReceiveAsync(new ArraySegment<byte>(packet), timeout.Token); // config
        await socket.ReceiveAsync(new ArraySegment<byte>(packet), timeout.Token); // finish marker
        byte[] malformed = Encoding.UTF8.GetBytes("""{"finished":"true"}""");
        await socket.SendAsync(malformed.AsMemory(), WebSocketMessageType.Text, true, timeout.Token);
        await Task.Delay(50, timeout.Token);
        byte[] valid = Encoding.UTF8.GetBytes("""{"finished":true}""");
        await socket.SendAsync(valid.AsMemory(), WebSocketMessageType.Text, true, timeout.Token);
    }, timeout.Token);
    var failure = new TaskCompletionSource<Exception>(TaskCreationOptions.RunContinuationsAsynchronously);
    bool malformedDelivered = false, finishedDelivered = false;
    await using var session = new SpeechSession(new Uri($"ws://127.0.0.1:{port}/"), captureEnabled: false);
    session.Failure += error => failure.TrySetResult(error);
    session.Message += message =>
    {
        if (!message.TryGetProperty("finished", out var finished)) return;
        malformedDelivered |= finished.ValueKind == JsonValueKind.String;
        finishedDelivered |= finished.ValueKind == JsonValueKind.True;
    };
    await session.StartAsync(new Preferences(), "synthetic", 0, null, null);
    bool stopped = true;
    try { await session.StopAsync(); } catch { stopped = false; }
    await server; listener.Stop();
    return stopped && malformedDelivered && finishedDelivered && !failure.Task.IsCompleted;
}
Check(await CheckMalformedFinishedFrameAsync(),
    "non-boolean finished is ignored safely and a later finished:true completes the stop handshake");
var sanitizedServiceError = SpeechServiceException.FromApiError(401, "unauthenticated", "Invalid\r\nkey", "request\r\nid");
Check(!sanitizedServiceError.Message.Contains('\r') && !sanitizedServiceError.Message.Contains('\n')
    && sanitizedServiceError.Message.Contains("requestid"),
    "service diagnostics remove line breaks from remote messages and request IDs before showing them in the status line");
async Task<bool> CheckConnectCancellationAsync()
{
    var probe = new TcpListener(IPAddress.Loopback, 0); probe.Start();
    int statusPort = ((IPEndPoint)probe.LocalEndpoint).Port; probe.Stop();
    using var slowListener = new HttpListener(); slowListener.Prefixes.Add($"http://127.0.0.1:{statusPort}/"); slowListener.Start();
    using var serverTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
    var rejectLater = Task.Run(async () =>
    {
        var context = await slowListener.GetContextAsync().WaitAsync(serverTimeout.Token);
        await Task.Delay(300, serverTimeout.Token);
        context.Response.StatusCode = 503; context.Response.Close();
    }, serverTimeout.Token);
    using var stop = new CancellationTokenSource(TimeSpan.FromMilliseconds(80));
    await using var canceledSession = new SpeechSession(new Uri($"ws://127.0.0.1:{statusPort}/"), captureEnabled: false);
    try { await canceledSession.StartAsync(new Preferences(), "synthetic", 0, null, null, stop.Token); }
    catch (OperationCanceledException) { await rejectLater; return true; }
    catch { await rejectLater; return false; }
    await rejectLater; return false;
}
Check(await CheckConnectCancellationAsync(), "user cancellation during reconnect is not mistaken for a connection timeout");
var disconnectProbe = new TcpListener(IPAddress.Loopback, 0); disconnectProbe.Start();
int disconnectPort = ((IPEndPoint)disconnectProbe.LocalEndpoint).Port; disconnectProbe.Stop();
using var disconnectListener = new HttpListener(); disconnectListener.Prefixes.Add($"http://127.0.0.1:{disconnectPort}/"); disconnectListener.Start();
using var disconnectTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
var unexpectedClose = new TaskCompletionSource<Exception>(TaskCreationOptions.RunContinuationsAsynchronously);
var disconnectServer = Task.Run(async () =>
{
    var context = await disconnectListener.GetContextAsync().WaitAsync(disconnectTimeout.Token);
    using var socket = (await context.AcceptWebSocketAsync(null)).WebSocket;
    var packet = new byte[65536];
    var config = await socket.ReceiveAsync(new ArraySegment<byte>(packet), disconnectTimeout.Token);
    using var configJson = JsonDocument.Parse(packet.AsMemory(0, config.Count));
    bool validConfig = config.MessageType == WebSocketMessageType.Text
        && configJson.RootElement.GetProperty("sample_rate").GetInt32() == 16000;
    WebSocketReceiveResult audio;
    do { audio = await socket.ReceiveAsync(new ArraySegment<byte>(packet), disconnectTimeout.Token); }
    while (audio.Count == 0 && !disconnectTimeout.IsCancellationRequested);
    if (!validConfig || audio.MessageType != WebSocketMessageType.Binary || audio.Count == 0)
        throw new InvalidDataException("synthetic session did not send configuration and audio");
    await socket.CloseOutputAsync(WebSocketCloseStatus.InternalServerError, "simulated network interruption", disconnectTimeout.Token);
}, disconnectTimeout.Token);
await using (var disconnectSession = new SpeechSession(new Uri($"ws://127.0.0.1:{disconnectPort}/"), captureEnabled: false))
{
    disconnectSession.Failure += error => unexpectedClose.TrySetResult(error);
    await disconnectSession.StartAsync(new Preferences(), "synthetic", 0, null, null);
    var disconnectError = await unexpectedClose.Task.WaitAsync(disconnectTimeout.Token);
    await disconnectServer; disconnectListener.Stop();
    Check(disconnectError is IOException && SpeechRetryPolicy.IsTransient(disconnectError),
        "an unexpected close after audio starts is reported as a transient failure eligible for bounded reconnection");
}
var drift = new ClockDriftController();
int neutralFrames = drift.InputFramesFor(320, ClockDriftController.TargetBufferSeconds);
int highBufferFrames = drift.InputFramesFor(320, 0.30);
var longDrift = new ClockDriftController(); int adjustedFrameTotal = 0;
for (int i = 0; i < 10000; i++) adjustedFrameTotal += longDrift.InputFramesFor(320, 0.30);
var lowBuffer = new ClockDriftController(); int lowBufferFrames = lowBuffer.InputFramesFor(320, 0.01);
Check(neutralFrames == 320 && highBufferFrames > 320 && lowBufferFrames < 320
    && longDrift.LastRatio <= 1.005 && longDrift.LastRatio >= 0.995
    && Math.Abs(adjustedFrameTotal - 320 * 10000 * longDrift.LastRatio) < 4,
    "dual-input drift correction responds to buffer direction, stays bounded and preserves fractional frame adjustments");
float[] mixerFirst = [0.8f, 0.6f, 0.4f, 1.2f];
float[] mixerSecond = [-0.2f, -0.4f, 0.9f, 0f];
float[] mixedFrame = new float[4];
AudioFrameMixer.Mix(mixerFirst, 4, mixerSecond, 2, mixedFrame);
Check(Math.Abs(mixedFrame[0] - 0.3f) < 0.001f && Math.Abs(mixedFrame[1] - 0.1f) < 0.001f
    && Math.Abs(mixedFrame[2] - 0.4f) < 0.001f && mixedFrame[3] == 1f,
    "dual-input mixing averages overlapping samples, keeps a surviving source at full level during underrun, and clamps PCM range");
AudioFrameMixer.Mix(mixerFirst, 0, mixerSecond, 0, mixedFrame);
Check(mixedFrame.All(sample => sample == 0),
    "dual-input mixing emits silence only when neither capture source supplied the output sample");
List<float> ReadNormalized(ISampleProvider input)
{
    ISampleProvider normalized = AudioCapture.ToMono16k(input);
    var samples = new List<float>(); var frame = new float[1024]; int read;
    while ((read = normalized.Read(frame.AsSpan())) > 0) samples.AddRange(frame.AsSpan(0, read).ToArray());
    return samples;
}
var normalized441 = ReadNormalized(new FiniteToneSampleProvider(44100, 2, 1.0, 0));
var normalized480 = ReadNormalized(new FiniteToneSampleProvider(48000, 1, 1.0, 0));
Console.WriteLine($"A03 synthetic resample samples: 44.1 kHz stereo={normalized441.Count}, 48 kHz mono={normalized480.Count}");
Check(normalized441.Count is >= 15900 and <= 16100 && normalized480.Count is >= 15900 and <= 16100,
    "44.1 kHz stereo and 48 kHz mono sources resample to one second of 16 kHz mono within 0.01 seconds");
var delayed480 = ReadNormalized(new FiniteToneSampleProvider(48000, 2, 1.0, 0.15));
int firstDelayedTone = delayed480.FindIndex(sample => sample > 0.1f);
Console.WriteLine($"A03 delayed source: expected onset=2400 samples, measured={firstDelayedTone}");
Check(firstDelayedTone is >= 1600 and <= 3200,
    "a 150 ms later-starting 48 kHz source retains its offset within the 50 ms timeline tolerance");
var boundedPrebuffer = new BoundedAudioPrebuffer(new WaveFormat(16000, 16, 1));
var spanPrebuffer = new BoundedAudioPrebuffer(new WaveFormat(16000, 16, 1));
byte[] spanPcm = [0x00, 0x40, 0x00, 0xC0];
float[] spanSamples = new float[2];
int emptySpanRead = spanPrebuffer.Samples.Read(spanSamples.AsSpan());
spanPrebuffer.AddSamples(spanPcm.AsSpan());
int spanRead = spanPrebuffer.Samples.Read(spanSamples.AsSpan());
Check(emptySpanRead == 0 && spanRead == 2 && Math.Abs(spanSamples[0] - 0.5f) < 0.001f && Math.Abs(spanSamples[1] + 0.5f) < 0.001f,
    "capture prebuffer reports starvation as unavailable frames and copies span-based PCM before the callback returns");
spanPrebuffer.AddSamples(spanPcm.AsSpan());
spanPrebuffer.Clear();
Check(spanPrebuffer.BufferedSeconds == 0 && spanPrebuffer.Samples.Read(spanSamples.AsSpan()) == 0,
    "a prepared audio source drops overlapping startup samples before the live mode switch commits");
int fullPrebufferBytes = (int)(boundedPrebuffer.CapacitySeconds * boundedPrebuffer.WaveFormat.AverageBytesPerSecond);
boundedPrebuffer.AddSamples(new byte[fullPrebufferBytes], 0, fullPrebufferBytes);
bool overflowReported = false;
try { boundedPrebuffer.AddSamples(new byte[640], 0, 640); }
catch (AudioPrebufferOverflowException e) { overflowReported = e.CapacitySeconds == AudioCapture.PrebufferSeconds && e.Message.Contains("避免静默丢失音频"); }
Check(overflowReported && boundedPrebuffer.BufferedSeconds <= AudioCapture.PrebufferSeconds,
    "pre-connect audio storage is capped at 2.5 seconds and overrun becomes an explicit failure");
(double Min, double Max) SimulateHour(double clockPpm)
{
    var controller = new ClockDriftController(); double bufferedFrames = 16000 * ClockDriftController.TargetBufferSeconds;
    double minimum = bufferedFrames, maximum = bufferedFrames;
    for (int i = 0; i < 180000; i++)
    {
        bufferedFrames += 320 * (1 + clockPpm / 1_000_000);
        bufferedFrames -= controller.InputFramesFor(320, bufferedFrames / 16000.0);
        minimum = Math.Min(minimum, bufferedFrames); maximum = Math.Max(maximum, bufferedFrames);
    }
    return (minimum, maximum);
}
var fastClock = SimulateHour(500); var slowClock = SimulateHour(-500);
Check(fastClock.Min > 0 && fastClock.Max < 16000 && slowClock.Min > 0 && slowClock.Max < 16000,
    "one-hour simulated independent capture clocks at plus/minus 500 ppm keep queues inside the one-second safety bound");
var fixedRoute = new AudioDeviceRoute(DataFlow.Render, "fixed-speaker", "fixed-speaker", "USB Speaker");
var defaultRoute = new AudioDeviceRoute(DataFlow.Capture, null, "old-default-mic", "Built-in Microphone");
Check(AudioEndpointChangePolicy.FindUnavailableRoute([fixedRoute], "fixed-speaker", DeviceState.Unplugged)?.Name == "USB Speaker"
    && AudioEndpointChangePolicy.FindUnavailableRoute([fixedRoute], "fixed-speaker", DeviceState.Active) is null
    && !AudioEndpointChangePolicy.ShouldRefreshDefaultAfterUnavailable(fixedRoute)
    && AudioEndpointChangePolicy.ShouldRefreshDefaultAfterUnavailable(defaultRoute)
    && !AudioEndpointChangePolicy.IsFollowingDefault(fixedRoute, DataFlow.Render, Role.Multimedia, "new-default-speaker")
    && AudioEndpointChangePolicy.IsFollowingDefault(defaultRoute, DataFlow.Capture, Role.Multimedia, "new-default-mic")
    && !AudioEndpointChangePolicy.IsFollowingDefault(defaultRoute, DataFlow.Capture, Role.Communications, "new-default-mic"),
    "audio endpoint policy follows multimedia defaults and reports loss of the explicitly selected endpoint");
Check(AudioEndpointChangePolicy.FindUnavailableRoute([defaultRoute], "old-default-mic", DeviceState.Unplugged) == defaultRoute
    && AudioEndpointChangePolicy.FindUnavailableRoute([fixedRoute], "another-device", DeviceState.Unplugged) is null
    && !AudioEndpointChangePolicy.IsFollowingDefault(defaultRoute, DataFlow.Render, Role.Multimedia, "new-default-mic")
    && !AudioEndpointChangePolicy.IsFollowingDefault(defaultRoute, DataFlow.Capture, Role.Console, "new-default-mic")
    && !AudioEndpointChangePolicy.IsFollowingDefault(defaultRoute, DataFlow.Capture, Role.Multimedia, "old-default-mic"),
    "endpoint notifications ignore active/unknown devices, unrelated flows and roles, and unchanged default IDs");
var accessDeniedCom = new COMException("synthetic access denied", unchecked((int)0x80070005));
var wrappedAccessDenied = new AudioCaptureFailureException("audio capture failed", accessDeniedCom);
Check(AudioCaptureErrorPresentation.IsAccessDenied(new UnauthorizedAccessException("synthetic"))
    && AudioCaptureErrorPresentation.IsAccessDenied(wrappedAccessDenied)
    && AudioCaptureErrorPresentation.GetUserMessage(wrappedAccessDenied, microphoneRequested: true).Contains("让桌面应用访问麦克风", StringComparison.Ordinal)
    && AudioCaptureErrorPresentation.GetUserMessage(wrappedAccessDenied, microphoneRequested: false).Contains("音频设备", StringComparison.Ordinal)
    && AudioCaptureErrorPresentation.GetUserMessage(new IOException("synthetic device error"), microphoneRequested: true) == "synthetic device error",
    "microphone access denial gives Windows privacy guidance while unrelated capture errors retain their original message");
var sleepState = new SleepRecoveryState();
bool endForSleep = sleepState.BeginSleep(recordingIntended: true);
bool scheduleWake = sleepState.BeginWake(); bool beginWakeRecovery = sleepState.BeginRecovery(); sleepState.FinishRecovery();
var stoppedBeforeSleep = new SleepRecoveryState();
stoppedBeforeSleep.BeginSleep(recordingIntended: false); stoppedBeforeSleep.CancelByUser();
Check(endForSleep && scheduleWake && beginWakeRecovery && !sleepState.IsRecovering
    && !stoppedBeforeSleep.BeginWake(),
    "sleep recovery restores only a recording that was intended before sleep and can be cancelled by the user");
int recoveredCycles = 0;
for (int cycle = 0; cycle < 10; cycle++)
{
    var repeatedSleep = new SleepRecoveryState();
    bool ended = repeatedSleep.BeginSleep(recordingIntended: true);
    bool queued = repeatedSleep.BeginWake();
    bool recovering = repeatedSleep.BeginRecovery();
    repeatedSleep.FinishRecovery();
    if (ended && queued && recovering && !repeatedSleep.IsRecovering && !repeatedSleep.HasPendingWakeRecovery) recoveredCycles++;
}
var userStoppedAfterWake = new SleepRecoveryState();
userStoppedAfterWake.BeginSleep(recordingIntended: true); userStoppedAfterWake.BeginWake(); userStoppedAfterWake.CancelByUser();
Check(recoveredCycles == 10 && !userStoppedAfterWake.BeginRecovery(),
    "ten simulated sleep/wake cycles recover an intended session, while a user stop after wake cancels pending recovery");
var switchPortProbe = new TcpListener(IPAddress.Loopback, 0); switchPortProbe.Start();
int switchPort = ((IPEndPoint)switchPortProbe.LocalEndpoint).Port; switchPortProbe.Stop();
using (var switchListener = new HttpListener())
using (var switchTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(12)))
{
    switchListener.Prefixes.Add($"http://127.0.0.1:{switchPort}/"); switchListener.Start();
    int switchConnections = 0, switchAudioFrames = 0, switchConfigValid = 0;
    var switchServer = Task.Run(async () =>
    {
        var context = await switchListener.GetContextAsync().WaitAsync(switchTimeout.Token);
        using var socket = (await context.AcceptWebSocketAsync(null)).WebSocket;
        Interlocked.Increment(ref switchConnections);
        var packet = new byte[4096];
        var configResult = await socket.ReceiveAsync(new ArraySegment<byte>(packet), switchTimeout.Token);
        using var config = JsonDocument.Parse(packet.AsMemory(0, configResult.Count));
        if (configResult.MessageType == WebSocketMessageType.Text
            && context.Request.Headers["Authorization"] == "Bearer synthetic"
            && config.RootElement.GetProperty("sample_rate").GetInt32() == 16000)
            Volatile.Write(ref switchConfigValid, 1);
        while (true)
        {
            var result = await socket.ReceiveAsync(new ArraySegment<byte>(packet), switchTimeout.Token);
            if (result.Count == 0) break;
            Interlocked.Increment(ref switchAudioFrames);
        }
        byte[] finished = Encoding.UTF8.GetBytes("""{"finished":true}""");
        await socket.SendAsync(finished.AsMemory(), WebSocketMessageType.Text, true, switchTimeout.Token);
    }, switchTimeout.Token);
    var syntheticCapture = new SyntheticSpeechSessionCapture { FailNextPrepare = true, FollowCaptureDefault = true };
    syntheticCapture.QueueMicrophoneFrameReadiness(false, true);
    int switchSessionFailureCount = 0;
    var switchStatuses = new ConcurrentQueue<string>();
    await using (var switchSession = new SpeechSession(new Uri($"ws://127.0.0.1:{switchPort}/"), captureEnabled: true, capture: syntheticCapture))
    {
        switchSession.Failure += _ => Interlocked.Increment(ref switchSessionFailureCount);
        switchSession.Status += message => switchStatuses.Enqueue(message);
        await switchSession.StartAsync(new Preferences(), "synthetic", 1, null, null, switchTimeout.Token);
        bool microphoneStartupRetried = syntheticCapture.MicrophoneReadinessChecks == 2
            && syntheticCapture.MicrophoneConversionChecks == 1
            && syntheticCapture.SuccessfulRestarts == 1 && syntheticCapture.IsRunning;
        int framesBeforeRejectedPrepare = syntheticCapture.FramesReadWhileRunning;
        AudioDeviceSwitchException? switchError = null;
        try { await switchSession.SwitchDevicesAsync("new-speaker", null, newMode: 0); }
        catch (AudioDeviceSwitchException error) { switchError = error; }
        DateTime frameDeadline = DateTime.UtcNow.AddSeconds(2);
        while (syntheticCapture.FramesReadWhileRunning <= framesBeforeRejectedPrepare && DateTime.UtcNow < frameDeadline)
            await Task.Delay(20, switchTimeout.Token);
        bool failedPrepareKeptOldCapture = switchError?.CaptureRestored == true
            && syntheticCapture.IsRunning && syntheticCapture.Mode == 1
            && syntheticCapture.FramesReadWhileRunning > framesBeforeRejectedPrepare && syntheticCapture.StopInputsCount == 0
            && Volatile.Read(ref switchSessionFailureCount) == 0;
        syntheticCapture.QueueMicrophoneFrameReadiness(false, true);
        syntheticCapture.NotifyDefaultDeviceChanged(DataFlow.Capture);
        DateTime deviceChangeDeadline = DateTime.UtcNow.AddSeconds(2);
        while (syntheticCapture.SuccessfulRestarts < 2 && DateTime.UtcNow < deviceChangeDeadline)
            await Task.Delay(20, switchTimeout.Token);
        bool defaultDeviceChangeContinued = syntheticCapture.SuccessfulRestarts == 2
            && syntheticCapture.IsRunning && syntheticCapture.Mode == 1
            && syntheticCapture.ActiveInputId == "synthetic-capture-default-2"
            && Volatile.Read(ref switchSessionFailureCount) == 0
            && switchStatuses.Any(message => message.Contains("已跟随系统默认音频设备切换", StringComparison.Ordinal));
        await switchSession.StopAsync();
        await switchServer.WaitAsync(switchTimeout.Token); switchListener.Stop();
        Check(microphoneStartupRetried && Volatile.Read(ref switchConfigValid) == 1
            && Volatile.Read(ref switchConnections) == 1 && Volatile.Read(ref switchAudioFrames) > 0,
            "synthetic microphone startup retries after a missing first frame while keeping one recognition WebSocket");
        Check(failedPrepareKeptOldCapture && Volatile.Read(ref switchConfigValid) == 1
            && Volatile.Read(ref switchConnections) == 1 && Volatile.Read(ref switchAudioFrames) > 0,
            "synthetic live audio switch preparation failure keeps the old input running and sends audio on the same recognition WebSocket");
        Check(defaultDeviceChangeContinued && Volatile.Read(ref switchConnections) == 1
            && Volatile.Read(ref switchAudioFrames) > 0,
            "synthetic default microphone change restarts capture on the new endpoint while preserving the recognition WebSocket and audio flow");
        Check(syntheticCapture.MicrophoneReadinessChecks == 4 && defaultDeviceChangeContinued,
            "synthetic default microphone switch retries a candidate with no first frame before replacing the active capture");
        Check(syntheticCapture.MicrophoneConversionChecks == 2 && defaultDeviceChangeContinued,
            "synthetic microphone startup and default-device switch both wait for converted PCM before reporting readiness");
    }
}
var conversionPortProbe = new TcpListener(IPAddress.Loopback, 0); conversionPortProbe.Start();
int conversionPort = ((IPEndPoint)conversionPortProbe.LocalEndpoint).Port; conversionPortProbe.Stop();
using (var conversionListener = new HttpListener())
using (var conversionTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(10)))
{
    conversionListener.Prefixes.Add($"http://127.0.0.1:{conversionPort}/"); conversionListener.Start();
    int conversionConnections = 0, conversionConfigValid = 0;
    var conversionServer = Task.Run(async () =>
    {
        try
        {
            var context = await conversionListener.GetContextAsync().WaitAsync(conversionTimeout.Token);
            using var socket = (await context.AcceptWebSocketAsync(null)).WebSocket;
            Interlocked.Increment(ref conversionConnections);
            var packet = new byte[4096];
            var configResult = await socket.ReceiveAsync(new ArraySegment<byte>(packet), conversionTimeout.Token);
            using var config = JsonDocument.Parse(packet.AsMemory(0, configResult.Count));
            if (configResult.MessageType == WebSocketMessageType.Text
                && context.Request.Headers["Authorization"] == "Bearer synthetic"
                && config.RootElement.GetProperty("sample_rate").GetInt32() == 16000)
                Volatile.Write(ref conversionConfigValid, 1);
            while (true)
            {
                var result = await socket.ReceiveAsync(new ArraySegment<byte>(packet), conversionTimeout.Token);
                if (result.MessageType == WebSocketMessageType.Close) break;
            }
        }
        catch (WebSocketException) { }
    }, conversionTimeout.Token);
    var conversionCapture = new SyntheticSpeechSessionCapture();
    conversionCapture.QueueMicrophoneConversionReadiness(false);
    AudioCaptureFailureException? conversionFailure = null;
    await using (var conversionSession = new SpeechSession(
        new Uri($"ws://127.0.0.1:{conversionPort}/"), captureEnabled: true, capture: conversionCapture))
    {
        try { await conversionSession.StartAsync(new Preferences(), "synthetic", 1, null, null, conversionTimeout.Token); }
        catch (AudioCaptureFailureException error) { conversionFailure = error; }
    }
    await conversionServer.WaitAsync(conversionTimeout.Token); conversionListener.Stop();
    Check(conversionFailure?.Message.Contains("音频转换失败", StringComparison.Ordinal) == true
        && conversionCapture.MicrophoneReadinessChecks == 1 && conversionCapture.MicrophoneConversionChecks == 1
        && Volatile.Read(ref conversionConnections) == 1 && Volatile.Read(ref conversionConfigValid) == 1,
        "synthetic raw microphone input without converted PCM fails explicitly without a duplicate recognition connection");
}
if (args.Contains("--audio"))
{
    Console.WriteLine($"Devices: render={AudioCapture.Devices(DataFlow.Render).Count}, capture={AudioCapture.Devices(DataFlow.Capture).Count}");
    foreach (int mode in new[] { 0, 1, 2 })
    {
        using var audio = new AudioCapture(); string? failure = null; audio.Failed += e => failure = e.Message;
        audio.Start(mode, null, null); await Task.Delay(120);
        int bytes = 0;
        for (int i = 0; i < 50; i++) { bytes += audio.ReadFrame(out _).Length; await Task.Delay(20); }
        Check(failure is null && bytes == 32000, $"audio mode {mode}: one second PCM frame contract, no capture failure");
        string? outputId = mode is 0 or 2 ? AudioCapture.Devices(DataFlow.Render).First().Id : null;
        string? inputId = mode is 1 or 2 ? AudioCapture.Devices(DataFlow.Capture).First().Id : null;
        audio.Restart(mode, outputId, inputId); await Task.Delay(120); int switchedBytes = 0;
        for (int i = 0; i < 5; i++) { switchedBytes += audio.ReadFrame(out _).Length; await Task.Delay(20); }
        Check(failure is null && switchedBytes == 3200, $"audio mode {mode}: hot device reinitialization keeps PCM capture active");
    }
    var portProbe = new TcpListener(IPAddress.Loopback, 0); portProbe.Start();
    int port = ((IPEndPoint)portProbe.LocalEndpoint).Port; portProbe.Stop();
    using var listener = new HttpListener(); listener.Prefixes.Add($"http://127.0.0.1:{port}/"); listener.Start();
    using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
    int receivedAudio = 0; bool validConfig = false, receivedFinal = false;
    var provisionalApplied = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    var switchedSegment = new Segment(); var switchedAssembler = new TokenAssembler(switchedSegment, _ => { });
    var server = Task.Run(async () =>
    {
        var context = await listener.GetContextAsync().WaitAsync(timeout.Token);
        await Task.Delay(TimeSpan.FromSeconds(1), timeout.Token);
        using var socket = (await context.AcceptWebSocketAsync(null)).WebSocket;
        var packet = new byte[65536];
        var configResult = await socket.ReceiveAsync(new ArraySegment<byte>(packet), timeout.Token);
        using var config = JsonDocument.Parse(packet.AsMemory(0, configResult.Count));
        validConfig = context.Request.Headers["Authorization"] == "Bearer synthetic"
            && configResult.MessageType == WebSocketMessageType.Text
            && config.RootElement.GetProperty("sample_rate").GetInt32() == 16000
            && !config.RootElement.TryGetProperty("api_key", out _);
        byte[] provisional = Encoding.UTF8.GetBytes("""{"tokens":[{"text":"Before","is_final":false,"start_ms":0,"end_ms":250}]}""");
        await socket.SendAsync(provisional.AsMemory(), WebSocketMessageType.Text, true, timeout.Token);
        while (true)
        {
            var result = await socket.ReceiveAsync(new ArraySegment<byte>(packet), timeout.Token);
            if (result.Count == 0) break;
            receivedAudio += result.Count;
        }
        byte[] final = Encoding.UTF8.GetBytes("""{"tokens":[{"text":"Before and after.","is_final":true,"start_ms":0,"end_ms":500}],"finished":true}""");
        await socket.SendAsync(final.AsMemory(), WebSocketMessageType.Text, true, timeout.Token);
    }, timeout.Token);
    await using (var session = new SpeechSession(new Uri($"ws://127.0.0.1:{port}/"), captureEnabled: true))
    {
        session.Message += m =>
        {
            switchedAssembler.Apply(m);
            if (m.TryGetProperty("tokens", out var tokens) && tokens.EnumerateArray().Any(t => t.TryGetProperty("is_final", out var finalToken) && !finalToken.GetBoolean())) provisionalApplied.TrySetResult();
            receivedFinal = m.TryGetProperty("finished", out var f) && f.GetBoolean();
        };
        await session.StartAsync(new Preferences(), "synthetic", 1, null, null);
        double bufferedAtReady = session.BufferedAudioSeconds;
        Check(bufferedAtReady >= 0.7, $"capture buffers audio while the WebSocket connects ({bufferedAtReady:0.00} seconds available)");
        await Task.Delay(TimeSpan.FromMilliseconds(200));
        double bufferedAfterCatchUp = session.BufferedAudioSeconds;
        Console.WriteLine($"A05 catch-up buffer: at connection={bufferedAtReady:0.00}s, after 200 ms={bufferedAfterCatchUp:0.00}s");
        Check(bufferedAfterCatchUp < bufferedAtReady - 0.25, "the sender catches up pre-connect audio instead of preserving a permanent subtitle delay");
        bool restoredInput = false;
        try { await session.SwitchDevicesAsync(null, "missing-device-id"); }
        catch (AudioDeviceSwitchException e) { restoredInput = e.CaptureRestored; }
        Check(restoredInput, "invalid live device switch restores capture and keeps the recognition session open");
        await provisionalApplied.Task.WaitAsync(timeout.Token);
        string[] inputIds = AudioCapture.Devices(DataFlow.Capture).Select(d => d.Id).ToArray();
        int successfulSwitches = 0;
        for (int i = 0; i < 20; i++)
        {
            await session.SwitchDevicesAsync(null, inputIds[i % inputIds.Length]);
            successfulSwitches++;
        }
        Check(successfulSwitches == 20, "live microphone capture switches among active inputs 20 times in one recognition session");
        await Task.Delay(450); await session.StopAsync();
    }
    await server; listener.Stop();
    Check(validConfig && receivedAudio > 0 && receivedFinal && switchedSegment.Entries.Count == 1 && switchedSegment.Entries[0].English == "Before and after.", "local WebSocket: 20 input switches preserve the session and subtitle while final PCM and end marker complete");

    var noFinalPortProbe = new TcpListener(IPAddress.Loopback, 0); noFinalPortProbe.Start();
    int noFinalPort = ((IPEndPoint)noFinalPortProbe.LocalEndpoint).Port; noFinalPortProbe.Stop();
    using var noFinalListener = new HttpListener(); noFinalListener.Prefixes.Add($"http://127.0.0.1:{noFinalPort}/"); noFinalListener.Start();
    using var noFinalTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
    var endMarkerReceived = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    var noFinalServer = Task.Run(async () =>
    {
        var context = await noFinalListener.GetContextAsync().WaitAsync(noFinalTimeout.Token);
        using var socket = (await context.AcceptWebSocketAsync(null)).WebSocket;
        var packet = new byte[65536];
        await socket.ReceiveAsync(new ArraySegment<byte>(packet), noFinalTimeout.Token); // config
        while (true)
        {
            var result = await socket.ReceiveAsync(new ArraySegment<byte>(packet), noFinalTimeout.Token);
            if (result.Count == 0) { endMarkerReceived.TrySetResult(); break; }
        }
        try { await socket.ReceiveAsync(new ArraySegment<byte>(packet), noFinalTimeout.Token); } catch (WebSocketException) { }
    }, noFinalTimeout.Token);
    Exception? finalTimeout = null;
    await using (var noFinalSession = new SpeechSession(new Uri($"ws://127.0.0.1:{noFinalPort}/")))
    {
        await noFinalSession.StartAsync(new Preferences(), "synthetic", 0, null, null);
        try { await noFinalSession.StopAsync(); } catch (Exception e) { finalTimeout = e; }
    }
    await endMarkerReceived.Task.WaitAsync(noFinalTimeout.Token);
    noFinalListener.Stop(); await noFinalServer;
    Check(finalTimeout is TimeoutException && finalTimeout.Message.Contains("最后识别结果超时") && endMarkerReceived.Task.IsCompleted,
        "stop reports an explicit incomplete-result timeout when the service receives the end marker but sends no final response");

    var delayedPortProbe = new TcpListener(IPAddress.Loopback, 0); delayedPortProbe.Start();
    int delayedPort = ((IPEndPoint)delayedPortProbe.LocalEndpoint).Port; delayedPortProbe.Stop();
    using var delayedListener = new HttpListener(); delayedListener.Prefixes.Add($"http://127.0.0.1:{delayedPort}/"); delayedListener.Start();
    using var delayedTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(12));
    var delayedServer = Task.Run(async () =>
    {
        var context = await delayedListener.GetContextAsync().WaitAsync(delayedTimeout.Token);
        await Task.Delay(TimeSpan.FromSeconds(3.2), delayedTimeout.Token);
        try { context.Response.StatusCode = 503; context.Response.Close(); }
        catch (HttpListenerException) { }
    }, delayedTimeout.Token);
    Exception? overflowFailure = null;
    await using (var slowSession = new SpeechSession(new Uri($"ws://127.0.0.1:{delayedPort}/"), captureEnabled: true))
    {
        try { await slowSession.StartAsync(new Preferences(), "synthetic", 1, null, null); }
        catch (Exception e) { overflowFailure = e; }
    }
    await delayedServer; delayedListener.Stop();
    Check(overflowFailure is AudioCaptureFailureException && overflowFailure.Message.Contains("2.5 秒上限"),
        "a WebSocket handshake slower than the 2.5-second prebuffer fails visibly instead of silently dropping captured audio");
}
Console.WriteLine($"Completed {passed} checks. No cloud calls; no audio was saved.");

sealed class FiniteToneSampleProvider(int sampleRate, int channels, double durationSeconds, double leadingSilenceSeconds) : ISampleProvider
{
    private readonly int totalSamples = (int)Math.Round(sampleRate * channels * durationSeconds);
    private int position;
    public WaveFormat WaveFormat { get; } = WaveFormat.CreateIeeeFloatWaveFormat(sampleRate, channels);
    public int Read(float[] buffer, int offset, int count) => Read(buffer.AsSpan(offset, count));
    public int Read(Span<float> buffer)
    {
        int available = Math.Min(buffer.Length, totalSamples - position);
        for (int i = 0; i < available; i++)
        {
            int sourceFrame = (position + i) / channels;
            buffer[i] = sourceFrame / (double)sampleRate < leadingSilenceSeconds ? 0 : 0.25f;
        }
        position += available;
        return available;
    }
}

sealed class SyntheticSpeechSessionCapture : ISpeechSessionCapture
{
    private readonly Queue<bool> microphoneFrameReadiness = new();
    private readonly Queue<bool> microphoneConversionReadiness = new();
    private readonly object readinessGate = new();
    private int defaultCaptureEndpointVersion = 1;
    private int tailFrames;
    private bool readingAfterRestore;
    private bool restorePending;
    private Action<DataFlow>? defaultDeviceChanged;
    public event Action<Exception>? Failed { add { } remove { } }
    public event Action<DataFlow>? DefaultDeviceChanged { add => defaultDeviceChanged += value; remove => defaultDeviceChanged -= value; }
    public string? ActiveOutputId { get; private set; }
    public string? ActiveInputId { get; private set; }
    public double BufferedSeconds => 0;
    public int TailFrames => tailFrames;
    public bool IsRunning { get; private set; }
    public int Mode { get; private set; }
    private int framesReadAfterRestore;
    public int FramesReadAfterRestore => Volatile.Read(ref framesReadAfterRestore);
    private int framesReadWhileRunning;
    public int FramesReadWhileRunning => Volatile.Read(ref framesReadWhileRunning);
    private int successfulRestarts;
    public int SuccessfulRestarts => Volatile.Read(ref successfulRestarts);
    private int microphoneReadinessChecks;
    public int MicrophoneReadinessChecks => Volatile.Read(ref microphoneReadinessChecks);
    private int microphoneConversionChecks;
    public int MicrophoneConversionChecks => Volatile.Read(ref microphoneConversionChecks);
    public bool FailNextPrepare { get; set; }
    public int StopInputsCount { get; private set; }
    private (int Mode, string? OutputId, string? InputId)? prepared;
    public bool FollowCaptureDefault { get; set; }
    public bool IsFollowingDefault(DataFlow flow) => FollowCaptureDefault && flow == DataFlow.Capture;
    public void QueueMicrophoneFrameReadiness(params bool[] results)
    {
        lock (readinessGate) foreach (bool result in results) microphoneFrameReadiness.Enqueue(result);
    }
    public void QueueMicrophoneConversionReadiness(params bool[] results)
    {
        lock (readinessGate) foreach (bool result in results) microphoneConversionReadiness.Enqueue(result);
    }
    public Task<bool> WaitForMicrophoneInputFrameAsync(bool prepared, TimeSpan timeout, CancellationToken cancellationToken)
    {
        Interlocked.Increment(ref microphoneReadinessChecks);
        cancellationToken.ThrowIfCancellationRequested();
        lock (readinessGate)
            return Task.FromResult(microphoneFrameReadiness.Count == 0 || microphoneFrameReadiness.Dequeue());
    }
    public Task<bool> WaitForMicrophoneConvertedFrameAsync(bool prepared, TimeSpan timeout, CancellationToken cancellationToken)
    {
        Interlocked.Increment(ref microphoneConversionChecks);
        cancellationToken.ThrowIfCancellationRequested();
        lock (readinessGate)
            return Task.FromResult(microphoneConversionReadiness.Count == 0 || microphoneConversionReadiness.Dequeue());
    }
    public void NotifyDefaultDeviceChanged(DataFlow flow)
    {
        if (flow == DataFlow.Capture) Interlocked.Increment(ref defaultCaptureEndpointVersion);
        defaultDeviceChanged?.Invoke(flow);
    }
    public void Start(int mode, string? outputId, string? inputId)
    {
        Mode = mode; ActiveOutputId = mode is 0 or 2 ? outputId ?? "synthetic-render-default" : null;
        ActiveInputId = mode is 1 or 2 ? inputId ?? $"synthetic-capture-default-{Volatile.Read(ref defaultCaptureEndpointVersion)}" : null;
        IsRunning = true; readingAfterRestore = restorePending; restorePending = false;
    }
    public void Restart(int mode, string? outputId, string? inputId)
    {
        Interlocked.Increment(ref successfulRestarts);
        Start(mode, outputId, inputId);
    }
    public void PrepareRestart(int mode, string? outputId, string? inputId)
    {
        if (FailNextPrepare)
        {
            FailNextPrepare = false;
            throw new IOException("synthetic endpoint rejected startup");
        }
        prepared = (mode, outputId, inputId);
    }
    public void CommitPreparedRestart()
    {
        if (prepared is not { } next) throw new InvalidOperationException("no prepared restart");
        prepared = null;
        Interlocked.Increment(ref successfulRestarts);
        Start(next.Mode, next.OutputId, next.InputId);
    }
    public void AbortPreparedRestart() => prepared = null;
    public byte[] ReadFrame(out double level)
    {
        level = 0.1;
        if (tailFrames > 0) tailFrames--;
        else if (IsRunning)
        {
            Interlocked.Increment(ref framesReadWhileRunning);
            if (readingAfterRestore) Interlocked.Increment(ref framesReadAfterRestore);
        }
        return new byte[640];
    }
    public void StopInputs() { StopInputsCount++; IsRunning = false; tailFrames = 1; }
    public void Dispose() { IsRunning = false; }
}
