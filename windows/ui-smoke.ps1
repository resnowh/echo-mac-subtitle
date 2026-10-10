param([Parameter(Mandatory)][int]$AppPid, [Parameter(Mandatory)][string]$ResultsPath)
$ErrorActionPreference = 'Stop'
$results = [System.Collections.Generic.List[object]]::new()
$resultsDirectory = Split-Path -Parent $ResultsPath
$evidenceDirectory = Join-Path $resultsDirectory 'screenshots'
New-Item -ItemType Directory -Path $resultsDirectory, $evidenceDirectory -Force | Out-Null

function Invoke-Ui([string[]]$Arguments) {
    $output = & winapp ui @Arguments 2>&1
    if ($LASTEXITCODE -ne 0) { throw ($output -join "`n") }
    return ($output -join "`n")
}

function Search-Ui([string]$Selector) {
    $output = & winapp ui search $Selector -a "$AppPid" --json 2>&1
    $json = $output -join "`n"
    try { return ($json | ConvertFrom-Json) }
    catch {
        if ($LASTEXITCODE -ne 0) { throw $json }
        throw
    }
}

function Select-Theme([string]$Theme) {
    Invoke-Ui @('invoke', 'ThemeChoice', '-a', "$AppPid") | Out-Null
    $tree = Invoke-Ui @('inspect', 'ThemeChoice', '-a', "$AppPid", '--interactive', '--json') | ConvertFrom-Json
    $combo = @($tree.windows | ForEach-Object { $_.elements } | Where-Object { $_.automationId -eq 'ThemeChoice' } | Select-Object -First 1)
    $item = @($combo[0].children | Where-Object { $_.name -eq $Theme -and $_.selector } | Select-Object -First 1)
    if (-not $item) { throw "Theme option '$Theme' was not exposed by UI Automation." }
    Invoke-Ui @('invoke', $item[0].selector, '-a', "$AppPid") | Out-Null
}

function Capture-UiWindow([string]$FileName) {
    $path = Join-Path $evidenceDirectory $FileName
    Invoke-Ui @('screenshot', '-a', "$AppPid", '-o', $path) | Out-Null
    if (-not (Test-Path -LiteralPath $path)) { throw "Screenshot was not created: $path" }
    $path
}

function Check([string]$Name, [scriptblock]$Action) {
    try {
        $detail = & $Action 2>&1
        $results.Add(@{ name = $Name; status = 'PASS'; detail = ($detail -join "`n") })
    }
    catch {
        $results.Add(@{ name = $Name; status = 'FAIL'; detail = "$_" })
    }
}

Check 'Recording page and idle recording actions are available' {
    Invoke-Ui @('wait-for', 'StartRecording', '-a', "$AppPid", '-t', '3000') | Out-Null
    Invoke-Ui @('wait-for', 'StopRecording', '-a', "$AppPid", '--gone', '-t', '3000') | Out-Null
    'Start is visible; Stop is hidden while idle.'
}

Check 'Audio source exposes the persisted current mode' {
    $element = (Search-Ui 'InputMode').matches | Select-Object -First 1
    if (-not $element -or $element.type -ne 'Button' -or $element.name -notmatch '^音频来源：.+') {
        throw 'Audio source button is missing its current accessible mode name.'
    }
    $element.name
}

Check 'Language buttons expose current selections instead of ComboBox values' {
    $source = (Search-Ui 'SourceLanguageChoice').matches | Select-Object -First 1
    $target = (Search-Ui 'TargetLanguageChoice').matches | Select-Object -First 1
    if (-not $source -or $source.type -ne 'Button' -or $source.name -notmatch '^识别语言：.+') {
        throw 'Source language button or its selected language name is missing.'
    }
    if (-not $target -or $target.type -ne 'Button' -or $target.name -notmatch '^翻译目标：.+') {
        throw 'Target language button or its selected language name is missing.'
    }
    "Source=$($source.name); target=$($target.name)"
}

Check 'Source language flyout selects automatic recognition' {
    $element = (Search-Ui 'SourceLanguageChoice').matches | Select-Object -First 1
    if (-not $element) { throw 'Source language button is missing.' }
    if ($element.name -notmatch '自动识别') {
        Invoke-Ui @('invoke', 'SourceLanguageChoice', '-a', "$AppPid") | Out-Null
        Invoke-Ui @('wait-for', 'SourceLanguage_none', '-a', "$AppPid", '-t', '3000') | Out-Null
        Invoke-Ui @('invoke', 'SourceLanguage_none', '-a', "$AppPid") | Out-Null
        $element = (Search-Ui 'SourceLanguageChoice').matches | Select-Object -First 1
        if (-not $element -or $element.name -notmatch '自动识别') { throw 'Selecting automatic recognition did not update the language button.' }
    }
    $element.name
}

Check 'Target language flyout selects no translation' {
    $element = (Search-Ui 'TargetLanguageChoice').matches | Select-Object -First 1
    if (-not $element) { throw 'Target language button is missing.' }
    if ($element.name -notmatch '不翻译') {
        Invoke-Ui @('invoke', 'TargetLanguageChoice', '-a', "$AppPid") | Out-Null
        Invoke-Ui @('wait-for', 'TargetLanguage_none', '-a', "$AppPid", '-t', '3000') | Out-Null
        Invoke-Ui @('invoke', 'TargetLanguage_none', '-a', "$AppPid") | Out-Null
        $element = (Search-Ui 'TargetLanguageChoice').matches | Select-Object -First 1
        if (-not $element -or $element.name -notmatch '不翻译') { throw 'Selecting no translation did not update the language button.' }
    }
    $element.name
}

