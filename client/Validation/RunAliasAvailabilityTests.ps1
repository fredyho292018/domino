$ErrorActionPreference='Stop'
& "$PSScriptRoot/RunOnboardingShellTests.ps1"
$folder=Join-Path $PSScriptRoot 'Generated/AliasAvailability'
New-Item -ItemType Directory -Force $folder|Out-Null
$xml=[IO.File]::ReadAllText((Join-Path $PSScriptRoot 'Generated/OnboardingShell/Tests.csproj')).Replace('OnboardingShellTests.cs','AliasAvailabilityTests.cs')
[IO.File]::WriteAllText((Join-Path $folder 'Tests.csproj'),$xml)
dotnet run --project (Join-Path $folder 'Tests.csproj') --verbosity quiet
if($LASTEXITCODE -ne 0){throw 'Alias availability tests failed'}
