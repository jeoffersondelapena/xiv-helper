using XIVHelper.Field;
using Xunit;

namespace XIVHelper.Tests;

public class ReviveWatchTests
{
    private const double Wait = 120;

    [Fact]
    public void Nothing_happens_while_alive()
    {
        Assert.Equal(ReviveMove.None, ReviveWatch.Next(dead: false, 500, raiseOffered: true, promptOpen: true, agentShown: false, Wait));
    }

    [Fact]
    public void A_raise_offer_is_accepted_at_once()
    {
        Assert.Equal(ReviveMove.AcceptRaise, ReviveWatch.Next(dead: true, 5, raiseOffered: true, promptOpen: true, agentShown: false, Wait));
        Assert.Equal(ReviveMove.AcceptRaise, ReviveWatch.Next(dead: true, 500, raiseOffered: false, promptOpen: true, agentShown: false, Wait));
    }

    [Fact]
    public void The_games_own_return_dialog_is_left_alone_before_the_wait_is_over()
    {
        Assert.Equal(ReviveMove.None, ReviveWatch.Next(dead: true, 30, raiseOffered: false, promptOpen: true, agentShown: true, Wait));
        Assert.Equal(ReviveMove.None, ReviveWatch.Next(dead: true, 30, raiseOffered: false, promptOpen: false, agentShown: false, Wait));
    }

    [Fact]
    public void After_the_wait_the_return_dialog_is_summoned_then_confirmed()
    {
        Assert.Equal(ReviveMove.AskReturn, ReviveWatch.Next(dead: true, 120, raiseOffered: false, promptOpen: false, agentShown: false, Wait));
        Assert.Equal(ReviveMove.None, ReviveWatch.Next(dead: true, 121, raiseOffered: false, promptOpen: false, agentShown: true, Wait));
        Assert.Equal(ReviveMove.ConfirmReturn, ReviveWatch.Next(dead: true, 122, raiseOffered: false, promptOpen: true, agentShown: true, Wait));
    }

    [Fact]
    public void A_raise_on_its_way_postpones_the_return()
    {
        Assert.Equal(ReviveMove.None, ReviveWatch.Next(dead: true, 300, raiseOffered: true, promptOpen: false, agentShown: false, Wait));
    }

    [Fact]
    public void The_wait_comes_from_the_override_file_when_it_holds_a_number()
    {
        Assert.Equal(90, ReviveWatch.ReturnAfterSeconds("90"));
        Assert.Equal(ReviveWatch.DefaultReturnAfterSeconds, ReviveWatch.ReturnAfterSeconds(null));
        Assert.Equal(ReviveWatch.DefaultReturnAfterSeconds, ReviveWatch.ReturnAfterSeconds("soon"));
        Assert.Equal(ReviveWatch.DefaultReturnAfterSeconds, ReviveWatch.ReturnAfterSeconds("-5"));
    }

    [Fact]
    public void The_return_line_names_the_wait()
    {
        Assert.Equal("dead for 120 s with no raise; returning to camp. BOCCHI carries on from there.", ReviveWatch.ReturnLine(120));
    }
}
