using CubeRadar.Core.Radar;

namespace CubeRadar.Tests;

public class RadarMathTests
{
    private static readonly Orientation Game = Orientation.Parse("-y");

    [Theory]
    [InlineData(0, -10000, 0, 0, -100, "Ön")]
    [InlineData(0, -10000, 90, -100, 0, "Sol")]
    [InlineData(10000, 0, 90, 0, -100, "Ön")]
    public void Targets_rotate_around_a_fixed_player(double dx, double dy, double heading,
        double x, double y, string direction)
    {
        var p = RadarMath.WorldToRadar(dx, dy, heading, Game, 150)!;
        Assert.Equal(x, p.X, 7);
        Assert.Equal(y, p.Y, 7);
        Assert.Equal(direction, RadarMath.GetRelativeDirection(p.RelativeDegrees));
    }

    [Fact]
    public void Moving_player_changes_target_offset_not_center()
    {
        Assert.Equal(-100, RadarMath.WorldToRadar(0, -10000, 0, Game, 150)!.Y);
        Assert.Equal(-50, RadarMath.WorldToRadar(0, -5000, 0, Game, 150)!.Y);
        Assert.Equal(50, RadarMath.CalculateHeightDifference(0, 5000));
    }

    [Theory]
    [InlineData(0)] [InlineData(50)] [InlineData(100)] [InlineData(150)]
    public void Rings_use_one_linear_scale(double metres) =>
        Assert.Equal(-metres * 2, RadarMath.WorldToRadar(0, -metres*100, 0, Game, 300)!.Y, 7);

    [Fact]
    public void Outside_range_is_hidden_unless_explicitly_clamped()
    {
        Assert.Null(RadarMath.WorldToRadar(0,-18700,0,Game,150));
        var p = RadarMath.WorldToRadar(0,-18700,0,Game,150,showOutside:true)!;
        Assert.True(p.Clamped);
        Assert.Equal(187,p.DistanceM);
        Assert.Equal(-150,p.Y);
        Assert.Null(RadarMath.WorldToRadar(100,100,null,Game,150));
    }

    [Theory]
    [InlineData(-22.49,"Ön")] [InlineData(22.5,"Sağ Ön")]
    [InlineData(90,"Sağ")] [InlineData(135,"Sağ Arka")]
    [InlineData(180,"Arka")] [InlineData(225,"Sol Arka")]
    [InlineData(270,"Sol")] [InlineData(315,"Sol Ön")]
    public void Relative_sectors_wrap_without_mirroring(double angle,string name) =>
        Assert.Equal(name,RadarMath.GetRelativeDirection(angle));

    [Fact]
    public void Movement_estimate_is_opt_in_expires_and_never_claims_packet_facing()
    {
        var tracker=new MovementHeadingTracker();
        tracker.Observe(new(20000,20000,0,1000,0,"BuffA"),1,1,Game);
        Assert.Equal(HeadingSource.Unknown,tracker.Read(1000,true).Source);
        tracker.Observe(new(21000,20000,0,2000,0,"BuffA"),1,1,Game);
        Assert.Equal(HeadingSource.Unknown,tracker.Read(2000,false).Source);
        Assert.Equal(HeadingSource.MovementDerived,tracker.Read(2000,true).Source);
        Assert.Equal(90,tracker.Read(2000,true).Degrees);
        Assert.Null(tracker.Read(5001,true).Degrees);
        tracker.Observe(new(22000,20000,0,2500,0,"Teleport"),1,1,Game);
        Assert.Null(tracker.Read(2500,true).Degrees);
    }

    [Fact]
    public void New_identity_or_large_jump_cannot_be_used_as_movement_direction()
    {
        var tracker=new MovementHeadingTracker();
        tracker.Observe(new(20000,20000,0,1000,0,"BuffA"),1,1,Game);
        tracker.Observe(new(21000,20000,0,2000,0,"BuffA"),2,1,Game);
        Assert.Null(tracker.Read(2000,true).Degrees);
        tracker.Observe(new(60000,20000,0,2100,0,"BuffA"),2,1,Game);
        Assert.Null(tracker.Read(2100,true).Degrees);
    }

    [Fact]
    public void Muting_alerts_disables_even_proximity_and_demo_never_sounds()
    {
        var cadence=new AlertCadence();
        Assert.False(cadence.ShouldPlay(10000,false,true,true,10,true));
        Assert.False(cadence.ShouldPlay(10000,true,false,true,10,true));
        Assert.True(cadence.ShouldPlay(10000,true,true,true,10,true));
        Assert.False(cadence.ShouldPlay(10400,true,true,true,10,true));
        Assert.True(cadence.ShouldPlay(10700,true,true,true,10,false));
        Assert.False(cadence.ShouldPlay(12000,true,true,true,null,false));
    }
}
