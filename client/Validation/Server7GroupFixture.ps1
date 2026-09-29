param([string]$StopPath,[int]$Fail=0)
'READY=20'
'AUTHENTICATED=20'
'ACTIVE=20'
for($i=1;$i -le 20;$i++){"SWARM_CLIENT_CONNECTED alias=Fixture$i"}
'DOMINO BOT SWARM mode=PARTNERS_2V2_ONLINE states={IDLE=20} activeMatches=0 metrics={}'
if($Fail -eq 1){Start-Sleep -Milliseconds 400;'CRITICAL_FAILURE=YES';'SWARM_CLIENT_FAILED alias=Fixture1 category=FIXTURE_FAILURE'}
$deadline=(Get-Date).AddSeconds(45)
while(!(Test-Path -LiteralPath $StopPath)){
 if((Get-Date) -gt $deadline){exit 2}
 Start-Sleep -Milliseconds 50
}
'ACTIVE=0'
exit 0

