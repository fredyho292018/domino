$ErrorActionPreference = 'Stop'
$fixture = Get-Content (Join-Path $PSScriptRoot 'MembershipTrialTargetFixture.json') -Raw | ConvertFrom-Json -DateKind String
$catalog = Get-Content (Join-Path $PSScriptRoot 'MembershipCatalogFixture.json') -Raw | ConvertFrom-Json -DateKind String
$count = 0
function Check($condition, $name) { if (!$condition) { throw $name }; $script:count++ }
Check ($fixture.authority -eq 'ISOLATED_TARGET_CONTRACT_NOT_BACKEND') 'Fixture authority'
Check ($fixture.membershipPlan -eq 'FREE') 'Membership separate from trial'
Check ($fixture.selectedPlan -eq 'DIAMOND' -and $fixture.trialPlan -eq 'DIAMOND') 'Plan binding'
Check ($fixture.selectedBillingPeriod -eq 'YEARLY' -and $fixture.trialBillingPeriod -eq 'YEARLY') 'Annual uses existing YEARLY key'
$start = [DateTimeOffset]::Parse($fixture.trialStartedAt, [Globalization.CultureInfo]::InvariantCulture)
$end = [DateTimeOffset]::Parse($fixture.trialEndsAt, [Globalization.CultureInfo]::InvariantCulture)
$reminder = [DateTimeOffset]::Parse($fixture.reminderAt, [Globalization.CultureInfo]::InvariantCulture)
Check ($end -eq $start.AddDays($fixture.durationDays)) 'Fixture duration'
Check ($reminder -eq $end.AddDays(-$fixture.reminderBeforeEndDays)) 'Reminder relative to end'
Check ($end.ToString('yyyy-MM-dd') -eq '2026-10-11') 'Owner example trial end'
Check ($reminder.ToString('yyyy-MM-dd') -eq '2026-10-09') 'Owner example reminder'
Check (!$fixture.firstChargeAt) 'No first-charge authority'
Check ($fixture.chargeTodayMinorUnits -eq 0) 'Example zero today'
Check (!$fixture.billingAuthorityConfirmed -and !$fixture.storeProductId -and !$fixture.postTrialPrice) 'No fabricated store authority'
Check (!$fixture.reminderDeliveryImplemented) 'No notification delivery claim'
foreach ($locale in @('en','es')) {
    $localized = $catalog.$locale
    $plan = $localized.plans | Where-Object key -eq $fixture.trialPlan
    Check ($null -ne $plan) "$locale plan exists"
    $included = @($plan.features | Where-Object included | ForEach-Object featureKey)
    Check ($included.Count -gt 0) "$locale selected plan benefits"
    foreach ($key in $included) { Check ($null -ne ($localized.features | Where-Object key -eq $key)) "$locale localized feature" }
}
$source = Get-Content (Join-Path $PSScriptRoot '../DominoGame/Assets/_Domino/Scripts/UI/AppShell/Editor/MembershipTrialContractPreview.cs') -Raw
Check ($source -notmatch 'UnityWebRequest|HttpClient|Firebase|ActivateMembershipTrialAsync|AppMembershipApiSource') 'No real transport in prototype'
Check ($source -match 'confirm.SetEnabled\(false\)') 'Explicit confirmation cannot activate'
Check ($source -notmatch 'DateTime.Now|DateTime.UtcNow|DateTimeOffset.Now|DateTimeOffset.UtcNow') 'No client-clock timeline'
Check ($source -notmatch 'firstChargeAt|Example first charge|Primer cobro') 'No false first-charge copy'
Check ($source -match 'plan!="FRIENDS_AND_FAMILY"') 'Family confirmation blocked'
$production = Get-Content (Join-Path $PSScriptRoot '../DominoGame/Assets/_Domino/Scripts/UI/AppShell/SharedMembershipExperience.cs') -Raw
Check ($production -notmatch 'AddDays|DateTime.Now|DateTime.UtcNow|DateTimeOffset.Now|DateTimeOffset.UtcNow|2026-10-11|2026-10-09') 'Server timelines only'
"TARGET_CONTRACT_FIXTURE_CHECKS=${count}_PASS"