Check 'No new-content action appears when already at latest' {
    $searchResult = Search-Ui 'ScrollToLatest'
    if ($searchResult.matchCount -gt 0) { throw 'Scroll-to-latest action is visible without new content.' }
    "Visible=False; matches=$($searchResult.matchCount)"
}

Check 'Settings pages and theme control are reachable' {
    Invoke-Ui @('invoke', 'SettingsTab', '-a', "$AppPid") | Out-Null
    try {
        Invoke-Ui @('invoke', 'SettingsRecognition', '-a', "$AppPid") | Out-Null
        Invoke-Ui @('wait-for', 'AutomaticSourceMode', '-a', "$AppPid", '-t', '3000') | Out-Null
        Invoke-Ui @('invoke', 'SettingsSegmentation', '-a', "$AppPid") | Out-Null
        Invoke-Ui @('wait-for', 'EndpointDelay', '-a', "$AppPid", '-t', '3000') | Out-Null
        Invoke-Ui @('invoke', 'SettingsServices', '-a', "$AppPid") | Out-Null
        Invoke-Ui @('wait-for', 'SonioxKey', '-a', "$AppPid", '-t', '3000') | Out-Null
        Invoke-Ui @('invoke', 'SettingsGeneral', '-a', "$AppPid") | Out-Null
        Invoke-Ui @('wait-for', 'ThemeChoice', '-a', "$AppPid", '-t', '3000') | Out-Null
        Select-Theme '浅色'
        Invoke-Ui @('wait-for', 'ThemeChoice', '-a', "$AppPid", '--value', '浅色', '-t', '3000') | Out-Null
        Capture-UiWindow 'settings-general-light.png' | Out-Null
        Select-Theme '深色'
        Invoke-Ui @('wait-for', 'ThemeChoice', '-a', "$AppPid", '--value', '深色', '-t', '3000') | Out-Null
    }
    finally { Invoke-Ui @('invoke', 'RecordingTab', '-a', "$AppPid") | Out-Null }
    'Recognition, segmentation, AI services and appearance controls are reachable.'
}

Check 'Archive picker, export action and transcript are available' {
    Invoke-Ui @('invoke', 'ArchiveMenu', '-a', "$AppPid") | Out-Null
    $archive = @(Search-Ui '课堂演示 - 合成字幕').matches | Where-Object { $_.type -eq 'ListItem' } | Select-Object -First 1
    if (-not $archive) { throw 'Synthetic populated archive is missing from the archive picker.' }
    Invoke-Ui @('invoke', $archive.selector, '-a', "$AppPid") | Out-Null
    Invoke-Ui @('wait-for', 'TranscriptList', '-a', "$AppPid", '-t', '3000') | Out-Null
    $subtitle = @(Search-Ui 'EditSubtitle_').matches | Where-Object { -not $_.isOffscreen } | Select-Object -First 1
    if (-not $subtitle) { throw 'Populated archive does not expose a visible subtitle row.' }
    Invoke-Ui @('wait-for', 'ExportMenu', '-a', "$AppPid", '-t', '3000') | Out-Null
    Invoke-Ui @('invoke', 'ExportMenu', '-a', "$AppPid") | Out-Null
    'Archive picker, export action and transcript list are reachable.'
}

Check 'Correction editor opens and remains dismissible without editing' {
    $edit = @(Search-Ui 'EditSubtitle_').matches | Where-Object { -not $_.isOffscreen } | Select-Object -First 1
    if (-not $edit) { throw 'No visible subtitle correction action was found in the synthetic transcript.' }
    Invoke-Ui @('invoke', $edit.automationId, '-a', "$AppPid") | Out-Null
    Invoke-Ui @('wait-for', 'CorrectionSource', '-a', "$AppPid", '-t', '3000') | Out-Null
    Capture-UiWindow 'correction-editor-dark.png' | Out-Null
    Invoke-Ui @('invoke', 'CloseButton', '-a', "$AppPid") | Out-Null
    Invoke-Ui @('wait-for', 'CorrectionSource', '-a', "$AppPid", '--gone', '-t', '3000') | Out-Null
    'Correction editor opened and closed without changing synthetic text.'
}

Check 'Empty archive displays its empty state without subtitle rows' {
    Invoke-Ui @('invoke', 'ArchiveMenu', '-a', "$AppPid") | Out-Null
    Invoke-Ui @('wait-for', 'ArchiveList', '-a', "$AppPid", '-t', '3000') | Out-Null
    Invoke-Ui @('invoke', '空白录音', '-a', "$AppPid") | Out-Null
    Invoke-Ui @('wait-for', '开始录音后，每条识别结果会保留在这里', '-a', "$AppPid", '-t', '3000') | Out-Null
    Capture-UiWindow 'empty-archive-dark.png' | Out-Null
    'Empty archive hint is visible.'
}

$results | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $ResultsPath -Encoding utf8
$results | ForEach-Object { [pscustomobject]$_ } | Format-Table name, status, detail -AutoSize
if (@($results | Where-Object status -eq 'FAIL').Count -gt 0) { exit 1 }
