using CubeRadar.Core.Pets;

namespace CubeRadar.Core.Radar;

public readonly record struct RadarCreaturePoint(PetMob Mob,RadarPoint Projection,double HeightM)
{public bool LastKnown {get;init;}}

/// <summary>Presentation of existing catalog observations only; never discovers or selects a game target.</summary>
public static class RadarCreaturePresentation
{
    public static bool TryProject(PetMob mob,RadarProjector frame,double playerZ,HeightFilter height,long now,out RadarCreaturePoint point,bool showLastKnown=false)
    {
        point=default;
        // Match PetPanel's existing directional-position window; a sighting without coordinates cannot be drawn.
        if(mob.EntityId==0||mob.WireEntityId==0||mob.LifeId<=0||
            !PetCatalog.Default.Npcs.TryGetValue(mob.NpcId,out var source)||source.PetId!=mob.PetId||
            mob.SeenMs<=0||now<mob.SeenMs||mob.PositionMs<=0||now<mob.PositionMs||
            mob.X is not {} x||mob.Y is not {} y||mob.Z is not {} z||!float.IsFinite(z)||!double.IsFinite(playerZ))return false;
        var lastKnown=now-mob.SeenMs>90000||now-mob.PositionMs>15000;
        if(lastKnown&&!showLastKnown)return false;
        var dz=z-playerZ;
        if(height switch {HeightFilter.All=>false,HeightFilter.SameLevel=>Math.Abs(dz)>=300,
            HeightFilter.Above=>dz<300,HeightFilter.Below=>dz> -300,_=>true})return false;
        if(!frame.TryProject(x,y,out var projection)||!projection.Inside)return false;
        point=new(mob,projection,dz/Units.PerMetre) {LastKnown=lastKnown};return true;
    }
}
