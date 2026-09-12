$ErrorActionPreference = 'Stop'
$output = Join-Path $PSScriptRoot 'Generated/H1Tests'
New-Item -ItemType Directory -Force -Path $output | Out-Null
$project = @'
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net9.0</TargetFramework><EnableDefaultCompileItems>false</EnableDefaultCompileItems></PropertyGroup>
  <ItemGroup>
    <Compile Include="../../AdsFoundationTests.cs" />
    <Compile Include="../../../DominoGame/Assets/_Domino/Scripts/Ads/AdsConfiguration.cs" />
    <Compile Include="../../../DominoGame/Assets/_Domino/Scripts/Ads/GoogleMobileAdsService.cs" />
  </ItemGroup>
</Project>
'@
[IO.File]::WriteAllText((Join-Path $output 'AdsFoundationTests.csproj'), $project)
dotnet run --project (Join-Path $output 'AdsFoundationTests.csproj') --configuration Release --verbosity quiet
if ($LASTEXITCODE -ne 0) { throw 'Ads foundation tests failed' }
