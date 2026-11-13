using XIVHelper.Demiatma;
using Xunit;

namespace XIVHelper.Tests;

public class DemiatmaWatchTests
{
    private static Dictionary<DemiatmaKind, int> Counts(params (DemiatmaKind Kind, int Count)[] pairs) =>
        pairs.ToDictionary(p => p.Kind, p => p.Count);

    [Fact]
    public void Three_of_a_kind_is_complete()
    {
        var complete = DemiatmaWatch.Complete(Counts((DemiatmaKind.Azurite, 3), (DemiatmaKind.Verdigris, 2), (DemiatmaKind.Realgar, 4)));
        Assert.Equal(new HashSet<DemiatmaKind> { DemiatmaKind.Azurite, DemiatmaKind.Realgar }, complete);
        Assert.Equal(6, DemiatmaWatch.Kinds.Count);
    }

    [Fact]
    public void The_lines_name_the_kind_and_cap_the_count()
    {
        Assert.Equal("Caput Mortuum demiatma complete (3 of 3).", DemiatmaWatch.CompleteLine(DemiatmaKind.CaputMortuum));
        Assert.StartsWith("Azurite 3/3, Verdigris 1/3, Malachite 0/3", DemiatmaWatch.StatusLine(Counts((DemiatmaKind.Azurite, 5), (DemiatmaKind.Verdigris, 1))));
    }
}
