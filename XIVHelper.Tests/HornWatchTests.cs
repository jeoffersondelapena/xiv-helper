using XIVHelper.Field;
using Xunit;

namespace XIVHelper.Tests;

public class HornWatchTests
{
    [Fact]
    public void Only_a_timer_at_zero_counts_as_a_kick()
    {
        Assert.True(HornWatch.WasKicked(0));
        Assert.True(HornWatch.WasKicked(19.5));
        Assert.False(HornWatch.WasKicked(21));
        Assert.False(HornWatch.WasKicked(5400));
        Assert.False(HornWatch.WasKicked(null));
    }

    [Fact]
    public void The_south_horn_entry_is_found_by_its_text_wherever_it_sits()
    {
        var menu = new[] { "Journey to the Occult Crescent: South Horn.", "Ask for more information.", "Review your knowledge level and currencies.", "Review your phantom jobs.", "Nothing." };
        Assert.Equal(0, HornWatch.MenuIndex(menu, "South Horn"));
        Assert.Equal(1, HornWatch.MenuIndex(new[] { "Nothing.", "Journey to the Occult Crescent: South Horn." }, "South Horn"));
        Assert.Null(HornWatch.MenuIndex(new[] { "Journey to the Occult Crescent: North Horn.", "Nothing." }, "South Horn"));
        Assert.Equal(0, HornWatch.MenuIndex(new[] { "Journey to the Occult Crescent: North Horn.", "Nothing." }, "North Horn"));
        Assert.True(HornWatch.IsConfirmPrompt("Journey to the Occult Crescent: South Horn?\n※If your knowledge level is above 20, it will be synced.", "South Horn"));
        Assert.False(HornWatch.IsConfirmPrompt("Return to the starting point for\nSouth?", "South Horn"));
        Assert.Equal("North Horn", HornWatch.Horns[1346]);
    }

    [Fact]
    public void The_duty_window_gets_the_long_wait()
    {
        Assert.Equal(HornWatch.QueueTimeoutSeconds, HornWatch.TimeoutFor(HornStep.Commence));
        Assert.Equal(HornWatch.StepTimeoutSeconds, HornWatch.TimeoutFor(HornStep.Menu));
    }

    [Fact]
    public void The_switch_is_on_unless_the_file_says_off()
    {
        Assert.True(HornWatch.ParseToggle("on"));
        Assert.True(HornWatch.ParseToggle(null));
        Assert.False(HornWatch.ParseToggle("off"));
        Assert.False(HornWatch.ParseToggle(" OFF \n"));
    }

    [Fact]
    public void The_lines_say_what_happened()
    {
        Assert.Equal("South Horn's timer ran out (3 s left at exit); going back in.", HornWatch.KickedLine("South Horn", 3.4));
        Assert.Equal("left North Horn with 5400 s on the timer; not going back in.", HornWatch.LeftLine("North Horn", 5400));
        Assert.Equal("re-entry stopped at Commence; take it from there by hand.", HornWatch.GaveUpLine(HornStep.Commence));
    }
}
