$ErrorActionPreference='Stop'
. "$PSScriptRoot/SwarmLogReader.ps1"
$run=Join-Path $PSScriptRoot ('Generated/S605/tests-'+[guid]::NewGuid())
New-Item -ItemType Directory $run | Out-Null
$path=Join-Path $run 'log';$checks=0
function Check($ok,$name){if(!$ok){throw "FAIL:$name"};$script:checks++;Write-Output "${name}=PASS"}
Check ((Read-SwarmCompleteLog $path) -eq '') 'MISSING'
$writer=[IO.FileStream]::new($path,[IO.FileMode]::CreateNew,[IO.FileAccess]::Write,[IO.FileShare]::ReadWrite)
function Append([string]$s){$b=[Text.Encoding]::UTF8.GetBytes($s);$writer.Write($b);$writer.Flush()}
try {
 Check ((Read-SwarmCompleteLog $path) -eq '') 'EMPTY'
 Append "SWARM_CLIENT_CONNECTED alias=synthetic-a`n"
 $reproduced=$false
 try{[IO.File]::ReadAllText($path)|Out-Null}catch{$reproduced=($_.Exception.GetBaseException() -is [IO.IOException])}
 Check $reproduced 'SHARING_VIOLATION_REPRODUCED'
 Check ((Get-SwarmControlLog $path) -eq 'SWARM_CLIENT_CONNECTED alias=synthetic-a') 'OPEN_WRITER'
 Append 'SWARM_MATCH_FOUND alias=synthetic-a matchId=sample'
 Check ((Get-SwarmControlLog $path) -notmatch 'MATCH_FOUND') 'PARTIAL_CONTROL'
 Append "`n"
 Check ((Get-SwarmControlLog $path) -match 'MATCH_FOUND') 'APPEND'
 $writer.Write([byte[]]@(0xC3));$writer.Flush()
 Check ((Read-SwarmCompleteLog $path) -notmatch 'é') 'UTF8_PARTIAL'
 $writer.Write([byte[]]@(0xA9,10));$writer.Flush()
 Check ((Read-SwarmCompleteLog $path) -match 'é') 'UTF8_COMPLETED'
 Check ((Get-SwarmControlLog $path) -match 'MATCH_FOUND') 'TEMPORARY_EOF'
}finally{$writer.Dispose()}
Check ((Get-SwarmControlLog $path) -match 'MATCH_FOUND') 'WRITER_CLOSED'
[IO.File]::AppendAllText($path,"SWARM_MATCH_FOUND invalid-secret-value`n")
try{Get-SwarmControlLog $path;throw 'Expected parse failure'}catch{
 $diag=Write-SwarmSafeStop "$run/stop" $_.Exception LOG
 Check ($diag -contains 'ERROR_CATEGORY=LOG_PARSE') 'MALFORMED_CONTROL'
 Check (($diag -join '') -notmatch 'invalid-secret-value') 'REDACTED'
 Check ((Get-Content "$run/stop") -eq 'STOP') 'STOP_PROPAGATION'
}
$locked=[IO.FileStream]::new($path,[IO.FileMode]::Open,[IO.FileAccess]::Write,[IO.FileShare]::None)
try{
 try{Read-SwarmCompleteLog $path;throw 'Expected IO failure'}catch{
  $diag=Write-SwarmSafeStop "$run/io-stop" $_.Exception LOG
  Check ($diag -contains 'ERROR_CATEGORY=LOG_SHARING') 'REAL_SHARING_ERROR'
 }
}finally{$locked.Dispose()}
try{Read-SwarmCompleteLog $run;throw 'Expected directory failure'}catch{
 $diag=Write-SwarmSafeStop "$run/directory-stop" $_.Exception LOG
 Check ($diag -contains 'ERROR_CATEGORY=LOG_IO') 'REAL_IO_ERROR'
}
foreach($category in @('CHILD_EXIT','CHILD_TIMEOUT','CONTROL_PROTOCOL','OTHER')){
 $diag=Write-SwarmSafeStop "$run/class-stop" ([Exception]::new('secret-do-not-print')) $category
 Check (($diag -contains "ERROR_CATEGORY=$category") -and (($diag -join '') -notmatch 'secret-do-not-print')) $category
}
Write-Output "CONCURRENT_LOG_READER_CHECKS=$checks"
