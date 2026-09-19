#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Domino.Catalog;
using Domino.Client;
using Domino.Configuration;
using Domino.Game;
using Domino.Infrastructure;
using Domino.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Domino.Editor
{
    public static class StarterTileValidation
    {
        const string Key="Domino.StarterTable.Validation";
        static bool running;static int checks;static double deadline;
        static string oldLanguage;static TileStyle oldStyle;
        static string Dir=>Path.GetFullPath(Path.Combine(Application.dataPath,"../../StarterTable"));
        sealed class Offline:IGameCatalogApi {public Task<string> FetchAsync(CancellationToken t)=>throw new IOException();}
        sealed class Empty:IGameCatalogCache {public CatalogCacheEntry Read()=>null;public void Write(CatalogCacheEntry e){} }
        public static void Run() {
            Domino.Infrastructure.ValidationNetworkPolicy.BeginIsolated();
            if(!Application.isBatchMode||!Application.dataPath.Replace('\\','/').Contains("/Validation/Generated/"))throw new Exception("ISOLATED_ONLY");
            Directory.CreateDirectory(Dir);SessionState.SetBool(Key,true);Register();
            EditorSceneManager.OpenScene(ClientEditorTools.ScenePath);EditorApplication.isPlaying=true;
        }
        [InitializeOnLoadMethod] static void Register() {
            if(!SessionState.GetBool(Key,false))return;
            ApplicationServices.ValidationGameCatalogFactory=_=>new GameCatalogService(new Offline(),new Empty(),Resources.Load<TextAsset>("GameCatalogFallback").text);
            deadline=EditorApplication.timeSinceStartup+240;EditorApplication.update-=Tick;EditorApplication.update+=Tick;
            Application.logMessageReceived+=Log;
        }
        static void Log(string m,string s,LogType t){if(t==LogType.Error||t==LogType.Exception||t==LogType.Assert)Finish(false,m);}
        static void Tick() {
            if(!SessionState.GetBool(Key,false))return;
            if(EditorApplication.timeSinceStartup>deadline){Finish(false,"TIMEOUT");return;}
            var c=UnityEngine.Object.FindFirstObjectByType<DominoClientController>();
            if(!running&&EditorApplication.isPlaying&&c&&c.Menu&&DominoLocalization.Ready){running=true;_=Validate(c);}
        }
        static void Check(bool value,string name){checks++;if(!value)throw new Exception(name);}
        static async Task Until(Func<bool> predicate,string name){double end=EditorApplication.timeSinceStartup+30;while(!predicate()&&EditorApplication.timeSinceStartup<end)await Task.Delay(40);Check(predicate(),name);}
        static async Task Capture(string name){Canvas.ForceUpdateCanvases();await Task.Delay(250);ScreenCapture.CaptureScreenshot(Path.Combine(Dir,name+".png"));await Task.Delay(350);}
        static void Tap(DominoTileView tile) {
            var e=new PointerEventData(EventSystem.current){position=RectTransformUtility.WorldToScreenPoint(null,tile.transform.position),button=PointerEventData.InputButton.Left,eligibleForClick=true};
            var hits=new List<RaycastResult>();EventSystem.current.RaycastAll(e,hits);
            Check(hits.Count>0&&hits[0].gameObject.transform.IsChildOf(tile.transform),"TILE_RAYCAST_UNOBSCURED_"+(hits.Count>0?hits[0].gameObject.name:"NONE"));
            ExecuteEvents.ExecuteHierarchy(hits[0].gameObject,e,ExecuteEvents.pointerClickHandler);
        }
        static async Task Validate(DominoClientController c) {
            try {
                oldLanguage=DominoLocalization.Language;oldStyle=TileStyles.Current;Time.timeScale=8;
                c.StartMatch(new GameModeDefinition(ApplicationServices.GameCatalog.ResolveMatch("DUEL_1V1").Mode));
                for(int attempt=0;c.StarterSelection.Method!=StarterMethod.HIGH_TILE_SELECTION&&attempt<40;attempt++){await Task.Delay(25);c.RestartClient();}
                Check(c.StarterSelection.Method==StarterMethod.HIGH_TILE_SELECTION,"HIGH_TILE_REAL_CONTROLLER");
                await Until(()=>c.View.StarterView&&c.SharedPrompt.AwaitingInput,"STARTER_READY");
                var board=c.View;var view=board.StarterView;int surface=board.BoardSurface.GetInstanceID();
                var sizes=new[]{(1080,1920),(1080,2160),(1080,2340),(1080,2400),(1080,2520),(1170,2532),(1284,2778),(1600,2560),(1536,2048)};
                foreach(var size in sizes) {
                    typeof(Phase1Validation).GetMethod("ResizeGameView",BindingFlags.NonPublic|BindingFlags.Static).Invoke(null,new object[]{size.Item1,size.Item2});
                    await Task.Delay(250);
                    foreach(string language in new[]{"en","es"}) {
                        DominoLocalization.Select(language);await Task.Delay(100);
                        Check(view.transform.parent==board.BoardSurface,"SAME_TABLE_PARENT");
                        Check(board.BoardSurface.gameObject.activeInHierarchy,"GAMEPLAY_SURFACE_VISIBLE");
                        Check(!c.SharedPrompt.transform.Find("Private handoff").gameObject.activeInHierarchy,"OPAQUE_PANEL_HIDDEN");
                        Check(view.Tiles.All(t=>!t.IsFaceUp),"CANDIDATES_PRIVATE_UNTIL_RESOLVED");
                        foreach(var tile in view.Tiles) {
                            var corners=new Vector3[4];tile.Rect.GetWorldCorners(corners);
                            Check(corners.All(v=>Screen.safeArea.Contains(v)),"TILE_SAFE_AREA");
                            Check(corners.All(v=>board.BoardSurface.rect.Contains(board.BoardSurface.InverseTransformPoint(v))),"TILE_ON_SURFACE");
                            Check(tile.Rect.sizeDelta==new Vector2(96,46),"SAME_TILE_ASPECT_RATIO");
                        }
                        foreach(var label in view.GetComponentsInChildren<UnityEngine.UI.Text>().Where(t=>t.name.StartsWith("Starter")||t.name=="Selecting player")) {
                            var corners=new Vector3[4];label.rectTransform.GetWorldCorners(corners);Check(corners.All(v=>Screen.safeArea.Contains(v)),"HEADER_SAFE_AREA");
                            Check(!label.text.Contains("Ficha 1")&&!label.text.Contains("Tile 1"),"NO_GENERIC_TILE_LABELS");
                        }
                    }
                }
                typeof(Phase1Validation).GetMethod("ResizeGameView",BindingFlags.NonPublic|BindingFlags.Static).Invoke(null,new object[]{1080,1920});
                foreach(TileStyle style in Enum.GetValues(typeof(TileStyle))){TileStyles.Set(style);await Capture("starter-"+style);Check(view.Tiles.All(t=>t.GetComponent<DominoFace>()),"GAMEPLAY_RENDERER_THEME_"+style);}
                TileStyles.Set(oldStyle);DominoLocalization.Select("es");await Task.Delay(200);
                Tap(view.Tiles[0]);await Task.Delay(100);Check(view.Tiles[0].Selected,"SELECTION_HIGHLIGHT");
                await Until(()=>c.SharedPrompt.AwaitingInput,"SECOND_PLAYER_READY");
                Check(!view.Tiles[0].IsFaceUp&&!view.Choices[0].interactable,"FIRST_PICK_HIDDEN_LOCKED");
                Tap(view.Tiles[1]);await Until(()=>view.Tiles.All(t=>t.IsFaceUp),"REAL_REVEAL");
                await Capture("result");
                double end=EditorApplication.timeSinceStartup+40;
                while(!c.AcceptingInput&&EditorApplication.timeSinceStartup<end) {
                    var p=c.SharedPrompt;
                    if(p.AwaitingInput){if(p.ContinueButton)p.ContinueButton.onClick.Invoke();else if(p.FirstChoice&&p.FirstChoice.interactable)p.FirstChoice.onClick.Invoke();else if(p.SecondChoice&&p.SecondChoice.interactable)p.SecondChoice.onClick.Invoke();}
                    await Task.Delay(60);
                }
                Check(c.AcceptingInput,"GAMEPLAY_READY");Check(c.View==board&&board.BoardSurface.GetInstanceID()==surface,"SAME_TABLE_THROUGH_DEAL");
                Check(board.VisuallyDealt==20&&board.HandViews(0).Count==10&&board.HandViews(1).Count==10,"TEN_TILES_EACH");
                await Capture("gameplay");
                // Find a tied pair using the unchanged model RNG; no production overrides.
                StarterSelection tie=null;
                for(int seed=0;seed<10000;seed++){var s=new StarterSelection(c.Session.Configuration,seed);if(s.Method!=StarterMethod.HIGH_TILE_SELECTION)continue;s.Choose(0,0);s.Choose(1,1);if(s.Attempt>1){tie=new StarterSelection(c.Session.Configuration,seed);break;}}
                Check(tie!=null,"TIE_FIXTURE_FROM_REAL_MODEL");c.StopAllCoroutines();board.Clear();c.StartCoroutine(c.SharedPrompt.ChooseStarter(tie,board));
                await Until(()=>c.SharedPrompt.AwaitingInput,"TIE_READY");await Task.Delay(250);Tap(view.Tiles[0]);await Until(()=>c.SharedPrompt.AwaitingInput,"TIE_SECOND");Tap(view.Tiles[1]);
                await Until(()=>view.Tiles.All(t=>t.IsFaceUp),"TIE_REVEAL");
                Check(!tie.Complete&&tie.Attempt==2,"EQUAL_SUM_REPEAT_UNCHANGED");await Capture("tie");
                await Until(()=>c.SharedPrompt.AwaitingInput&&view.Tiles.All(t=>!t.IsFaceUp),"TIE_REPLACED_ON_SAME_TABLE");
                Check(board.BoardSurface.GetInstanceID()==surface,"TIE_TABLE_CONTINUITY");c.ExitMatch();
                Finish(true,"STARTER_TABLE=PASS CHECKS="+checks+" PORTRAIT_SIZES=9 LANGUAGES=en,es THEMES=3 SAME_BOARD=YES TIE=PASS CONSOLE_ERRORS=0");
            }catch(Exception e){Finish(false,e.ToString());}
        }
        static void Finish(bool ok,string detail) {
            if(!SessionState.GetBool(Key,false))return;SessionState.SetBool(Key,false);Time.timeScale=1;
            if(oldLanguage!=null){DominoLocalization.Select(oldLanguage);TileStyles.Set(oldStyle);}
            File.WriteAllText(Path.Combine(Dir,"result.txt"),(ok?"PASS\n":"FAIL\n")+detail);EditorApplication.Exit(ok?0:1);
        }
    }
}
#endif
