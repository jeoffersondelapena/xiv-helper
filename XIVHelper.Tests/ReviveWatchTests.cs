using XIVHelper.Field;
using Xunit;

namespace XIVHelper.Tests;

public class ReviveWatchTests
{
    private const double Wait = 20;

    [Fact]
    public void Nothing_happens_while_alive()
    {
        Assert.Equal(ReviveMove.None, ReviveWatch.Next(dead: false, 500, raiseIncoming: true, PromptKind.Raise, inActivity: false, Wait));
    }

    [Fact]
    public void A_raise_offer_is_accepted_at_once_even_mid_fate()
    {
        Assert.Equal(ReviveMove.AcceptRaise, ReviveWatch.Next(dead: true, 2, raiseIncoming: true, PromptKind.Raise, inActivity: true, Wait));
        Assert.Equal(ReviveMove.AcceptRaise, ReviveWatch.Next(dead: true, 500, raiseIncoming: false, PromptKind.Raise, inActivity: false, Wait));
    }

    [Fact]
    public void Before_the_wait_the_games_own_return_dialog_is_left_alone()
    {
        Assert.Equal(ReviveMove.None, ReviveWatch.Next(dead: true, 10, raiseIncoming: false, PromptKind.Return, inActivity: false, Wait));
        Assert.Equal(ReviveMove.None, ReviveWatch.Next(dead: true, 10, raiseIncoming: false, PromptKind.None, inActivity: false, Wait));
    }

    [Fact]
    public void After_the_wait_the_return_dialog_is_summoned_then_confirmed()
    {
        Assert.Equal(ReviveMove.AskReturn, ReviveWatch.Next(dead: true, 20, raiseIncoming: false, PromptKind.None, inActivity: false, Wait));
        Assert.Equal(ReviveMove.ConfirmReturn, ReviveWatch.Next(dead: true, 21, raiseIncoming: false, PromptKind.Return, inActivity: false, Wait));
        Assert.Equal(ReviveMove.None, ReviveWatch.Next(dead: true, 21, raiseIncoming: false, PromptKind.Other, inActivity: false, Wait));
    }

    [Fact]
    public void No_return_while_the_fate_or_ce_runs_or_a_raise_is_on_its_way()
    {
        Assert.Equal(ReviveMove.None, ReviveWatch.Next(dead: true, 300, raiseIncoming: false, PromptKind.None, inActivity: true, Wait));
        Assert.Equal(ReviveMove.None, ReviveWatch.Next(dead: true, 300, raiseIncoming: true, PromptKind.None, inActivity: false, Wait));
        Assert.Equal(ReviveMove.AskReturn, ReviveWatch.Next(dead: true, 300, raiseIncoming: false, PromptKind.None, inActivity: false, Wait));
        // the caller restarts the idle clock after a cancelled raise or a finished activity, so a short idle never returns
        Assert.Equal(ReviveMove.None, ReviveWatch.Next(dead: true, 5, raiseIncoming: false, PromptKind.None, inActivity: false, Wait));
    }

    [Fact]
    public void Every_player_raise_counts_as_incoming()
    {
        Assert.Contains(125u, ReviveWatch.RaiseActions);    // Raise
        Assert.Contains(7523u, ReviveWatch.RaiseActions);   // Verraise
        Assert.Contains(41634u, ReviveWatch.RaiseActions);  // Phantom Chemist's Revive
        Assert.Equal(7, ReviveWatch.RaiseActions.Count);
        Assert.Equal(4570u, ReviveWatch.PhoenixDown);
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
    public void Rsr_is_re_armed_only_when_it_went_off_inside_an_activity_on_a_bocchi_run()
    {
        Assert.True(ReviveWatch.NeedsRearm(bocchiLoaded: true, inActivity: true, rsrActive: false));
        Assert.False(ReviveWatch.NeedsRearm(bocchiLoaded: true, inActivity: true, rsrActive: true));
        Assert.False(ReviveWatch.NeedsRearm(bocchiLoaded: true, inActivity: false, rsrActive: false));
        Assert.False(ReviveWatch.NeedsRearm(bocchiLoaded: false, inActivity: true, rsrActive: false));
        Assert.Equal(5, (byte)RsrMode.Henched);   // RSR's own enum order: Off, Auto, TargetOnly, Manual, AutoDuty, Henched, PvP
    }

    [Fact]
    public void Only_a_death_inside_bocchis_fate_or_ce_holds_the_return_and_only_while_it_runs()
    {
        Assert.True(ReviveWatch.HoldFor(ReviveWatch.InFate, fateRunning: true, ceRunning: false));
        Assert.False(ReviveWatch.HoldFor(ReviveWatch.InFate, fateRunning: false, ceRunning: true));
        Assert.True(ReviveWatch.HoldFor(ReviveWatch.InCriticalEncounter, fateRunning: false, ceRunning: true));
        Assert.False(ReviveWatch.HoldFor(ReviveWatch.InCriticalEncounter, fateRunning: true, ceRunning: false));
        Assert.False(ReviveWatch.HoldFor("InCombat", fateRunning: true, ceRunning: true));
        Assert.False(ReviveWatch.HoldFor("Pathfinding", fateRunning: true, ceRunning: true));
        Assert.True(ReviveWatch.HoldFor(null, fateRunning: false, ceRunning: true));
        Assert.False(ReviveWatch.HoldFor(null, fateRunning: false, ceRunning: false));
    }

    [Fact]
    public void A_stray_fight_with_rsr_off_arms_it_after_two_seconds_on_a_run()
    {
        Assert.True(ReviveWatch.NeedsSelfDefence(ReviveWatch.InCombat, 2, rsrActive: false, onRun: true));
        Assert.False(ReviveWatch.NeedsSelfDefence(ReviveWatch.InCombat, 1, rsrActive: false, onRun: true));
        Assert.False(ReviveWatch.NeedsSelfDefence(ReviveWatch.InCombat, 5, rsrActive: true, onRun: true));
        Assert.False(ReviveWatch.NeedsSelfDefence(ReviveWatch.InCombat, 5, rsrActive: false, onRun: false));
        Assert.False(ReviveWatch.NeedsSelfDefence(ReviveWatch.InFate, 5, rsrActive: false, onRun: true));
        Assert.False(ReviveWatch.NeedsSelfDefence(null, 5, rsrActive: false, onRun: true));
    }

    [Fact]
    public void The_notes_name_the_problem_the_time_and_the_consequence()
    {
        var at = new DateTime(2026, 11, 13, 17, 40, 0);
        Assert.Equal("cannot tell whether BOCCHI is running since 17:40 (IAutomator.Enabled is missing); a loaded BOCCHI counts as running until this is fixed",
            ReviveWatch.BocchiNote("IAutomator.Enabled is missing", at));
        Assert.Equal("the revive watch stopped working at 17:40 (boom); raises and returns are yours until it heals", ReviveWatch.ReviveNote("boom", at));
    }

    [Fact]
    public void The_return_line_names_the_wait()
    {
        Assert.Equal("dead for 20 s with no raise; returning to camp. BOCCHI carries on from there.", ReviveWatch.ReturnLine(20));
    }
}
