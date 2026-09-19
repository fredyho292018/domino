$ErrorActionPreference='Stop'
$out=Join-Path $PSScriptRoot 'Generated/M5Parity'
New-Item -ItemType Directory -Force $out | Out-Null
$json=(Get-ChildItem "$PSScriptRoot/../DominoGame/Library/PackageCache/com.unity.nuget.newtonsoft-json*/Runtime/Newtonsoft.Json.dll" | Select-Object -First 1).FullName
$project=@'
<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net9.0</TargetFramework><EnableDefaultCompileItems>false</EnableDefaultCompileItems></PropertyGroup><ItemGroup>
<Compile Include="../../M5ParityFixtures.cs" />
<Compile Include="../../../DominoGame/Assets/_Domino/Scripts/Configuration/*.cs" />
<Compile Include="../../../DominoGame/Assets/_Domino/Scripts/Game/*.cs" />
<Compile Include="../../../DominoGame/Assets/_Domino/Scripts/Core/*.cs" />
<Reference Include="Newtonsoft.Json"><HintPath>JSON_PATH</HintPath></Reference></ItemGroup></Project>
'@
[IO.File]::WriteAllText((Join-Path $out 'M5Parity.csproj'),$project.Replace('JSON_PATH',$json))
dotnet run --project (Join-Path $out 'M5Parity.csproj') -c Release --verbosity quiet -- "$PSScriptRoot/../DominoGame/Assets/_Domino/Config/double-nine-partners-v1.json" "$out/local-traces.json"
if($LASTEXITCODE -ne 0){throw 'M5 parity fixture generation failed'}
