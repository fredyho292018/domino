#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Domino.Client;
using Domino.Infrastructure;
using Domino.Replay;
using Domino.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Newtonsoft.Json.Linq;
using System.Threading;

namespace Domino.Editor {
 public static class S607HistoryValidation {
  static string Dir=>Path.GetFullPath(Path.Combine(Application.dataPath,"../../Validation/Generated/S607"));
  static void Record(string s){Directory.CreateDirectory(Dir);File.AppendAllText(Path.Combine(Dir,"diagnostic.txt"),s+"\n");}
  [MenuItem("Domino/Validation/S6-07/Open and diagnose History")]
  static async void Run(){
   if(!Application.isPlaying){Debug.LogWarning("S607 requires Play.");return;}
   try {
    if(!ApplicationServices.Player.IsFresh)throw new Exception("SESSION_NOT_FRESH");
    foreach(var old in UnityEngine.Object.FindObjectsByType<HistoryReplayView>(FindObjectsSortMode.None))UnityEngine.Object.Destroy(old.gameObject);
    await Task.Delay(150);
    var local=UnityEngine.Object.FindFirstObjectByType<DominoClientController>();local.Menu.Show(StartScreen.Match);
    var view=new GameObject("S607 History",typeof(RectTransform)).AddComponent<HistoryReplayView>();
    var flags=BindingFlags.Instance|BindingFlags.NonPublic;
    view.Initialize(new ReplayClient(ApplicationServices.OnlineApi),(DominoTileView)typeof(DominoClientController).GetField("tilePrefab",flags).GetValue(local),(PlayerView)typeof(DominoClientController).GetField("playerPrefab",flags).GetValue(local),()=>local.Menu.Show(StartScreen.MainMenu));
    while((bool)typeof(HistoryReplayView).GetField("loading",flags).GetValue(view))await Task.Delay(100);
    await Task.Delay(500);Canvas.ForceUpdateCanvases();
    var scroll=view.GetComponentInChildren<ScrollRect>();
    Record($"VERTICAL={scroll.vertical} HORIZONTAL={scroll.horizontal} ACTIVE={scroll.IsActive()} CONTENT={scroll.content.rect.height} VIEWPORT={scroll.viewport.rect.height} SENSITIVITY={scroll.scrollSensitivity}");
    var start=(Vector2)scroll.viewport.TransformPoint(new Vector3(0,-100));
    var data=new PointerEventData(EventSystem.current){position=start,button=PointerEventData.InputButton.Left};
    var hits=new System.Collections.Generic.List<RaycastResult>();EventSystem.current.RaycastAll(data,hits);
    Record("RAYCAST="+string.Join(",",hits.Select(x=>x.gameObject.name)));
    var target=hits.Count>0?ExecuteEvents.GetEventHandler<IDragHandler>(hits[0].gameObject):null;
    Record("DRAG_TARGET="+(target?target.name:"NONE"));
    float before=scroll.content.anchoredPosition.y;
    if(target){ExecuteEvents.Execute(target,data,ExecuteEvents.initializePotentialDrag);ExecuteEvents.Execute(target,data,ExecuteEvents.beginDragHandler);data.position+=Vector2.up*180;ExecuteEvents.Execute(target,data,ExecuteEvents.dragHandler);ExecuteEvents.Execute(target,data,ExecuteEvents.endDragHandler);}
    await Task.Delay(200);Record($"DRAG_BEFORE={before} AFTER={scroll.content.anchoredPosition.y}");
    scroll.StopMovement();scroll.verticalNormalizedPosition=1;
    data.scrollDelta=new Vector2(0,-3);scroll.OnScroll(data);await Task.Delay(100);Record("WHEEL_OFFSET="+scroll.content.anchoredPosition.y);
    scroll.StopMovement();scroll.verticalNormalizedPosition=1;
    Record("DIAGNOSTIC_COMPLETE=YES");
    await Counts(view,flags);
    scroll.gameObject.AddComponent<S607InputEvidence>();
   }catch(Exception e){Record("STOP="+e.GetType().Name+":"+e.Message);}
  }
  sealed class FixtureApi:Domino.Online.IOnlineMatchApi {
   public JArray Items;
   public Task<JObject> SendAsync(string method,string path,JObject body,CancellationToken token){
    if(method!="GET"||!path.StartsWith("players/me/history?"))throw new InvalidOperationException("FIXTURE_READ_ONLY");
    return Task.FromResult(new JObject{["items"]=Items.DeepClone(),["nextCursor"]=null});
   }
  }
  static void Need(bool condition,string name){if(!condition)throw new Exception(name);}
  static async Task Counts(HistoryReplayView real,BindingFlags flags){
   var rows=(System.Collections.Generic.List<JObject>)typeof(HistoryReplayView).GetField("history",flags).GetValue(real);
   Need(rows.Count==5,"REAL_HISTORY_COUNT");
   real.gameObject.SetActive(false);
   try{foreach(int count in new[]{0,1,5,12}){
    var fake=new FixtureApi{Items=new JArray(Enumerable.Range(0,count).Select(i=>rows[i%rows.Count].DeepClone()))};
    var test=new GameObject("S607 count fixture",typeof(RectTransform)).AddComponent<HistoryReplayView>();
    try{
     test.Initialize(new ReplayClient(fake),null,null,()=>{});await Task.Delay(250);Canvas.ForceUpdateCanvases();
     var scroll=test.GetComponentInChildren<ScrollRect>();
     Need(scroll.vertical&&!scroll.horizontal,"VERTICAL_ONLY");
     var buttons=scroll.content.GetComponentsInChildren<Button>();Need(buttons.Length==count,"CARD_COUNT");
     Need(Mathf.Abs(scroll.content.anchoredPosition.y)<.1f,"INITIAL_TOP");
     if(count==0)Need(test.GetComponentsInChildren<Text>().Any(t=>t.text==DominoLocalization.Get("history.empty")),"EMPTY_STATE");
     if(count>=5){
      Need(scroll.content.rect.height>scroll.viewport.rect.height,"OVERFLOW");
      var pointer=new PointerEventData(EventSystem.current){position=scroll.viewport.TransformPoint(Vector3.zero),button=PointerEventData.InputButton.Left,scrollDelta=new Vector2(0,-3)};
      var hits=new System.Collections.Generic.List<RaycastResult>();EventSystem.current.RaycastAll(pointer,hits);
      Need(hits.Count>0&&ExecuteEvents.GetEventHandler<IScrollHandler>(hits[0].gameObject)==scroll.gameObject,"WHEEL_ROUTING");
      ExecuteEvents.ExecuteHierarchy(hits[0].gameObject,pointer,ExecuteEvents.scrollHandler);await Task.Delay(100);
      Need(scroll.content.anchoredPosition.y>=100,"WHEEL_MOVEMENT");
      scroll.StopMovement();scroll.verticalNormalizedPosition=1;
      ExecuteEvents.Execute(scroll.gameObject,pointer,ExecuteEvents.initializePotentialDrag);
      ExecuteEvents.Execute(scroll.gameObject,pointer,ExecuteEvents.beginDragHandler);pointer.position+=Vector2.up*200;
      ExecuteEvents.Execute(scroll.gameObject,pointer,ExecuteEvents.dragHandler);ExecuteEvents.Execute(scroll.gameObject,pointer,ExecuteEvents.endDragHandler);
      Need(scroll.content.anchoredPosition.y>20,"DRAG_MOVEMENT");
      scroll.StopMovement();scroll.verticalNormalizedPosition=0;Canvas.ForceUpdateCanvases();
      var oldest=buttons.Last();pointer.position=oldest.transform.position;hits.Clear();EventSystem.current.RaycastAll(pointer,hits);
      Need(hits.Count>0&&ExecuteEvents.GetEventHandler<IPointerClickHandler>(hits[0].gameObject)==oldest.gameObject,"OLDEST_BUTTON_HITTABLE");
      var corners=new Vector3[4];oldest.GetComponent<RectTransform>().GetWorldCorners(corners);
      Need(corners.All(p=>RectTransformUtility.RectangleContainsScreenPoint(scroll.viewport,p)),"OLDEST_INSIDE_VIEWPORT");
      for(int i=1;i<buttons.Length;i++)Need(buttons[i].transform.position.y<buttons[i-1].transform.position.y,"NO_OVERLAP_ORDER");
     }
     typeof(HistoryReplayView).GetMethod("LoadHistory",flags).Invoke(test,new object[]{true});await Task.Delay(100);Canvas.ForceUpdateCanvases();
     Need(Mathf.Abs(scroll.content.anchoredPosition.y)<.1f,"REFRESH_TOP");
     Record("COUNT_"+count+"_LAYOUT_INPUT_REFRESH=PASS");
    }finally{UnityEngine.Object.Destroy(test.gameObject);await Task.Delay(100);}
   }}finally{real.gameObject.SetActive(true);}
   Record("FOCUSED_UI_TESTS=PASS");
  }
 }
 public sealed class S607InputEvidence:MonoBehaviour,IBeginDragHandler,IScrollHandler {
  static void Save(string value){var dir=Path.GetFullPath(Path.Combine(Application.dataPath,"../../Validation/Generated/S607"));File.AppendAllText(Path.Combine(dir,"native-input.txt"),value+"\n");}
  public void OnBeginDrag(PointerEventData data){Save("NATIVE_BEGIN_DRAG=YES");}
  public void OnScroll(PointerEventData data){Save("NATIVE_WHEEL=YES");}
 }
}
#endif
