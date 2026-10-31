namespace CubeRadar.Core.Radar;

public sealed record RadarProjection(double X, double Y, double DistanceM, double RelativeDegrees, bool Clamped);

/// <summary>XY world centimetres → screen pixels, with clockwise degrees from the configured world north.</summary>
public static class RadarMath
{
    public static double CalculateDistance(double dx, double dy) => Math.Sqrt(dx*dx+dy*dy)/Units.PerMetre;
    public static double CalculateHeightDifference(double playerZ, double cubeZ) => (cubeZ-playerZ)/Units.PerMetre;
    public static double CalculateRelativeBearing(double bearing, double heading) => ((bearing-heading)%360+540)%360-180;
    public static string GetRelativeDirection(double angle)
    {
        string[] names=["Ön","Sağ Ön","Sağ","Sağ Arka","Arka","Sol Arka","Sol","Sol Ön"];
        return names[(int)Math.Floor((((angle%360)+360)%360+22.5)/45)%8];
    }

    public static RadarProjection? WorldToRadar(double dx, double dy, double? heading, Orientation axes,
        double radius, double rangeM=150, bool showOutside=false)
    {
        // Keep the legacy nullable/clamped API while using the same XY projection as the tactical canvas.
        if(heading is not {} h||!RadarProjector.TryNormalizeHeading(h,out var normalized)||
            !RadarProjector.TryCreate(0,0,normalized,axes,rangeM,radius,RadarOrientationMode.CharacterUp,out var frame)||
            !frame.TryProject(dx,dy,out var point)||!point.Inside&&!showOutside)return null;
        var clamp=point.Inside?1:rangeM/point.DistanceM;
        return new(point.ScreenX*clamp,point.ScreenY*clamp,point.DistanceM,
            CalculateRelativeBearing(axes.Bearing(0,0,dx,dy),normalized),!point.Inside);
    }
}

public enum HeadingSource { Unknown, Packet, MovementDerived }
public sealed record HeadingView(double? Degrees, HeadingSource Source, long TimeMs);

/// <summary>Explicitly opt-in travel-direction estimate. It is never character/camera facing.</summary>
public sealed class MovementHeadingTracker
{
    private FixView? _previous;
    private uint? _self;
    private int? _map;
    private HeadingView _heading=new(null,HeadingSource.Unknown,0);

    public void Observe(FixView? fix, uint? self, int? map, Orientation axes)
    {
        if(fix is null || _self!=self || _map!=map)
        {
            _heading=new(null,HeadingSource.Unknown,0);
            _previous=null; _self=self; _map=map;
        }
        if(fix is null || fix.TimeMs==_previous?.TimeMs)return;
        if(_previous is { } previous)
        {
            var dt=fix.TimeMs-previous.TimeMs;
            var distance=RadarMath.CalculateDistance(fix.X-previous.X,fix.Y-previous.Y);
            if(fix.Source is "Teleport" or "MapLoad" || dt<=0 || dt>5000 || distance/(dt/1000.0)>40)
                _heading=new(null,HeadingSource.Unknown,0);
            else if(distance>=1)
                _heading=new(axes.Bearing(previous.X,previous.Y,fix.X,fix.Y),HeadingSource.MovementDerived,fix.TimeMs);
        }
        _previous=fix;
    }

    public HeadingView Read(long nowMs,bool enabled) => enabled && nowMs>=_heading.TimeMs && nowMs-_heading.TimeMs<=3000
        ? _heading : new(null,HeadingSource.Unknown,0);
}

/// <summary>Audio scheduling only; capture/render threads never invoke an audio device.</summary>
public sealed class AlertCadence
{
    private long? _last;
    public bool ShouldPlay(long nowMs,bool soundEnabled,bool liveSession,bool proximity,double? nearestM,bool newCube)
    {
        if(!soundEnabled||!liveSession)return false;
        var interval=nearestM switch { >=0 and <=25=>700, <=50=>1200, <=100=>2200, <=150=>3500, _=>int.MaxValue };
        var elapsed=_last is { } last?nowMs-last:long.MaxValue;
        if((newCube&&elapsed>=3000)||(proximity&&interval!=int.MaxValue&&elapsed>=interval))
        { _last=nowMs; return true; }
        return false;
    }
}
