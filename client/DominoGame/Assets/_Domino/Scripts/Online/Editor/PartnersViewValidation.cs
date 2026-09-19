#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Domino.Catalog;
using Domino.Client;
using Domino.Infrastructure;
using Domino.Realtime;
using Domino.UI;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace Domino.Online.Editor
{
    public static class PartnersViewValidation
    {
        const string Key="M5.Visual";static bool running;static int checks;static double deadline;
        static string PathFor(string name)=>Path.GetFullPath(Path.Combine(Application.dataPath,"../../"+name));
        sealed class Channel:IRealtimeConnectionService,IRealtimeMatchChannel {
            public RealtimeConnectionState State=>RealtimeConnectionState.CONNECTED;public GlobalActivitySnapshot Activity=>null;
            public event Action Changed;public event Action<string,JObject> MatchMessage;
            public void Start(){}public void Dispose(){}public void SetBackground(bool b){}public Task SendMatchCommandAsync(JObject c)=>Task.CompletedTask;
        }
        sealed class CatalogApi:IGameCatalogApi {public string Json;public Task<string> FetchAsync(CancellationToken t)=>Task.FromResult(Json);}
        sealed class Cache:IGameCatalogCache {public CatalogCacheEntry Read()=>null;public void Write(CatalogCacheEntry e){}}
        sealed class Api:IOnlineMatchApi {public Task<JObject> SendAsync(string m,string p,JObject b,CancellationToken t)=>Task.FromResult(new JObject{["state"]="NOT_QUEUED"});}
        public static void Run(){
            Domino.Infrastructure.ValidationNetworkPolicy.BeginIsolated();
            if(!Application.isBatchMode||!Application.dataPath.Replace('\\','/').Contains("/Validation/Generated/"))throw new Exception("ISOLATED_ONLY");
            Domino.Editor.LocalizationAssets.Import();SessionState.SetBool(Key,true);Register();EditorSceneManager.OpenScene(Domino.Editor.ClientEditorTools.ScenePath);EditorApplication.isPlaying=true;
        }
        [InitializeOnLoadMethod]static void Register(){if(!SessionState.GetBool(Key,false))return;deadline=EditorApplication.timeSinceStartup+360;Application.logMessageReceived+=Log;EditorApplication.update-=Tick;EditorApplication.update+=Tick;}
        static void Tick(){if(!SessionState.GetBool(Key,false))return;if(EditorApplication.timeSinceStartup>deadline){Finish(false,"TIMEOUT");return;}if(!running&&EditorApplication.isPlaying&&DominoLocalization.Ready&&ApplicationServices.Realtime!=null){running=true;_=Validate();}}
        static void Log(string m,string s,LogType t){if(t==LogType.Error||t==LogType.Exception||t==LogType.Assert)Finish(false,m);}
        static void Check(bool b,string m){checks++;if(!b)throw new Exception(m);}
        static async Task Frames(){int target=Time.frameCount+5;while(Time.frameCount<target)await Task.Delay(40);}
        static void ClickVisible(Button button) {
            var data=new UnityEngine.EventSystems.PointerEventData(UnityEngine.EventSystems.EventSystem.current){position=button.transform.position,eligibleForClick=true};
            var hits=new System.Collections.Generic.List<UnityEngine.EventSystems.RaycastResult>();
            UnityEngine.EventSystems.EventSystem.current.RaycastAll(data,hits);
            Check(hits.Count>0&&UnityEngine.EventSystems.ExecuteEvents.GetEventHandler<UnityEngine.EventSystems.IPointerClickHandler>(hits[0].gameObject)==button.gameObject,"NAVIGATION_BUTTON_HITTABLE");
            UnityEngine.EventSystems.ExecuteEvents.ExecuteHierarchy(hits[0].gameObject,data,UnityEngine.EventSystems.ExecuteEvents.pointerClickHandler);
        }
        static void DragList(StartMenuView menu) {
            var viewport=menu.ModeScroll.viewport;var start=(Vector2)viewport.TransformPoint(new Vector3(0,100,0));
            var data=new UnityEngine.EventSystems.PointerEventData(UnityEngine.EventSystems.EventSystem.current){position=start};
            var hits=new System.Collections.Generic.List<UnityEngine.EventSystems.RaycastResult>();UnityEngine.EventSystems.EventSystem.current.RaycastAll(data,hits);
            Check(hits.Count>0,"SCROLL_SURFACE_HITTABLE");
            var target=UnityEngine.EventSystems.ExecuteEvents.GetEventHandler<UnityEngine.EventSystems.IDragHandler>(hits[0].gameObject);
            Check(target==menu.ModeScroll.gameObject,"TOUCH_REACHES_SCROLL");
            UnityEngine.EventSystems.ExecuteEvents.Execute(target,data,UnityEngine.EventSystems.ExecuteEvents.beginDragHandler);
            data.position=start+Vector2.up*100;
            UnityEngine.EventSystems.ExecuteEvents.Execute(target,data,UnityEngine.EventSystems.ExecuteEvents.dragHandler);
            UnityEngine.EventSystems.ExecuteEvents.Execute(target,data,UnityEngine.EventSystems.ExecuteEvents.endDragHandler);
            menu.ModeScroll.StopMovement();
        }
        static void Resize(Vector2Int s)=>typeof(Domino.Editor.Phase1Validation).GetMethod("ResizeGameView",System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.NonPublic).Invoke(null,new object[]{s.x,s.y});
        static async Task Validate(){try{
            Application.runInBackground=true;Time.timeScale=20;ApplicationServices.Realtime.Dispose();var channel=new Channel();
            typeof(ApplicationServices).GetProperty("Realtime").SetValue(null,channel);typeof(ApplicationServices).GetProperty("OnlineApi").SetValue(null,new Api());
            var json=File.ReadAllText(PathFor("m5-catalog.json"));var catalog=new GameCatalogService(new CatalogApi{Json=json},new Cache(),json);await catalog.RefreshAsync(true);
            typeof(ApplicationServices).GetProperty("GameCatalog").SetValue(null,catalog);
            var menu=UnityEngine.Object.FindFirstObjectByType<DominoClientController>().Menu;
            var sizes=new[]{new Vector2Int(1080,1920),new Vector2Int(1170,2532),new Vector2Int(1179,2556),new Vector2Int(1290,2796),new Vector2Int(1206,2622),new Vector2Int(1320,2868),new Vector2Int(1080,2400),new Vector2Int(1440,3120),new Vector2Int(1536,2048)};
            foreach(var lang in new[]{"en","es"})foreach(var size in sizes){
                DominoLocalization.Select(lang);Resize(size);menu.Show(StartScreen.ModeSelector);await Frames();
                Check(menu.VisibleModeKeys.Count==3,"THREE_CARDS");Check(menu.ModeScroll.vertical&&!menu.ModeScroll.horizontal,"PORTRAIT_SCROLL");
                foreach(var key in new[]{"PARTNERS_2V2","DUEL_1V1","PARTNERS_2V2_ONLINE"})Check(menu.ButtonFor(key)!=null,"CARD_"+key);
                Check(menu.ModeScroll.verticalScrollbar&&menu.ModeScroll.verticalScrollbar.gameObject.activeInHierarchy,"VISIBLE_SCROLLBAR");
                var choose=menu.GetComponentsInChildren<Text>().Single(t=>t.name=="Choose");
                // SelectedLocale changes immediately; its UI event waits for table preloading.
                double localeDeadline=EditorApplication.timeSinceStartup+3;
                while(choose.text!=DominoLocalization.Get("menu.choose_modes",3)&&EditorApplication.timeSinceStartup<localeDeadline)await Frames();
                Check(choose.text==DominoLocalization.Get("menu.choose_modes",3),"TOTAL_MODE_COUNT_VISIBLE actual="+choose.text+" expected="+DominoLocalization.Get("menu.choose_modes",3));
                menu.ModeScroll.verticalNormalizedPosition=1;await Frames();DragList(menu);await Frames();Check(menu.ModeScroll.verticalNormalizedPosition<.99f,"TOUCH_DRAG_SCROLLS");
                menu.ModeScroll.verticalNormalizedPosition=1;await Frames();
                var more=menu.GetComponentsInChildren<Button>().Single(b=>b.name=="More modes");Check(more.interactable,"MORE_MODES_AVAILABLE");ClickVisible(more);await Frames();
                if(more.interactable){ClickVisible(more);await Frames();}
                var button=menu.ButtonFor("PARTNERS_2V2_ONLINE");
                var corners=new Vector3[4];button.GetComponent<RectTransform>().GetWorldCorners(corners);Check(corners.All(p=>Screen.safeArea.Contains(p)),"THIRD_BUTTON_SAFE");
                Check(button.GetComponentInChildren<Text>().text==DominoLocalization.Get("online.play"),"LOCALIZED_PLAY");
                var title=button.transform.parent.Find("Mode name").GetComponent<Text>();Check(title.text==DominoLocalization.Get("mode.partners_online.title"),"LOCALIZED_TITLE");
                Check(title.preferredWidth<=title.rectTransform.rect.width,"TITLE_FITS");
                float position=menu.ModeScroll.verticalNormalizedPosition;await catalog.RefreshAsync(true);await Frames();
                Check(Mathf.Abs(position-menu.ModeScroll.verticalNormalizedPosition)<.002f,"REFRESH_PRESERVES_VISIBLE_MODE");
                if(size==sizes[0]){ScreenCapture.CaptureScreenshot(PathFor("m5-selector-"+lang+".png"));await Frames();}
            }
            // An extended compatible catalog must grow the same list, without another card branch.
            var expanded=JObject.Parse(json);expanded["catalogVersion"]=5;var modes=(JArray)expanded["modes"];
            for(int n=0;n<2;n++){var extra=(JObject)modes[0].DeepClone();extra["id"]="validation-"+n;extra["key"]="VALIDATION_"+n;extra["sortOrder"]=40+n;modes.Add(extra);}
            var extendedJson=expanded.ToString();var extended=new GameCatalogService(new CatalogApi{Json=extendedJson},new Cache(),extendedJson);
            typeof(ApplicationServices).GetProperty("GameCatalog").SetValue(null,extended);menu.Show(StartScreen.ModeSelector);await Frames();
            Check(menu.VisibleModeKeys.Count==5&&menu.ButtonFor("VALIDATION_1"),"N_MODE_LIST_GROWS");
            var next=menu.GetComponentsInChildren<Button>().Single(b=>b.name=="More modes");
            for(int i=0;i<6&&next.interactable;i++){ClickVisible(next);await Frames();}
            var lastCorners=new Vector3[4];menu.ButtonFor("VALIDATION_1").GetComponent<RectTransform>().GetWorldCorners(lastCorners);
            Check(lastCorners.All(p=>Screen.safeArea.Contains(p)),"FIFTH_CARD_REACHABLE");
            typeof(ApplicationServices).GetProperty("GameCatalog").SetValue(null,catalog);await Frames();Check(menu.VisibleModeKeys.Count==3,"REMOVED_MODES_DISAPPEAR");
            menu.Show(StartScreen.Match);
            foreach(var lang in new[]{"en","es"})for(int seat=0;seat<4;seat++){
                DominoLocalization.Select(lang);var client=new OnlineMatchClient(new Api(),channel);client.ApplySnapshot(JObject.Parse(File.ReadAllText(PathFor("m5-snapshot-"+seat+".json"))));
                var controller=new GameObject("M5 visual").AddComponent<OnlineMatchController>();controller.Initialize(client,AssetDatabase.LoadAssetAtPath<DominoTileView>("Assets/_Domino/Prefabs/DominoTile.prefab"),AssetDatabase.LoadAssetAtPath<PlayerView>("Assets/_Domino/Prefabs/Player.prefab"));
                foreach(var size in sizes){Resize(size);await Frames();var view=controller.Board;Check(view!=null&&!view.IsPreparingRound,"BOARD_READY");
                    Check(view.LocalPlayerSeat==seat,"LOCAL_BOTTOM");Check(view.ReserveViews.Count==15,"RESERVE15");Check(view.PlayedCount==client.Snapshot.Public["board"].Count(),"CHAIN");
                    for(int p=0;p<4;p++){Check(view.HandViews(p).Count==client.Snapshot.Public["tilesRemainingPerSeat"][p].Value<int>(),"HAND_COUNT");Check(view.HandViews(p).All(t=>t.IsFaceUp==(p==seat)),"PRIVATE_HAND");}
                    foreach(var tile in view.LocalTiles){var corners=new Vector3[4];tile.Rect.GetWorldCorners(corners);Check(corners.All(v=>Screen.safeArea.Contains(v)),"HAND_SAFE");}
                    if(size==sizes[0]){ScreenCapture.CaptureScreenshot(PathFor("m5-board-"+lang+"-"+seat+".png"));await Frames();}
                }
                UnityEngine.Object.Destroy(controller.gameObject);await Frames();
            }
            Finish(true,"SIZES=9 LANGUAGES=en,es SEATS=4 CARDS=3 CHECKS="+checks+" CONSOLE_ERRORS=0");
        }catch(Exception e){Finish(false,e.ToString());}}
        static void Finish(bool pass,string details){if(!SessionState.GetBool(Key,false))return;SessionState.SetBool(Key,false);File.WriteAllText(PathFor("m5-visual-result.txt"),(pass?"PASS":"FAIL")+"\n"+details);EditorApplication.Exit(pass?0:1);}
    }
}
#endif
