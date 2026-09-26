function Read-SwarmCompleteLog {
 param([string]$Path)
 $stream=$null;$reader=$null;$snapshot=$null
 try {
  try {$stream=[IO.FileStream]::new($Path,[IO.FileMode]::Open,[IO.FileAccess]::Read,[IO.FileShare]::ReadWrite)}
  catch [IO.FileNotFoundException] {return ''}
  catch [IO.DirectoryNotFoundException] {return ''}
  # Bound this poll to the observed length; decode only newline-terminated bytes.
  $bytes=[byte[]]::new($stream.Length);$count=0
  while($count -lt $bytes.Length){$n=$stream.Read($bytes,$count,$bytes.Length-$count);if($n -eq 0){break};$count+=$n}
  $end=$count-1
  while($end -ge 0 -and $bytes[$end] -ne 10){$end--}
  if($end -lt 0){return ''}
  $snapshot=[IO.MemoryStream]::new($bytes,0,$end+1,$false)
  $reader=[IO.StreamReader]::new($snapshot,[Text.UTF8Encoding]::new($false,$true),$false)
  return $reader.ReadToEnd()
 }finally{
  if($null -ne $reader){$reader.Dispose()}
  if($null -ne $snapshot){$snapshot.Dispose()}
  if($null -ne $stream){$stream.Dispose()}
 }
}

function Get-SwarmControlLog {
 param([string]$Path)
 $text=Read-SwarmCompleteLog $Path
 $patterns=@{
  SWARM_CLIENT_CONNECTED='^SWARM_CLIENT_CONNECTED alias=\S+$'
  SWARM_MATCH_FOUND='^SWARM_MATCH_FOUND alias=\S+ matchId=[a-zA-Z0-9_-]+$'
  SWARM_MATCH_FINISHED='^SWARM_MATCH_FINISHED alias=\S+ matchId=[a-zA-Z0-9_-]+$'
  SWARM_CLIENT_FAILED='^SWARM_CLIENT_FAILED alias=\S+ category=[A-Z0-9_]+$'
  SWARM_START_FAILED='^SWARM_START_FAILED \(check configuration, identity provisioning and local services\)$'
  SWARM_STOP_REQUESTED='^SWARM_STOP_REQUESTED reason=(CLIENT_FAILED|LOCAL_SIGNAL)$'
 }
 $records=[Collections.Generic.List[string]]::new()
 foreach($line in ($text -split "`n")){
  $line=$line.TrimEnd("`r")
  foreach($prefix in $patterns.Keys){
   if($line.StartsWith($prefix)){
    if($line -cnotmatch $patterns[$prefix]){throw [IO.InvalidDataException]::new('Malformed control record')}
    $records.Add($line);break
   }
  }
 }
 return ($records -join "`n")
}

function Write-SwarmSafeStop {
 param([string]$StopPath,[Exception]$Exception,[string]$Context='OTHER')
 $cause=$Exception.GetBaseException();$category=$Context
 if($Context -eq 'LOG'){
  $category='LOG_IO'
  if($cause -is [IO.FileNotFoundException] -or $cause -is [IO.DirectoryNotFoundException]){$category='LOG_NOT_FOUND'}
  elseif($cause -is [IO.InvalidDataException] -or $cause -is [Text.DecoderFallbackException]){$category='LOG_PARSE'}
  elseif(($cause.HResult -band 65535) -in @(32,33)){$category='LOG_SHARING'}
 }
 if($category -notin @('LOG_NOT_FOUND','LOG_SHARING','LOG_IO','LOG_PARSE','CHILD_EXIT','CHILD_TIMEOUT','CONTROL_PROTOCOL','OTHER')){$category='OTHER'}
 Set-Content -LiteralPath $StopPath 'STOP'
 $diagnostic=@('COORDINATOR=STOP_REVIEW_LOGS',"ERROR_CATEGORY=$category","ERROR_TYPE=$($cause.GetType().Name)")
 Set-Content -LiteralPath "$StopPath.diagnostic" -Value $diagnostic
 $diagnostic
}
