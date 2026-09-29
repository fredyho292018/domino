# Shared supervision state for prepared SERVER-7 processes. No launch occurs on import.
. (Join-Path $PSScriptRoot 'SwarmProcessLauncher.ps1')
. (Join-Path $PSScriptRoot 'SwarmLogReader.ps1')
function Get-Server7GroupState {
 param([string]$LogPath,[int]$Group)
 $text=Read-SwarmCompleteLog $LogPath
 $connected=@([regex]::Matches($text,'(?m)^SWARM_CLIENT_CONNECTED alias=(\S+)\r?$') | ForEach-Object {$_.Groups[1].Value} | Sort-Object -Unique)
 $matches=@([regex]::Matches($text,'(?m)^SWARM_MATCH_FOUND alias=\S+ matchId=([A-Za-z0-9_-]+)\r?$') | ForEach-Object {$_.Groups[1].Value} | Sort-Object -Unique)
 $finished=@([regex]::Matches($text,'(?m)^SWARM_MATCH_FINISHED alias=\S+ matchId=([A-Za-z0-9_-]+)\r?$') | ForEach-Object {$_.Groups[1].Value} | Sort-Object -Unique)
 # Active count is intentionally unknown without current dashboard state; never equate historical connections with liveness.
 $active=$null
 $dash=@($text -split "`n" | Where-Object {$_ -like 'DOMINO BOT SWARM *'})
 if($dash.Count){
  $states=[regex]::Match($dash[-1],'states=\{([^}]*)\}')
  if($states.Success){$active=0;foreach($m in [regex]::Matches($states.Groups[1].Value,'(\w+)=(\d+)')){if($m.Groups[1].Value -in @('IDLE','JOINING_QUEUE','SEARCHING','MATCH_FOUND','ENTERING_MATCH','PLAYING','MATCH_FINISHED','REQUEUE_DELAY')){$active += [int]$m.Groups[2].Value}}}
 }
 [pscustomobject]@{Group=$Group;Ready=$connected.Count;Authenticated=$connected.Count;Active=$active;MatchIds=$matches;CompletedMatchIds=$finished;Critical=($text -match '(?m)^SWARM_(CLIENT_FAILED|START_FAILED|STOP_REQUESTED)')}
}
function Stop-Server7Groups {
 param([array]$Groups)
 # Signal every owned process before waiting for any child.
 foreach($g in $Groups){Set-Content -LiteralPath $g.StopPath 'STOP'}
 foreach($g in $Groups){Complete-SwarmChild -Child $g.Child -Stop}
 if(@($Groups | Where-Object {!$_.Child.Process.HasExited}).Count){throw 'SERVER7_ORPHAN_PROCESS'}
}
function New-Server7GroupLaunch {
 param($Group,[string]$JavaExecutable,[string]$Backend,[string]$RunDirectory)
 $stop=Join-Path $RunDirectory ('group-{0:00}.stop' -f $Group.group)
 $args='--clients=20 --slotOffset=0 --mode=PARTNERS_2V2_ONLINE --environment=TEST --baseUrl=https://domino-api-test.teamfho.com --requeue=false --targetMatches=5 --durationSeconds=2700 --maxFailures=1 --summarySeconds=10'
 $info=New-SwarmProcessInfo -JavaExecutable $JavaExecutable -WorkingDirectory $Backend -IdentityDirectory $Group.directory -GradleArguments @(':bot-swarm:run',"--args=$args",'--console=plain','--no-daemon')
 # Administrative credentials are explicitly removed from child runtime.
 $info.Environment.Remove('GOOGLE_APPLICATION_CREDENTIALS') | Out-Null
 $info.Environment['DOMINO_SWARM_STOP_FILE']=$stop
 [pscustomobject]@{Info=$info;StopPath=$stop;Group=$Group.group}
}
# Actual ramp/load dispatch is deliberately not called by SERVER-7P. Its timing and metrics require load review.
function Invoke-Server7Supervision {
 param([array]$Groups,[string]$GlobalStopPath,[string]$SummaryPath,[string]$MatchRegistryPath,[int]$TimeoutSeconds=2700)
 $deadline=(Get-Date).AddSeconds($TimeoutSeconds)
 $known=[Collections.Generic.HashSet[string]]::new()
 try {
  while($true){
   $states=@($Groups | ForEach-Object {Get-Server7GroupState -LogPath $_.LogPath -Group $_.Group})
   foreach($s in $states){foreach($id in $s.MatchIds){$null=$known.Add($id)}}
   # Exact observed match IDs only; no Firebase identifiers or participant credentials.
   @($known) | ConvertTo-Json -AsArray | Set-Content -LiteralPath $MatchRegistryPath
   $exits=@($Groups | ForEach-Object {if($_.Child.Process.HasExited){$_.Child.Process.ExitCode}else{$null}})
   @{Groups=$states;ProcessesStarted=$Groups.Count;ExitCodes=$exits;StopRequested=(Test-Path -LiteralPath $GlobalStopPath)} | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath $SummaryPath
   if(Test-Path -LiteralPath $GlobalStopPath){break}
   if(@($states | Where-Object {$_.Critical}).Count){throw 'GROUP_CRITICAL_FAILURE'}
   if(@($Groups | Where-Object {$_.Child.Process.HasExited -and $_.Child.Process.ExitCode -ne 0}).Count){throw 'GROUP_EXIT_FAILURE'}
   if(@($Groups | Where-Object {!$_.Child.Process.HasExited}).Count -eq 0){break}
   if((Get-Date) -gt $deadline){throw 'GROUP_TIMEOUT'}
   Start-Sleep -Milliseconds 250
  }
 } finally {Stop-Server7Groups -Groups $Groups}
}

