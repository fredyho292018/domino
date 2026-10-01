param([switch]$Checkpoint)
$ErrorActionPreference='Stop'
$output=Join-Path $PSScriptRoot 'Generated/OnboardingClient'
New-Item -ItemType Directory -Force $output | Out-Null
$base='../../../DominoGame/Assets/_Domino/Scripts/'
$files=@('Identity/*.cs','Player/*.cs','Infrastructure/CancellableTask.cs','Infrastructure/Api/*.cs','Online/OnlineMatchClient.cs','Online/OnlineTurnClock.cs','Realtime/*.cs','Replay/ReplayClient.cs','Replay/ReplayReducer.cs')
$testFile=if($Checkpoint){'OnboardingCheckpointTests.cs'}else{'OnboardingClientTests.cs'}
$items='<Compile Include="../../'+$testFile+'" />'
foreach($file in $files){$items+='<Compile Include="'+$base+$file+'" Exclude="'+$base+'Infrastructure/Api/UnityApiTransport.cs;'+$base+'Infrastructure/Api/DominoApiSettings.cs;'+$base+'Realtime/RealtimeLifecycle.cs" />'}
$json=Get-ChildItem "$PSScriptRoot/../DominoGame/Library/PackageCache/com.unity.nuget.newtonsoft-json*/Runtime/Newtonsoft.Json.dll"|Select-Object -First 1
$items+='<Reference Include="Newtonsoft.Json"><HintPath>'+$json.FullName+'</HintPath></Reference>'
$project='<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net9.0</TargetFramework><EnableDefaultCompileItems>false</EnableDefaultCompileItems></PropertyGroup><ItemGroup>'+$items+'</ItemGroup></Project>'
[IO.File]::WriteAllText((Join-Path $output 'Tests.csproj'),$project)
dotnet run --project (Join-Path $output 'Tests.csproj') --verbosity quiet
if($LASTEXITCODE -ne 0){throw 'Onboarding client validation failed'}
