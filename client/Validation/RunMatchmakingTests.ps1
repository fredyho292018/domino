$ErrorActionPreference='Stop'
$output=Join-Path $PSScriptRoot 'Generated/I3Tests'
New-Item -ItemType Directory -Force $output | Out-Null
$json=(Get-ChildItem "$PSScriptRoot/../DominoGame/Library/PackageCache/com.unity.nuget.newtonsoft-json*/Runtime/Newtonsoft.Json.dll" | Select-Object -First 1).FullName
$project=@'
<Project Sdk="Microsoft.NET.Sdk">
<PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net9.0</TargetFramework><EnableDefaultCompileItems>false</EnableDefaultCompileItems></PropertyGroup>
<ItemGroup>
<Compile Include="../../MatchmakingClientTests.cs" />
<Compile Include="../../../DominoGame/Assets/_Domino/Scripts/Online/OnlineMatchClient.cs" />
<Compile Include="../../../DominoGame/Assets/_Domino/Scripts/Online/OnlineTurnClock.cs" /><Compile Include="../../../DominoGame/Assets/_Domino/Scripts/Online/MatchmakingClient.cs" />
<Compile Include="../../../DominoGame/Assets/_Domino/Scripts/Realtime/*.cs" Exclude="../../../DominoGame/Assets/_Domino/Scripts/Realtime/RealtimeLifecycle.cs" />
<Compile Include="../../../DominoGame/Assets/_Domino/Scripts/Identity/*.cs" />
<Compile Include="../../../DominoGame/Assets/_Domino/Scripts/Player/*.cs" />
<Compile Include="../../../DominoGame/Assets/_Domino/Scripts/Infrastructure/CancellableTask.cs" />
<Compile Include="../../../DominoGame/Assets/_Domino/Scripts/Infrastructure/Api/*.cs" Exclude="../../../DominoGame/Assets/_Domino/Scripts/Infrastructure/Api/UnityApiTransport.cs;../../../DominoGame/Assets/_Domino/Scripts/Infrastructure/Api/DominoApiSettings.cs" />
<Reference Include="Newtonsoft.Json"><HintPath>JSON_PATH</HintPath></Reference>
</ItemGroup></Project>
'@
[IO.File]::WriteAllText((Join-Path $output 'I3Tests.csproj'),$project.Replace('JSON_PATH',$json))
dotnet run --project (Join-Path $output 'I3Tests.csproj') --configuration Release --verbosity quiet
if($LASTEXITCODE-ne 0){throw 'Matchmaking client tests failed'}
