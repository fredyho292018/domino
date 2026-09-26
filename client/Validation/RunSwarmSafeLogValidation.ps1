$ErrorActionPreference='Stop'
. "$PSScriptRoot/SwarmProcessLauncher.ps1"
. "$PSScriptRoot/SwarmLogReader.ps1"
$repo=Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$run=Join-Path $PSScriptRoot 'Generated/S605/real-safe-process'
if(Test-Path $run){throw 'Safe process already attempted; review required'}
New-Item -ItemType Directory $run|Out-Null
$identities=Join-Path $env:LOCALAPPDATA 'TeamFHO/DominoSwarm'
$before=@{}
Get-ChildItem "$identities/TEST","$identities/LOCAL" -File | ForEach-Object {$before[$_.FullName]=(Get-FileHash $_.FullName).Hash}
$info=New-SwarmProcessInfo -JavaExecutable (Join-Path $env:JAVA_HOME 'bin/java.exe') -WorkingDirectory "$repo/server/domino" -IdentityDirectory "$identities/TEST" -GradleArguments @(':bot-swarm:coordinateIdentities','--args=--launch-check','--console=plain','--no-daemon')
$info.Environment['DOMINO_SWARM_FIREBASE_PROJECT_ID']='teamfho-domino'
$info.Environment['DOMINO_SWARM_FIREBASE_API_KEY']='launch-check-no-auth'
$info.Environment['DOMINO_SWARM_TEST_BASE_URL']='https://domino-api-test.teamfho.com'
$info.Environment['DOMINO_REAL_FIRESTORE_TESTS']='true'
$child=$null;$polls=0
try {
 $child=Start-SwarmChild $info "$run/stdout.log" "$run/stderr.log"
 $deadline=(Get-Date).AddMinutes(3)
 while(!$child.Process.HasExited){
  $null=Get-SwarmControlLog "$run/stdout.log"
  $polls++
  if((Get-Date) -gt $deadline){throw 'Safe process timeout'}
  Start-Sleep -Milliseconds 50
 }
 Complete-SwarmChild $child
 if($child.Process.ExitCode -ne 0){throw 'Safe process exit failure'}
 $log=Read-SwarmCompleteLog "$run/stdout.log"
 foreach($expected in @('SWARM_PROCESS_START=PASS','SWARM_PROCESS_IDENTITY_DISCOVERY=3','SWARM_PROCESS_REMOTE_TARGET_VALID=YES','AUTHENTICATION_ATTEMPTED=NO','MATCHMAKING_STARTED=NO','MATCHES_STARTED=0')){
  if(!$log.Contains($expected)){throw 'Safe process evidence missing'}
  Write-Output $expected
 }
 if($polls -lt 1){throw 'No concurrent polls'}
 Write-Output "CONCURRENT_POLLS=$polls"
 Write-Output 'COORDINATOR_CONCURRENT_LOG_READ=PASS'
 Write-Output 'SWARM_PROCESS_EXIT=PASS'
}finally{
 if($null -ne $child){Complete-SwarmChild $child -Stop;$child.Process.Dispose()}
}
foreach($path in $before.Keys){if((Get-FileHash $path).Hash -ne $before[$path]){throw 'Identity file changed'}}
Write-Output 'IDENTITY_FILES_UNCHANGED=YES'
$orphans=@(Get-CimInstance Win32_Process | Where-Object {$_.Name -match '^java(w)?\.exe$' -and $_.CommandLine -match 'com\.teamfho\.swarm\.|--args=--launch-check'})
if($orphans.Count -ne 0){throw 'Orphan process found'}
Write-Output 'ORPHAN_SWARM_PROCESSES=0'
