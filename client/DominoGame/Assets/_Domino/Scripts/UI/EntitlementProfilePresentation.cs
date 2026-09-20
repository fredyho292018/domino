using System;
using System.Collections;
using System.Globalization;
using System.Threading;
using Domino.Infrastructure;
using Domino.Infrastructure.Api;
using Domino.Player;
using UnityEngine;
using UnityEngine.UI;

namespace Domino.UI
{
    // UI only. No local clock can authorize a feature or extend a trial.
    public sealed class EntitlementProfilePresentation : MonoBehaviour
    {
        PlayerService service; Text label; Coroutine boundary; bool refreshing;
        string welcomeUid;
        readonly CancellationTokenSource lifetime=new CancellationTokenSource();
        public string DisplayedText=>label?label.text:"";
        public void Initialize(PlayerService player,Text text) {
            if(service!=null)service.EntitlementsChanged-=Changed;
            service=player;label=text;service.EntitlementsChanged+=Changed;
            DominoLocalization.Bind(label,Text);Changed();
        }
        public void Bind(PlayerService player){Initialize(player,label);}
        string Text() {
            var envelope=service?.Entitlements;var value=envelope?.snapshot;
            if(envelope?.availability!="AVAILABLE"||value==null)return DominoLocalization.Get("premium.unavailable");
            if(value.plan=="FREE")return DominoLocalization.Get(value.trialConsumed?"premium.expired":"premium.free");
            if(!value.trialActive)return DominoLocalization.Get("premium.active");
            string date=DateTimeOffset.TryParse(value.trialEndsAt,CultureInfo.InvariantCulture,DateTimeStyles.RoundtripKind,out var end)
                ?end.ToLocalTime().ToString("g",CultureInfo.GetCultureInfo(DominoLocalization.Language)):"";
            return DominoLocalization.Get(welcomeUid==service.Player?.Uid?"premium.welcome":"premium.trial",date);
        }
        void Changed() {
            if(!this)return;
            if(service.Entitlements?.trialGranted==true)welcomeUid=service.Player?.Uid;
            if(boundary!=null)StopCoroutine(boundary);
            if(label)label.text=Text();
            var value=service.Entitlements?.snapshot;
            if(DateTimeOffset.TryParse(value?.serverTime,out var now)&&DateTimeOffset.TryParse(value?.nextTransitionAt,out var end)&&end>now)
                boundary=StartCoroutine(AtBoundary((float)Math.Min((end-now).TotalSeconds,int.MaxValue)));
        }
        IEnumerator AtBoundary(float seconds){yield return new WaitForSecondsRealtime(seconds);Refresh();}
        public async void Refresh() {
            if(refreshing||service?.Player==null||ApplicationServices.OnlineApi==null)return;
            refreshing=true;var uid=service.Player.Uid;
            try {
                var json=await ApplicationServices.OnlineApi.SendAsync("GET","player/entitlements",null,lifetime.Token);
                if(this)service.ReceiveEntitlements(uid,JsonUtility.FromJson<EntitlementSummaryDto>(json.ToString()));
            }catch {if(this)service.ReceiveEntitlements(uid,new EntitlementSummaryDto{availability="UNAVAILABLE"});}
            finally{refreshing=false;}
        }
        void OnApplicationPause(bool paused){if(!paused)Refresh();}
        void OnDestroy(){lifetime.Cancel();lifetime.Dispose();if(service!=null)service.EntitlementsChanged-=Changed;}
    }
}
