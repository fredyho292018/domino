using System;
using System.Collections;
using System.Collections.Generic;
using Domino.Core;
using Domino.Game;
using Domino.UI;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Domino.Client
{
    public sealed class DominoClientController : MonoBehaviour
    {
        [SerializeField] DominoTileView tilePrefab;
        [SerializeField] PlayerView playerPrefab;
        [Header("Configuración local de reglas")]
        [SerializeField] TextAsset configurationJson;
        ClientGame game;
        readonly Queue<GameEvent> events = new();
        BoardView board;
        DominoTileView selected;
        bool acceptingInput;
        bool previewing;
        Domino.Configuration.GameConfigurationSnapshot configuration;
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
            try
            {
                configuration = LocalGameConfiguration.Load(configurationJson);
                Debug.Log($"CONFIGURATION_LOAD=SUCCESS id={configuration.Id} version={configuration.Version} schema={configuration.SchemaVersion} ruleset={configuration.RulesetVersion}");
            }
            catch (Exception error)
            {
                Debug.LogError("CONFIGURATION_LOAD=FAILURE: " + error.Message, this);
                enabled = false;
                yield break;
            }
            if (!tilePrefab || !playerPrefab) throw new InvalidOperationException("Assign the DominoTile and Player prefabs in DominoClient.");
            if (!FindFirstObjectByType<EventSystem>()) new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            startMenu = new GameObject("Domino Start Menu", typeof(RectTransform)).AddComponent<StartMenuView>();
            startMenu.Initialize(GameModeDefinition.TeamMatch, configuration);
            startMenu.StartRequested += StartMatch;
        }
        public void StartMatch(GameModeDefinition mode) => StartMatch(mode, mode.LocalPlayerSeat);
        public void StartMatch(GameModeDefinition mode, int localPlayerSeat)
        {
            if (Session != null) return;
            Session = new SessionSetup(mode, configuration, localPlayerSeat);
            game = new ClientGame(new GameRules(Session.Configuration));
            startMenu.Show(StartScreen.Match);
            board = new GameObject("Domino Canvas", typeof(RectTransform)).AddComponent<BoardView>();
            board.Initialize(tilePrefab, playerPrefab, game.Configuration, Session.LocalPlayerSeat);
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
        public void ExitMatch()
        {
            StopAllCoroutines(); events.Clear(); acceptingInput = false; previewing = false; selected = null;
            if (game != null) game.Changed -= Enqueue;
            if (board) { board.Clear(); board.gameObject.SetActive(false); Destroy(board.gameObject); }
            board = null; game = null; Session = null;
            startMenu.Show(StartScreen.ModeSelector);
        }
        void Enqueue(GameEvent e) => events.Enqueue(e);
        public void RestartClient()
        {
            if (game == null || !board) return;
            previewing = false;
            StopAllCoroutines(); events.Clear(); acceptingInput = false; selected = null;
            board.Clear(); game.Start(Environment.TickCount);
            StartCoroutine(PresentEvents());
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
            selected = selected == tile ? null : tile;
            board.SetSelected(selected);
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
            if (!game.TryPlay(Session.LocalPlayerSeat, tile, end))
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
                        board.SetTurn(e.Player);
                        acceptingInput = e.Player == Session.LocalPlayerSeat && game.HasLegalMove(Session.LocalPlayerSeat);
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
                            return matchFinished ? DominoLocalization.Get("result.match_summary", DominoLocalization.Get(localWon ? "result.victory" : "result.defeat"), text) : text;
                        };
                        if (!result.Tie) yield return board.ShowWinner(winnerName, game.Match.Finished, localWon);
                        // Wash after every round, including a tied block, before allowing the next deal.
                        yield return board.WashDominoes(game.Reserve);
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
