#if UNITY_EDITOR
using System;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Domino.Ads;
using Domino.Client;
using Domino.Identity;
using Domino.Infrastructure;
using Domino.Infrastructure.Api;
using Domino.Infrastructure.Firebase;
using Domino.Rewards;
using Domino.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Domino.Editor
{
    // Opt-in isolated Editor fixture. No endpoint, production assembly, Firebase or real ad call.
    public static class RoundRewardValidation
    {
        const string Flag="Domino.H5Validation";
        static int errors, checks;
        static Fixture fixture;
        sealed class FakeIdentity : IFirebaseClient, IAuthTokenProvider
        {
            public Task<string> CheckDependenciesAsync()=>Task.FromResult("Available");
            public void InitializeApp(){}
            public PlayerIdentity GetCurrentUser()=>new PlayerIdentity("h5-fixture",true);
            public Task<PlayerIdentity> SignInAnonymouslyAsync()=>throw new Exception("NO_REAL_GUEST");
            public Task<string> GetIdTokenAsync(bool refresh,CancellationToken token)=>throw new Exception("NO_NETWORK_TOKEN");
        }
        sealed class Fixture : IDominoApiClient,IRewardIntentApi,IRewardConsumptionApi
        {
            public long Coins; public int Credits, Creates, Consumes;
            public string Id=Guid.NewGuid().ToString(), Status="ISSUED";
            public bool IsAvailable=>true;
            public Task<PlayerBootstrapResponseDto> BootstrapAsync(string language,CancellationToken token)=>Task.FromResult(new PlayerBootstrapResponseDto {
                player=new PlayerResponseDto {uid="h5-fixture",accountType="GUEST",displayName="Fredy",language="es",status="ACTIVE"},wallet=new WalletResponseDto {coins=Coins}});
            public Task<PlayerBootstrapResponseDto> UpdateDisplayNameAsync(string value,CancellationToken token)=>BootstrapAsync("es",token);
            RewardIntentReceipt Receipt()=>new RewardIntentReceipt(Id,Status,DateTimeOffset.UtcNow.AddMinutes(10));
            public Task<RewardIntentReceipt> CreateAsync(CancellationToken token){ Creates++; Id=Guid.NewGuid().ToString(); Status="ISSUED"; return Task.FromResult(Receipt()); }
            public async Task<RewardIntentReceipt> StatusAsync(string id,CancellationToken token){ await Task.Delay(700,token); Status="VERIFIED"; return Receipt(); }
            public Task<RewardIntentReceipt> PendingAsync(CancellationToken token)=>Task.FromResult(Status=="VERIFIED"?Receipt():null);
            public async Task<RewardConsumeReceipt> ConsumeRewardAsync(string id,CancellationToken token)
            { await Task.Delay(300,token); Consumes++; if(Status!="CONSUMED"){Coins+=10;Credits++;Status="CONSUMED";}return new RewardConsumeReceipt(Id,10,Coins); }
        }
        sealed class FakeAd : IRewardedAdsService
        {
            readonly RewardVerificationService verification;
            public FakeAd(RewardVerificationService service){verification=service;}
            public RewardedState State{get;private set;}=RewardedState.READY;
            public bool IsAvailable=>State==RewardedState.READY;
            public void SetState(RewardedState state) { State=state;RewardedStateChanged?.Invoke(state); }
            public event Action<RewardedState> RewardedStateChanged;
            public event Action<bool> RewardedAvailabilityChanged;
            public event Action<RewardedCompletionResult> RewardEarned;
            public Task InitializeAsync()=>Task.CompletedTask; public Task LoadRewardedAsync()=>Task.CompletedTask;
            public void PreloadIfNeeded(){}
            public async Task<RewardedShowResult> ShowRewardedAsync()
            {
                var receipt=await verification.CreateAsync(default);
                Check(receipt.Status=="ISSUED","intent before show");
                State=RewardedState.SHOWING;RewardedStateChanged?.Invoke(State);
                await Task.Delay(500);
                verification.ClientEarned(receipt.IntentId); State=RewardedState.READY;RewardedStateChanged?.Invoke(State);
                return RewardedShowResult.Earned;
            }
            public void Dispose(){}
        }
        [InitializeOnLoadMethod] static void Register()
        {
            if(!SessionState.GetBool(Flag,false))return;
            fixture=new Fixture();
            ApplicationServices.ValidationFirebaseFactory=()=>new FakeIdentity();
            ApplicationServices.ValidationPlayerApiFactory=()=>fixture;
            ApplicationServices.ValidationRewardIntentApiFactory=()=>fixture;
            ApplicationServices.ValidationRewardedFactory=service=>new FakeAd(service);
            Application.logMessageReceived+=(_,__,type)=>{if(type==LogType.Error||type==LogType.Exception)errors++;};
            EditorApplication.playModeStateChanged+=state=>{if(state==PlayModeStateChange.EnteredPlayMode)Validate();};
        }
        public static void Run()
        {
            if(!Application.isBatchMode||!Application.dataPath.Replace('\\','/').Contains("/Validation/Generated/"))throw new Exception("ISOLATED_EDITOR_ONLY");
            SessionState.SetBool(Flag,true);Register();
            EditorSceneManager.OpenScene("Assets/_Domino/Scenes/DominoClient.unity");
            Phase1Validation.OpenPortraitPreview();EditorApplication.isPlaying=true;
        }
        static void Check(bool condition,string label){checks++;if(!condition)throw new Exception(label);}
        static async Task Until(Func<bool> condition,int seconds){var end=DateTime.UtcNow.AddSeconds(seconds);while(!condition()){if(DateTime.UtcNow>end)throw new Exception("H5_WAIT_TIMEOUT");await Task.Delay(30);}}
        static async void Validate()
        {
            var output=Path.GetFullPath(Path.Combine(Application.dataPath,"../../../Validation/Generated/H5"));Directory.CreateDirectory(output);
            string report;
            try
            {
                var controller=UnityEngine.Object.FindFirstObjectByType<DominoClientController>();
                await Until(()=>controller.Menu!=null&&ApplicationServices.Player.HasConfirmedSnapshots,40);
                controller.StartMatch(GameModeDefinition.TeamMatch);
                var end=DateTime.UtcNow.AddSeconds(200);
                Time.timeScale=8;
                while(!controller.View.RoundPresentationFinished)
                {
                    if(DateTime.UtcNow>end)throw new Exception("ROUND_TIMEOUT");
                    if(controller.AcceptingInput) foreach(var tile in controller.View.LocalTiles) if(controller.State.CanPlay(tile.Tile)){controller.Select(tile);controller.PlaySelected();break;}
                    await Task.Delay(20);
                }
                Time.timeScale=1;
                var panel=controller.View.RoundRewardPanel;
                Check(panel!=null&&panel.ContinueButton.interactable,"canonical round continue");
                Check(panel.WatchButton.interactable,"CTA available");
                Check(fixture.Creates==0,"no auto show");
                ((FakeAd)ApplicationServices.Rewarded).SetState(RewardedState.DISABLED);
                Check(!panel.WatchButton.gameObject.activeSelf && panel.ContinueButton.interactable,"disabled hides CTA, preserves continue");
                ((FakeAd)ApplicationServices.Rewarded).SetState(RewardedState.READY);
                int[,] sizes={{1080,1920},{1080,2160},{1080,2340},{1080,2400},{1080,2520},{1170,2532},{1284,2778},{1600,2560},{1536,2048}};
                var resize=typeof(Phase1Validation).GetMethod("ResizeGameView",BindingFlags.Static|BindingFlags.NonPublic);
                foreach(var lang in new[]{"es","en"})
                {
                    DominoLocalization.Select(lang);await Task.Delay(100);
                    for(int i=0;i<sizes.GetLength(0);i++)
                    {
                        resize.Invoke(null,new object[]{sizes[i,0],sizes[i,1]});await Task.Delay(120);
                        var corners=new Vector3[4];panel.GetComponent<RectTransform>().GetWorldCorners(corners);
                        foreach(var corner in corners)Check(Screen.safeArea.Contains(RectTransformUtility.WorldToScreenPoint(null,corner)),"panel safe area");
                        var panelBounds = new Rect(corners[0],corners[2]-corners[0]);
                        for(int seat=0;seat<4;seat++) {
                            var playerCorners=new Vector3[4]; var playerRect=controller.View.PlayerRect(seat);
                            var visiblePanel=playerRect.GetComponent<PlayerView>().PanelRect;
                            if (!visiblePanel.gameObject.activeInHierarchy) visiblePanel=(RectTransform)playerRect.Find("Name");
                            visiblePanel.GetWorldCorners(playerCorners);
                            Check(!panelBounds.Overlaps(new Rect(playerCorners[0],playerCorners[2]-playerCorners[0])),"result preserves player panels");
                        }
                        Check(panel.ContinueButton.interactable,"continue all sizes");
                        var hits = new System.Collections.Generic.List<UnityEngine.EventSystems.RaycastResult>();
                        var pointer = new UnityEngine.EventSystems.PointerEventData(UnityEngine.EventSystems.EventSystem.current) {
                            position=RectTransformUtility.WorldToScreenPoint(null,panel.ContinueButton.transform.position) };
                        UnityEngine.EventSystems.EventSystem.current.RaycastAll(pointer,hits);
                        Check(hits.Count>0 && hits[0].gameObject.transform.IsChildOf(panel.ContinueButton.transform),"continue not obscured");
                    }
                    ScreenCapture.CaptureScreenshot(Path.Combine(output,"available-"+lang+".png"));await Task.Delay(150);
                }
                panel.WatchButton.onClick.Invoke();panel.WatchButton.onClick.Invoke();
                Check(ApplicationServices.Player.Wallet.Coins==0,"no optimistic credit");
                await Until(()=>ApplicationServices.RoundRewards.State==RoundRewardState.REWARDED,10);
                Check(fixture.Creates==1&&fixture.Credits==1&&fixture.Consumes==1,"one grant");
                Check(ApplicationServices.Player.Wallet.Coins==10,"confirmed snapshot");
                Check(panel.BalanceText.Contains("10")&&controller.Menu.Profile.DisplayedCoins.Contains("10"),"round and profile same balance");
                Check(!panel.WatchButton.interactable&&panel.ContinueButton.interactable,"reward locks only ad");
                ScreenCapture.CaptureScreenshot(Path.Combine(output,"rewarded.png"));await Task.Delay(200);
                panel.ContinueButton.onClick.Invoke();Check(!controller.View.RoundPresentationFinished,"continue advances round");
                Check(errors==0,"console errors");
                report=$"PASS\nH5_UI_CHECKS={checks}\nCONSOLE_ERRORS={errors}\nPROFILE_BALANCE=10\nROUND_BALANCE=10\nREAL_FIRESTORE=NOT_RUN\nSSV=TEST_DOUBLE\nREAL_AD_REQUESTS=0";
            }
            catch(Exception error){report="FAIL\n"+error.Message+"\nCONSOLE_ERRORS="+errors;}
            File.WriteAllText(Path.Combine(output,"ui-result.txt"),report);Debug.Log(report);
            SessionState.SetBool(Flag,false);EditorApplication.Exit(report.StartsWith("PASS")?0:1);
        }
    }
}
#endif
