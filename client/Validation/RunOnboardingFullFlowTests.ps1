$ErrorActionPreference='Stop'
& "$PSScriptRoot/RunOnboardingShellTests.ps1"
$folder=Join-Path $PSScriptRoot 'Generated/OnboardingFullFlow'
New-Item -ItemType Directory -Force $folder|Out-Null
$xml=[IO.File]::ReadAllText((Join-Path $PSScriptRoot 'Generated/OnboardingShell/Tests.csproj'))
$xml=$xml.Replace('OnboardingShellTests.cs','OnboardingFullFlowTests.cs').Replace('</ItemGroup>','<Compile Include="../../../DominoGame/Assets/_Domino/Scripts/UI/AppShell/Editor/IsolatedOnboardingServer.cs" /></ItemGroup>')
[IO.File]::WriteAllText((Join-Path $folder 'Tests.csproj'),$xml)
dotnet run --project (Join-Path $folder 'Tests.csproj') --verbosity quiet
if($LASTEXITCODE -ne 0){throw 'Full flow tests failed'}
