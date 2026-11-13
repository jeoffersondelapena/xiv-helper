using XIVHelper.HotbarButton;
using Xunit;

namespace XIVHelper.Tests;

public class RsrStateTests
{
    [Fact]
    public void The_button_flips_between_off_and_manual()
    {
        Assert.Equal("/rotation off", RsrState.CommandFor(running: true));
        Assert.Equal("/rotation manual", RsrState.CommandFor(running: false));
    }

    [Fact]
    public void Henched_is_wanted_only_when_turning_on_during_a_bocchi_run()
    {
        RsrState.BocchiRun = () => true;
        RsrState.ArmHenched = () => { };
        Assert.True(RsrState.WantsHenched(turningOn: true));
        Assert.False(RsrState.WantsHenched(turningOn: false));
        RsrState.BocchiRun = () => false;
        Assert.False(RsrState.WantsHenched(turningOn: true));
        RsrState.BocchiRun = null;
        RsrState.ArmHenched = null;
        Assert.False(RsrState.WantsHenched(turningOn: true));
    }

    [Fact]
    public void Nothing_is_read_while_rsr_is_not_loaded()
    {
        Assert.Null(RsrState.Running);
        Assert.Null(RsrState.Manual);
    }
}
