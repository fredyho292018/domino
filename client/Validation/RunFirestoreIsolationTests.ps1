$ErrorActionPreference='Stop'
$output=Join-Path $PSScriptRoot 'Generated/F0Tests'
New-Item -ItemType Directory -Force $output | Out-Null
$project=@'
<Project Sdk="Microsoft.NET.Sdk">
<PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net9.0</TargetFramework><EnableDefaultCompileItems>false</EnableDefaultCompileItems><DefineConstants>UNITY_EDITOR</DefineConstants></PropertyGroup>
<ItemGroup>
<Compile Include="../../FirestoreIsolationPolicyTests.cs" />
<Compile Include="../../../DominoGame/Assets/_Domino/Scripts/Infrastructure/ValidationNetworkPolicy.cs" />
</ItemGroup></Project>
'@
[IO.File]::WriteAllText((Join-Path $output 'F0Tests.csproj'),$project)
dotnet run --project (Join-Path $output 'F0Tests.csproj') --configuration Release --verbosity quiet
if($LASTEXITCODE -ne 0){throw 'F0 isolation policy tests failed'}
