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
    public void The_frontline_command_pair_is_auto_on_low_hp_and_off()
    {
        Assert.Equal("/rotation auto LowHP", RsrState.CommandFor(running: false, frontline: true));
        Assert.Equal("/rotation off", RsrState.CommandFor(running: true, frontline: true));
    }

    [Theory]
    [InlineData(18u, true)]    // Frontline: the Borderland Ruins, Seal Rock, the Fields of Glory, Onsal Hakair, Worqor Chirteh
    [InlineData(28u, false)]   // Crystalline Conflict
    [InlineData(37u, false)]   // Crystalline Conflict, custom match
    [InlineData(39u, false)]   // Rival Wings
    [InlineData(1u, false)]    // Wolves' Den Pier
    [InlineData(0u, false)]    // a zone the tables do not know
    public void Only_zone_use_18_reads_as_frontline(uint use, bool frontline)
    {
        RsrState.ZoneUse = () => use;
        try
        {
            Assert.Equal(frontline, RsrState.InFrontline);
        }
        finally
        {
            RsrState.ZoneUse = null;
        }
    }

    [Fact]
    public void Without_zone_data_or_with_a_failing_lookup_nothing_reads_as_frontline()
    {
        RsrState.ZoneUse = null;
        Assert.False(RsrState.InFrontline);
        RsrState.ZoneUse = () => throw new InvalidOperationException("sheet unavailable");
        try
        {
            Assert.False(RsrState.InFrontline);
        }
        finally
        {
            RsrState.ZoneUse = null;
        }
    }

    [Theory]
    [InlineData(false, false, "/rotation manual")]
    [InlineData(true, false, "/rotation off")]
    [InlineData(false, true, "/rotation auto LowHP")]
    [InlineData(true, true, "/rotation off")]
    public void A_press_starts_rsr_when_it_is_off_and_stops_it_when_it_runs(bool running, bool frontline, string command)
    {
        Assert.Equal(command, RsrState.PressCommand(running, frontline));
    }

    [Fact]
    public void In_a_bocchi_run_a_press_arms_henched_when_rsr_is_off_and_sends_off_when_it_runs()
    {
        RsrState.BocchiRun = () => true;
        RsrState.ArmHenched = () => { };
        try
        {
            Assert.Null(RsrState.PressCommand(running: false, frontline: false));
            Assert.Null(RsrState.PressCommand(running: false, frontline: true));
            Assert.Equal("/rotation off", RsrState.PressCommand(running: true, frontline: false));
            Assert.Equal("/rotation off", RsrState.PressCommand(running: true, frontline: true));
        }
        finally
        {
            RsrState.BocchiRun = null;
            RsrState.ArmHenched = null;
        }
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
