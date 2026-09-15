#if UNITY_EDITOR
using Domino.Infrastructure;
using Domino.Realtime;
using Domino.UI;
using UnityEditor;
using UnityEngine;

namespace Domino.Online.Editor
{
    public sealed class OnlineDevelopmentMenu : EditorWindow
    {
        string matchId="",result="";bool busy;
        [MenuItem("Domino/Online/Open DUEL development controls")]
        static void Open()=>GetWindow<OnlineDevelopmentMenu>("Online DUEL I1");
        [MenuItem("Domino/Online/Start local shared-device DUEL (development)")]
        static void StartLocal() {
            if(!EditorApplication.isPlaying||!Domino.Online.OnlineDevelopmentAccess.Allowed||Object.FindFirstObjectByType<MatchmakingView>()||Object.FindFirstObjectByType<OnlineMatchController>()||Object.FindFirstObjectByType<OnlineEntryView>())return;
            var controller=Object.FindFirstObjectByType<Domino.Client.DominoClientController>();
            if(controller&&controller.Session==null)controller.StartMatch(new Domino.Client.GameModeDefinition(ApplicationServices.GameCatalog.ResolveMatch("DUEL_1V1").Mode));
        }
        void OnGUI() {
            EditorGUILayout.LabelField("Authenticated DUEL — explicit development flow");
            matchId=EditorGUILayout.TextField("Match ID",matchId);
            EditorGUI.BeginDisabledGroup(!EditorApplication.isPlaying||!Domino.Online.OnlineDevelopmentAccess.Allowed||Object.FindFirstObjectByType<MatchmakingView>()||busy||ApplicationServices.Realtime?.State!=RealtimeConnectionState.CONNECTED);
            if(GUILayout.Button("Create online DUEL (seat 0)"))Run(false);
            if(GUILayout.Button("Join Match ID (seat 1)"))Run(true);
            EditorGUI.EndDisabledGroup();EditorGUILayout.HelpBox(result,MessageType.Info);
        }
        async void Run(bool join) {
            busy=true;
            try {
                if(!Domino.Online.OnlineDevelopmentAccess.Allowed||Object.FindFirstObjectByType<MatchmakingView>()||Object.FindFirstObjectByType<OnlineMatchController>())throw new System.InvalidOperationException("Close the current online view first");
                var client=new OnlineMatchClient(ApplicationServices.OnlineApi,(IRealtimeMatchChannel)ApplicationServices.Realtime);
                try {if(join)await client.JoinAsync(matchId.Trim());else await client.CreateAsync();}
                catch {client.Dispose();throw;}
                var tile=AssetDatabase.LoadAssetAtPath<DominoTileView>("Assets/_Domino/Prefabs/DominoTile.prefab");
                var player=AssetDatabase.LoadAssetAtPath<PlayerView>("Assets/_Domino/Prefabs/Player.prefab");
                var controller=new GameObject("Online match presentation").AddComponent<OnlineMatchController>();controller.Initialize(client,tile,player);
                matchId=client.Snapshot.MatchId;result="Match created/joined. Share this ID with the other authenticated client.";
            } catch {result="Online operation unavailable. Verify authenticated realtime and use a different Firebase UID on the second client.";}
            finally {busy=false;Repaint();}
        }
    }
}
#endif
