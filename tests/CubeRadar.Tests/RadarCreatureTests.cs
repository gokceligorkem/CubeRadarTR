using CubeRadar.Core.Pets;
using CubeRadar.Core.Radar;

namespace CubeRadar.Tests;

public class RadarCreatureTests
{
    static readonly PetSource Source=PetCatalog.Default.Sources[0];
    static PetMob Mob=>new(123,Source.NpcId,Source.PetId,Source.Name,0,-5000,0,10000,10000)
        {WireEntityId=123,LifeId=1,BirthMs=10000};
    static RadarProjector Frame(double heading=0)
    {
        Assert.True(RadarProjector.TryCreate(0,0,heading,Orientation.Parse("-y"),75,150,RadarOrientationMode.CharacterUp,out var frame));
        return frame;
    }
    [Theory] [InlineData(0,0,-100)] [InlineData(90,-100,0)]
    public void Known_creatures_share_the_cube_projection_without_requiring_a_pet_goal(double heading,double x,double y)
    {
        Assert.True(RadarCreaturePresentation.TryProject(Mob,Frame(heading),0,HeightFilter.All,10000,out var point));
        Assert.Equal(x,point.Projection.ScreenX,7);Assert.Equal(y,point.Projection.ScreenY,7);
        Assert.Equal(123u,point.Mob.EntityId);Assert.Equal(50,point.Projection.DistanceM);
    }
    [Theory]
    [InlineData(25000,true)] [InlineData(25001,false)] [InlineData(9999,false)]
    public void Position_freshness_uses_existing_pet_direction_fifteen_second_window(long now,bool visible) =>
        Assert.Equal(visible,RadarCreaturePresentation.TryProject(Mob,Frame(),0,HeightFilter.All,now,out _));
    [Theory] [InlineData(7500,true)] [InlineData(7501,false)]
    public void Normal_creatures_are_hidden_outside_radius_even_when_cube_edge_markers_are_enabled(float x,bool visible) =>
        Assert.Equal(visible,RadarCreaturePresentation.TryProject(Mob with {X=x,Y=0},Frame(),0,HeightFilter.All,10000,out _));
    [Theory] [InlineData(0,HeightFilter.SameLevel,true)] [InlineData(300,HeightFilter.SameLevel,false)]
    [InlineData(300,HeightFilter.Above,true)] [InlineData(-300,HeightFilter.Below,true)] [InlineData(300,HeightFilter.Below,false)]
    public void Existing_height_filter_applies_to_creatures(float z,HeightFilter filter,bool visible) =>
        Assert.Equal(visible,RadarCreaturePresentation.TryProject(Mob with {Z=z},Frame(),0,filter,10000,out _));
    [Fact]
    public void Unknown_identity_or_position_does_not_become_a_radar_creature()
    {
        var mob=Mob;var frame=Frame();
        foreach(var invalid in new[]{mob with {X=null},mob with {Y=null},mob with {Z=null},mob with {X=float.NaN},
            mob with {Z=float.PositiveInfinity},mob with {NpcId=9999999},mob with {PetId=-1},
            mob with {PositionMs=0},mob with {SeenMs=0},mob with {LifeId=0},mob with {WireEntityId=0}})
            Assert.False(RadarCreaturePresentation.TryProject(invalid,frame,0,HeightFilter.All,10000,out _));
        Assert.False(RadarCreaturePresentation.TryProject(mob,default,0,HeightFilter.All,10000,out _));
    }
    [Fact]
    public void Projecting_a_creature_does_not_change_source_world_or_life_state()
    {
        var mob=Mob;var original=mob with {};
        Assert.True(RadarCreaturePresentation.TryProject(mob,Frame(90),0,HeightFilter.All,10000,out var point));
        Assert.Same(mob,point.Mob);Assert.Equal(original,mob);Assert.Equal(0,point.HeightM);
    }
}
