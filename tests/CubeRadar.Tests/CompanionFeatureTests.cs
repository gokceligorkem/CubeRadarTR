using CubeRadar.Core.Radar;

namespace CubeRadar.Tests;

public class CompanionFeatureTests
{
    [Fact]
    public void Only_checked_boss_alerts_and_reenabling_can_alert_in_the_current_window()
    {
        var now=DateTimeOffset.Parse("2026-10-30T10:00:00Z");
        var disabled=new BossEvent {Id="unchecked",Name="Unchecked",Map="Altgard",OnceAt=now.AddMinutes(4),Enabled=false};
        var enabled=disabled with {Id="checked",Name="Checked",Enabled=true};
        var gate=new BossAlertGate([]);
        Assert.False(gate.Take(BossClock.CurrentOrNext(disabled,now)!,now,true));
        Assert.True(gate.Take(BossClock.CurrentOrNext(enabled,now)!,now,true));
        Assert.True(gate.Take(BossClock.CurrentOrNext(disabled with {Enabled=true},now)!,now,true));
        Assert.False(gate.Take(BossClock.CurrentOrNext(enabled,now)!,now,true));
    }

    private static CubeView Cube(uint id,double z,double distance)=>new(id,1200039,"Verteron",null,0,0,z,0,0,0,"Visible",0,null,false,1010,distance,distance,0,"N",z,null,null);

    [Fact]
    public void Lock_survives_sort_change_and_never_switches_to_another_cube()
    {
        var target=new TargetTracker(); target.ObserveMap(1);target.Lock(2);
        var view=new CubeCompanionView(true,true,0,[Cube(1,0,10),Cube(2,0,20)],Cube(1,0,10));
        Assert.Equal(2u,target.Resolve(view)!.ObjectId);
        Assert.Equal(2u,target.Resolve(view with {Cubes=[Cube(2,0,5),Cube(1,0,10)]})!.ObjectId);
        Assert.Null(target.Resolve(view with {Cubes=[Cube(1,0,10)]}));
        target.ObserveMap(2);Assert.Null(target.LockedId);
    }

    [Fact]
    public void Filter_recalculates_nearest_but_never_filters_using_stale_height()
    {
        var view=new CubeCompanionView(true,true,0,[Cube(1,1000,10),Cube(2,-1000,20),Cube(3,0,30)],Cube(1,1000,10));
        Assert.Equal(2u,TargetTracker.Filter(view,HeightFilter.Below).Nearest!.ObjectId);
        Assert.Equal(3u,TargetTracker.Filter(view,HeightFilter.SameLevel).Nearest!.ObjectId);
        Assert.Empty(TargetTracker.Filter(view with {FreshPosition=false,Nearest=null},HeightFilter.Above).Cubes);
    }

    [Fact]
    public void Schedule_converts_server_weekday_across_midnight_without_using_display_timezone()
    {
        var e=new BossEvent {Id="weekly",Name="Boss",Map="Reshanta",ZoneId="Tokyo Standard Time",Days=[DayOfWeek.Friday],Times=["01:00"]};
        var next=BossClock.CurrentOrNext(e,DateTimeOffset.Parse("2026-10-29T15:59:00Z"))!;
        Assert.Equal(DateTimeOffset.Parse("2026-10-29T16:00:00Z"),next.Start);
        Assert.Equal(DayOfWeek.Friday,TimeZoneInfo.ConvertTime(next.Start,TimeZoneInfo.FindSystemTimeZoneById(e.ZoneId)).DayOfWeek);
    }

    [Fact]
    public void Countdown_expires_and_alert_is_once_per_occurrence_and_threshold_even_after_restart()
    {
        var now=DateTimeOffset.Parse("2026-10-30T10:00:00Z");
        var e=new BossEvent {Id="one",Name="Boss",Map="Verteron",OnceAt=now.AddMinutes(5),Enabled=true};
        var delivered=new HashSet<string>();
        var gate=new BossAlertGate(delivered);
        Assert.True(gate.Take(BossClock.CurrentOrNext(e,now)!,now,true));
        Assert.False(gate.Take(BossClock.CurrentOrNext(e,now.AddSeconds(1))!,now.AddSeconds(1),true));
        Assert.False(new BossAlertGate(delivered).Take(BossClock.CurrentOrNext(e,now)!,now,true));
        Assert.True(gate.Take(BossClock.CurrentOrNext(e,now.AddMinutes(5))!,now.AddMinutes(5),true));
        Assert.False(new BossAlertGate([]).Take(BossClock.CurrentOrNext(e,now.AddMinutes(20))!,now.AddMinutes(20),true));
        Assert.Null(BossClock.CurrentOrNext(e,now.AddMinutes(36)));
    }

    [Fact]
    public void Muted_events_do_not_play_late_and_unknown_schedule_is_not_scheduled()
    {
        var now=DateTimeOffset.Parse("2026-10-30T10:00:00Z");
        var e=new BossEvent {Id="one",Name="Boss",Map="Altgard",OnceAt=now.AddMinutes(4),Enabled=true};
        var gate=new BossAlertGate([]);var occurrence=BossClock.CurrentOrNext(e,now)!;
        Assert.False(gate.Take(occurrence,now,false));Assert.False(gate.Take(occurrence,now,true));
        Assert.Null(BossClock.CurrentOrNext(new BossEvent {Id="unknown",Name="Unknown",Map="?"},now));
    }

    [Fact]
    public void DST_gap_is_skipped_and_fold_uses_one_deterministic_instant()
    {
        var e=new BossEvent {Id="dst",Name="Boss",Map="Map",ZoneId="W. Europe Standard Time",Times=["02:30"]};
        var next=BossClock.CurrentOrNext(e,DateTimeOffset.Parse("2026-03-29T00:00:00Z"))!;
        Assert.Equal(DateTimeOffset.Parse("2026-03-30T00:30:00Z"),next.Start);
        var fall=BossClock.CurrentOrNext(e,DateTimeOffset.Parse("2026-10-25T00:00:00Z"))!;
        Assert.Equal(DateTimeOffset.Parse("2026-10-25T00:30:00Z"),fall.Start);
    }
}
