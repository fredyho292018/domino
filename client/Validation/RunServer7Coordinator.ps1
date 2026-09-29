param([ValidateSet('DryRun','Fixture')][string]$Mode='DryRun',[int]$FailGroup=0)
$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot 'SwarmProcessLauncher.ps1')
. (Join-Path $PSScriptRoot 'SwarmLogReader.ps1')
. (Join-Path $PSScriptRoot 'Server7Supervisor.ps1')
$plan=Get-Content -Raw (Join-Path $PSScriptRoot 'Generated/SERVER7P/plan.json') | ConvertFrom-Json
if($plan.project -ne 'teamfho-domino' -or $plan.environment -ne 'TEST' -or $plan.groups.Count -ne 5){throw 'INVALID_PLAN'}
$slots=@($plan.groups | ForEach-Object {$_.slots})
$assigned=@($plan.intendedMatches | ForEach-Object {$_})
if($slots.Count -ne 100 -or @($slots | Sort-Object -Unique).Count -ne 100 -or $assigned.Count -ne 100 -or @($assigned | Sort-Object -Unique).Count -ne 100 -or $plan.intendedMatches.Count -ne 25){throw 'INVALID_ASSIGNMENT'}
foreach($g in $plan.groups){if($g.clients -ne 20 -or $g.localSlots.Count -ne 20){throw 'INVALID_GROUP'}}
if($Mode -eq 'DryRun'){'SERVER7_100_DRY_RUN=PASS';'LOAD_TEST_STARTED=NO';return}
# Fixture mode tests supervision without network or credentials. Actual load remains gated for later review.
$dir=Join-Path $PSScriptRoot ('Generated/SERVER7P/fixture-'+[guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $dir | Out-Null
$children=@();$stopped=$false
try {
 foreach($g in $plan.groups){
  $id=$g.group;$stop=Join-Path $dir "group-$id.stop"
  $info=[Diagnostics.ProcessStartInfo]::new();$info.FileName=(Get-Process -Id $PID).Path
  $info.UseShellExecute=$false;$info.CreateNoWindow=$true;$info.RedirectStandardOutput=$true;$info.RedirectStandardError=$true
  foreach($a in @('-NoProfile','-File',(Join-Path $PSScriptRoot 'Server7GroupFixture.ps1'),'-StopPath',$stop,'-Fail',([int]($id -eq $FailGroup)).ToString())){$info.ArgumentList.Add($a)}
  $child=Start-SwarmChild -Info $info -Stdout (Join-Path $dir "group-$id.log") -Stderr (Join-Path $dir "group-$id.err")
  $children+=@{Id=$id;Child=$child;Stop=$stop;Log=(Join-Path $dir "group-$id.log")}
 }
 $deadline=(Get-Date).AddSeconds(30)
 while($true){
  $ready=0;$authenticated=0;$active=0;$failed=$false
  foreach($c in $children){
   $log=Read-SwarmCompleteLog $c.Log
   if($log -match '(?m)^READY=20\r?$'){$ready+=20}
   if($log -match '(?m)^AUTHENTICATED=20\r?$'){$authenticated+=20}
   if($log -match '(?m)^ACTIVE=20\r?$'){$active+=20}
   if($log -match '(?m)^CRITICAL_FAILURE=YES\r?$' -or $c.Child.Process.HasExited){$failed=$true}
  }
  if($failed -or ($ready -eq 100 -and $FailGroup -eq 0)){$stopped=$true;break}
  if((Get-Date) -gt $deadline){throw 'FIXTURE_TIMEOUT'}
  Start-Sleep -Milliseconds 100
 }
} finally {
 Stop-Server7Groups -Groups @($children | ForEach-Object { @{ StopPath=$_.Stop; Child=$_.Child } })
}
if(@($children | Where-Object {!$_.Child.Process.HasExited}).Count){throw 'ORPHAN_PROCESS'}
$summary=@{Mode='OFFLINE_FIXTURE';ProcessesStarted=$children.Count;Ready=$ready;Authenticated=$authenticated;Active=$active;Matches=0;GlobalStop=$stopped;ExitCodes=@($children | ForEach-Object {$_.Child.Process.ExitCode});Orphans=0;RealAuthenticatedUsers=0}
$summary | ConvertTo-Json | Set-Content (Join-Path $dir 'summary.json')
if($children.Count -ne 5 -or !$stopped){throw 'SUPERVISION_FAILED'}
'GLOBAL_STOP_FIXTURE=PASS'
'ORPHAN_PROCESSES=0'
'LOAD_TEST_STARTED=NO'

