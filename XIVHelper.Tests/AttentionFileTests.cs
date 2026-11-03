using Xunit;

namespace XIVHelper.Tests;

public class AttentionFileTests
{
    [Fact]
    public void A_note_replaces_its_sources_line_and_leaves_the_others()
    {
        var text = AttentionFile.Lines("IINACT: the parser stalled at 10:41\nRSRButton: old\n", "RSRButton", "could not be set up at 12:00");
        Assert.Equal("IINACT: the parser stalled at 10:41\nRSRButton: could not be set up at 12:00\n", text);
    }

    [Fact]
    public void A_null_note_clears_the_line()
    {
        Assert.Equal("", AttentionFile.Lines("RSRButton: old\n", "RSRButton", null));
    }
}
