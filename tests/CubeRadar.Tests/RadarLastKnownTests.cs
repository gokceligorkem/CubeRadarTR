using CubeRadar.Core.Radar;
using CubeRadar.Core.Pets;

namespace CubeRadar.Tests;

public class RadarLastKnownTests
{
    static ClientMotionFix Motion=>new(20000,30000,1000,-90,null,10000,"0137")
        {PoseGeneration=1,PoseSerial=2,PositionChangedAtMs=10000};
    static RadarSnapshot Snapshot(long now)=>new(now,null,1110,null,null,null,[],[],[],new(0,-1,1,0,"-y"),5,1);
    static RadarPoseDisplay? Read(RadarPoseMemory memory,long now,bool ready=true,long session=1,int pid=123,
        ClientMotionFix? motion=null,int map=1110)
    {
        motion??=Motion;var snapshot=ClientMotionPresentation.Apply(Snapshot(now) with {MapId=map},motion,now,ready);
        return memory.Read(snapshot,CubeCompanionView.Build(snapshot,now),motion,ready,true,session,pid);
    }
    [Fact]
    public void Quiet_motion_keeps_last_known_visual_without_renewing_position_or_claiming_stop()
    {
        var memory=new RadarPoseMemory();var fresh=Read(memory,10000)!.Value;
        var held=Read(memory,40000)!.Value;
        Assert.False(fresh.LastKnown);Assert.True(held.LastKnown);Assert.Equal(fresh.Self.X,held.Self.X);Assert.Equal(30000,held.Self.AgeMs);
        Assert.Equal(10000,held.Self.TimeMs);Assert.False(Motion.Stopped);
        var strict=ClientMotionPresentation.Apply(Snapshot(40000),Motion,40000,true);
        Assert.False(CubeCompanionView.Build(strict,40000).FreshPosition);
    }
    [Fact]
    public void An_old_sample_cannot_seed_display_memory_before_a_fresh_observation()
        =>Assert.Null(Read(new(),40000));
    [Fact]
    public void New_motion_returns_the_display_to_live()
    {
        var memory=new RadarPoseMemory();Read(memory,10000);Read(memory,40000);
        var next=Read(memory,41000,motion:Motion with {X=21000,TimeMs=41000,PoseSerial=3})!.Value;
        Assert.False(next.LastKnown);Assert.Equal(21000,next.Self.X);
    }
    [Fact]
    public void Reader_loss_clears_visual_memory_and_clock_recovery_does_not_reanimate_it()
    {
        var memory=new RadarPoseMemory();Read(memory,10000);
        Assert.Null(Read(memory,40000,ready:false));Assert.Null(Read(memory,41000));
    }
    [Theory] [InlineData("map")] [InlineData("session")] [InlineData("pid")] [InlineData("generation")]
    public void Context_changes_invalidate_last_known_position(string change)
    {
        var memory=new RadarPoseMemory();Read(memory,10000);
        Assert.Null(Read(memory,40000,map:change=="map"?2222:1110,session:change=="session"?2:1,
            pid:change=="pid"?124:123,motion:change=="generation"?Motion with {PoseGeneration=2}:Motion));
    }
    [Fact]
    public void A_different_unvalidated_sample_cannot_keep_the_previous_display_alive()
    {
        var memory=new RadarPoseMemory();Read(memory,10000);
        Assert.Null(Read(memory,40000,motion:Motion with {TimeMs=20000,PoseSerial=3}));
    }
    [Fact]
    public void Old_creature_coordinates_are_explicit_last_known_not_fresh_combat_evidence()
    {
        var source=PetCatalog.Default.Sources[0];
        var mob=new PetMob(123,source.NpcId,source.PetId,source.Name,1000,0,0,10000,10000)
            {WireEntityId=123,LifeId=1};
        Assert.True(RadarProjector.TryCreate(0,0,0,Orientation.Parse("-y"),75,150,RadarOrientationMode.CharacterUp,out var frame));
        Assert.False(RadarCreaturePresentation.TryProject(mob,frame,0,HeightFilter.All,200000,out _));
        Assert.True(RadarCreaturePresentation.TryProject(mob,frame,0,HeightFilter.All,200000,out var ghost,showLastKnown:true));
        Assert.True(ghost.LastKnown);Assert.Same(mob,ghost.Mob);Assert.Equal(10000,ghost.Mob.PositionMs);
    }
    [Fact]
    public void Retained_radar_observations_are_separate_from_fresh_nearby_and_respect_removal()
    {
        var tracker=new PetTracker();var source=PetCatalog.Default.Sources[0];
        byte[] spawn=[123,0x0c,0x20,0,..BitConverter.GetBytes(source.NpcId),0,2,
            ..BitConverter.GetBytes(1000f),..BitConverter.GetBytes(2000f),..BitConverter.GetBytes(0f)];
        tracker.OnFrame(0x4136,spawn,10000,1110);
        Assert.Empty(tracker.Read(200000).Nearby);Assert.Single(tracker.ReadRadarMobs());
        tracker.OnFrame(0x4236,[123,0,3],200001,1110);Assert.Empty(tracker.ReadRadarMobs());
    }
    [Fact]
    public void Map_change_clears_retained_radar_creatures()
    {
        var tracker=new PetTracker();var source=PetCatalog.Default.Sources[0];
        byte[] spawn=[123,0x0c,0x20,0,..BitConverter.GetBytes(source.NpcId),0,2];
        tracker.OnFrame(0x4136,spawn,10000,1110);Assert.Single(tracker.ReadRadarMobs());
        tracker.OnFrame(0x9999,[],10001,2222);Assert.Empty(tracker.ReadRadarMobs());
    }
    [Fact]
    public void Visual_history_never_returns_a_verified_nearest_or_locked_target()
    {
        var self=new FixView(0,0,0,10000,30000,"C2S");
        var view=new CubeCompanionView(true,false,30000,[],null).WithLastKnownOrigin(self,new(0,-1,1,0,"-y"));
        Assert.False(view.FreshPosition);Assert.True(view.LastKnownPosition);Assert.Null(view.Nearest);
        var targets=new TargetTracker();targets.Lock(123);Assert.Null(targets.Resolve(view));
        Assert.False(TargetTracker.Filter(view,HeightFilter.Above).FreshPosition);
    }
}
