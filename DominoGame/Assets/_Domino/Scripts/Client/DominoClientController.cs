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
        public ClientGame State => game;
        public BoardView View => board;
        public bool AcceptingInput => acceptingInput;
        void Start()
        {
            Application.targetFrameRate = 60;
            try
            {
                var configuration = LocalGameConfiguration.Load(configurationJson);
                game = new ClientGame(new GameRules(configuration));
                Debug.Log($"CONFIGURATION_LOAD=SUCCESS id={configuration.Id} version={configuration.Version} schema={configuration.SchemaVersion} ruleset={configuration.RulesetVersion}");
            }
            catch (Exception error)
            {
                Debug.LogError("CONFIGURATION_LOAD=FAILURE: " + error.Message, this);
                enabled = false;
                return;
            }
            if (!tilePrefab || !playerPrefab) throw new InvalidOperationException("Assign the DominoTile and Player prefabs in DominoClient.");
            if (!FindFirstObjectByType<EventSystem>()) new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            board = new GameObject("Domino Canvas", typeof(RectTransform)).AddComponent<BoardView>();
            board.Initialize(tilePrefab, playerPrefab, game.Configuration);
            board.CanPlace = (tile, end) => game.CanPlay(tile, end);
            board.TileSelected += Select;
            board.TileDropped += PlayDropped;
            board.PlayRequested += PlaySelected;
            board.RestartRequested += RestartClient;
            board.NextRoundRequested += NextRound;
            game.Changed += Enqueue;
            RestartClient();
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
            { Debug.Log("Espera al turno de Fredy para previsualizar darle agua."); return; }
            StartCoroutine(PreviewWashRoutine());
        }
        IEnumerator PreviewWashRoutine()
        {
            previewing = true; acceptingInput = false;
            board.SetInteraction(false, false);
            yield return board.PreviewWash();
            previewing = false; acceptingInput = true;
            board.SetTurn(0); board.SetSelected(selected);
            board.SetInteraction(true, selected != null);
        }
        void PlaySelection(ChainEnd end)
        {
            if (!acceptingInput || board.IsDragging || !selected) return;
            var tile = selected.Tile;
            acceptingInput = false; board.SetInteraction(false, false);
            if (!game.TryPlay(0, tile, end))
            {
                acceptingInput = true; board.SetInteraction(true, true);
                board.ShowMessage($"No encaja: necesitas un {game.LeftEnd} o un {game.RightEnd}");
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
                        acceptingInput = e.Player == 0 && game.HasLegalMove(0);
                        board.SetInteraction(acceptingInput, false);
                        if (!game.HasLegalMove(e.Player))
                        {
                            board.ShowMessage(new[] { "Fredy", "Alex", "Maria", "John" }[e.Player] + " no tiene jugada: pasa");
                            game.TryPass(e.Player);
                        }
                        // TODO: Human/bot seat ownership belongs to SessionSetup, not rules.
                        else if (e.Player != 0)
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
                        string summary = result.Tie ? $"Tranca empatada · Próxima ronda ×{game.Match.Multiplier}"
                            : $"{winnerName}: +{result.Award} pts ({result.BasePoints} + {result.Bonus}) ×{result.Multiplier}";
                        if (game.Match.Finished) summary = "PARTIDA GANADA · " + summary;
                        if (!result.Tie) yield return board.ShowWinner(winnerName, game.Match.Finished);
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
