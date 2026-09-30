$ErrorActionPreference = 'Stop'
$output = Join-Path $PSScriptRoot 'Generated/Auth02A'
New-Item -ItemType Directory -Force -Path $output | Out-Null
$base = '../../../DominoGame/Assets/_Domino/Scripts/'
$files = @('Auth/*.cs','Identity/*.cs','Player/*.cs','Infrastructure/CancellableTask.cs','Infrastructure/Firebase/IFirebaseClient.cs','Infrastructure/Firebase/FirebaseBootstrap.cs','Infrastructure/Firebase/FirebaseAuthService.cs','Infrastructure/Api/IDominoApiClient.cs','Infrastructure/Api/DominoApiClient.cs','Infrastructure/Api/DominoApiException.cs','Infrastructure/Api/PlayerBootstrapDtos.cs','Infrastructure/Api/PlayerSnapshotMapper.cs')
$items = '<Compile Include="../../Auth02ATests.cs" />'
foreach ($file in $files) { $items += '<Compile Include="' + $base + $file + '" />' }
$project = '<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net9.0</TargetFramework><EnableDefaultCompileItems>false</EnableDefaultCompileItems></PropertyGroup><ItemGroup>' + $items + '</ItemGroup></Project>'
[IO.File]::WriteAllText((Join-Path $output 'Tests.csproj'), $project)
dotnet run --project (Join-Path $output 'Tests.csproj') --verbosity quiet
if ($LASTEXITCODE -ne 0) { throw 'AUTH-02A validation failed' }
