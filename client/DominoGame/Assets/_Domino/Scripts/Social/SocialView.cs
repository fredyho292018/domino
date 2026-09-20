using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Domino.UI;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace Domino.Social
{
    public sealed class SocialView : MonoBehaviour
    {
        SocialClient client;Action closed;CancellationTokenSource lifetime=new CancellationTokenSource();
        RectTransform safe,panel,rows,viewport;Text status,code;InputField input;Button more;JObject summary;
        readonly List<JObject> items=new List<JObject>();string cursor,query,section="search";bool busy,confirming;
        public InputField SearchInput=>input;
        public bool Busy=>busy;
        public int ResultCount=>items.Count;
        public void Initialize(SocialClient source,Action onClose) {
            client=source;closed=onClose;
            gameObject.AddComponent<SocialAccessibility>();
            var canvas=gameObject.AddComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.sortingOrder=55;
            BoardView.ConfigureCanvas(gameObject);gameObject.AddComponent<GraphicRaycaster>();
            var bg=UiKit.Rect("Social background",transform,Vector2.zero,Vector2.zero);bg.anchorMin=Vector2.zero;bg.anchorMax=Vector2.one;bg.sizeDelta=Vector2.zero;bg.gameObject.AddComponent<SoftBackdrop>();
            safe=UiKit.Rect("Safe area",transform,Vector2.zero,Vector2.zero);safe.gameObject.AddComponent<SafeArea>();
            panel=UiKit.Rect("Social panel",safe,new Vector2(720,1400),Vector2.zero);
            Label("Title","social.title",panel,660,new Vector2(0,640));
            Button("Back","menu.back",panel,160,new Vector2(-240,535),Close);
            code=Label("Own code","social.code",panel,420,new Vector2(90,535));
            Button("Copy","social.copy",panel,205,new Vector2(220,430),()=>{if(summary!=null){GUIUtility.systemCopyBuffer=(string)summary["profile"]["friendCode"];Message("social.copied");}});
            Button("Friends tab","social.friends",panel,205,new Vector2(-220,430),()=>Navigate("friends"));
            Button("Requests tab","social.requests",panel,205,new Vector2(0,430),()=>Navigate("incoming"));
            Button("Search tab","social.search",panel,205,new Vector2(-220,320),()=>Navigate("search"));
            Button("Blocked tab","social.blocked",panel,205,new Vector2(0,320),()=>Navigate("blocks"));
            Button("Privacy tab","social.privacy",panel,205,new Vector2(220,320),()=>Navigate("privacy"));
            Button("Following tab","social.following",panel,315,new Vector2(-165,210),()=>Navigate("following"));
            Button("Followers tab","social.followers",panel,315,new Vector2(165,210),()=>Navigate("followers"));
            var field=UiKit.Panel("Search input",panel,new Vector2(480,100),new Vector2(-80,95),UiKit.Hex("1D403E"));field.raycastTarget=true;
            input=field.gameObject.AddComponent<InputField>();input.textComponent=UiKit.Label("Input text",field.transform,"",new Vector2(450,90),Vector2.zero,30,UiKit.Cream,TextAnchor.MiddleLeft);
            input.placeholder=UiKit.LLabel("Search label",field.transform,"social.hint",new Vector2(450,90),Vector2.zero,26,UiKit.Muted,TextAnchor.MiddleLeft);input.characterLimit=32;
            input.onEndEdit.AddListener(_=>{if(Input.GetKeyDown(KeyCode.Return))Search();});
            Button("Clear search","social.clear",panel,130,new Vector2(250,95),()=>input.text="");
            Button("Search submit","social.search",panel,300,new Vector2(0,-15),Search);
            viewport=UiKit.Rect("Social viewport",panel,new Vector2(670,480),new Vector2(0,-385));viewport.gameObject.AddComponent<RectMask2D>();viewport.gameObject.AddComponent<Image>().color=Color.clear;
            rows=UiKit.Rect("Social rows",viewport,new Vector2(650,0),Vector2.zero);rows.anchorMin=rows.anchorMax=new Vector2(.5f,1);rows.pivot=new Vector2(.5f,1);
            var scroll=viewport.gameObject.AddComponent<ScrollRect>();scroll.viewport=viewport;scroll.content=rows;scroll.horizontal=false;scroll.movementType=ScrollRect.MovementType.Clamped;
            more=Button("More","history.more",panel,300,new Vector2(0,-690),()=>Run(()=>Load(false)));more.gameObject.SetActive(false);
            status=Label("Status","social.hint",panel,670,new Vector2(0,-65));status.rectTransform.sizeDelta=new Vector2(670,120);
            Run(LoadSummary);
        }
        static Text Label(string name,string key,Transform parent,float width,Vector2 position)=>UiKit.LLabel(name,parent,key,new Vector2(width,100),position,30,UiKit.Cream);
        static Button Button(string name,string key,Transform parent,float width,Vector2 position,UnityEngine.Events.UnityAction action) {
            var b=UiKit.LButton(name,parent,key,new Vector2(width,100),position,UiKit.Hex("254B47"),action);
            var text=b.GetComponentInChildren<Text>();text.rectTransform.sizeDelta=new Vector2(width-20,90);text.fontSize=27;text.resizeTextForBestFit=true;text.resizeTextMinSize=20;text.resizeTextMaxSize=27;return b;
        }
        void Message(string key){if(status)DominoLocalization.Set(status,key);}
        async Task LoadSummary(){summary=await client.Summary(lifetime.Token);DominoLocalization.Bind(code,()=>DominoLocalization.Get("social.code")+"\n"+(string)summary?["profile"]?["friendCode"]);Message("social.hint");}
        void ClearRows(){foreach(Transform child in rows){child.gameObject.SetActive(false);Destroy(child.gameObject);}rows.sizeDelta=new Vector2(650,0);confirming=false;}
        void Navigate(string next) {
            if(busy)return;section=next;items.Clear();cursor=null;ClearRows();more.gameObject.SetActive(false);
            input.transform.gameObject.SetActive(next=="search");panel.Find("Clear search").gameObject.SetActive(next=="search");panel.Find("Search submit").gameObject.SetActive(next=="search");
            status.rectTransform.anchoredPosition=new Vector2(0,next=="search"?-120:95);
            viewport.sizeDelta=new Vector2(670,next=="search"?430:620);viewport.anchoredPosition=new Vector2(0,next=="search"?-410:-310);
            if(next=="following"||next=="followers"||next=="blocks"||next=="friends"||next=="incoming"||next=="outgoing")Run(async()=>{await LoadSummary();await Load(true);});else if(next=="privacy")Run(async()=>{summary=await client.Summary(lifetime.Token);DrawPrivacy();});else Message("social.hint");
        }
        public void Search(){if(busy)return;section="search";query=input.text;Run(()=>Load(true));}
        async Task Load(bool reset) {
            if(reset){items.Clear();cursor=null;ClearRows();}
            var page=section=="following"||section=="followers"?await client.Follows(section=="following",cursor,lifetime.Token):section=="blocks"?await client.Blocks(cursor,lifetime.Token):section=="friends"?await client.Friends(cursor,lifetime.Token):section=="incoming"||section=="outgoing"?await client.Requests(section=="incoming",cursor,lifetime.Token):await client.Search(query,cursor,lifetime.Token);
            foreach(JObject item in (JArray)page["items"])items.Add(item);cursor=(string)page["nextCursor"];Draw();more.gameObject.SetActive(cursor!=null);
            Message(items.Count==0?(section=="following"?"social.no_following":section=="followers"?"social.no_followers":section=="blocks"?"social.no_blocks":section=="friends"?"social.no_friends":section=="incoming"?"social.no_incoming":section=="outgoing"?"social.no_outgoing":"social.empty"):"social.results");
            if(section=="friends")DominoLocalization.Bind(status,()=>CapacityText()+(items.Count==0?"\n"+DominoLocalization.Get("social.no_friends"):""));
        }
        void Draw() {
            if(section=="friends"||section=="incoming"||section=="outgoing"||section=="following"||section=="followers"){DrawRelations();return;}
            ClearRows();for(int i=0;i<items.Count;i++) {
                var item=items[i];var row=UiKit.Panel("Player row",rows,new Vector2(640,210),new Vector2(0,-110-i*225),UiKit.Hex("1D403E")).rectTransform;row.anchorMin=row.anchorMax=new Vector2(.5f,1);
                UiKit.Disc("Default avatar",row,65,new Vector2(-265,40),UiKit.Gold);
                var label=UiKit.Label("Public name and code",row,(string)item["displayName"]+"\n"+(string)item["friendCode"],new Vector2(500,100),new Vector2(40,45),30,UiKit.Cream);label.resizeTextForBestFit=true;label.resizeTextMinSize=24;label.resizeTextMaxSize=30;
                Button(section=="blocks"?"Unblock":"View profile",section=="blocks"?"social.unblock":"social.profile",row,400,new Vector2(0,-60),()=>{
                    if(section=="blocks")Run(async()=>{await client.Block((string)item["publicPlayerId"],false,lifetime.Token);await Load(true);Message("social.unblocked");});
                    else Run(async()=>ShowProfile(await client.Profile((string)item["publicPlayerId"],lifetime.Token)));
                });
            }rows.sizeDelta=new Vector2(650,items.Count*225);
        }
        void ShowProfile(JObject item) {
            ClearRows();more.gameObject.SetActive(false);Message("social.profile");rows.sizeDelta=new Vector2(650,980);
            var label=UiKit.Label("Public profile",rows,(string)item["displayName"]+"\n"+(string)item["friendCode"],new Vector2(640,120),new Vector2(0,-90),32,UiKit.Cream);label.rectTransform.anchorMin=label.rectTransform.anchorMax=new Vector2(.5f,1);
            var button=Button("Block player","social.block",rows,480,new Vector2(0,-250),()=>{
                if(busy||confirming)return;confirming=true;Message("social.block_confirm");
                var yes=Button("Confirm block","system.confirm",rows,260,new Vector2(-145,-410),()=>Run(async()=>{await client.Block((string)item["publicPlayerId"],true,lifetime.Token);await LoadSummary();items.Clear();cursor=null;ClearRows();Message("social.blocked_success");}));
                var no=Button("Cancel block","system.cancel",rows,260,new Vector2(145,-410),()=>ShowProfile(item));
                foreach(var b in new[]{yes,no})b.GetComponent<RectTransform>().anchorMin=b.GetComponent<RectTransform>().anchorMax=new Vector2(.5f,1);
            });button.GetComponent<RectTransform>().anchorMin=button.GetComponent<RectTransform>().anchorMax=new Vector2(.5f,1);
            var relation=item["relationship"] as JObject;
            string incoming=(string)relation?["incomingRequestId"],outgoing=(string)relation?["outgoingRequestId"],id=(string)item["publicPlayerId"];
            if((string)relation?["friendship"]=="FRIENDS")RelationButton("Remove friend","social.remove_friend",-570,()=>ConfirmRemove(id));
            else if(incoming!=null){RelationButton("Accept request","social.accept",-570,()=>Mutate(()=>client.ResolveRequest(incoming,"accept",lifetime.Token),id));RelationButton("Decline request","social.decline",-685,()=>Mutate(()=>client.ResolveRequest(incoming,"decline",lifetime.Token),id));}
            else if(outgoing!=null)RelationButton("Cancel request","social.cancel_request",-570,()=>Mutate(()=>client.ResolveRequest(outgoing,"cancel",lifetime.Token),id));
            else {var add=RelationButton("Add friend","social.add_friend",-570,()=>Mutate(()=>client.AddFriend(id,lifetime.Token),id));add.interactable=(bool?)summary?["friends"]?["canAddFriend"]??false;}
            bool following=(bool?)relation?["following"]??false,followedBy=(bool?)relation?["followedBy"]??false;
            RelationButton(following?"Unfollow":followedBy?"Follow back":"Follow",following?"social.unfollow":followedBy?"social.follow_back":"social.follow",-970,()=>Mutate(()=>client.Follow(id,!following,lifetime.Token),id));
            rows.sizeDelta=new Vector2(650,1050);
            var capacity=Label("Friend capacity","social.limit_help",rows,630,new Vector2(0,-830));capacity.rectTransform.anchorMin=capacity.rectTransform.anchorMax=new Vector2(.5f,1);capacity.rectTransform.sizeDelta=new Vector2(630,170);DominoLocalization.Bind(capacity,()=>CapacityText());
        }
        string CapacityText() {
            var f=summary?["friends"];if(f==null||(string)f["availability"]!="AVAILABLE")return DominoLocalization.Get("social.error");
            return DominoLocalization.Get("social.capacity",(int)f["friendCount"],(int)f["effectiveFriendLimit"])+((bool)f["canAddFriend"]?"":"\n"+DominoLocalization.Get("social.limit_help"));
        }
        Button RelationButton(string name,string key,float y,UnityEngine.Events.UnityAction action) {var b=Button(name,key,rows,600,new Vector2(0,y),action);b.GetComponent<RectTransform>().anchorMin=b.GetComponent<RectTransform>().anchorMax=new Vector2(.5f,1);return b;}
        void Mutate(Func<Task<JObject>> mutation,string profileId=null)=>Run(async()=>{await mutation();await LoadSummary();items.Clear();cursor=null;if(profileId!=null)ShowProfile(await client.Profile(profileId,lifetime.Token));else await Load(true);});
        void ConfirmRemove(string id) {if(busy)return;ClearRows();Message("social.remove_confirm");rows.sizeDelta=new Vector2(650,280);RelationButton("Confirm remove","system.confirm",-80,()=>Mutate(()=>client.RemoveFriend(id,lifetime.Token)));RelationButton("Cancel remove","system.cancel",-200,()=>Navigate("friends"));}
        void DrawRelations() {
            ClearRows();float offset=0;
            if(section=="incoming"||section=="outgoing") {RelationButton("Request direction",section=="incoming"?"social.show_outgoing":"social.show_incoming",-60,()=>Navigate(section=="incoming"?"outgoing":"incoming"));offset=130;}
            for(int i=0;i<items.Count;i++) {
                var item=items[i];var p=(JObject)item["profile"];string id=(string)p["publicPlayerId"],request=(string)item["requestId"];
                var row=UiKit.Panel("Relationship row",rows,new Vector2(640,370),new Vector2(0,-offset-185-i*385),UiKit.Hex("1D403E")).rectTransform;row.anchorMin=row.anchorMax=new Vector2(.5f,1);
                UiKit.Disc("Default avatar",row,65,new Vector2(-275,95),UiKit.Gold);
                var label=UiKit.Label("Friend name code date",row,"",new Vector2(500,140),new Vector2(40,95),26,UiKit.Cream);label.resizeTextForBestFit=true;
                DominoLocalization.Bind(label,()=>{DateTimeOffset.TryParse((string)(item["friendsSince"]??item["createdAt"]??item["followedSince"]),out var date);return (string)p["displayName"]+"\n"+(string)p["friendCode"]+"\n"+date.ToLocalTime().ToString("g",System.Globalization.CultureInfo.GetCultureInfo(DominoLocalization.Language));});
                Button("View profile","social.profile",row,590,new Vector2(0,-25),()=>Run(async()=>ShowProfile(await client.Profile(id,lifetime.Token))));
                if(section=="friends")Button("Remove friend","social.remove_friend",row,590,new Vector2(0,-140),()=>ConfirmRemove(id));
                else if(section=="following")Button("Unfollow","social.unfollow",row,590,new Vector2(0,-140),()=>Mutate(()=>client.Follow(id,false,lifetime.Token)));
                else if(section=="followers") { }
                else if(section=="outgoing")Button("Cancel request","social.cancel_request",row,590,new Vector2(0,-140),()=>Mutate(()=>client.ResolveRequest(request,"cancel",lifetime.Token)));
                else {Button("Accept request","social.accept",row,285,new Vector2(-150,-140),()=>Mutate(()=>client.ResolveRequest(request,"accept",lifetime.Token)));Button("Decline request","social.decline",row,285,new Vector2(150,-140),()=>Mutate(()=>client.ResolveRequest(request,"decline",lifetime.Token)));}
            }rows.sizeDelta=new Vector2(650,offset+items.Count*385);
        }
        void DrawPrivacy() {
            ClearRows();Message("social.privacy");rows.sizeDelta=new Vector2(650,800);
            bool enabled=(bool)summary["privacy"]["discoverableByName"];
            var label=Label("Privacy explanation","social.discovery_help",rows,640,new Vector2(0,-100));label.rectTransform.anchorMin=label.rectTransform.anchorMax=new Vector2(.5f,1);label.rectTransform.sizeDelta=new Vector2(640,190);
            var b=Button("Discoverable toggle",enabled?"social.discoverable_on":"social.discoverable_off",rows,620,new Vector2(0,-270),()=>Run(async()=>{
                summary["privacy"]=await client.Privacy(!enabled,(long)summary["privacy"]["revision"],lifetime.Token);DrawPrivacy();}));b.GetComponent<RectTransform>().anchorMin=b.GetComponent<RectTransform>().anchorMax=new Vector2(.5f,1);
            PrivacyChoice("Friend privacy","friendRequests","social.who_requests",-420);
            PrivacyChoice("Follow privacy","follow","social.who_follows",-570);
        }
        void PrivacyChoice(string name,string field,string key,float y) {
            string value=(string)summary["privacy"][field];
            var b=RelationButton(name,key,y,()=>Run(async()=>{summary["privacy"]=await client.Privacy(field,value=="EVERYONE"?"NO_ONE":"EVERYONE",(long)summary["privacy"]["revision"],lifetime.Token);DrawPrivacy();Message("social.privacy_updated");}));
            DominoLocalization.Bind(b.GetComponentInChildren<Text>(),()=>DominoLocalization.Get(key)+"\n"+DominoLocalization.Get(value=="EVERYONE"?"social.everyone":"social.no_one"));
        }
        async void Run(Func<Task> operation) {
            if(busy)return;busy=true;foreach(var b in GetComponentsInChildren<Button>())b.interactable=false;Message("system.loading");
            try{await operation();}
            catch(OperationCanceledException){}
            catch(Exception e){if(this){ClearRows();items.Clear();cursor=null;more.gameObject.SetActive(false);Message(e is SocialException limit&&limit.Code=="FRIEND_LIMIT_REACHED"?"social.limit_reached":e is SocialException denied&&denied.Code=="SOCIAL_ACTION_NOT_ALLOWED"?"social.cannot_complete":e is SocialException s&&s.Code=="INVALID_SEARCH_QUERY"?"social.invalid_query":e is SocialException p&&p.Code=="PLAYER_NOT_FOUND"?"social.unavailable":"social.error");
                var retry=Button("Retry","system.retry",rows,400,new Vector2(0,-90),()=>{if(section=="privacy")Navigate("privacy");else if(summary==null)Run(LoadSummary);else Run(()=>Load(true));});retry.GetComponent<RectTransform>().anchorMin=retry.GetComponent<RectTransform>().anchorMax=new Vector2(.5f,1);rows.sizeDelta=new Vector2(650,220);}}
            finally{busy=false;if(this){foreach(var b in GetComponentsInChildren<Button>())b.interactable=true;var add=rows.Find("Add friend");if(add)add.GetComponent<Button>().interactable=(bool?)summary?["friends"]?["canAddFriend"]??false;}}
        }
        void LateUpdate(){if(client==null)return;if(!client.SessionValid){Close();return;}panel.localScale=Vector3.one*Mathf.Max(.01f,Mathf.Min(safe.rect.width/750,safe.rect.height/1500));}
        public void Close(){lifetime.Cancel();items.Clear();summary=null;closed?.Invoke();closed=null;Destroy(gameObject);}
        void OnDestroy(){lifetime.Cancel();lifetime.Dispose();items.Clear();summary=null;}
    }
}
