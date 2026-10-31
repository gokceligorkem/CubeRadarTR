namespace CubeRadar.Core.Radar;

/// <summary>Presentation policy for the cube-only companion. Stale traffic must never imply live presence.</summary>
public sealed record CubeCompanionView(bool LiveTraffic, bool FreshPosition, long PositionAgeMs,
    List<CubeView> Cubes, CubeView? Nearest)
{
    /// <summary>Explicit visual-history flag; FreshPosition remains false and no gameplay consumer receives renewed data.</summary>
    public bool LastKnownPosition {get;init;}
    public CubeCompanionView WithLastKnownOrigin(FixView self,OrientationView axes)=>this with {
        FreshPosition=false,LastKnownPosition=true,Nearest=null,Cubes=Cubes.Select(c=>Geometry(c,self,axes)).ToList()};
    public static CubeCompanionView Build(RadarSnapshot snapshot, long wallMs)
    {
        var packetAge = Math.Max(0, wallMs - snapshot.NowMs);
        var live = snapshot.NowMs > 0 && packetAge <= 10_000;
        var age = snapshot.Self is { } fix ? Math.Max(0, wallMs - fix.TimeMs) : long.MaxValue;
        var identified = snapshot.IdentitySource is SelfIdentitySource.SelfInfo or SelfIdentitySource.OwnRecords or SelfIdentitySource.OwnedConnection;
        var fresh = live && identified && snapshot.Self is not null && age <= snapshot.FixFreshSeconds * 1000;
        var cubes = live
            ? snapshot.Cubes.Where(c => c.Status is "Visible" or "Opening")
                .Select(c=>fresh?Geometry(c,snapshot.Self!,snapshot.Orientation):c)
                .OrderBy(c => c.DistanceM ?? double.MaxValue).ToList()
            : [];
        return new(live, fresh, age, cubes, fresh ? cubes.FirstOrDefault(c => c.DistanceM is not null) : null);
    }

    private static CubeView Geometry(CubeView cube,FixView self,OrientationView orientation)
    {
        var dx=cube.X-self.X;var dy=cube.Y-self.Y;var dz=cube.Z-self.Z;
        var axes=new Orientation(orientation.NorthX,orientation.NorthY,orientation.EastX,orientation.EastY);
        var bearing=axes.Bearing(self.X,self.Y,cube.X,cube.Y);
        return cube with { DistanceM=RadarMath.CalculateDistance(dx,dy),Distance3dM=Math.Sqrt(dx*dx+dy*dy+dz*dz)/Units.PerMetre,
            Bearing=bearing,Compass=Orientation.Compass(bearing),DzUnits=dz };
    }
}
