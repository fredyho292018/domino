using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Domino.Catalog;
using Domino.UI;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace Domino.Replay
{
    public sealed class HistoryReplayView : MonoBehaviour
    {
        readonly CancellationTokenSource lifetime=new CancellationTokenSource();
        ReplayClient client;DominoTileView tilePrefab;PlayerView playerPrefab;Action closed;
        RectTransform safe,panel,list,toolbar;GameObject background;Text message,title,eventLabel,position,result;
        Button more,play,perspectiveButton,speedButton;Slider slider;BoardView board;
        JObject manifest;ReplayTimeline timeline;string cursor;bool loading,playing,rendering;int sequence,perspective=-1;
        readonly List<JObject> history=new List<JObject>();float speed=1,elapsed;
        public ReplayTimeline Timeline=>timeline;
        public BoardView Board=>board;
        public int Sequence=>sequence;
        public float PlaybackSpeed=>speed;
        public void Initialize(ReplayClient source,DominoTileView tiles,PlayerView players,Action onClose)
        {
            client=source;tilePrefab=tiles;playerPrefab=players;closed=onClose;
            var canvas=gameObject.AddComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.sortingOrder=50;
            BoardView.ConfigureCanvas(gameObject);gameObject.AddComponent<GraphicRaycaster>();
            var bg=UiKit.Rect("History background",transform,Vector2.zero,Vector2.zero);bg.anchorMin=Vector2.zero;bg.anchorMax=Vector2.one;bg.sizeDelta=Vector2.zero;
            bg.gameObject.AddComponent<SoftBackdrop>();background=bg.gameObject;
            safe=UiKit.Rect("Safe area",transform,Vector2.zero,Vector2.zero);safe.gameObject.AddComponent<SafeArea>();
            title=UiKit.LLabel("Title",safe,"history.title",new Vector2(600,64),Vector2.zero,32,UiKit.Cream);
            var back=UiKit.LButton("Back",safe,"menu.back",new Vector2(140,64),Vector2.zero,UiKit.Hex("254B47"),Back);
            back.GetComponent<RectTransform>().anchorMin=back.GetComponent<RectTransform>().anchorMax=new Vector2(0,1);
            back.GetComponent<RectTransform>().anchoredPosition=new Vector2(90,-48);
            panel=UiKit.Rect("History panel",safe,new Vector2(900,1240),Vector2.zero);
            message=UiKit.Label("Message",panel,"",new Vector2(850,100),new Vector2(0,520),26,UiKit.Cream);
            var viewport=UiKit.Rect("History viewport",panel,new Vector2(900,900),new Vector2(0,-10));
            viewport.gameObject.AddComponent<RectMask2D>();viewport.gameObject.AddComponent<Image>().color=Color.clear;
            list=UiKit.Rect("History rows",viewport,new Vector2(900,0),Vector2.zero);list.anchorMin=list.anchorMax=new Vector2(.5f,1);list.pivot=new Vector2(.5f,1);
            var scroll=viewport.gameObject.AddComponent<ScrollRect>();scroll.viewport=viewport;scroll.content=list;scroll.horizontal=false;scroll.movementType=ScrollRect.MovementType.Clamped;
            more=UiKit.LButton("Load more",panel,"history.more",new Vector2(380,70),new Vector2(0,-550),UiKit.Hex("397566"),()=>LoadHistory(false));
            LoadHistory(true);StyleControls();
        }
        async void LoadHistory(bool reset)
        {
            if(loading)return;loading=true;more.interactable=false;DominoLocalization.Set(message,"system.loading");
            try {
                if(reset){history.Clear();cursor=null;}
                var page=await client.History(cursor,lifetime.Token);if(!this)return;
                history.AddRange(((JArray)page["items"]).Cast<JObject>());cursor=(string)page["nextCursor"];
                DrawHistory();DominoLocalization.Set(message,history.Count==0?"history.empty":(bool?)page["historyLimited"]==true?"premium.history_limited":"history.subtitle");
                more.gameObject.SetActive(cursor!=null);more.interactable=true;
            }catch(OperationCanceledException){}
            catch(Exception) {
                if(this){DominoLocalization.Set(message,"history.error");more.gameObject.SetActive(true);more.interactable=true;DominoLocalization.Set(more.GetComponentInChildren<Text>(),"system.retry");}}
            finally{loading=false;}
        }
        void DrawHistory()
        {
            foreach(Transform child in list)Destroy(child.gameObject);
            for(int i=0;i<history.Count;i++) {
                var item=history[i];var h=item["history"];var row=UiKit.Panel("Match "+(string)h["matchId"],list,new Vector2(870,240),new Vector2(0,-125-i*260),UiKit.Hex("1D403E")).rectTransform;
                row.anchorMin=row.anchorMax=new Vector2(.5f,1);
                string names=Names(item["participants"] as JArray,item["teams"] as JArray);
                var label=UiKit.Label("Summary",row,"",new Vector2(810,150),new Vector2(0,30),24,UiKit.Cream);
                DominoLocalization.Bind(label,()=>DominoLocalization.Get((string)h["modeKey"]=="DUEL_1V1"?"mode.duel.title":"mode.partners_online.title")+"\n"+names+"\n"+string.Join(" — ",h["score"].Values<int>())+" · "+DominoLocalization.Get((string)h["result"]=="WIN"?"result.victory":"result.defeat")+"\n"+ReadTimestamp(h["finishedAt"]).ToLocalTime().ToString("g",System.Globalization.CultureInfo.GetCultureInfo(DominoLocalization.Language)));
                bool available=(bool)item["replayAvailable"];
                bool locked=(bool?)item["premiumLocked"]==true;
                UiKit.LButton("View match",row,locked?"premium.locked":available?"history.view":"replay.unavailable",new Vector2(400,62),new Vector2(0,-80),UiKit.Hex("397566"),()=>{
                    if(locked)DominoLocalization.Set(message,"premium.replay_limited");else Open((string)h["matchId"]);
                });
            }
            list.sizeDelta=new Vector2(900,history.Count*260);StyleControls();
        }
        static DateTimeOffset ReadTimestamp(JToken token) {
            if(token is JValue value && value.Value is DateTime date)return new DateTimeOffset(date);
            return DateTimeOffset.Parse((string)token,System.Globalization.CultureInfo.InvariantCulture,System.Globalization.DateTimeStyles.RoundtripKind);
        }
        static string Names(JArray players,JArray teams)
        {
            if(players==null||players.Count==0)return "";
            string Name(int seat)=>(string)players.First(p=>(int)p["seat"]==seat)["displayNameSnapshot"];
            if(teams!=null&&teams.Count==2)return string.Join(" + ",teams[0].Values<int>().Select(Name))+"\nvs\n"+string.Join(" + ",teams[1].Values<int>().Select(Name));
            return string.Join(" vs ",players.Select(p=>(string)p["displayNameSnapshot"]));
        }
        public async void Open(string id)
        {
            if(loading)return;loading=true;DominoLocalization.Set(message,"system.loading");
            try {
                manifest=await client.Manifest(id,lifetime.Token);if(!this)return;
                if((bool?)manifest["replayAvailable"]!=true){DominoLocalization.Set(message,"replay.incomplete");return;}
                ShowDetail();
            }catch(OperationCanceledException){}catch{if(this)DominoLocalization.Set(message,"history.error");}finally{loading=false;}
        }
        void ShowDetail()
        {
            foreach(Transform child in list)Destroy(child.gameObject);more.gameObject.SetActive(false);
            var text=UiKit.Label("Match detail",list,"",new Vector2(860,660),new Vector2(0,-340),26,UiKit.Cream);text.rectTransform.anchorMin=text.rectTransform.anchorMax=new Vector2(.5f,1);
            DominoLocalization.Bind(text,()=>Names((JArray)manifest["participants"],(JArray)manifest["teams"])+"\n\n"+string.Join(" — ",manifest["finalScore"].Values<int>())+"\n"+DominoLocalization.Get("replay.rounds",((JArray)manifest["rounds"]).Count)+"\n"+DominoLocalization.Get("rules.target_score",(int)JObject.Parse((string)manifest["ruleSnapshot"]["effectiveModeJson"])["ruleSet"]["targetScore"]));
            var button=UiKit.LButton("Replay",list,"replay.title",new Vector2(460,76),new Vector2(0,-720),UiKit.Hex("397566"),LoadReplay);
            button.GetComponent<RectTransform>().anchorMin=button.GetComponent<RectTransform>().anchorMax=new Vector2(.5f,1);
            list.sizeDelta=new Vector2(900,820);DominoLocalization.Set(message,"history.detail");StyleControls();
        }
        public async void LoadReplay()
        {
            if(loading)return;loading=true;DominoLocalization.Set(message,"system.loading");
            try {
                timeline=await client.Load(manifest,lifetime.Token);if(!this)return;
                var snapshot=manifest["ruleSnapshot"];
                var catalog=new JObject {["catalogSchemaVersion"]=1,["catalogVersion"]=snapshot["catalogVersion"].DeepClone(),["modes"]=new JArray(JObject.Parse((string)snapshot["effectiveModeJson"]))};
                var config=new GameCatalogCodec().Read(catalog.ToString()).Modes.Single().RuleSet.Configuration;
                // Screen-space gameplay canvas must be a root canvas, not inherit this overlay's 100x100 child rect.
                board=new GameObject("Replay table",typeof(RectTransform)).AddComponent<BoardView>();
                board.Initialize(tilePrefab,playerPrefab,config);board.GetComponent<Canvas>().sortingOrder=40;board.ConfigureReplay();
                background.SetActive(false);panel.gameObject.SetActive(false);DominoLocalization.Set(title,"replay.title");
                CreateControls();Seek(0);
            }catch(OperationCanceledException){}catch{if(this){timeline=null;DominoLocalization.Set(message,"replay.incomplete");}}finally{loading=false;}
        }
        void CreateControls()
        {
            toolbar=UiKit.Rect("Replay controls",safe,new Vector2(960,320),Vector2.zero);toolbar.anchorMin=toolbar.anchorMax=new Vector2(.5f,0);
            position=UiKit.Label("Position",toolbar,"",new Vector2(900,40),new Vector2(0,140),22,UiKit.Cream);
            eventLabel=UiKit.Label("Event",toolbar,"",new Vector2(900,40),new Vector2(0,102),22,UiKit.Gold);
            var track=UiKit.Panel("Timeline",toolbar,new Vector2(840,28),new Vector2(0,60),UiKit.Hex("254B47"));
            var thumb=UiKit.Panel("Handle",track.transform,new Vector2(34,40),Vector2.zero,UiKit.Gold);
            slider=track.gameObject.AddComponent<Slider>();slider.handleRect=thumb.rectTransform;slider.targetGraphic=thumb;slider.minValue=0;slider.maxValue=timeline.Count;slider.wholeNumbers=true;
            slider.onValueChanged.AddListener(v=>Seek((int)v));
            UiKit.LButton("Start",toolbar,"replay.start",new Vector2(164,62),new Vector2(-370,4),UiKit.Hex("254B47"),()=>Seek(0));
            UiKit.LButton("Previous",toolbar,"replay.previous",new Vector2(164,62),new Vector2(-185,4),UiKit.Hex("254B47"),()=>Seek(sequence-1));
            play=UiKit.LButton("Play pause",toolbar,"replay.play",new Vector2(164,62),new Vector2(0,4),UiKit.Hex("397566"),()=>SetPlaying(!playing));
            UiKit.LButton("Next",toolbar,"replay.next",new Vector2(164,62),new Vector2(185,4),UiKit.Hex("254B47"),()=>Seek(sequence+1));
            UiKit.LButton("End",toolbar,"replay.end",new Vector2(164,62),new Vector2(370,4),UiKit.Hex("254B47"),()=>Seek(timeline.Count));
            perspectiveButton=UiKit.LButton("Perspective",toolbar,"replay.table",new Vector2(400,60),new Vector2(-240,-70),UiKit.Hex("254B47"),NextPerspective);
            speedButton=UiKit.LButton("Speed",toolbar,"replay.play",new Vector2(260,60),new Vector2(220,-70),UiKit.Hex("254B47"),()=>{speed=speed==.5f?1:speed==1?2:.5f;RefreshControls();});
            UiKit.LButton("Previous round",toolbar,"replay.previous_round",new Vector2(400,60),new Vector2(-240,-140),UiKit.Hex("254B47"),()=>Seek(timeline.RoundStarts.LastOrDefault(n=>n<sequence)));
            UiKit.LButton("Next round",toolbar,"replay.next_round",new Vector2(400,60),new Vector2(240,-140),UiKit.Hex("254B47"),()=>Seek(timeline.RoundStarts.FirstOrDefault(n=>n>sequence) is int n&&n>0?n:timeline.Count));
            result=UiKit.Label("Historical result",safe,"",new Vector2(920,100),Vector2.zero,22,UiKit.Cream);
            StyleControls();
        }
        public void Seek(int target) {if(timeline==null)return;SetPlaying(false);StartRender(Mathf.Clamp(target,0,timeline.Count),false);}
        public void SetPlaying(bool value){playing=value;elapsed=0;if(play)DominoLocalization.Set(play.GetComponentInChildren<Text>(),playing?"replay.pause":"replay.play");}
        public void SelectPerspective(int seat){perspective=seat;Seek(sequence);}
        void NextPerspective(){perspective++;if(perspective>=((JArray)manifest["participants"]).Count)perspective=-1;Seek(sequence);}
        void StartRender(int target,bool animate)
        {
            if(rendering){board.StopAllCoroutines();StopAllCoroutines();}
            sequence=target;StartCoroutine(Render(animate));
        }
        IEnumerator Render(bool animate)
        {
            rendering=true;var state=timeline.Seek(sequence);yield return board.RenderReplay(state,perspective,animate,speed);RefreshControls();rendering=false;
        }
        void RefreshControls()
        {
            slider.SetValueWithoutNotify(sequence);var s=timeline.Seek(sequence).Data;
            DominoLocalization.Set(position,"replay.position",sequence,timeline.Count,(int)s["round"]);
            DominoLocalization.Bind(eventLabel,()=> {
                string type=(string)s["eventType"];var p=s["eventPayload"];int? seat=(int?)p["seat"];
                string name=seat.HasValue?(string)manifest["participants"][seat.Value]["displayNameSnapshot"]:"";
                if(type=="TILE_PLAYED")return DominoLocalization.Get("replay.played",name,(int)p["tile"]["sideA"],(int)p["tile"]["sideB"]);
                if(type=="PLAYER_PASSED")return DominoLocalization.Get("replay.passed",name);
                if(type=="TURN_STARTED"||type=="TURN_CHANGED")return DominoLocalization.Get("replay.turn",name);
                if(type=="STARTER_SELECTION_PRIVATE")return DominoLocalization.Get("replay.selection",string.Join(" · ",p["result"]["chosenTiles"].Select(t=>"["+(int)t["sideA"]+"|"+(int)t["sideB"]+"]")));
                return DominoLocalization.Get("replay.event."+(type==""?"INITIAL":type));
            });
            DominoLocalization.Set(speedButton.GetComponentInChildren<Text>(),"replay.speed",speed);
            if(perspective<0)DominoLocalization.Set(perspectiveButton.GetComponentInChildren<Text>(),"replay.table");
            else DominoLocalization.Bind(perspectiveButton.GetComponentInChildren<Text>(),()=>"✓ "+(string)manifest["participants"][perspective]["displayNameSnapshot"]);
            DominoLocalization.Bind(result,()=> {
                if(!(s["roundResult"] is JObject r))return "";
                int? winner=(int?)r["winnerSeat"];
                string name=winner.HasValue?(string)manifest["participants"][winner.Value]["displayNameSnapshot"]:DominoLocalization.Get("result.draw");
                return name+" · "+DominoLocalization.Get("replay.round_result",DominoLocalization.Get((string)r["finishType"]=="BLOCKED"?"replay.blocked":(string)r["finishType"]=="CAPICUA"?"replay.capicua":"replay.normal"),(int)r["scoreAwarded"])+
                    "\n"+DominoLocalization.Get("replay.pips",string.Join(" / ",r["remainingPips"].Values<int>()))+
                    ((string)s["phase"]=="MATCH_FINISHED"?" · "+DominoLocalization.Get("result.game_over"):"");
            });
        }
        void StyleControls()
        {
            // Give dynamic-font button labels their full touch-target height at every display scale.
            foreach(var label in GetComponentsInChildren<Text>())if(label.name=="Label"&&label.transform.parent.GetComponent<Button>()) {
                label.rectTransform.sizeDelta=((RectTransform)label.transform.parent).sizeDelta-new Vector2(20,12);
                label.verticalOverflow=VerticalWrapMode.Overflow;
            }
        }
        void Update()
        {
            if(!safe)return;
            float scale=Mathf.Min(safe.rect.width/960,safe.rect.height/1450);panel.localScale=Vector3.one*scale;
            title.rectTransform.anchorMin=title.rectTransform.anchorMax=new Vector2(.5f,1);title.rectTransform.anchoredPosition=new Vector2(0,-48);title.rectTransform.localScale=Vector3.one*Mathf.Min(1,safe.rect.width/960);
            if(toolbar&&result){float ts=Mathf.Min(safe.rect.width/980,safe.rect.height*.21f/340);toolbar.localScale=Vector3.one*ts;toolbar.anchoredPosition=new Vector2(0,180*ts);result.rectTransform.anchoredPosition=new Vector2(0,safe.rect.height*.4f);result.rectTransform.localScale=Vector3.one*scale;}
            if(playing&&!rendering){elapsed+=Time.unscaledDeltaTime*speed;if(elapsed>=.65f){elapsed=0;if(sequence>=timeline.Count)SetPlaying(false);else StartRender(sequence+1,true);}}
        }
        void Back()
        {
            if(loading)return;
            if(timeline!=null){SetPlaying(false);StopAllCoroutines();Destroy(board.gameObject);Destroy(toolbar.gameObject);Destroy(result.gameObject);timeline=null;rendering=false;background.SetActive(true);panel.gameObject.SetActive(true);ShowDetail();DominoLocalization.Set(title,"history.title");}
            else if(manifest!=null){manifest=null;DrawHistory();more.gameObject.SetActive(cursor!=null);DominoLocalization.Set(message,"history.subtitle");}
            else Destroy(gameObject);
        }
        void OnDestroy(){lifetime.Cancel();lifetime.Dispose();if(board)Destroy(board.gameObject);closed?.Invoke();}
    }
}
