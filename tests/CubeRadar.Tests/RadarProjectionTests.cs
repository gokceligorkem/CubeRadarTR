using CubeRadar.Core.Radar;

namespace CubeRadar.Tests;

public class RadarProjectionTests
{
    static readonly Orientation Game=Orientation.Parse("-y");
    static RadarProjector Frame(double heading=0,RadarOrientationMode mode=RadarOrientationMode.CharacterUp,
        double playerX=0,double playerY=0)
    {
        Assert.True(RadarProjector.TryCreate(playerX,playerY,heading,Game,75,150,mode,out var frame,200,200));
        return frame;
    }
    [Theory]
    [InlineData(0,0,-100)] [InlineData(90,-100,0)] [InlineData(180,0,100)]
    [InlineData(270,100,0)] [InlineData(45,-70.7106781187,-70.7106781187)]
    public void World_north_rotates_left_when_player_turns_east(double heading,double x,double y)
    {
        // World XY centimetres; north=-Y, east=+X. Screen +X=right, +Y=down.
        Assert.True(Frame(heading).TryProject(0,-5000,out var p));
        Assert.Equal(200+x,p.ScreenX,7);Assert.Equal(200+y,p.ScreenY,7);
        Assert.Equal(x/2,p.LocalXM,7);Assert.Equal(y/2,p.LocalYM,7);Assert.Equal(50,p.DistanceM,7);
    }
    [Theory] [InlineData(0)] [InlineData(90)] [InlineData(180)] [InlineData(270)] [InlineData(45)]
    public void Player_is_centered_and_character_up_arrow_remains_up(double heading)
    {
        var f=Frame(heading,playerX:-12345,playerY:6789);
        Assert.True(f.TryProject(-12345,6789,out var p));
        Assert.Equal(200,p.ScreenX);Assert.Equal(200,p.ScreenY);Assert.Equal(0,p.DistanceM);
        Assert.True(p.Inside);Assert.Equal(0,f.PlayerArrowDegrees);
    }
    [Theory] [InlineData(7500,true)] [InlineData(7500.01,false)]
    public void Outer_ring_is_exactly_the_configured_world_radius(double targetX,bool inside)
    {
        Assert.True(Frame().TryProject(targetX,0,out var p));Assert.Equal(inside,p.Inside);
        Assert.Equal(targetX/50,p.ScreenX-200,7);
    }
    [Theory] [InlineData(-25000,-40000)] [InlineData(1000000000000,1000000000000)]
    public void Translation_preserves_local_geometry_in_negative_and_large_worlds(double x,double y)
    {
        Assert.True(Frame(90,playerX:x,playerY:y).TryProject(x+5000,y,out var p));
        Assert.Equal(200,p.ScreenX,7);Assert.Equal(100,p.ScreenY,7);
    }
    [Theory] [InlineData(-90,270)] [InlineData(360,0)] [InlineData(450,90)]
    [InlineData(360000000090,90)] [InlineData(-360000000090,270)]
    public void Heading_is_normalized_once(double input,double expected)
    {
        Assert.True(RadarProjector.TryNormalizeHeading(input,out var actual));Assert.Equal(expected,actual);
        Assert.Equal(Frame(expected).PlayerArrowDegrees,Frame(input).PlayerArrowDegrees);
        Assert.True(Frame(input).TryProject(0,-5000,out var point));
        Assert.True(Frame(expected).TryProject(0,-5000,out var expectedPoint));Assert.Equal(expectedPoint,point);
    }
    [Theory] [InlineData(double.NaN)] [InlineData(double.PositiveInfinity)] [InlineData(double.NegativeInfinity)]
    public void Invalid_facing_does_not_become_an_invented_heading(double heading)
    {
        Assert.False(RadarProjector.TryNormalizeHeading(heading,out _));
        Assert.False(RadarProjector.TryCreate(0,0,heading,Game,75,150,RadarOrientationMode.CharacterUp,out _));
    }
    [Fact]
    public void North_up_can_draw_without_facing_but_character_up_cannot()
    {
        Assert.False(RadarProjector.TryCreate(0,0,null,Game,75,150,RadarOrientationMode.CharacterUp,out _));
        Assert.True(RadarProjector.TryCreate(0,0,null,Game,75,150,RadarOrientationMode.NorthUp,out var frame));
        Assert.Null(frame.PlayerArrowDegrees);Assert.True(frame.TryProject(0,-5000,out var p));Assert.Equal(-100,p.ScreenY);
    }
    [Fact]
    public void Orientation_switch_rotates_world_and_north_up_arrow_instead_of_moving_player()
    {
        var tactical=Frame(90);var north=Frame(90,RadarOrientationMode.NorthUp);
        Assert.True(tactical.TryProject(0,-5000,out var a));Assert.True(north.TryProject(0,-5000,out var b));
        Assert.Equal(100,a.ScreenX,7);Assert.Equal(200,b.ScreenX,7);Assert.Equal(100,b.ScreenY,7);
        Assert.Equal(90,north.PlayerArrowDegrees);
    }
    [Fact]
    public void Route_points_and_entities_share_one_projection_without_mutating_world_points()
    {
        (double X,double Y)[] route=[(-5000,0),(0,-5000),(5000,0)];var original=route.ToArray();
        var frame=Frame(90);Assert.True(frame.TryProject(route[1].X,route[1].Y,out var point));
        Assert.Equal(100,point.ScreenX,7);Assert.Equal(200,point.ScreenY,7);Assert.Equal(original,route);
    }
    [Fact]
    public void Moving_the_player_recomputes_relative_distance_without_moving_center()
    {
        Assert.True(Frame().TryProject(0,-5000,out var before));
        Assert.True(Frame(playerY:-2500).TryProject(0,-5000,out var after));
        Assert.Equal(50,before.DistanceM);Assert.Equal(25,after.DistanceM);
        Assert.Equal(100,before.ScreenY);Assert.Equal(150,after.ScreenY);
        Assert.True(Frame(playerY:-2500).TryProject(0,-2500,out var self));Assert.Equal(200,self.ScreenY);
    }
    [Fact]
    public void Configured_world_north_is_used_instead_of_hardcoded_world_axes()
    {
        Assert.True(RadarProjector.TryCreate(0,0,0,Orientation.Parse("+x"),75,150,RadarOrientationMode.CharacterUp,out var frame));
        Assert.True(frame.TryProject(5000,0,out var north));Assert.Equal(0,north.ScreenX);Assert.Equal(-100,north.ScreenY);
    }
    [Theory] [InlineData(0)] [InlineData(-1)] [InlineData(double.NaN)] [InlineData(double.PositiveInfinity)]
    public void Invalid_radius_is_rejected(double range) =>
        Assert.False(RadarProjector.TryCreate(0,0,0,Game,range,150,RadarOrientationMode.CharacterUp,out _));
    [Fact]
    public void Nonfinite_world_input_or_default_frame_never_reaches_drawing()
    {
        Assert.False(Frame().TryProject(double.NaN,0,out _));Assert.False(Frame().TryProject(double.PositiveInfinity,0,out _));
        Assert.False(default(RadarProjector).TryProject(0,0,out _));
        Assert.False(RadarProjector.TryCreate(double.NaN,0,0,Game,75,150,RadarOrientationMode.CharacterUp,out _));
    }
    [Fact]
    public void Sonar_only_samples_visual_phase_and_never_changes_projected_entities()
    {
        var frame=Frame();Assert.True(frame.TryProject(1000,-2000,out var before));
        var pulse=RadarPulse.Sample(1250,true);Assert.Equal(.5,pulse.RadiusRatio);Assert.True(pulse.Opacity>0);
        Assert.Equal(0,RadarPulse.Sample(1250,false).Opacity);Assert.Equal(RadarPulse.Sample(0,true),RadarPulse.Sample(2500,true));
        Assert.True(frame.TryProject(1000,-2000,out var after));Assert.Equal(before,after);
    }
}
