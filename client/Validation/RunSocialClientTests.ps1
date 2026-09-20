$ErrorActionPreference='Stop'
$output=Join-Path $PSScriptRoot 'Generated/S11Tests'
New-Item -ItemType Directory -Force $output | Out-Null
$json=(Get-ChildItem "$PSScriptRoot/../DominoGame/Library/PackageCache/com.unity.nuget.newtonsoft-json*/Runtime/Newtonsoft.Json.dll" | Select-Object -First 1).FullName
$project=@'
<Project Sdk="Microsoft.NET.Sdk">
<PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net9.0</TargetFramework><EnableDefaultCompileItems>false</EnableDefaultCompileItems></PropertyGroup>
<ItemGroup>
<Compile Include="../../SocialClientTests.cs" />
<Compile Include="../../../DominoGame/Assets/_Domino/Scripts/Social/SocialClient.cs" />
<Compile Include="../../../DominoGame/Assets/_Domino/Scripts/Identity/*.cs" />
<Compile Include="../../../DominoGame/Assets/_Domino/Scripts/Player/*.cs" />
<Compile Include="../../../DominoGame/Assets/_Domino/Scripts/Infrastructure/CancellableTask.cs" />
<Compile Include="../../../DominoGame/Assets/_Domino/Scripts/Infrastructure/Api/*.cs" Exclude="../../../DominoGame/Assets/_Domino/Scripts/Infrastructure/Api/UnityApiTransport.cs;../../../DominoGame/Assets/_Domino/Scripts/Infrastructure/Api/DominoApiSettings.cs" />
<Reference Include="Newtonsoft.Json"><HintPath>JSON_PATH</HintPath></Reference>
</ItemGroup></Project>
'@
[IO.File]::WriteAllText((Join-Path $output 'SocialTests.csproj'),$project.Replace('JSON_PATH',$json))
dotnet run --project (Join-Path $output 'SocialTests.csproj') --configuration Release --verbosity quiet
if($LASTEXITCODE-ne 0){throw 'Social client tests failed'}
