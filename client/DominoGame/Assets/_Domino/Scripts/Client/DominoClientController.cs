using System;
using System.Collections;
using System.Collections.Generic;
using Domino.Core;
using Domino.Game;
using Domino.UI;
using Domino.Catalog;
using Domino.Infrastructure;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Domino.Client
{
    public sealed class DominoClientController : MonoBehaviour
    {
        [SerializeField] DominoTileView tilePrefab;
        [SerializeField] PlayerView playerPrefab;
        // Serialized legacy reference retained for differential/editor validation only.
        [SerializeField, HideInInspector] TextAsset configurationJson;
        ClientGame game;
        readonly Queue<GameEvent> events = new();
        BoardView board;
        DominoTileView selected;
        bool acceptingInput;
        bool previewing;
        SharedDevicePrompt sharedPrompt;
        public SharedDevicePrompt SharedPrompt => sharedPrompt;
        public StarterSelection StarterSelection { get; private set; }
        int InputSeat => Session.SharedDevice?game.CurrentPlayer:Session.LocalPlayerSeat;
        StartMenuView startMenu;
        public StartMenuView Menu => startMenu;
        public SessionSetup Session { get; private set; }
        public ClientGame State => game;
        public BoardView View => board;
        public bool AcceptingInput => acceptingInput;
        IEnumerator Start()
        {
            Application.targetFrameRate = 60;
            yield return DominoLocalization.Initialize();
            var preview = ResolveConfiguration();
            if (!tilePrefab || !playerPrefab) throw new InvalidOperationException("Assign the DominoTile and Player prefabs in DominoClient.");
            if (!FindFirstObjectByType<EventSystem>()) new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            startMenu = new GameObject("Domino Start Menu", typeof(RectTransform)).AddComponent<StartMenuView>();
            startMenu.Initialize(new GameModeDefinition(preview.Mode), preview.Configuration);
            startMenu.StartRequested += StartFromMenu;
            startMenu.SocialRequested += () => {
                startMenu.Show(StartScreen.Match);
                var social=new GameObject("Social",typeof(RectTransform)).AddComponent<Domino.Social.SocialView>();
                social.Initialize(new Domino.Social.SocialClient(ApplicationServices.SocialApi,()=>ApplicationServices.Identity?.Current?.Uid),()=>{if(startMenu)startMenu.Show(StartScreen.MainMenu);});
            };
            startMenu.HistoryRequested += () => {
                startMenu.Show(StartScreen.Match);
                var history=new GameObject("Match history",typeof(RectTransform)).AddComponent<Domino.Replay.HistoryReplayView>();
                history.Initialize(new Domino.Replay.ReplayClient(ApplicationServices.OnlineApi),tilePrefab,playerPrefab,()=>{if(startMenu)startMenu.Show(StartScreen.MainMenu);});
            };
        }
        void StartFromMenu(GameModeDefinition mode)
        {
            if(!mode.Online){StartMatch(mode);return;}
            startMenu.Show(StartScreen.Match);
            var entry=new GameObject("Find opponent",typeof(RectTransform)).AddComponent<Domino.Online.MatchmakingView>();
            entry.Initialize(tilePrefab,playerPrefab,()=>{if(startMenu)startMenu.Show(StartScreen.ModeSelector);},mode.Key);
        }
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        public void OpenDevelopmentOnlineEntry()
        {
            if(!Domino.Online.OnlineDevelopmentAccess.Allowed||!startMenu||startMenu.Screen==StartScreen.Match||Session!=null)return;
            startMenu.Show(StartScreen.Match);
            var entry=new GameObject("Development online entry",typeof(RectTransform)).AddComponent<Domino.Online.OnlineEntryView>();
            entry.Initialize(tilePrefab,playerPrefab,()=>{if(startMenu)startMenu.Show(StartScreen.ModeSelector);});
        }
#endif
        public void StartMatch(GameModeDefinition mode) => StartMatch(mode, mode.LocalPlayerSeat);
        public void StartMatch(GameModeDefinition mode, int localPlayerSeat)
        {
            if (Session != null) return;
            if(mode==null||(mode.Key!=GameCatalogConfigurationAdapter.SupportedModeKey&&mode.Key!=GameCatalogConfigurationAdapter.DuelModeKey))throw new ArgumentException("Unsupported mode.");
            var resolved=ResolveConfiguration(mode.Key);
            Session = new SessionSetup(resolved, localPlayerSeat);
            Debug.Log(resolved.Diagnostic);
            game = new ClientGame(new GameRules(Session.Configuration));
            startMenu.Show(StartScreen.Match);
            board = new GameObject("Domino Canvas", typeof(RectTransform)).AddComponent<BoardView>();
            board.Initialize(tilePrefab, playerPrefab, game.Configuration, Session.LocalPlayerSeat,Session.SharedDevice);
            board.CanPlace = (tile, end) => game.CanPlay(tile, end);
            board.TileSelected += Select;
            board.TileDropped += PlayDropped;
            board.PlayRequested += PlaySelected;
            board.RestartRequested += RestartClient;
            board.NextRoundRequested += NextRound;
            board.ExitRequested += ExitMatch;
            game.Changed += Enqueue;
            RestartClient();
        }
        static MatchRuleSnapshot ResolveConfiguration(string key=GameCatalogConfigurationAdapter.SupportedModeKey) =>
            (ApplicationServices.GameCatalog ?? throw new InvalidOperationException("No valid bundled game catalog available.")).ResolveMatch(key);
        public void ExitMatch()
        {
            StopAllCoroutines(); events.Clear(); acceptingInput = false; previewing = false; selected = null;
            if (game != null) game.Changed -= Enqueue;
            if (board) { board.Clear(); board.gameObject.SetActive(false); Destroy(board.gameObject); }
            if(sharedPrompt)Destroy(sharedPrompt.gameObject);sharedPrompt=null;StarterSelection=null;
            board = null; game = null; Session = null;
            startMenu.Show(StartScreen.ModeSelector);
        }
        void Enqueue(GameEvent e)
        {
            events.Enqueue(e);
            if(!Session.SharedDevice) Domino.Ads.RewardedRoundPreload.Handle(e, Domino.Infrastructure.ApplicationServices.Rewarded);
        }
        public void RestartClient()
        {
            if (game == null || !board) return;
            previewing = false;
            StopAllCoroutines(); events.Clear(); acceptingInput = false; selected = null;
            board.Clear();
            if(Session.SharedDevice)StartCoroutine(StartSharedMatch());
            else {game.Start(Environment.TickCount);StartCoroutine(PresentEvents());}
        }
        IEnumerator StartSharedMatch()
        {
            if(!sharedPrompt)sharedPrompt=new GameObject("Shared device prompt",typeof(RectTransform)).AddComponent<SharedDevicePrompt>();
            StarterSelection=new StarterSelection(Session.Configuration,Environment.TickCount);
            yield return sharedPrompt.ChooseStarter(StarterSelection,board);
            game.Start(Environment.TickCount,StarterSelection.WinnerSeat);
            yield return PresentEvents();
        }
        void NextRound()
        {
            if (!game.Finished || game.Match.Finished) return;
            StopAllCoroutines(); events.Clear(); acceptingInput = false; selected = null;
            board.Clear(); game.StartNextRound(Environment.TickCount);
            StartCoroutine(PresentEvents());
        }
        public void Select(DominoTileView tile)
        {
            if (!acceptingInput || board.IsDragging || !board.LocalTiles.ContainsReference(tile)) return;
            selected = board.ToggleSelection(tile);
        }
        public void PlaySelected()
        { PlaySelection(ChainEnd.Auto); }
        public void PreviewWash()
        {
            if (!acceptingInput || previewing || board.IsDragging)
            { Debug.Log("Espera al turno del jugador local para previsualizar darle agua."); return; }
            StartCoroutine(PreviewWashRoutine());
        }
        IEnumerator PreviewWashRoutine()
        {
            previewing = true; acceptingInput = false;
            board.SetInteraction(false, false);
            yield return board.PreviewWash();
            previewing = false; acceptingInput = true;
            board.SetTurn(Session.LocalPlayerSeat); board.SetSelected(selected);
            board.SetInteraction(true, selected != null);
        }
        void PlaySelection(ChainEnd end)
        {
            if (!acceptingInput || board.IsDragging || !selected) return;
            var tile = selected.Tile;
            acceptingInput = false; board.SetInteraction(false, false);
            if (!game.TryPlay(InputSeat, tile, end))
            {
                acceptingInput = true; board.SetInteraction(true, true);
                board.ShowMessage("game.no_match", game.LeftEnd, game.RightEnd);
                return;
            }
            selected = null;
        }
        void PlayDropped(DominoTileView tile, ChainEnd end)
        {
            if (!acceptingInput || !board.LocalTiles.ContainsReference(tile)) return;
            selected = tile;
            board.SetSelected(tile);
            PlaySelection(end);
        }
        IEnumerator PresentEvents()
        {
            while (true)
            {
                if (events.Count == 0) { yield return null; continue; }
                var e = events.Dequeue();
                switch (e.Type)
                {
                    case GameEventType.GAME_STARTED:
                        board.UpdateScore(game.Rules, game.Match);
                        var hands = new IReadOnlyList<DominoTile>[game.Configuration.PlayerCount];
                        for (int p = 0; p < hands.Length; p++) hands[p] = game.Hand(p);
                        yield return board.Deal(hands);
                        break;
                    case GameEventType.TILE_PLAYED:
                        yield return board.Play(e);
                        break;
                    case GameEventType.TURN_CHANGED:
                        if(Session.SharedDevice) {
                            acceptingInput=false;board.HideHands();
                            yield return sharedPrompt.Handoff(e.Player);
                            board.RevealActiveSeat(e.Player);
                        }
                        board.SetTurn(e.Player);
                        acceptingInput = e.Player == InputSeat && game.HasLegalMove(InputSeat);
                        board.SetInteraction(acceptingInput, false);
                        if (!game.HasLegalMove(e.Player))
                        {
                            board.ShowMessage("game.player_passed", new[] { "Fredy", "Alex", "Maria", "John" }[e.Player]);
                            game.TryPass(e.Player);
                        }
                        else if (Session.IsBot(e.Player))
                        {
                            yield return new WaitForSeconds(UnityEngine.Random.Range(1.1f, 1.7f));
                            foreach (var candidate in game.Hand(e.Player))
                            {
                                if (!game.CanPlay(candidate)) continue;
                                game.TryPlay(e.Player, candidate);
                                break;
                            }
                        }
                        break;
                    case GameEventType.PLAYER_PASSED:
                        yield return board.ShowPass(e.Player);
                        break;
                    case GameEventType.ROUND_FINISHED:
                        acceptingInput = false;
                        board.SetInteraction(false, false);
                        yield return board.RevealHands(game.Result.HandPoints);
                        board.UpdateScore(game.Rules, game.Match);
                        var result = game.Result;
                        string winnerName = result.Tie ? "" : game.Rules.Teams
                            ? TeamName(result.WinnerSide)
                            : new[] { "Fredy", "Alex", "Maria", "John" }[result.WinnerPlayer];
                        bool localWon = !result.Tie && (game.Rules.Teams ? result.WinnerSide == game.Configuration.GetTeamForPlayer(Session.LocalPlayerSeat) : result.WinnerPlayer == Session.LocalPlayerSeat);
                        int multiplier = game.Match.Multiplier;
                        bool matchFinished = game.Match.Finished;
                        Func<string> summary = () =>
                        {
                            string text = result.Tie ? DominoLocalization.Get("result.tie_summary", multiplier)
                                : DominoLocalization.Get("result.award", winnerName, result.Award, result.BasePoints, result.Bonus, result.Multiplier);
                            if(result.FinishType==RoundFinishType.CAPICUA)text=DominoLocalization.Get("result.capicua")+" · "+text;
                            return matchFinished ? DominoLocalization.Get("result.match_summary", DominoLocalization.Get(localWon ? "result.victory" : "result.defeat"), text) : text;
                        };
                        if (!result.Tie) yield return board.ShowWinner(winnerName, game.Match.Finished, localWon);
                        // Wash after every round, including a tied block, before allowing the next deal.
                        yield return board.WashDominoes(game.Reserve);
                        if(!Session.SharedDevice) Domino.Infrastructure.ApplicationServices.RoundRewards?.PresentRound(result);
                        board.Finish(summary, game.Match.Finished);
                        break;
                }
            }
        }
        void OnDestroy()
        {
            if (game != null) game.Changed -= Enqueue;
            if (board) Destroy(board.gameObject);
            if (startMenu) Destroy(startMenu.gameObject);
            if(sharedPrompt)Destroy(sharedPrompt.gameObject);
        }
        string TeamName(int team)
        {
            string[] names = { "Fredy", "Alex", "Maria", "John" };
            var members = game.Rules.GetTeamMembers(team);
            var labels = new string[members.Count];
            for (int i = 0; i < members.Count; i++) labels[i] = names[members[i]];
            return string.Join("–", labels);
        }
    }
    internal static class ViewListExtensions
    {
        public static bool ContainsReference(this IReadOnlyList<DominoTileView> list, DominoTileView tile)
        { for (int i = 0; i < list.Count; i++) if (list[i] == tile) return true; return false; }
    }
}
