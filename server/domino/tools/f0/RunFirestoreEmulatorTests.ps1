param([string]$Java = 'C:/Program Files/Java/jdk-21/bin/java.exe')
$ErrorActionPreference = 'Stop'
$root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$jar = Join-Path $root 'build/cloud-firestore-emulator-v1.22.0.jar'
$hash = '9b6498b7f62714d67f48f59b3818883cd682dbcd46b9f59511de81c97bb5166c'
function PortOpen {
    $client = [Net.Sockets.TcpClient]::new()
    try { $client.Connect('127.0.0.1',18085); return $true } catch { return $false } finally { $client.Dispose() }
}
if (PortOpen) { throw 'Port 18085 already occupied; will not stop another process.' }
if (!(Test-Path -LiteralPath $jar)) {
    New-Item -ItemType Directory -Force (Join-Path $root 'build') | Out-Null
    Invoke-WebRequest 'https://storage.googleapis.com/firebase-preview-drop/emulator/cloud-firestore-emulator-v1.22.0.jar' -OutFile $jar
}
if ((Get-FileHash -LiteralPath $jar -Algorithm SHA256).Hash.ToLowerInvariant() -ne $hash) { throw 'Emulator checksum mismatch' }
$emulatorProcess = $null
Push-Location $root
try {
    $emulatorProcess = Start-Process -FilePath $Java -ArgumentList @('-jar',('"'+$jar+'"'),'--host','127.0.0.1','--port','18085','--project_id','demo-domino-f0','--single_project_mode','true') -PassThru -WindowStyle Hidden -RedirectStandardOutput 'build/f0-emulator.stdout.log' -RedirectStandardError 'build/f0-emulator.stderr.log'
    $deadline = [DateTime]::UtcNow.AddSeconds(45)
    while (!(PortOpen)) {
        if ($emulatorProcess.HasExited -or [DateTime]::UtcNow -gt $deadline) { throw 'Emulator startup failed' }
        Start-Sleep -Milliseconds 250
    }
    & .\gradlew.bat emulatorTest --console=plain
    if ($LASTEXITCODE -ne 0) { throw 'Emulator tests failed' }
} finally {
    if ($emulatorProcess -and !$emulatorProcess.HasExited) { $emulatorProcess.Kill(); $emulatorProcess.WaitForExit() }
    Pop-Location
    if (PortOpen) { throw 'EMULATOR_PORT_CLEANUP=FAIL' }
    Write-Output 'EMULATOR_PORT_CLEANUP=PASS'
    Write-Output 'EMULATOR_PROCESS_CLEANUP=PASS'
}
