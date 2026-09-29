$ErrorActionPreference='Stop'
$loadRoot=Join-Path $env:LOCALAPPDATA 'TeamFHO/DominoSwarm/LOAD'
if(Test-Path -LiteralPath $loadRoot){throw 'LOAD already exists: inspect before initialization'}
New-Item -ItemType Directory -Path $loadRoot | Out-Null
$sid=[Security.Principal.WindowsIdentity]::GetCurrent().User
$acl=[Security.AccessControl.DirectorySecurity]::new()
$acl.SetOwner($sid)
$acl.SetAccessRuleProtection($true,$false)
$rule=[Security.AccessControl.FileSystemAccessRule]::new($sid,'FullControl','ContainerInherit,ObjectInherit','None','Allow')
$acl.AddAccessRule($rule)
Set-Acl -LiteralPath $loadRoot -AclObject $acl
for($g=1;$g -le 5;$g++){New-Item -ItemType Directory -Path (Join-Path $loadRoot ('group-{0:00}' -f $g)) | Out-Null}
$actual=Get-Acl -LiteralPath $loadRoot
if(!$actual.AreAccessRulesProtected -or @($actual.Access).Count -ne 1){throw 'ACL verification failed'}
'LOAD_IDENTITY_PERMISSIONS=RESTRICTED'
'LOAD_GROUP_DIRECTORIES=5'
