param([string]$NewtonsoftPath)
$ErrorActionPreference = 'Stop'
$output = Join-Path $PSScriptRoot 'Generated/H3Tests'
New-Item -ItemType Directory -Force $output | Out-Null
if (!$NewtonsoftPath) {
    $NewtonsoftPath = (Get-ChildItem "$PSScriptRoot/../DominoGame/Library/PackageCache/com.unity.nuget.newtonsoft-json*/Runtime/Newtonsoft.Json.dll" | Select-Object -First 1).FullName
}
if (!$NewtonsoftPath) { throw 'Import Unity packages first.' }
$project = @'
<Project Sdk="Microsoft.NET.Sdk">
<PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net9.0</TargetFramework><EnableDefaultCompileItems>false</EnableDefaultCompileItems></PropertyGroup>
<ItemGroup>
<Compile Include="../../RewardIntentClientTests.cs" />
<Compile Include="../../../DominoGame/Assets/_Domino/Scripts/Ads/*.cs" Exclude="../../../DominoGame/Assets/_Domino/Scripts/Ads/UnityRewardedAdLoader.cs;../../../DominoGame/Assets/_Domino/Scripts/Ads/UnityGoogleAdsSdk.cs;../../../DominoGame/Assets/_Domino/Scripts/Ads/DominoAdsSettings.cs;../../../DominoGame/Assets/_Domino/Scripts/Ads/EditorMockAdsConsent.cs" />
<Compile Include="../../../DominoGame/Assets/_Domino/Scripts/Core/GameEvents.cs" />
<Compile Include="../../../DominoGame/Assets/_Domino/Scripts/Core/DominoTile.cs" />
<Compile Include="../../../DominoGame/Assets/_Domino/Scripts/Identity/*.cs" />
<Compile Include="../../../DominoGame/Assets/_Domino/Scripts/Infrastructure/CancellableTask.cs" />
<Compile Include="../../../DominoGame/Assets/_Domino/Scripts/Infrastructure/Api/IDominoApiClient.cs" />
<Compile Include="../../../DominoGame/Assets/_Domino/Scripts/Infrastructure/Api/PlayerBootstrapDtos.cs" />
<Compile Include="../../../DominoGame/Assets/_Domino/Scripts/Infrastructure/Api/DominoApiException.cs" />
<Reference Include="Newtonsoft.Json"><HintPath>JSON_PATH</HintPath></Reference>
</ItemGroup>
</Project>
'@
$project = $project.Replace('JSON_PATH', $NewtonsoftPath)
[IO.File]::WriteAllText((Join-Path $output 'H3Tests.csproj'), $project)
dotnet run --project (Join-Path $output 'H3Tests.csproj') --configuration Release --verbosity quiet
if ($LASTEXITCODE -ne 0) { throw 'H3 client tests failed' }
