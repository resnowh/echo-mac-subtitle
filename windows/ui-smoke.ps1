param([Parameter(Mandatory)][int]$AppPid, [Parameter(Mandatory)][string]$ResultsPath)
$ErrorActionPreference = 'Stop'
$results = [System.Collections.Generic.List[object]]::new()

function Invoke-Ui([string[]]$Arguments) {
    $output = & winapp ui @Arguments 2>&1
    if ($LASTEXITCODE -ne 0) { throw ($output -join "`n") }
    return ($output -join "`n")
}

function Search-Ui([string]$Selector) {
    $json = Invoke-Ui @('search', $Selector, '-a', "$AppPid", '--json')
    return ($json | ConvertFrom-Json)
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

Check 'Source language flyout retains automatic recognition' {
    Invoke-Ui @('invoke', 'SourceLanguageChoice', '-a', "$AppPid") | Out-Null
    try { Invoke-Ui @('wait-for', 'SourceLanguage_none', '-a', "$AppPid", '-t', '3000') | Out-Null }
    finally { Invoke-Ui @('send-keys', 'esc', '-a', "$AppPid", '--via', 'send-input') | Out-Null }
    'Automatic recognition option is reachable.'
}

Check 'Target language flyout retains the no-translation option' {
    Invoke-Ui @('invoke', 'TargetLanguageChoice', '-a', "$AppPid") | Out-Null
    try { Invoke-Ui @('wait-for', 'TargetLanguage_none', '-a', "$AppPid", '-t', '3000') | Out-Null }
    finally { Invoke-Ui @('send-keys', 'esc', '-a', "$AppPid", '--via', 'send-input') | Out-Null }
    'No-translation option is reachable.'
}

Check 'No new-content action appears when already at latest' {
    $matches = (Search-Ui 'ScrollToLatest').matches
    $visible = @($matches | Where-Object { -not $_.isOffscreen }).Count -gt 0
    if ($visible) { throw 'Scroll-to-latest action is visible without new content.' }
    "Visible=$visible"
}

Check 'Settings pages and theme control are reachable' {
    Invoke-Ui @('invoke', 'SettingsTab', '-a', "$AppPid") | Out-Null
    try {
        Invoke-Ui @('wait-for', 'SettingsSections', '-a', "$AppPid", '-t', '3000') | Out-Null
        Invoke-Ui @('invoke', 'SettingsRecognition', '-a', "$AppPid") | Out-Null
        Invoke-Ui @('wait-for', 'SettingsSourceMode', '-a', "$AppPid", '-t', '3000') | Out-Null
        Invoke-Ui @('invoke', 'SettingsSegmentation', '-a', "$AppPid") | Out-Null
        Invoke-Ui @('wait-for', 'EndpointDelay', '-a', "$AppPid", '-t', '3000') | Out-Null
        Invoke-Ui @('invoke', 'SettingsServices', '-a', "$AppPid") | Out-Null
        Invoke-Ui @('wait-for', 'SonioxKey', '-a', "$AppPid", '-t', '3000') | Out-Null
        Invoke-Ui @('invoke', 'SettingsGeneral', '-a', "$AppPid") | Out-Null
        Invoke-Ui @('wait-for', 'ThemeChoice', '-a', "$AppPid", '-t', '3000') | Out-Null
    }
    finally { Invoke-Ui @('invoke', 'RecordingTab', '-a', "$AppPid") | Out-Null }
    'Recognition, segmentation, AI services and appearance controls are reachable.'
}

Check 'Archive picker, export action and transcript are available' {
    Invoke-Ui @('wait-for', 'TranscriptList', '-a', "$AppPid", '-t', '3000') | Out-Null
    Invoke-Ui @('invoke', 'ArchiveMenu', '-a', "$AppPid") | Out-Null
    try { Invoke-Ui @('wait-for', 'ArchiveList', '-a', "$AppPid", '-t', '3000') | Out-Null }
    finally { Invoke-Ui @('send-keys', 'esc', '-a', "$AppPid", '--via', 'send-input') | Out-Null }
    Invoke-Ui @('wait-for', 'ExportMenu', '-a', "$AppPid", '-t', '3000') | Out-Null
    'Archive picker, export action and transcript list are reachable.'
}

Check 'Correction editor opens and remains dismissible without editing' {
    $edit = @(Search-Ui 'EditSubtitle_').matches | Where-Object { -not $_.isOffscreen } | Select-Object -First 1
    if (-not $edit) { throw 'No visible subtitle correction action was found in the synthetic transcript.' }
    Invoke-Ui @('invoke', $edit.automationId, '-a', "$AppPid") | Out-Null
    try { Invoke-Ui @('wait-for', 'CorrectionSource', '-a', "$AppPid", '-t', '3000') | Out-Null }
    finally { Invoke-Ui @('send-keys', 'esc', '-a', "$AppPid", '--via', 'send-input') | Out-Null }
    Invoke-Ui @('wait-for', 'CorrectionSource', '-a', "$AppPid", '--gone', '-t', '3000') | Out-Null
    'Correction editor opened and closed without changing synthetic text.'
}

New-Item -ItemType Directory -Path (Split-Path -Parent $ResultsPath) -Force | Out-Null
$results | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $ResultsPath -Encoding utf8
$results | ForEach-Object { [pscustomobject]$_ } | Format-Table name, status, detail -AutoSize
if (@($results | Where-Object status -eq 'FAIL').Count -gt 0) { exit 1 }
