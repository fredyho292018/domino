Set-StrictMode -Version Latest
function New-SwarmProcessInfo {
 param([string]$JavaExecutable,[string]$WorkingDirectory,[string]$IdentityDirectory,[string[]]$GradleArguments)
 if(!(Test-Path -LiteralPath $JavaExecutable -PathType Leaf)){throw 'LAUNCH_FAILED:EXECUTABLE_MISSING'}
 if(!(Test-Path -LiteralPath $WorkingDirectory -PathType Container)){throw 'LAUNCH_FAILED:WORKING_DIRECTORY_MISSING'}
 if(!(Test-Path -LiteralPath $IdentityDirectory -PathType Container)){throw 'LAUNCH_FAILED:IDENTITY_DIRECTORY_MISSING'}
 $wrapper=Join-Path $WorkingDirectory 'gradle/wrapper/gradle-wrapper.jar'
 if(!(Test-Path -LiteralPath $wrapper -PathType Leaf)){throw 'LAUNCH_FAILED:WRAPPER_MISSING'}
 $info=[Diagnostics.ProcessStartInfo]::new()
 $info.FileName=[IO.Path]::GetFullPath($JavaExecutable)
 $info.WorkingDirectory=[IO.Path]::GetFullPath($WorkingDirectory)
 $info.UseShellExecute=$false;$info.CreateNoWindow=$true
 $info.RedirectStandardOutput=$true;$info.RedirectStandardError=$true
 foreach($item in @('-Xmx64m','-Xms64m','-Dorg.gradle.appname=gradlew','-jar',$wrapper)+$GradleArguments){$info.ArgumentList.Add($item)}
 $info.Environment['DOMINO_SWARM_IDENTITIES_DIR']=[IO.Path]::GetFullPath($IdentityDirectory)
 return $info
}
function Start-SwarmChild {
 param([Diagnostics.ProcessStartInfo]$Info,[string]$Stdout,[string]$Stderr)
 $child=[Diagnostics.Process]::new();$child.StartInfo=$Info
 $outStream=$null;$errStream=$null;$didStart=$false
 try {
  $outStream=[IO.FileStream]::new($Stdout,[IO.FileMode]::CreateNew,[IO.FileAccess]::Write,[IO.FileShare]::ReadWrite,1,$true)
  $errStream=[IO.FileStream]::new($Stderr,[IO.FileMode]::CreateNew,[IO.FileAccess]::Write,[IO.FileShare]::ReadWrite,1,$true)
  if(!$child.Start()){throw 'LAUNCH_FAILED:PROCESS_START'};$didStart=$true
  return [pscustomobject]@{Process=$child;OutStream=$outStream;ErrStream=$errStream;OutTask=$child.StandardOutput.BaseStream.CopyToAsync($outStream);ErrTask=$child.StandardError.BaseStream.CopyToAsync($errStream);Closed=$false}
 }catch{
  if($null -ne $outStream){$outStream.Dispose()};if($null -ne $errStream){$errStream.Dispose()}
  if($didStart -and !$child.HasExited){$child.Kill($true);$child.WaitForExit()};$child.Dispose();throw 'LAUNCH_FAILED:PROCESS_START'
 }
}
function Complete-SwarmChild {
 param($Child,[switch]$Stop)
 if($null -eq $Child -or $Child.Closed){return}
 try {
  if($Stop -and !$Child.Process.WaitForExit(8000)){$Child.Process.Kill($true)}
  $Child.Process.WaitForExit()
  $null=$Child.OutTask.GetAwaiter().GetResult();$null=$Child.ErrTask.GetAwaiter().GetResult()
 }finally{$Child.OutStream.Dispose();$Child.ErrStream.Dispose();$Child.Closed=$true}
}
