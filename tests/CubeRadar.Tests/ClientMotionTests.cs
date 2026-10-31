using CubeRadar.Core.Radar;
using System.Text.Json;

namespace CubeRadar.Tests;

public class ClientMotionTests
{
    static RadarSnapshot StaleSnapshot()=>new(10000,null,null,null,new(0,0,0,100,9900,"BuffA"),null,[],
        [new(1,1200039,null,null,21000,30000,6000,0,0,0,"Visible",0,null,false,null,999,999,0,"N",0,null,null)],[],
        new(0,-1,1,0,"-y"),5,10);
    static ClientMotionFix ObservedStop()
    {
        var stopped=new ClientMotionFix(20000,30000,1000,-90,null,10000,"0137")
            {Stopped=true,PoseGeneration=1,PoseSerial=2,PositionChangedAtMs=10000};
        using var observation=JsonDocument.Parse("{\"motionAtMs\":10000,\"observationAtMs\":14000,\"positionChangedAtMs\":10000,\"stopped\":true,\"generation\":1,\"serial\":2}");
        return ClientPoseObservation.Apply(stopped,observation.RootElement,14000)!;
    }
    [Fact]
    public void Confirmed_stationary_observations_keep_radar_position_visible_without_rewriting_motion_time()
    {
        var motion=ObservedStop();var snapshot=ClientMotionPresentation.Apply(StaleSnapshot(),motion,14000,true);
        var radar=CubeCompanionView.Build(snapshot,14000);
        Assert.True(radar.FreshPosition);Assert.NotNull(radar.Nearest);Assert.Equal(10,radar.Nearest.DistanceM);
        Assert.Equal(14000,snapshot.Self!.TimeMs);Assert.Equal(0,radar.PositionAgeMs);
        Assert.Equal(10000,motion.TimeMs);Assert.Equal(10000,motion.LastPositionChangedAt);
    }
    [Theory] [InlineData(16501,true)] [InlineData(14000,false)]
    public void Stationary_radar_still_expires_if_observations_stop_or_reader_is_not_ready(long now,bool ready)
    {
        var original=StaleSnapshot();var result=ClientMotionPresentation.Apply(original,ObservedStop(),now,ready);
        Assert.Same(original,result);Assert.False(CubeCompanionView.Build(result,now).FreshPosition);
    }
    [Fact]
    public void Owned_connection_position_replaces_stale_server_fix_and_recomputes_cube_geometry()
    {
        var snapshot=new RadarSnapshot(10000,null,null,null,new(0,0,0,100,9900,"BuffA"),null,[],
            [new(1,1200039,null,null,21000,30000,6000,0,0,0,"Visible",0,null,false,null,999,999,0,"N",0,null,null)],[],
            new(0,-1,1,0,"-y"),5,10);
        var fix=new ClientMotionFix(20000,30000,1000,-90,90,10000,"0137");
        var updated=ClientMotionPresentation.Apply(snapshot,fix,10001,true);
        var view=CubeCompanionView.Build(updated,10001);
        Assert.True(view.FreshPosition);
        Assert.Equal(SelfIdentitySource.OwnedConnection,updated.IdentitySource);
        Assert.Equal(10,view.Nearest!.DistanceM);
        Assert.Equal(90,view.Nearest.Bearing);
        Assert.Equal("O",view.Nearest.Compass);
        Assert.Equal(5000,view.Nearest.DzUnits);
    }

    [Theory]
    [InlineData(-90,0)] [InlineData(0,90)] [InlineData(90,180)] [InlineData(180,270)]
    public void Packet_facing_uses_world_yaw_and_configured_axes(double yaw,double expected)
    {
        var fix=new ClientMotionFix(20000,30000,1000,yaw,0,10000,"0137");
        Assert.Equal(expected,ClientMotionPresentation.Facing(fix,Orientation.Parse("-y")),7);
    }

    [Fact]
    public void Old_or_unvalidated_motion_never_replaces_server_state()
    {
        var snapshot=new RadarSnapshot(10000,null,null,null,null,null,[],[],[],new(0,-1,1,0,"-y"),5,1);
        var fix=new ClientMotionFix(20000,30000,1000,0,0,10000,"0137");
        Assert.Same(snapshot,ClientMotionPresentation.Apply(snapshot,fix,14000,true));
        Assert.Same(snapshot,ClientMotionPresentation.Apply(snapshot,fix,10000,false));
    }
}
