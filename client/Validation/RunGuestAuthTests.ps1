$ErrorActionPreference = 'Stop'
$output = Join-Path $PSScriptRoot 'Generated'
New-Item -ItemType Directory -Force -Path $output | Out-Null
$project = @'
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net9.0</TargetFramework><EnableDefaultCompileItems>false</EnableDefaultCompileItems></PropertyGroup>
  <ItemGroup>
    <Compile Include="../GuestAuthTests.cs" />
    <Compile Include="../../DominoGame/Assets/_Domino/Scripts/Identity/*.cs" />
    <Compile Include="../../DominoGame/Assets/_Domino/Scripts/Infrastructure/CancellableTask.cs" />
    <Compile Include="../../DominoGame/Assets/_Domino/Scripts/Infrastructure/Firebase/*.cs" Exclude="../../DominoGame/Assets/_Domino/Scripts/Infrastructure/Firebase/FirebaseSdkClient.cs" />
  </ItemGroup>
</Project>
'@
[IO.File]::WriteAllText((Join-Path $output 'GuestAuthTests.csproj'), $project)
dotnet run --project (Join-Path $output 'GuestAuthTests.csproj') --configuration Release --verbosity quiet
if ($LASTEXITCODE -ne 0) { throw 'Guest auth tests failed' }
