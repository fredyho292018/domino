$ErrorActionPreference = 'Stop'
$output = Join-Path $PSScriptRoot 'Generated/GuestAccount01A'
New-Item -ItemType Directory -Force -Path $output | Out-Null
$base = '../../../DominoGame/Assets/_Domino/Scripts/'
$items = '<Compile Include="../../GuestAccount01ATests.cs" />'
foreach ($file in @('Identity/PlayerIdentity.cs','Identity/EmailAuthState.cs','Auth/ProductionLogoutService.cs','Auth/AccountProtectionController.cs')) {
    $items += '<Compile Include="' + $base + $file + '" />'
}
$project = '<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net9.0</TargetFramework><EnableDefaultCompileItems>false</EnableDefaultCompileItems></PropertyGroup><ItemGroup>' + $items + '</ItemGroup></Project>'
[IO.File]::WriteAllText((Join-Path $output 'Tests.csproj'), $project)
dotnet run --project (Join-Path $output 'Tests.csproj') --verbosity quiet
if ($LASTEXITCODE -ne 0) { throw 'GUEST-ACCOUNT-01A validation failed' }
