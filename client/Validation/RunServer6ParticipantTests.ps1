$ErrorActionPreference='Stop'
$dir=Join-Path $PSScriptRoot 'Generated/S606R/tests'
New-Item -ItemType Directory -Force $dir | Out-Null
$json=(Get-ChildItem "$PSScriptRoot/../DominoGame/Library/PackageCache/com.unity.nuget.newtonsoft-json*/Runtime/Newtonsoft.Json.dll" | Select-Object -First 1).FullName
$helper=(Resolve-Path "$PSScriptRoot/../DominoGame/Assets/_Domino/Scripts/Client/Editor/Server6ParticipantValidator.cs").Path
$tests=(Resolve-Path "$PSScriptRoot/Server6ParticipantTests.cs").Path
$xml=@"
<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net9.0</TargetFramework><EnableDefaultCompileItems>false</EnableDefaultCompileItems></PropertyGroup><ItemGroup><Compile Include="$helper"/><Compile Include="$tests"/><Reference Include="Newtonsoft.Json"><HintPath>$json</HintPath></Reference></ItemGroup></Project>
"@
Set-Content "$dir/Tests.csproj" $xml
dotnet run --project "$dir/Tests.csproj" --configuration Release --verbosity quiet
if($LASTEXITCODE -ne 0){throw 'Participant tests failed'}
