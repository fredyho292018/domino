param([string]$EditorData = 'C:/Program Files/Unity/Hub/Editor/6000.0.41f1/Editor/Data')
$ErrorActionPreference = 'Stop'
$output = Join-Path $PSScriptRoot 'Generated'
New-Item -ItemType Directory -Force -Path $output | Out-Null
$api = Join-Path $EditorData 'UnityReferenceAssemblies/unity-4.8-api'
$references = @(Get-ChildItem $api -Filter '*.dll') + @(Get-ChildItem "$api/Facades" -Filter '*.dll') + @(Get-ChildItem "$EditorData/Managed/UnityEngine" -Filter '*.dll')
$packageAssemblies = Join-Path $PSScriptRoot '../DominoGame/Library/ScriptAssemblies'
if (!(Test-Path (Join-Path $packageAssemblies 'Unity.Localization.dll'))) {
    $packageAssemblies = Join-Path $PSScriptRoot 'Generated/UnityPhase1/Library/ScriptAssemblies'
}
foreach ($packageAssembly in @('Unity.Localization.dll','Unity.Localization.Editor.dll','Unity.ResourceManager.dll','Unity.Addressables.dll','Unity.Addressables.Editor.dll')) {
    $assemblyPath = Join-Path $packageAssemblies $packageAssembly
    if (!(Test-Path $assemblyPath)) { throw "Import the project in Unity first: missing $packageAssembly" }
    $references += Get-Item $assemblyPath
}
$references += Get-Item "$PSScriptRoot/../DominoGame/Assets/Firebase/Plugins/Firebase.App.dll", "$PSScriptRoot/../DominoGame/Assets/Firebase/Plugins/Firebase.Auth.dll"
$references += Get-Item "$PSScriptRoot/../DominoGame/Assets/GoogleMobileAds/GoogleMobileAds.dll", "$PSScriptRoot/../DominoGame/Assets/GoogleMobileAds/GoogleMobileAds.Core.dll"
$references += Get-ChildItem "$PSScriptRoot/../DominoGame/Library/PackageCache/com.unity.nuget.newtonsoft-json*/Runtime/Newtonsoft.Json.dll"
$common = @('-nologo', '-target:library', '-langversion:9', '-nostdlib+', '-define:UNITY_6000_0_OR_NEWER;UNITY_2023_2_OR_NEWER;UNITY_2022_2_OR_NEWER;UNITY_2021_2_OR_NEWER;UNITY_2020_1_OR_NEWER;UNITY_2019_1_OR_NEWER;UNITY_2018_1_OR_NEWER;ENABLE_LEGACY_INPUT_MANAGER')
$common += $references | ForEach-Object { '-r:"' + $_.FullName + '"' }
function Compile([string]$name, [string[]]$files, [string[]]$extra) {
    $argsList = $common + $extra + ('-out:"' + (Join-Path $output "$name.dll") + '"') + ($files | ForEach-Object { '"' + $_ + '"' })
    $response = Join-Path $output "$name.rsp"
    [System.IO.File]::WriteAllLines($response, $argsList)
    & "$EditorData/NetCoreRuntime/dotnet.exe" "$EditorData/DotNetSdkRoslyn/csc.dll" "@$response"
    if ($LASTEXITCODE -ne 0) { throw "Compilation failed: $name" }
}
$ugui = Get-ChildItem "$EditorData/Resources/PackageManager/BuiltInPackages/com.unity.ugui/Runtime/UGUI" -Filter '*.cs' -Recurse | ForEach-Object FullName
Compile 'UnityEngine.UI' $ugui @()
$runtime = Get-ChildItem "$PSScriptRoot/../DominoGame/Assets/_Domino/Scripts" -Filter '*.cs' -Recurse | Where-Object FullName -NotMatch '[\\/]Editor[\\/]' | ForEach-Object FullName
Compile 'Domino.Runtime' $runtime @('-r:"' + (Join-Path $output 'UnityEngine.UI.dll') + '"')
$editor = Get-ChildItem "$PSScriptRoot/../DominoGame/Assets/_Domino/Scripts/Client/Editor" -Filter '*.cs' | ForEach-Object FullName
Compile 'Domino.Editor' $editor @('-r:"' + (Join-Path $output 'UnityEngine.UI.dll') + '"', '-r:"' + (Join-Path $output 'Domino.Runtime.dll') + '"')
Write-Output 'STATIC_COMPILATION=SUCCESS (Unity imports and Play Mode still require a licensed editor)'


