using XIVHelper.Field;
using Xunit;

namespace XIVHelper.Tests;

public class ReviveWatchTests
{
    private const double Wait = 20;

    [Fact]
    public void Nothing_happens_while_alive()
    {
        Assert.Equal(ReviveMove.None, ReviveWatch.Next(dead: false, 500, raiseOffered: true, PromptKind.Raise, inActivity: false, Wait));
    }

    [Fact]
    public void A_raise_offer_is_accepted_at_once_even_mid_fate()
    {
        Assert.Equal(ReviveMove.AcceptRaise, ReviveWatch.Next(dead: true, 2, raiseOffered: true, PromptKind.Raise, inActivity: true, Wait));
        Assert.Equal(ReviveMove.AcceptRaise, ReviveWatch.Next(dead: true, 500, raiseOffered: false, PromptKind.Raise, inActivity: false, Wait));
    }

    [Fact]
    public void Before_the_wait_the_games_own_return_dialog_is_left_alone()
    {
        Assert.Equal(ReviveMove.None, ReviveWatch.Next(dead: true, 10, raiseOffered: false, PromptKind.Return, inActivity: false, Wait));
        Assert.Equal(ReviveMove.None, ReviveWatch.Next(dead: true, 10, raiseOffered: false, PromptKind.None, inActivity: false, Wait));
    }

    [Fact]
    public void After_the_wait_the_return_dialog_is_summoned_then_confirmed()
    {
        Assert.Equal(ReviveMove.AskReturn, ReviveWatch.Next(dead: true, 20, raiseOffered: false, PromptKind.None, inActivity: false, Wait));
        Assert.Equal(ReviveMove.ConfirmReturn, ReviveWatch.Next(dead: true, 21, raiseOffered: false, PromptKind.Return, inActivity: false, Wait));
        Assert.Equal(ReviveMove.None, ReviveWatch.Next(dead: true, 21, raiseOffered: false, PromptKind.Other, inActivity: false, Wait));
    }

    [Fact]
    public void No_return_while_the_fate_or_ce_runs_or_a_raise_is_on_its_way()
    {
        Assert.Equal(ReviveMove.None, ReviveWatch.Next(dead: true, 300, raiseOffered: false, PromptKind.None, inActivity: true, Wait));
        Assert.Equal(ReviveMove.None, ReviveWatch.Next(dead: true, 300, raiseOffered: true, PromptKind.None, inActivity: false, Wait));
        Assert.Equal(ReviveMove.AskReturn, ReviveWatch.Next(dead: true, 300, raiseOffered: false, PromptKind.None, inActivity: false, Wait));
    }

    [Fact]
    public void Prompts_are_told_apart_by_the_games_own_texts()
    {
        var raise = ReviveWatch.Prefix("Accept Raise?", "x");
        var ret = ReviveWatch.Prefix("Return to the starting point for\nUNKNOWN?", "x");
        Assert.Equal("Accept Raise", raise);
        Assert.Equal("Return to the starting point for", ret);
        Assert.Equal(PromptKind.Raise, ReviveWatch.Classify("Accept Raise from Someone?", raise, ret));
        Assert.Equal(PromptKind.Return, ReviveWatch.Classify("Return to the starting point for\nSouth Horn?", raise, ret));
        Assert.Equal(PromptKind.Other, ReviveWatch.Classify("Leave the party?", raise, ret));
        Assert.Equal(PromptKind.None, ReviveWatch.Classify("  ", raise, ret));
        Assert.Equal("Return to", ReviveWatch.Prefix("?", "Return to"));
    }

    [Fact]
    public void The_wait_comes_from_the_override_file_when_it_holds_a_number()
    {
        Assert.Equal(90, ReviveWatch.ReturnAfterSeconds("90"));
        Assert.Equal(20, ReviveWatch.ReturnAfterSeconds(null));
        Assert.Equal(20, ReviveWatch.ReturnAfterSeconds("soon"));
        Assert.Equal(20, ReviveWatch.ReturnAfterSeconds("-5"));
    }

    [Fact]
    public void The_return_line_names_the_wait()
    {
        Assert.Equal("dead for 20 s with no raise; returning to camp. BOCCHI carries on from there.", ReviveWatch.ReturnLine(20));
    }
}
