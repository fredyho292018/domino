$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot 'SwarmProcessLauncher.ps1')
$java=Join-Path $env:JAVA_HOME 'bin/java.exe'
$root=Join-Path $PSScriptRoot ('Generated/S604/tests-'+[guid]::NewGuid().ToString('N'))
$work=Join-Path $root 'space & (parentheses)';$ids=Join-Path $root 'TEST identities'
New-Item -ItemType Directory -Force (Join-Path $work 'gradle/wrapper'),$ids | Out-Null
[IO.File]::WriteAllText((Join-Path $work 'gradle/wrapper/gradle-wrapper.jar'),'fixture')
$script:checks=0
function Check($value,$label){if(!$value){throw $label};$script:checks++}
$gradle=@(':bot-swarm:coordinateIdentities','--args=--launch-check','--console=plain')
$info=New-SwarmProcessInfo $java $work $ids $gradle
Check ($info.FileName -eq $java) 'Executable with spaces'
Check ($info.WorkingDirectory -eq $work) 'Explicit working directory'
Check (!$info.UseShellExecute -and $info.CreateNoWindow) 'No shell or visible process'
Check ($info.ArgumentList.Count -eq 8) 'Separate wrapper arguments'
Check ($info.ArgumentList[4] -eq (Join-Path $work 'gradle/wrapper/gradle-wrapper.jar')) 'Literal special path'
Check ($info.ArgumentList[6] -eq '--args=--launch-check') 'Gradle argument separation'
Check ($info.Environment['DOMINO_SWARM_IDENTITIES_DIR'] -eq $ids) 'Identity path environment'
foreach($missing in @('executable','working','identity')){
 try {
  $exe=if($missing -eq 'executable'){Join-Path $root 'missing.exe'}else{$java}
  $wd=if($missing -eq 'working'){Join-Path $root 'missing'}else{$work}
  $id=if($missing -eq 'identity'){Join-Path $root 'missing'}else{$ids}
  $null=New-SwarmProcessInfo $exe $wd $id $gradle
  throw 'Expected controlled failure'
 }catch{Check ($_.Exception.Message.StartsWith('LAUNCH_FAILED:')) "Missing $missing controlled"}
}
$probe=Join-Path $work 'ArgProbe.java'
@'
import java.util.*;
class ArgProbe { public static void main(String[] a) throws Exception {
 if(a.length==1&&a[0].equals("sleep")){Thread.sleep(60000);return;}
 System.out.println(Base64.getEncoder().encodeToString(System.getProperty("user.dir").getBytes("UTF-8")));
 for(String s:a)System.out.println(Base64.getEncoder().encodeToString(s.getBytes("UTF-8")));
 System.out.println("X".repeat(150000));System.err.println("Y".repeat(150000));
}}
'@ | Set-Content $probe
$inputArgs=@('plain','path with spaces','C:\Users\fixture\AppData\Local\TeamFHO\DominoSwarm\TEST','a&(b)','ends\','literal"quote')
$info.ArgumentList.Clear();$info.ArgumentList.Add($probe);foreach($item in $inputArgs){$info.ArgumentList.Add($item)}
$child=Start-SwarmChild $info (Join-Path $root 'stdout') (Join-Path $root 'stderr')
try {
 Check ($child.Process.WaitForExit(45000)) 'Output deadlock timeout'
 Complete-SwarmChild $child
 Check ($child.Process.ExitCode -eq 0) 'Probe exit'
 $lines=[IO.File]::ReadAllLines((Join-Path $root 'stdout'))
 Check ([Text.Encoding]::UTF8.GetString([Convert]::FromBase64String($lines[0])) -eq $work) 'Actual child working directory'
 for($i=0;$i -lt $inputArgs.Count;$i++){Check ([Text.Encoding]::UTF8.GetString([Convert]::FromBase64String($lines[$i+1])) -ceq $inputArgs[$i]) "Argument $i preserved"}
 Check ((Get-Item (Join-Path $root 'stderr')).Length -gt 150000) 'Concurrent stderr drain'
}finally{Complete-SwarmChild $child -Stop;$child.Process.Dispose()}
$info.ArgumentList.Clear();$info.ArgumentList.Add($probe);$info.ArgumentList.Add('sleep')
$child=Start-SwarmChild $info (Join-Path $root 'sleep.stdout') (Join-Path $root 'sleep.stderr')
try{Complete-SwarmChild $child -Stop;Check $child.Process.HasExited 'STOP terminates child'}finally{$child.Process.Dispose()}
Write-Output "S604_LAUNCHER_CHECKS=$checks PASS"
Write-Output 'REAL_CREDENTIALS_USED=NO'
