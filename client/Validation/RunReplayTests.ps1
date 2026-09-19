$ErrorActionPreference='Stop'
$output=Join-Path $PSScriptRoot 'Generated/I4Tests'
New-Item -ItemType Directory -Force $output | Out-Null
$json=(Get-ChildItem "$PSScriptRoot/../DominoGame/Library/PackageCache/com.unity.nuget.newtonsoft-json*/Runtime/Newtonsoft.Json.dll" | Select-Object -First 1).FullName
$project=@'
<Project Sdk="Microsoft.NET.Sdk">
<PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net9.0</TargetFramework><EnableDefaultCompileItems>false</EnableDefaultCompileItems></PropertyGroup>
<ItemGroup><Compile Include="../../ReplayTests.cs"/><Compile Include="../../../DominoGame/Assets/_Domino/Scripts/Replay/ReplayReducer.cs"/>
<Reference Include="Newtonsoft.Json"><HintPath>JSON_PATH</HintPath></Reference></ItemGroup></Project>
'@
[IO.File]::WriteAllText((Join-Path $output 'I4Tests.csproj'),$project.Replace('JSON_PATH',$json))
$env:DOMINO_REPLAY_EXPORTS=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../../server/domino/build/swarm-emulator/retained-matches'))
$env:DOMINO_REPLAY_OUTPUT=$output
dotnet run --project (Join-Path $output 'I4Tests.csproj') --configuration Release --verbosity quiet
if($LASTEXITCODE -ne 0){throw 'Replay validation failed'}
