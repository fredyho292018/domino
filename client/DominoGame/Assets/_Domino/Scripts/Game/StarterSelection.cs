using System;
using Domino.Core;
using Domino.Configuration;

namespace Domino.Game
{
    // Virtual tiles are isolated from the match deck and its shuffle RNG.
    public sealed class StarterSelection
    {
        readonly Random random;
        readonly int maxPip;
        readonly DominoTile[] candidates = new DominoTile[2];
        public StarterMethod Method { get; }
        public int SelectorSeat { get; }
        public int GuesserSeat => 1-SelectorSeat;
        public int NextSelectionSeat { get; private set; }
        public int WinnerSeat { get; private set; } = -1;
        public bool Complete => WinnerSeat >= 0;
        public bool WaitingForGuess { get; private set; }
        public int Attempt { get; private set; } = 1;
        public DominoTile? FirstRevealed { get; private set; }
        public DominoTile? SecondRevealed { get; private set; }
        int firstIndex = -1;
        public int SelectedCandidateIndex => firstIndex;
        public StarterSelection(GameConfigurationSnapshot config, int seed)
        {
            if(config.FirstRoundStarting.Mode!=StartingPolicy.RandomStartMethod || config.PlayerCount!=2) throw new ArgumentException("Starter selection needs two seats.");
            random=new Random(seed);maxPip=config.MaxPip;
            Method=config.FirstRoundStarting.Methods[random.Next(config.FirstRoundStarting.Methods.Count)];
            SelectorSeat=random.Next(2);NextSelectionSeat=Method==StarterMethod.HIGH_TILE_SELECTION?0:SelectorSeat;
            DrawCandidates();
        }
        void DrawCandidates()
        {
            var tiles=DominoTile.CreateSet(maxPip);
            int a=random.Next(tiles.Count);candidates[0]=tiles[a];tiles.RemoveAt(a);
            candidates[1]=tiles[random.Next(tiles.Count)];firstIndex=-1;
        }
        public bool Choose(int seat,int index)
        {
            if(Complete||WaitingForGuess||seat!=NextSelectionSeat||index<0||index>1||index==firstIndex)return false;
            if(Method==StarterMethod.EVEN_ODD_GUESS) { firstIndex=index;WaitingForGuess=true;return true; }
            if(firstIndex<0) {FirstRevealed=null;SecondRevealed=null;firstIndex=index;NextSelectionSeat=1;return true;}
            FirstRevealed=candidates[firstIndex];SecondRevealed=candidates[index];
            WinnerSeat=Compare(FirstRevealed.Value,SecondRevealed.Value);
            if(WinnerSeat<0) {Attempt++;NextSelectionSeat=0;DrawCandidates();}
            return true;
        }
        public bool Guess(int seat,bool even)
        {
            if(Complete||!WaitingForGuess||seat!=GuesserSeat)return false;
            FirstRevealed=candidates[firstIndex];WinnerSeat=ResolveGuess(FirstRevealed.Value,SelectorSeat,even);WaitingForGuess=false;return true;
        }
        public static int Compare(DominoTile a,DominoTile b) => Sum(a)==Sum(b)?-1:Sum(a)>Sum(b)?0:1;
        public static int ResolveGuess(DominoTile tile,int selector,bool even) => (Sum(tile)%2==0)==even?1-selector:selector;
        static int Sum(DominoTile tile)=>tile.SideA+tile.SideB;
    }
}
