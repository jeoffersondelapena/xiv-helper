using XIVHelper.Demiatma;
using Xunit;

namespace XIVHelper.Tests;

public class DemiatmaWatchTests
{
    private static readonly HashSet<uint> None = new();

    private static Dictionary<DemiatmaKind, int> Counts(params (DemiatmaKind Kind, int Count)[] pairs) =>
        pairs.ToDictionary(p => p.Kind, p => p.Count);

    [Fact]
    public void Every_kind_has_fates_and_critical_encounters_in_south_horn()
    {
        Assert.Equal(13, DemiatmaWatch.Fates.Count);
        Assert.Equal(15, DemiatmaWatch.CriticalEncounters.Count);
        foreach (var kind in DemiatmaWatch.Kinds)
        {
            Assert.Contains(kind, DemiatmaWatch.Fates.Values);
            Assert.Contains(kind, DemiatmaWatch.CriticalEncounters.Values);
        }
        Assert.Equal(DemiatmaKind.Orpiment, DemiatmaWatch.Fates[1976]);
        Assert.Equal(DemiatmaKind.Verdigris, DemiatmaWatch.Fates[1977]);
    }

    [Fact]
    public void Three_of_a_kind_is_complete()
    {
        var complete = DemiatmaWatch.Complete(Counts((DemiatmaKind.Azurite, 3), (DemiatmaKind.Verdigris, 2), (DemiatmaKind.Realgar, 4)));
        Assert.Equal(new HashSet<DemiatmaKind> { DemiatmaKind.Azurite, DemiatmaKind.Realgar }, complete);
    }

    [Fact]
    public void A_complete_kind_goes_on_both_lists_except_its_pot_fate()
    {
        var plan = DemiatmaWatch.Plan(new HashSet<DemiatmaKind> { DemiatmaKind.Orpiment }, None, None, None, None);
        Assert.Equal(new HashSet<uint> { 1964, 1971 }, plan.AddFates);
        Assert.Equal(new HashSet<uint> { 34, 44 }, plan.AddCriticalEncounters);
        Assert.Empty(plan.RemoveFates);
        Assert.Empty(plan.RemoveCriticalEncounters);
        Assert.Equal(plan.AddFates, plan.MineFates);
        Assert.Equal(plan.AddCriticalEncounters, plan.MineCriticalEncounters);
        Assert.True(plan.Changed);
    }

    [Fact]
    public void The_players_own_skips_are_never_touched()
    {
        var byHand = new HashSet<uint> { 1962 };
        var untouched = DemiatmaWatch.Plan(new HashSet<DemiatmaKind>(), byHand, None, None, None);
        Assert.False(untouched.Changed);

        var azurite = new HashSet<DemiatmaKind> { DemiatmaKind.Azurite };
        var added = DemiatmaWatch.Plan(azurite, byHand, None, None, None);
        Assert.Equal(new HashSet<uint> { 1963, 1970 }, added.AddFates);
        Assert.Equal(new HashSet<uint> { 1963, 1970 }, added.MineFates);

        var disabled = new HashSet<uint> { 1962, 1963, 1970 };
        var ces = new HashSet<uint> { 33, 35, 36 };
        var neededAgain = DemiatmaWatch.Plan(new HashSet<DemiatmaKind>(), disabled, ces, added.MineFates, added.MineCriticalEncounters);
        Assert.Equal(new HashSet<uint> { 1963, 1970 }, neededAgain.RemoveFates);
        Assert.Equal(new HashSet<uint> { 33, 35, 36 }, neededAgain.RemoveCriticalEncounters);
        Assert.Empty(neededAgain.MineFates);
        Assert.Empty(neededAgain.MineCriticalEncounters);
    }

    [Fact]
    public void A_complete_kinds_activity_turned_back_on_by_hand_goes_off_again()
    {
        var mine = new HashSet<uint> { 1962, 1963, 1970 };
        var plan = DemiatmaWatch.Plan(new HashSet<DemiatmaKind> { DemiatmaKind.Azurite }, new HashSet<uint> { 1962 }, None, mine, None);
        Assert.Equal(new HashSet<uint> { 1963, 1970 }, plan.AddFates);
        Assert.Equal(mine, plan.MineFates);
    }

    [Fact]
    public void Nothing_changes_once_the_lists_are_in_step()
    {
        var complete = new HashSet<DemiatmaKind> { DemiatmaKind.Malachite };
        var first = DemiatmaWatch.Plan(complete, None, None, None, None);
        var second = DemiatmaWatch.Plan(complete, first.AddFates, first.AddCriticalEncounters, first.MineFates, first.MineCriticalEncounters);
        Assert.False(second.Changed);
        Assert.Equal(first.MineFates, second.MineFates);
        Assert.Equal(first.MineCriticalEncounters, second.MineCriticalEncounters);
    }

    [Fact]
    public void The_lines_say_what_was_skipped()
    {
        Assert.Equal("Orpiment demiatma complete (3 of 3); BOCCHI skips its 2 FATEs and 2 critical encounters from now on; its pot FATE stays under BOCCHI's pot settings.",
            DemiatmaWatch.CompleteLine(DemiatmaKind.Orpiment));
        Assert.Equal("Azurite demiatma complete (3 of 3); BOCCHI skips its 3 FATEs and 3 critical encounters from now on.",
            DemiatmaWatch.CompleteLine(DemiatmaKind.Azurite));
        Assert.Equal("Caput Mortuum demiatma needed again (1 of 3); its FATEs and critical encounters are back on.",
            DemiatmaWatch.NeededAgainLine(DemiatmaKind.CaputMortuum, 1));
        var status = DemiatmaWatch.StatusLine(Counts((DemiatmaKind.Azurite, 5), (DemiatmaKind.Verdigris, 1)), new HashSet<DemiatmaKind> { DemiatmaKind.Azurite });
        Assert.StartsWith("Azurite 3/3 (skipped), Verdigris 1/3, Malachite 0/3", status);
    }
}
