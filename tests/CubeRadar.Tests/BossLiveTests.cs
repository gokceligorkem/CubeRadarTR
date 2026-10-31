using CubeRadar.Core.Protocol;
using CubeRadar.Core.Radar;

namespace CubeRadar.Tests;

public class BossLiveTests
{
    private static readonly DateTimeOffset Now=DateTimeOffset.Parse("2026-10-30T10:40:00Z");
    private static BossEvent Event()=>new() {Id="gartua",Name="Immortal Gartua",Map="Altgard",OnceAt=Now.AddMinutes(-10),Enabled=false};
    private static BossEvent Listed(BossEvent e,DateTimeOffset at,bool alive,DateTimeOffset? spawn)=>
        BossLiveSync.Listed(e,new(1110,at.ToUnixTimeMilliseconds(),[]),new(111021,alive,spawn?.ToUnixTimeMilliseconds()??0));

    [Fact]
    public void Fresh_game_time_replaces_old_snapshot_preserving_id_location_and_watch_choice()
    {
        var e=Event() with {Location="Immortal Isle"};var updated=Listed(e,Now,false,Now.AddHours(12));
        Assert.Equal(e.Id,updated.Id);Assert.Equal(e.Location,updated.Location);Assert.False(updated.Enabled);
        Assert.Equal(Now.AddHours(12),BossClock.CurrentOrNext(updated,Now)!.Start);
        Assert.Equal(e.OnceAt,updated.OnceAt);
    }

    [Fact]
    public void Living_and_dead_without_time_do_not_reuse_the_old_clock_or_play_its_alarm()
    {
        var alive=Listed(Event(),Now,true,Now.AddMinutes(-1));
        Assert.Null(BossClock.CurrentOrNext(alive,Now));
        var unknown=Listed(alive,Now.AddSeconds(1),false,null);
        Assert.Null(BossClock.CurrentOrNext(unknown,Now.AddSeconds(1)));
    }

    [Fact]
    public void Kill_clears_old_time_and_a_new_game_time_rearms_the_alert_once()
    {
        var keys=new HashSet<string>();var gate=new BossAlertGate(keys);
        var e=Listed(Event() with {Enabled=true},Now,false,Now.AddMinutes(4));
        Assert.True(gate.Take(BossClock.CurrentOrNext(e,Now)!,Now,true));
        e=Listed(e,Now.AddMinutes(4),true,Now.AddMinutes(4));
        var killed=BossLiveSync.Killed(e,Now.AddMinutes(5).ToUnixTimeMilliseconds());
        Assert.Null(BossClock.CurrentOrNext(killed,Now.AddMinutes(5)));
        e=Listed(killed,Now.AddMinutes(5).AddSeconds(1),false,Now.AddHours(6).AddMinutes(5));
        var next=Now.AddHours(6).AddMinutes(1);
        Assert.True(gate.Take(BossClock.CurrentOrNext(e,next)!,next,true));
        Assert.False(gate.Take(BossClock.CurrentOrNext(e,next)!,next.AddSeconds(1),true));
    }

    [Fact]
    public void Known_interval_from_a_fresh_death_can_estimate_the_next_kill_but_game_time_wins()
    {
        var e=Listed(Event(),Now,true,Now.AddMinutes(-2));
        var death=Now.AddMinutes(1);
        e=BossLiveSync.Killed(e,death.ToUnixTimeMilliseconds());
        e=Listed(e,death.AddSeconds(1),false,death.AddHours(6));
        Assert.Equal(360,e.Live!.RespawnMinutes);
        e=Listed(e,death.AddHours(6),true,death.AddHours(6));
        var kill2=death.AddHours(6).AddMinutes(1);
        e=BossLiveSync.Killed(e,kill2.ToUnixTimeMilliseconds());
        Assert.True(e.Live!.Estimated);Assert.Equal(kill2.AddHours(6),e.Live.SpawnAt);
        var corrected=kill2.AddHours(6).AddMinutes(2);
        e=Listed(e,kill2.AddSeconds(1),false,corrected);
        Assert.False(e.Live!.Estimated);Assert.Equal(corrected,e.Live.SpawnAt);
    }

    [Fact]
    public void Old_records_and_repeated_death_cannot_push_the_timer_forward()
    {
        var e=Listed(Event(),Now,false,Now.AddHours(6));
        Assert.Equal(e,Listed(e,Now.AddSeconds(-1),true,null));
        e=BossLiveSync.Killed(e,Now.AddSeconds(1).ToUnixTimeMilliseconds());
        Assert.Equal(e,BossLiveSync.Killed(e,Now.AddSeconds(2).ToUnixTimeMilliseconds()));
    }

    [Fact]
    public void Verified_fixed_schedule_advances_after_a_kill_when_the_game_sends_no_time()
    {
        var anchor=Now.AddHours(1);var e=Listed(Event() with {OnceAt=anchor,RepeatMinutes=180},Now,false,null);
        Assert.Equal(anchor,BossClock.CurrentOrNext(e,Now)!.Start);
        e=BossLiveSync.Killed(e,anchor.AddMinutes(2).ToUnixTimeMilliseconds());
        Assert.Equal(anchor.AddHours(3),BossClock.CurrentOrNext(e,anchor.AddMinutes(2))!.Start);
        Assert.Equal(anchor.AddHours(6),BossClock.CurrentOrNext(e,anchor.AddHours(3).AddMinutes(31))!.Start);
    }

    [Fact]
    public void Fixed_schedule_rearms_after_the_start_alert_window_instead_of_waiting_thirty_minutes()
    {
        var anchor=Now.AddMinutes(1);var e=Listed(Event() with {OnceAt=anchor,RepeatMinutes=180,Enabled=true},Now,false,null);
        Assert.Equal(anchor,BossClock.CurrentOrNext(e,anchor.AddSeconds(45))!.Start);
        Assert.Equal(anchor.AddHours(3),BossClock.CurrentOrNext(e,anchor.AddMinutes(2))!.Start);
        var keys=new HashSet<string>();var gate=new BossAlertGate(keys);
        Assert.True(gate.Take(BossClock.CurrentOrNext(e,anchor)!,anchor,true));
        var nextAlert=anchor.AddHours(3).AddMinutes(-5);
        Assert.True(gate.Take(BossClock.CurrentOrNext(e,nextAlert)!,nextAlert,true));
        Assert.False(gate.Take(BossClock.CurrentOrNext(e,nextAlert)!,nextAlert.AddSeconds(1),true));
    }

    [Fact]
    public void New_list_restores_an_overdue_field_boss_without_inventing_a_repeat_interval()
    {
        var e=Listed(Event(),Now,false,Now.AddMinutes(1));var later=Now.AddMinutes(20);
        Assert.Equal(0,e.RepeatMinutes);Assert.Equal(0,e.Live!.RespawnMinutes);
        e=Listed(e,later,false,later.AddHours(4));
        Assert.Equal(later.AddHours(4),BossClock.CurrentOrNext(e,later)!.Start);
        Assert.False(e.Live!.Estimated);
    }
}
