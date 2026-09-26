param([Parameter(Mandatory=$true)][ValidateRange(3,5)][int]$MatchNumber)
$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot 'SwarmProcessLauncher.ps1')
. (Join-Path $PSScriptRoot 'SwarmLogReader.ps1')
$failureContext='OTHER'
$child=$null
$repo=Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$run=Join-Path $PSScriptRoot ('Generated/SERVER6-match'+$MatchNumber)
New-Item -ItemType Directory -Force -Path $run | Out-Null
$stop=Join-Path $run 'stop'
if(Test-Path (Join-Path $run 'coordinator-started')){throw 'Existing execution: review required'}
Set-Content (Join-Path $run 'coordinator-started') 'STARTED'
$cfg=Get-Content -Raw (Join-Path $repo 'client/DominoGame/Assets/google-services.json') | ConvertFrom-Json
$env:DOMINO_SWARM_FIREBASE_PROJECT_ID='teamfho-domino'
$env:DOMINO_SWARM_FIREBASE_API_KEY=$cfg.client[0].api_key[0].current_key
$env:DOMINO_SWARM_IDENTITIES_DIR='C:\Users\fredy\AppData\Local\TeamFHO\DominoSwarm\TEST'
$env:DOMINO_SWARM_TEST_BASE_URL='https://domino-api-test.teamfho.com'
$env:DOMINO_REAL_FIRESTORE_TESTS='true'
$env:DOMINO_SWARM_STOP_FILE=$stop
$backend=Join-Path $repo 'server/domino'
function Await-File($path,$seconds){
 $until=(Get-Date).AddSeconds($seconds)
 while(!(Test-Path -LiteralPath $path)){
  if(Test-Path $stop){$script:failureContext='CONTROL_PROTOCOL';throw 'EXTERNAL_STOP'}
  if((Get-Date) -gt $until){$script:failureContext='CHILD_TIMEOUT';throw 'WAIT_TIMEOUT'}
  Start-Sleep -Milliseconds 500
 }
}
try {
 for($n=$MatchNumber;$n -le $MatchNumber;$n++){
  Await-File (Join-Path $run "request-$n") 900
  $out=Join-Path $run "swarm-$n.log"; $err=Join-Path $run "swarm-$n.stderr.log"
  $args='--clients=3 --mode=PARTNERS_2V2_ONLINE --environment=TEST --baseUrl=https://domino-api-test.teamfho.com --requeue=false --targetMatches=1 --durationSeconds=2700 --maxFailures=1 --summarySeconds=10'
  $launchInfo=New-SwarmProcessInfo -JavaExecutable (Join-Path $env:JAVA_HOME 'bin/java.exe') -WorkingDirectory $backend -IdentityDirectory $env:DOMINO_SWARM_IDENTITIES_DIR -GradleArguments @(':bot-swarm:run',"--args=$args",'--console=plain','--no-daemon')
  $child=Start-SwarmChild -Info $launchInfo -Stdout $out -Stderr $err
  $process=$child.Process
  $until=(Get-Date).AddMinutes(45);$ready=$false;$matched=$false
  while(!$process.HasExited){
   if(Test-Path $stop){$failureContext='CONTROL_PROTOCOL';throw 'EXTERNAL_STOP'}
   if((Get-Date) -gt $until){$failureContext='CHILD_TIMEOUT';throw 'MATCH_TIMEOUT'}
   $failureContext='LOG';$log=Get-SwarmControlLog $out;$failureContext='CONTROL_PROTOCOL'
   if($log -match 'SWARM_CLIENT_FAILED|SWARM_START_FAILED|SWARM_STOP_REQUESTED'){throw 'SWARM_FAILURE'}
   if(!$ready -and ([regex]::Matches($log,'SWARM_CLIENT_CONNECTED alias=')).Count -eq 3){Set-Content (Join-Path $run "opponents-ready-$n") 'READY';$ready=$true;Write-Output "MATCH_${n}_OPPONENTS_READY=YES"}
   $found=[regex]::Matches($log,'SWARM_MATCH_FOUND alias=(\S+) matchId=([a-zA-Z0-9_-]+)')
   if($found.Count -gt 3){throw 'UNEXPECTED_ASSIGNMENT'}
   $matchFile=Join-Path $run "match-$n.txt"
   if(!$matched -and $found.Count -eq 3 -and (Test-Path $matchFile)){
    $id=[IO.File]::ReadAllText($matchFile).Trim()
    if(@($found | ForEach-Object {$_.Groups[1].Value} | Select-Object -Unique).Count -ne 3){throw 'OPPONENT_COUNT'}
    if(@($found | Where-Object {$_.Groups[2].Value -ne $id}).Count -ne 0){throw 'ASSIGNMENT_MISMATCH'}
    & C:/Python312/python.exe (Join-Path $PSScriptRoot 'ReadServer6MatchSeats.py') $MatchNumber initial
    if($LASTEXITCODE -ne 0){throw 'INITIAL_SEAT_EVIDENCE_FAILED'}
    Set-Content (Join-Path $run "participants-confirmed-$n") $id
    $matched=$true;Write-Output "MATCH_${n}_FOUR_EXPECTED_PLAYERS=YES"
   }
   Start-Sleep -Milliseconds 500;$process.Refresh()
  }
  $failureContext='CHILD_EXIT';Complete-SwarmChild $child
  $failureContext='LOG';$log=Get-SwarmControlLog $out;$failureContext='CHILD_EXIT'
  if($process.ExitCode -ne 0 -or !$matched -or ([regex]::Matches($log,'SWARM_MATCH_FINISHED alias=')).Count -ne 3){throw 'INCOMPLETE_SWARM_MATCH'}
  & C:/Python312/python.exe (Join-Path $PSScriptRoot 'ReadServer6MatchSeats.py') $MatchNumber final
  if($LASTEXITCODE -ne 0){throw 'FINAL_SEAT_EVIDENCE_FAILED'}
  Set-Content (Join-Path $run "opponents-done-$n") 'PASS'
  Await-File (Join-Path $run "full-gate") 240
  Write-Output "MATCH_${n}_GATE=PASS"
 }
 Write-Output "COORDINATOR_MATCH_${MatchNumber}=PASS"
}catch{
 Write-SwarmSafeStop -StopPath $stop -Exception $_.Exception -Context $failureContext
}finally{
 if($null -ne $child){
  try{if(!$child.Process.HasExited){Set-Content $stop 'STOP'};Complete-SwarmChild $child -Stop}
  catch{Write-SwarmSafeStop -StopPath $stop -Exception $_.Exception -Context 'CHILD_EXIT'}
  finally{$child.Process.Dispose()}
 }
 Remove-Item Env:DOMINO_SWARM_FIREBASE_API_KEY -ErrorAction SilentlyContinue
}
