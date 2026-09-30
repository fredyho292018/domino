using System;
using System.Collections.Generic;

namespace Domino.UI.AppShell
{
    public enum RootDestination { Puzzle, Lesson, CoachGames, WatchGame }
    public sealed class PuzzleSummary
    {
        public string Title { get; } public string Description { get; } public string Difficulty { get; } public string Progress { get; }
        public PuzzleSummary(string title,string description,string difficulty,string progress){Title=title;Description=description;Difficulty=difficulty;Progress=progress;}
    }
    public sealed class PuzzleCategorySummary
    {
        public string Name { get; } public string Description { get; }
        public PuzzleCategorySummary(string name,string description){Name=name;Description=description;}
    }
    public interface IPuzzlesDataSource { PuzzleSummary Daily(); IReadOnlyList<PuzzleCategorySummary> Categories(); }
    public sealed class DemoPuzzlesDataSource : IPuzzlesDataSource
    {
        public PuzzleSummary Daily()=>new PuzzleSummary("Find the next move","Read the table. Choose your best opportunity.","Intermediate","0 / 4 demo challenges");
        public IReadOnlyList<PuzzleCategorySummary> Categories()=>Array.AsReadOnly(new[]{new PuzzleCategorySummary("Opening","Build a strong first move."),new PuzzleCategorySummary("Strategy","See beyond the next tile."),new PuzzleCategorySummary("Counting","Keep track of the possibilities."),new PuzzleCategorySummary("Endgame","Make the final moves count.")});
    }
    public interface ILearnDataSource { HomeCoachSummary Coach { get; } }
    public sealed class DemoLearnDataSource : ILearnDataSource
    {
        public HomeCoachSummary Coach { get; }
        public DemoLearnDataSource(HomeCoachSummary coach){Coach=coach??throw new ArgumentNullException(nameof(coach));}
    }
    public sealed class WatchGameSummary
    {
        public string Players { get; } public string Mode { get; } public string Duration { get; }
        public WatchGameSummary(string players,string mode,string duration){Players=players;Mode=mode;Duration=duration;}
    }
    public interface IWatchDataSource { IReadOnlyList<WatchGameSummary> Games(); }
    public sealed class DemoWatchDataSource : IWatchDataSource
    {
        public IReadOnlyList<WatchGameSummary> Games()=>Array.AsReadOnly(new[]{new WatchGameSummary("Ana & Luis vs Pedro & Isabel","2v2","Demo · 12 min"),new WatchGameSummary("Alex vs Nora","1v1","Demo · 8 min"),new WatchGameSummary("Carlos & Elena vs Juan & Sofía","2v2","Demo · 15 min")});
    }
}
