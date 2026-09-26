$ErrorActionPreference = 'Stop'
$output = Join-Path $PSScriptRoot 'Generated'
New-Item -ItemType Directory -Force -Path $output | Out-Null
$project = @'
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net9.0</TargetFramework><EnableDefaultCompileItems>false</EnableDefaultCompileItems></PropertyGroup>
  <ItemGroup>
    <Compile Include="../PlayerFoundationClientTests.cs" />
    <Compile Include="../Server6CorrelationTests.cs" />
    <Compile Include="../../DominoGame/Assets/_Domino/Scripts/Client/Editor/Server6PlayerCorrelation.cs" />
    <Compile Include="../../DominoGame/Assets/_Domino/Scripts/Client/Editor/S601CodecValidation.cs" />
    <Compile Include="../PlayerRetryTests.cs" />
    <Compile Include="../PlayerAliasTests.cs" />
    <Compile Include="../PlayerConnectionTests.cs" />
    <Compile Include="../RealtimeClientTests.cs" />
    <Compile Include="../../DominoGame/Assets/_Domino/Scripts/Realtime/*.cs" Exclude="../../DominoGame/Assets/_Domino/Scripts/Realtime/RealtimeLifecycle.cs" />
    <Compile Include="../../DominoGame/Assets/_Domino/Scripts/UI/PlayerSyncPresentation.cs" />
    <Compile Include="../../DominoGame/Assets/_Domino/Scripts/Identity/*.cs" />
    <Compile Include="../../DominoGame/Assets/_Domino/Scripts/Player/*.cs" />
    <Compile Include="../../DominoGame/Assets/_Domino/Scripts/Infrastructure/CancellableTask.cs" />
    <Compile Include="../../DominoGame/Assets/_Domino/Scripts/Infrastructure/Firebase/*.cs" Exclude="../../DominoGame/Assets/_Domino/Scripts/Infrastructure/Firebase/FirebaseSdkClient.cs" />
    <Compile Include="../../DominoGame/Assets/_Domino/Scripts/Infrastructure/Api/*.cs" Exclude="../../DominoGame/Assets/_Domino/Scripts/Infrastructure/Api/UnityApiTransport.cs;../../DominoGame/Assets/_Domino/Scripts/Infrastructure/Api/DominoApiSettings.cs" />
  </ItemGroup>
</Project>
'@
[IO.File]::WriteAllText((Join-Path $output 'PlayerFoundationClientTests.csproj'), $project)
$jsonAssembly = Get-ChildItem "$PSScriptRoot/../DominoGame/Library/PackageCache/com.unity.nuget.newtonsoft-json*/Runtime/Newtonsoft.Json.dll" | Select-Object -First 1
if (!$jsonAssembly) { throw 'Import Unity packages before running validation.' }
$project = $project.Replace('</ItemGroup>', '<Reference Include="Newtonsoft.Json"><HintPath>' + $jsonAssembly.FullName + '</HintPath></Reference></ItemGroup>')
[IO.File]::WriteAllText((Join-Path $output 'PlayerFoundationClientTests.csproj'), $project)
dotnet run --project (Join-Path $output 'PlayerFoundationClientTests.csproj') --configuration Release --verbosity quiet
if ($LASTEXITCODE -ne 0) { throw 'Player foundation client tests failed' }
