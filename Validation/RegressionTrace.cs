using System;
using System.Linq;
using System.Text;
using System.Security.Cryptography;
using Domino.Core;
using Domino.Game;

// Captured against source 31679fe before changing the engine. Tile IDs are intentionally
// excluded: changing the identity encoding must not change hands, actions or scores.
static class RegressionTrace
{
    public static string Compute(Func<ClientGame> create)
    {
        var text = new StringBuilder();
        for (int seed = 0; seed < 100; seed++)
        {
            var game = create();
            game.Changed += e => text.Append($"E:{e.Type}:{e.Player}:{e.Tile.SideA},{e.Tile.SideB}:{e.ChainIndex};");
            game.Start(seed);
            for (int round = 0; round < 100; round++)
            {
                for (int p = 0; p < 4; p++)
                    text.Append($"H{p}:" + string.Join(",", game.Hand(p).Select(t => t.ToString())) + ";");
                text.Append("R:" + string.Join(",", game.Reserve.Select(t => t.ToString())) + ";");
                int actions = 0;
                while (!game.Finished)
                {
                    if (++actions > 200) throw new Exception("Regression round did not finish");
                    int player = game.CurrentPlayer;
                    var legal = game.Hand(player).Where(t => game.CanPlay(t)).ToArray();
                    if (legal.Length == 0) game.TryPass(player);
                    else game.TryPlay(player, legal[(seed + actions) % legal.Length]);
                }
                var result = game.Result;
                text.Append($"W:{result.WinnerPlayer}:{result.WinnerSide}:{result.Blocked}:{result.Award}:{result.Multiplier};");
                text.Append(string.Join(",", result.HandPoints));
                text.Append($"S:{game.Match.Score(0)}:{game.Match.Score(1)}:{game.Match.Multiplier};");
                if (game.Match.Finished) break;
                if (round == 99 || !game.StartNextRound(seed + (round + 1) * 1000)) throw new Exception("Regression match did not finish");
            }
        }
        using var sha = SHA256.Create();
        return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(text.ToString()))).Replace("-", "");
    }
}
