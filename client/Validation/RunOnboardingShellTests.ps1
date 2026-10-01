$ErrorActionPreference='Stop'
& "$PSScriptRoot/RunOnboardingClientTests.ps1" -Checkpoint
$folder=Join-Path $PSScriptRoot 'Generated/OnboardingShell'
New-Item -ItemType Directory -Force $folder|Out-Null
$xml=[IO.File]::ReadAllText((Join-Path $PSScriptRoot 'Generated/OnboardingClient/Tests.csproj'))
$xml=$xml.Replace('OnboardingCheckpointTests.cs','OnboardingShellTests.cs').Replace('</ItemGroup>','<Compile Include="../../../DominoGame/Assets/_Domino/Scripts/UI/AppShell/OnboardingShellController.cs" /><Compile Include="../../../DominoGame/Assets/_Domino/Scripts/UI/AppShell/BasicProfileController.cs" /><Compile Include="../../../DominoGame/Assets/_Domino/Scripts/UI/AppShell/ExperienceController.cs" /><Compile Include="../../../DominoGame/Assets/_Domino/Scripts/UI/AppShell/CoachController.cs" /><Compile Include="../../../DominoGame/Assets/_Domino/Scripts/UI/AppShell/ContactsController.cs" /><Compile Include="../../../DominoGame/Assets/_Domino/Scripts/UI/AppShell/MembershipController.cs" /></ItemGroup>')
[IO.File]::WriteAllText((Join-Path $folder 'Tests.csproj'),$xml)
dotnet run --project (Join-Path $folder 'Tests.csproj') --verbosity quiet
if($LASTEXITCODE -ne 0){throw 'Shell tests failed'}
