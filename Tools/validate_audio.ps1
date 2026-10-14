param([string]$UnityEditorData = 'C:/Program Files/Unity/Hub/Editor/6000.3.8f1/Editor/Data')
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
Set-Location -LiteralPath $projectRoot
python Tools/generate_sfx.py --check
if ($LASTEXITCODE -ne 0) { throw 'WAV validation failed' }
$audioGuids = @()
foreach ($cue in @('Select','Invalid','Match','Striped','Combo','Win','Fail')) {
    $cueMeta = Get-Content -LiteralPath "Assets/_CatMatch3/Art/Audio/SFX/SFX_$cue.wav.meta" -Raw
    $guidMatch = [regex]::Match($cueMeta, '(?m)^guid: ([0-9a-f]{32})\r?$')
    if (-not $guidMatch.Success -or $cueMeta -notmatch 'AudioImporter:' -or
        $cueMeta -notmatch 'compressionFormat: 0' -or $cueMeta -notmatch 'preloadAudioData: 1') {
        throw "Missing or invalid PCM import metadata for $cue"
    }
    $audioGuids += $guidMatch.Groups[1].Value
}
if (($audioGuids | Sort-Object -Unique).Count -ne 7) { throw 'Duplicate sound asset GUIDs' }
'PASS: seven unique AudioImporter references with preloaded PCM settings.'
$validationRoot = Join-Path $projectRoot 'Temp/AudioValidation'
New-Item -ItemType Directory -Force $validationRoot | Out-Null
$monoRoot = Join-Path $UnityEditorData 'MonoBleedingEdge'
$mono = Join-Path $monoRoot 'bin/mono.exe'
$compiler = Join-Path $monoRoot 'lib/mono/4.5/csc.exe'
function Compile([string[]]$CompilerArguments) {
    & $mono $compiler /nologo @CompilerArguments
    if ($LASTEXITCODE -ne 0) { throw 'Audio validation compilation failed' }
}
$core = Join-Path $validationRoot 'CatMatch3.Core.dll'
$runtime = Join-Path $validationRoot 'CatMatch3.Runtime.dll'
$commonRefs = (Get-ChildItem "$UnityEditorData/Managed/UnityEngine/UnityEngine*.dll").FullName | ForEach-Object { '/r:' + $_ }
$commonRefs += @("/r:$monoRoot/lib/mono/4.5/Facades/netstandard.dll", "/r:$projectRoot/Library/ScriptAssemblies/UnityEngine.UI.dll")
Compile (@('/target:library','/define:CATMATCH3_OFFLINE',"/out:$core") + (Get-ChildItem Assets/_CatMatch3/Scripts/Core/*.cs).FullName)
Compile (@('/target:library',"/out:$runtime","/r:$core") + $commonRefs + (Get-ChildItem Assets/_CatMatch3/Scripts/Runtime/*.cs).FullName)
Compile (@("/out:$validationRoot/AudioFeedbackChecks.exe","/r:$core","/r:$runtime") + $commonRefs + @("$PSScriptRoot/AudioFeedbackChecks.cs"))
$previousMonoPath = $env:MONO_PATH
try {
    $env:MONO_PATH = "$UnityEditorData/Managed/UnityEngine;$projectRoot/Library/ScriptAssemblies"
    & $mono "$validationRoot/AudioFeedbackChecks.exe"
    if ($LASTEXITCODE -ne 0) { throw 'Focused audio checks failed' }
} finally { $env:MONO_PATH = $previousMonoPath }
