using System;
using System.Linq;
using Domino.UI;

static class PerspectiveTests
{
    public static void Run()
    {
        var config = ConfigurationTests.Rules().Configuration;
        int[,] expected = { {0,1,2,3}, {1,2,3,0}, {2,3,0,1}, {3,0,1,2} };
        for (int local = 0; local < 4; local++)
        {
            var mapping = new SeatPerspectiveMapper(local, config);
            for (int position = 0; position < 4; position++)
            {
                int seat = mapping.PlayerAt((VisualSeat)position);
                if (seat != expected[local,position] || mapping.PositionFor(seat) != (VisualSeat)position) throw new Exception("Incorrect perspective");
            }
            if (config.GetTeamForPlayer(mapping.TopPlayer) != config.GetTeamForPlayer(local)) throw new Exception("Partner mapping");
            if (!config.TurnOrder.SequenceEqual(new[] {0,3,2,1})) throw new Exception("Turn order mutated");
            Console.WriteLine("PERSPECTIVE_SEAT_" + local + "=PASS");
        }
        foreach (int bad in new[] {-1,4})
        {
            try { new SeatPerspectiveMapper(bad,config); throw new Exception("Accepted invalid seat"); }
            catch (ArgumentOutOfRangeException) { }
        }
        var reassigned = ConfigurationTests.Rules(d =>
        {
            d.teamAssignments[0].members = new[] {0,1};
            d.teamAssignments[1].members = new[] {2,3};
            d.turnOrder = new[] {0,2,1,3};
        }).Configuration;
        var other = new SeatPerspectiveMapper(0,reassigned);
        if (other.TopPlayer != 1 || other.RightPlayer != 2 || other.LeftPlayer != 3) throw new Exception("Mapper assumed seat pairs");
        Console.WriteLine("PERSPECTIVE_ASSIGNMENT_INDEPENDENCE=PASS");
    }
}
