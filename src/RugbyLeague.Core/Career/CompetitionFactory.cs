using RugbyLeague.Core.Model;

namespace RugbyLeague.Core.Careers;

/// <summary>Builds fixtures, knockout brackets and finals series.</summary>
public static class CompetitionFactory
{
    /// <summary>
    /// Double round-robin using the circle method. Odd-sized leagues receive a bye each round; a bye
    /// fixture has an empty away team.
    /// </summary>
    public static List<Fixture> GenerateDoubleRoundRobin(IReadOnlyList<Guid> teamIds, Random rng, int? maxRounds = null)
    {
        var order = teamIds.OrderBy(_ => rng.Next()).ToList();
        if (order.Count % 2 == 1) order.Add(Guid.Empty);

        var n = order.Count;
        var roundsPerHalf = n - 1;
        var fixtures = new List<Fixture>();

        for (var half = 0; half < 2; half++)
        {
            for (var r = 0; r < roundsPerHalf; r++)
            {
                var roundNumber = half * roundsPerHalf + r;

                for (var i = 0; i < n / 2; i++)
                {
                    var a = order[i];
                    var b = order[n - 1 - i];

                    if (a == Guid.Empty && b == Guid.Empty) continue;

                    if (a == Guid.Empty || b == Guid.Empty)
                    {
                        fixtures.Add(new Fixture
                        {
                            Round = roundNumber,
                            HomeTeamId = a == Guid.Empty ? b : a,
                            AwayTeamId = Guid.Empty
                        });
                        continue;
                    }

                    var home = a;
                    var away = b;
                    if (half == 1 || r % 2 == 1) (home, away) = (away, home);

                    fixtures.Add(new Fixture { Round = roundNumber, HomeTeamId = home, AwayTeamId = away });
                }

                // Rotate all but the first entrant.
                var last = order[^1];
                order.RemoveAt(order.Count - 1);
                order.Insert(1, last);
            }
        }

        if (maxRounds is int cap && cap > 0)
            fixtures.RemoveAll(f => f.Round >= cap);

        return fixtures;
    }

    public static Cup CreateKnockout(string name, string shortName, IReadOnlyList<Guid> seeds, long prizeMoney,
        Guid leagueId = default, bool isFinals = false)
    {
        var cup = new Cup
        {
            Name = name,
            ShortName = shortName,
            Entrants = seeds.ToList(),
            PrizeMoney = prizeMoney,
            LeagueId = leagueId,
            IsFinalsSeries = isFinals
        };

        var bracketSize = NextPowerOfTwo(seeds.Count);
        var totalRounds = (int)Math.Log2(bracketSize);
        var byes = bracketSize - seeds.Count;

        var first = new CupRound { Index = 0, Name = RoundName(0, totalRounds, isFinals) };
        first.Byes.AddRange(seeds.Take(byes));

        var playing = seeds.Skip(byes).ToList();
        for (var i = 0; i < playing.Count / 2; i++)
        {
            first.Fixtures.Add(new Fixture
            {
                Round = 0,
                HomeTeamId = playing[i],
                AwayTeamId = playing[playing.Count - 1 - i]
            });
        }

        cup.Rounds.Add(first);

        for (var i = 1; i < totalRounds; i++)
            cup.Rounds.Add(new CupRound { Index = i, Name = RoundName(i, totalRounds, isFinals) });

        return cup;
    }

    /// <summary>Seeds a new round from the winners plus the previous round's byes, re-seeding best v worst.</summary>
    public static void Advance(Cup cup, IReadOnlyList<Guid> winners)
    {
        var round = cup.CurrentRound;
        if (round is null || cup.IsComplete) return;

        var qualified = round.Byes.Concat(winners)
            .Distinct()
            .OrderBy(id => cup.Entrants.IndexOf(id))
            .ToList();

        if (qualified.Count <= 1)
        {
            cup.WinnerId = qualified.FirstOrDefault();
            if (cup.WinnerId == Guid.Empty) cup.WinnerId = null;
            return;
        }

        if (cup.CurrentRoundIndex + 1 >= cup.Rounds.Count)
        {
            cup.WinnerId = qualified[0];
            return;
        }

        cup.CurrentRoundIndex++;
        var next = cup.Rounds[cup.CurrentRoundIndex];
        next.Fixtures.Clear();

        if (qualified.Count % 2 == 1)
        {
            next.Byes.Add(qualified[0]);
            qualified.RemoveAt(0);
        }

        for (var i = 0; i < qualified.Count / 2; i++)
        {
            next.Fixtures.Add(new Fixture
            {
                Round = cup.CurrentRoundIndex,
                HomeTeamId = qualified[i],
                AwayTeamId = qualified[qualified.Count - 1 - i]
            });
        }
    }

    public static int NextPowerOfTwo(int value)
    {
        var power = 2;
        while (power < value) power *= 2;
        return power;
    }

    private static string RoundName(int index, int totalRounds, bool finals)
    {
        var remaining = totalRounds - index;
        if (remaining == 1) return finals ? "Grand Final" : "Final";
        if (remaining == 2) return "Semi-Finals";
        if (remaining == 3) return "Quarter-Finals";
        return finals ? $"Finals Round {index + 1}" : $"Round {index + 1}";
    }
}
