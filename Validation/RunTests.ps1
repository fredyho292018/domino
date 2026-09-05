param([string]$EditorData = 'C:/Program Files/Unity/Hub/Editor/6000.0.41f1/Editor/Data')
$ErrorActionPreference = 'Stop'
$output = Join-Path $PSScriptRoot 'Generated'
New-Item -ItemType Directory -Force -Path $output | Out-Null
$project = @"
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net9.0</TargetFramework><EnableDefaultCompileItems>false</EnableDefaultCompileItems></PropertyGroup>
  <ItemGroup>
    <Compile Include="../DomainTests.cs" />
    <Compile Include="../../DominoGame/Assets/_Domino/Scripts/Core/*.cs" />
    <Compile Include="../../DominoGame/Assets/_Domino/Scripts/Game/*.cs" />
    <Compile Include="../../DominoGame/Assets/_Domino/Scripts/UI/BoardLayout.cs" />
    <Reference Include="UnityEngine.CoreModule"><HintPath>$EditorData/Managed/UnityEngine/UnityEngine.CoreModule.dll</HintPath></Reference>
  </ItemGroup>
</Project>
"@
[System.IO.File]::WriteAllText((Join-Path $output 'DomainTests.csproj'), $project)
& dotnet run --project (Join-Path $output 'DomainTests.csproj') --configuration Release --verbosity quiet
if ($LASTEXITCODE -ne 0) { throw 'Domain tests failed' }
