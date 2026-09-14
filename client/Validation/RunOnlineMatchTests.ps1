$ErrorActionPreference='Stop'
$output=Join-Path $PSScriptRoot 'Generated/I1Tests'
New-Item -ItemType Directory -Force $output | Out-Null
$json=(Get-ChildItem "$PSScriptRoot/../DominoGame/Library/PackageCache/com.unity.nuget.newtonsoft-json*/Runtime/Newtonsoft.Json.dll" | Select-Object -First 1).FullName
$project=@'
<Project Sdk="Microsoft.NET.Sdk">
<PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net9.0</TargetFramework><EnableDefaultCompileItems>false</EnableDefaultCompileItems></PropertyGroup>
<ItemGroup>
<Compile Include="../../OnlineMatchClientTests.cs" />
<Compile Include="../../../DominoGame/Assets/_Domino/Scripts/Online/OnlineMatchClient.cs" />
<Compile Include="../../../DominoGame/Assets/_Domino/Scripts/Online/OnlineTurnClock.cs" />
<Compile Include="../../../DominoGame/Assets/_Domino/Scripts/Realtime/*.cs" Exclude="../../../DominoGame/Assets/_Domino/Scripts/Realtime/RealtimeLifecycle.cs" />
<Compile Include="../../../DominoGame/Assets/_Domino/Scripts/Identity/*.cs" />
<Compile Include="../../../DominoGame/Assets/_Domino/Scripts/Player/*.cs" />
<Compile Include="../../../DominoGame/Assets/_Domino/Scripts/Infrastructure/CancellableTask.cs" />
<Compile Include="../../../DominoGame/Assets/_Domino/Scripts/Infrastructure/Api/*.cs" Exclude="../../../DominoGame/Assets/_Domino/Scripts/Infrastructure/Api/UnityApiTransport.cs;../../../DominoGame/Assets/_Domino/Scripts/Infrastructure/Api/DominoApiSettings.cs" />
<Reference Include="Newtonsoft.Json"><HintPath>JSON_PATH</HintPath></Reference>
</ItemGroup></Project>
'@
[IO.File]::WriteAllText((Join-Path $output 'I1Tests.csproj'),$project.Replace('JSON_PATH',$json))
$env:DOMINO_I1_FIXTURES=Join-Path $PSScriptRoot 'Generated'
if(!(Test-Path (Join-Path $env:DOMINO_I1_FIXTURES 'i1-wire-message.json'))){throw 'Run server gradlew exportOnlineFixtures first.'}
dotnet run --project (Join-Path $output 'I1Tests.csproj') --configuration Release --verbosity quiet
if($LASTEXITCODE-ne 0){throw 'Online client tests failed'}
