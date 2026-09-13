$ErrorActionPreference = 'Stop'
$output = Join-Path $PSScriptRoot 'Generated/H2Tests'
New-Item -ItemType Directory -Force -Path $output | Out-Null
$project = @'
<Project Sdk="Microsoft.NET.Sdk">
 <PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net9.0</TargetFramework><EnableDefaultCompileItems>false</EnableDefaultCompileItems></PropertyGroup>
 <ItemGroup>
  <Compile Include="../../RewardedAdsTests.cs" />
  <Compile Include="../../../DominoGame/Assets/_Domino/Scripts/Ads/*.cs" Exclude="../../../DominoGame/Assets/_Domino/Scripts/Ads/Unity*.cs;../../../DominoGame/Assets/_Domino/Scripts/Ads/DominoAdsSettings.cs;../../../DominoGame/Assets/_Domino/Scripts/Ads/EditorMockAdsConsent.cs" />
  <Compile Include="../../../DominoGame/Assets/_Domino/Scripts/Core/GameEvents.cs" />
  <Compile Include="../../../DominoGame/Assets/_Domino/Scripts/Core/DominoTile.cs" />
 </ItemGroup>
</Project>
'@
[IO.File]::WriteAllText((Join-Path $output 'RewardedAdsTests.csproj'), $project)
dotnet run --project (Join-Path $output 'RewardedAdsTests.csproj') --configuration Release --verbosity quiet
if ($LASTEXITCODE -ne 0) { throw 'Rewarded tests failed' }
