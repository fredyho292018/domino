param(
    [ValidateSet('DUEL_1V1','PARTNERS_2V2_ONLINE')][string]$Mode='DUEL_1V1',
    [ValidateRange(1,20)][int]$Clients=10,
    [int]$TargetMatches=10,
    [int]$DurationSeconds=600,
    [string]$Name='duel10',
    [int]$ThinkMinMs=100,
    [int]$ThinkMaxMs=150
)
$ErrorActionPreference='Stop'
if ($Name -notmatch '^[a-zA-Z0-9_-]+$') { throw 'Invalid case name' }
$root=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
Push-Location $root
try {
    if (!(Test-Path build/swarm-emulator/ready)) { throw 'Emulator backend not ready' }
    $env:DOMINO_SWARM_EMULATOR='true'
    $env:FIRESTORE_EMULATOR_HOST='127.0.0.1:18085'
    $env:FIREBASE_AUTH_EMULATOR_HOST='127.0.0.1:19099'
    $env:DOMINO_SWARM_FIREBASE_PROJECT_ID='demo-domino-swarm'
    $env:DOMINO_SWARM_FIREBASE_API_KEY='emulator-only'
    $env:DOMINO_SWARM_IDENTITIES_DIR=Join-Path ([IO.Path]::GetTempPath()) 'domino-swarm-emulator-i31'
    $env:DOMINO_SWARM_STOP_FILE=Join-Path $root "build/swarm-emulator/$Name.stop"
    if (Test-Path $env:DOMINO_SWARM_STOP_FILE) { throw 'Use a fresh case name; stop file exists' }
    Copy-Item build/swarm-emulator/metrics.json "build/swarm-emulator/$Name-before.json"
    & .\gradlew.bat :bot-swarm:run "--args=--clients=$Clients --mode=$Mode --baseUrl=http://127.0.0.1:18086 --targetMatches=$TargetMatches --durationSeconds=$DurationSeconds --thinkMinMs=$ThinkMinMs --thinkMaxMs=$ThinkMaxMs --summarySeconds=5" --console=plain *> "build/swarm-emulator/$Name.log"
    $result=$LASTEXITCODE
    Start-Sleep -Seconds 2
    Copy-Item build/swarm-emulator/metrics.json "build/swarm-emulator/$Name-after.json"
    Get-Content "build/swarm-emulator/$Name.log" | Select-String 'SWARM_STOPPED|SWARM_STOP_REQUESTED|SWARM_CLIENT_FAILED'
    if ($result -ne 0) { throw "Emulator case failed: $Name" }
} finally { Pop-Location }
