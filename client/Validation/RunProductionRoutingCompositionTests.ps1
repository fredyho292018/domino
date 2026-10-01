param([ValidateSet('ProductionRoutingComposition','Auth01','Auth02A','Auth02B','AuthLogout')][string]$Suite='ProductionRoutingComposition')
$ErrorActionPreference='Stop'
$folder=Join-Path $PSScriptRoot 'Generated/Routing02'
New-Item -ItemType Directory -Force $folder|Out-Null
$base='../../../DominoGame/Assets/_Domino/Scripts/'
$files=@('Auth/*.cs','Identity/*.cs','Player/*.cs','Infrastructure/CancellableTask.cs','Infrastructure/Firebase/IFirebaseClient.cs','Infrastructure/Firebase/FirebaseBootstrap.cs','Infrastructure/Firebase/FirebaseAuthService.cs','Infrastructure/Api/*.cs','Online/OnlineMatchClient.cs','Online/OnlineTurnClock.cs','Realtime/*.cs','Replay/ReplayClient.cs','Replay/ReplayReducer.cs','UI/AppShell/OnboardingShellController.cs','UI/AppShell/BasicProfileController.cs','UI/AppShell/ExperienceController.cs','UI/AppShell/CoachController.cs','UI/AppShell/ContactsController.cs','UI/AppShell/MembershipController.cs','UI/AppShell/ProductionRoutingComposition.cs','UI/AppShell/Editor/IsolatedOnboardingServer.cs','UI/AppShell/Editor/RoutingCompositionFixture.cs')
$items='<Compile Include="../../'+$Suite+'Tests.cs" />'
foreach($file in $files){$items+='<Compile Include="'+$base+$file+'" Exclude="'+$base+'Infrastructure/Api/UnityApiTransport.cs;'+$base+'Infrastructure/Api/DominoApiSettings.cs;'+$base+'Realtime/RealtimeLifecycle.cs" />'}
$json=Get-ChildItem "$PSScriptRoot/../DominoGame/Library/PackageCache/com.unity.nuget.newtonsoft-json*/Runtime/Newtonsoft.Json.dll"|Select-Object -First 1
$items+='<Reference Include="Newtonsoft.Json"><HintPath>'+$json.FullName+'</HintPath></Reference>'
$project='<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net9.0</TargetFramework><EnableDefaultCompileItems>false</EnableDefaultCompileItems></PropertyGroup><ItemGroup>'+$items+'</ItemGroup></Project>'
[IO.File]::WriteAllText((Join-Path $folder 'Tests.csproj'),$project)
dotnet run --project (Join-Path $folder 'Tests.csproj') --verbosity quiet
if($LASTEXITCODE -ne 0){throw 'Routing composition tests failed'}