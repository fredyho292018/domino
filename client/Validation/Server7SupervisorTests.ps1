$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot 'Server7Supervisor.ps1')
$dir=Join-Path $PSScriptRoot ('Generated/SERVER7P/supervisor-'+[guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $dir | Out-Null
$groups=@()
try {
 for($g=1;$g -le 5;$g++){
  $stop=Join-Path $dir "group-$g.stop";$log=Join-Path $dir "group-$g.log"
  $info=[Diagnostics.ProcessStartInfo]::new();$info.FileName=(Get-Process -Id $PID).Path
  $info.UseShellExecute=$false;$info.CreateNoWindow=$true;$info.RedirectStandardOutput=$true;$info.RedirectStandardError=$true
  foreach($a in @('-NoProfile','-File',(Join-Path $PSScriptRoot 'Server7GroupFixture.ps1'),'-StopPath',$stop,'-Fail',([int]($g -eq 3)).ToString())){$info.ArgumentList.Add($a)}
  $child=Start-SwarmChild -Info $info -Stdout $log -Stderr (Join-Path $dir "group-$g.err")
  $groups+=@{Group=$g;Child=$child;LogPath=$log;StopPath=$stop}
 }
 $expected=$false
 try {Invoke-Server7Supervision -Groups $groups -GlobalStopPath (Join-Path $dir 'stop') -SummaryPath (Join-Path $dir 'summary.json') -MatchRegistryPath (Join-Path $dir 'matches.json') -TimeoutSeconds 15}
 catch {if($_.Exception.Message -ne 'GROUP_CRITICAL_FAILURE'){throw};$expected=$true}
 if(!$expected -or @($groups | Where-Object {!$_.Child.Process.HasExited}).Count){throw 'GLOBAL_STOP_FAILED'}
 'REAL_SUPERVISION_OFFLINE_FAILURE_TEST=PASS'
 'ORPHAN_PROCESSES=0'
} finally {Stop-Server7Groups -Groups $groups}
