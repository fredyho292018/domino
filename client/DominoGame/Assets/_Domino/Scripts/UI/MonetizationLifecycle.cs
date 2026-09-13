using System.Collections;
using Domino.Rewards;
using UnityEngine;

namespace Domino.UI
{
    public sealed class MonetizationLifecycle : MonoBehaviour
    {
        IMonetizationPolicyService policy;
        RoundRewardFlow flow;
        System.DateTimeOffset? refreshedDeadline;
        public void Initialize(IMonetizationPolicyService service,RoundRewardFlow rewardFlow){policy=service;flow=rewardFlow;StartCoroutine(Countdown());}
        IEnumerator Countdown()
        {
            while(true){yield return new WaitForSecondsRealtime(1);
                var observed=policy.Eligibility;
                if(observed?.NextEligibleAt==null)continue;
                flow.Refresh(); // Presentation countdown only; never a per-second HTTP request.
                if(policy.RemainingSeconds==0&&refreshedDeadline!=observed.NextEligibleAt){refreshedDeadline=observed.NextEligibleAt;_=policy.RefreshEligibilityAsync();}
            }
        }
        async void OnApplicationPause(bool paused)
        {
            if(paused||policy==null||policy.Fresh)return;
            await policy.RefreshAsync();await policy.RefreshEligibilityAsync();
        }
    }
}
