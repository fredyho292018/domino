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
        void OnGUI() {
            EditorGUILayout.LabelField("Authenticated DUEL — explicit development flow");
            matchId=EditorGUILayout.TextField("Match ID",matchId);
            EditorGUI.BeginDisabledGroup(!EditorApplication.isPlaying||busy||ApplicationServices.Realtime?.State!=RealtimeConnectionState.CONNECTED);
            if(GUILayout.Button("Create online DUEL (seat 0)"))Run(false);
            if(GUILayout.Button("Join Match ID (seat 1)"))Run(true);
            EditorGUI.EndDisabledGroup();EditorGUILayout.HelpBox(result,MessageType.Info);
        }
        async void Run(bool join) {
            busy=true;
            try {
                if(Object.FindFirstObjectByType<OnlineMatchController>())throw new System.InvalidOperationException("Close the current online view first");
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
