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
        Assert.True(HornWatch.ConfirmAnyway(9.5));
        Assert.False(HornWatch.ConfirmAnyway(10.5));
        Assert.Equal("Journey to the Occult Crescent: South Horn? ※If your knowledge level is above 20, it will be synced.", HornWatch.OneLine("Journey to the Occult Crescent: South Horn?\n※If your knowledge level is above 20, it will be synced.\n"));
        Assert.Equal("(nothing)", HornWatch.OneLine(null));
        Assert.Equal("North Horn", HornWatch.Horns[1346]);
    }

    [Fact]
    public void A_run_seen_within_the_last_minute_still_counts_at_the_exit()
    {
        var now = new DateTime(2026, 11, 13, 20, 58, 47);
        Assert.True(HornWatch.RunWasOn(now.AddSeconds(-2), now));
        Assert.True(HornWatch.RunWasOn(now.AddSeconds(-59), now));
        Assert.False(HornWatch.RunWasOn(now.AddSeconds(-61), now));
        Assert.False(HornWatch.RunWasOn(null, now));
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
        Assert.True(HornWatch.ReadSwitch(fileExists: false, firstLine: null));
        Assert.True(HornWatch.ReadSwitch(fileExists: true, firstLine: "on"));
        Assert.False(HornWatch.ReadSwitch(fileExists: true, firstLine: "off"));
    }

    [Fact]
    public void A_stopped_re_entry_leaves_a_standing_note()
    {
        var at = new DateTime(2026, 11, 13, 17, 40, 0);
        Assert.Equal("re-entry stopped at Commence; take it from there by hand at 17:40; the character was sent home",
            HornWatch.GaveUpNote(HornWatch.GaveUpLine(HornStep.Commence).TrimEnd('.'), at, sentHome: true));
        Assert.Equal("re-entry stopped at Menu (the menu offered: Ask for more information. | Nothing.); take it from there by hand at 17:40; the character was left outside the horn, Lifestream could not take it home",
            HornWatch.GaveUpNote(HornWatch.GaveUpLine(HornStep.Menu, HornWatch.MenuDetail(new[] { "Ask for more information.", "Nothing." })).TrimEnd('.'), at, sentHome: false));
        Assert.Equal("(empty)", HornWatch.Describe(Array.Empty<string>()));
        Assert.Equal("sending the character home.", HornWatch.HomeLine(true));
        Assert.Equal("going into North Horn now.", HornWatch.NowLine("North Horn"));
        Assert.Equal("the re-entry stopped working at 17:40 (boom); go back in by hand", HornWatch.BrokenNote("boom", at));
    }

    [Fact]
    public void The_lines_say_what_happened()
    {
        Assert.Equal("South Horn's timer ran out (3 s left at exit); going back in.", HornWatch.KickedLine("South Horn", 3.4));
        Assert.Equal("left North Horn with 5400 s on the timer; not going back in.", HornWatch.LeftLine("North Horn", 5400));
        Assert.Equal("re-entry stopped at Commence; take it from there by hand.", HornWatch.GaveUpLine(HornStep.Commence));
    }
}
