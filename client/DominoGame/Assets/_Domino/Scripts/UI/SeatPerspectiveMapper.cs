using System;
using Domino.Configuration;

namespace Domino.UI
{
    public enum VisualSeat { Bottom, Left, Top, Right }

    /// <summary>Immutable presentation mapping. Logical seats and turn order are never rewritten.</summary>
    public sealed class SeatPerspectiveMapper
    {
        readonly int[] seats = new int[4];
        public int BottomPlayer => seats[(int)VisualSeat.Bottom];
        public int TopPlayer => seats[(int)VisualSeat.Top];
        public int LeftPlayer => seats[(int)VisualSeat.Left];
        public int RightPlayer => seats[(int)VisualSeat.Right];
        public SeatPerspectiveMapper(int localPlayerSeat, GameConfigurationSnapshot configuration)
        {
            if (configuration == null) throw new ArgumentNullException(nameof(configuration));
            if ((configuration.PlayerCount != 4 && configuration.PlayerCount != 2) || localPlayerSeat < 0 || localPlayerSeat >= configuration.PlayerCount)
                throw new ArgumentOutOfRangeException(nameof(localPlayerSeat));
            seats[0] = localPlayerSeat;
            if(configuration.PlayerCount==2) {seats[1]=-1;seats[3]=-1;seats[2]=1-localPlayerSeat;return;}
            // Also supports the existing individual development configuration: the opposite
            // seat occupies Top, but presentation must not call that player a partner.
            int localIndex = 0;
            for (int i = 0; i < 4; i++) if (configuration.TurnOrder[i] == localPlayerSeat) localIndex = i;
            seats[2] = configuration.TurnOrder[(localIndex + 2) % 4];
            if (configuration.TeamMode == TeamMode.FixedTeams)
            {
                var members = configuration.TeamAssignments[configuration.GetTeamForPlayer(localPlayerSeat)];
                if (members.Count != 2) throw new ArgumentException("Perspective requires two players per team.");
                seats[2] = members[0] == localPlayerSeat ? members[1] : members[0];
            }
            bool first = true;
            for (int step = 1; step < 4; step++)
            {
                int seat = configuration.TurnOrder[(localIndex + step) % 4];
                if (seat == seats[2]) continue;
                seats[first ? 3 : 1] = seat;
                first = false;
            }
        }
        public int PlayerAt(VisualSeat position) => seats[(int)position];
        public VisualSeat PositionFor(int logicalSeat)
        {
            for (int i = 0; i < seats.Length; i++) if (seats[i] == logicalSeat) return (VisualSeat)i;
            throw new ArgumentOutOfRangeException(nameof(logicalSeat));
        }
    }
}
