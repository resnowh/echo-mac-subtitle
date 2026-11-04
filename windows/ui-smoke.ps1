param([Parameter(Mandatory)][int]$AppPid, [Parameter(Mandatory)][string]$ResultsPath)
$ErrorActionPreference = 'Stop'
$results = [System.Collections.Generic.List[object]]::new()
function Check([string]$Name, [scriptblock]$Action) {
    try {
        $detail = & $Action 2>&1
        if ($LASTEXITCODE -ne 0) { throw ($detail -join "`n") }
        $results.Add(@{name=$Name;status='PASS'})
    } catch { $results.Add(@{name=$Name;status='FAIL';detail="$_"}) }
}
Check 'Recording page available' { winapp ui wait-for StartRecording -a $AppPid -t 3000 }
Check 'Stop hidden before recording' { winapp ui wait-for StopRecording -a $AppPid --gone -t 3000 }
Check 'Default source is computer audio' { winapp ui wait-for InputMode -a $AppPid --value '电脑音频' -t 3000 }
Check 'Source language displays its label' {
    $value = winapp ui get-value SourceLanguageChoice -a $AppPid --json | ConvertFrom-Json
    if ($value.text -match '^LanguageChoice \{') { throw 'ComboBox displays the model debug representation.' }
    if ([string]::IsNullOrWhiteSpace($value.text)) { throw 'No source language selection is displayed.' }
}
Check 'Target language displays its label' {
    $value = winapp ui get-value TargetLanguageChoice -a $AppPid --json | ConvertFrom-Json
    if ($value.text -match '^LanguageChoice \{') { throw 'ComboBox displays the model debug representation.' }
    if ([string]::IsNullOrWhiteSpace($value.text)) { throw 'No translation target selection is displayed.' }
}
Check 'Open settings' { winapp ui invoke SettingsTab -a $AppPid }
Check 'Open AI services category' { winapp ui invoke SettingsServices -a $AppPid }
Check 'Key entry available' { winapp ui wait-for SonioxKey -a $AppPid -t 3000 }
Check 'Service model is fixed by current Mac parity' { winapp ui wait-for SonioxModel -a $AppPid --gone -t 3000 }
Check 'Return to recording' { winapp ui invoke RecordingTab -a $AppPid }
Check 'Export menu available' { winapp ui wait-for ExportMenu -a $AppPid -t 3000 }
Check 'Transcript list available' { winapp ui wait-for TranscriptList -a $AppPid -t 3000 }
$results | ConvertTo-Json | Set-Content -LiteralPath $ResultsPath -Encoding utf8
$results | ForEach-Object { [pscustomobject]$_ } | Format-Table name,status
if (@($results | Where-Object status -eq 'FAIL').Count -gt 0) { exit 1 }
