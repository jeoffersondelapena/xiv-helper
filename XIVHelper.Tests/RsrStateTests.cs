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
    public void Nothing_is_read_while_rsr_is_not_loaded()
    {
        Assert.Null(RsrState.Running);
        Assert.Null(RsrState.Manual);
    }
}
