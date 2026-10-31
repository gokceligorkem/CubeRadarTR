namespace CubeRadar.Core.Radar;

public enum RadarOrientationMode { CharacterUp, NorthUp }

/// <summary>Local XY metres (+right, +down), absolute screen pixels and planar distance; no world mutation.</summary>
public readonly record struct RadarPoint(double LocalXM,double LocalYM,double ScreenX,double ScreenY,double DistanceM,bool Inside);

/// <summary>
/// One immutable transform per frame. World XY is centimetres; Z is height and never enters this transform.
/// Heading is clockwise degrees from configured north. Character-up rotates bearings by -heading.
/// </summary>
public readonly struct RadarProjector
{
    readonly double _playerX,_playerY,_sin,_cos,_scale,_centerX,_centerY,_range;
    readonly Orientation _axes;
    readonly bool _valid;
    public double? PlayerArrowDegrees {get;}

    RadarProjector(double x,double y,double? heading,Orientation axes,double range,double pixels,
        RadarOrientationMode mode,double centerX,double centerY)
    {
        _playerX=x;_playerY=y;_axes=axes;_range=range;_scale=pixels/range;_centerX=centerX;_centerY=centerY;
        var angle=(mode==RadarOrientationMode.CharacterUp?heading!.Value:0)*Math.PI/180;
        (_sin,_cos)=Math.SinCos(angle);_valid=true;
        PlayerArrowDegrees=mode==RadarOrientationMode.CharacterUp?0:heading;
    }

    public static bool TryNormalizeHeading(double degrees,out double normalized)
    {
        normalized=0;if(!double.IsFinite(degrees))return false;
        normalized=(degrees%360+360)%360;return true;
    }

    public static bool TryCreate(double playerX,double playerY,double? heading,Orientation axes,double rangeM,double screenRadius,
        RadarOrientationMode mode,out RadarProjector frame,double centerX=0,double centerY=0)
    {
        frame=default;
        if(!double.IsFinite(playerX)||!double.IsFinite(playerY)||!double.IsFinite(rangeM)||rangeM<=0||
            !double.IsFinite(screenRadius)||screenRadius<=0||!double.IsFinite(centerX)||!double.IsFinite(centerY)||
            mode is not (RadarOrientationMode.CharacterUp or RadarOrientationMode.NorthUp)||
            !double.IsFinite(axes.NorthX)||!double.IsFinite(axes.NorthY)||!double.IsFinite(axes.EastX)||!double.IsFinite(axes.EastY)||
            Math.Abs(axes.NorthX*axes.NorthX+axes.NorthY*axes.NorthY-1)>1e-8||
            Math.Abs(axes.EastX*axes.EastX+axes.EastY*axes.EastY-1)>1e-8||
            Math.Abs(axes.NorthX*axes.EastX+axes.NorthY*axes.EastY)>1e-8)return false;
        double? facing=heading is {} h&&TryNormalizeHeading(h,out var n)?n:null;
        if(mode==RadarOrientationMode.CharacterUp&&facing is null)return false;
        frame=new(playerX,playerY,facing,axes,rangeM,screenRadius,mode,centerX,centerY);return true;
    }

    public bool TryProject(double targetX,double targetY,out RadarPoint point)
    {
        point=default;if(!_valid||!double.IsFinite(targetX)||!double.IsFinite(targetY))return false;
        var dx=(targetX-_playerX)/Units.PerMetre;var dy=(targetY-_playerY)/Units.PerMetre;
        if(!double.IsFinite(dx)||!double.IsFinite(dy))return false;
        var north=dx*_axes.NorthX+dy*_axes.NorthY;var east=dx*_axes.EastX+dy*_axes.EastY;
        var x=east*_cos-north*_sin;var y=-(north*_cos+east*_sin);
        var max=Math.Max(Math.Abs(dx),Math.Abs(dy));
        var distance=max==0?0:max*Math.Sqrt((dx/max)*(dx/max)+(dy/max)*(dy/max));
        var screenX=_centerX+x*_scale;var screenY=_centerY+y*_scale;
        if(!double.IsFinite(distance)||!double.IsFinite(screenX)||!double.IsFinite(screenY))return false;
        point=new(x,y,screenX,screenY,distance,distance<=_range);return true;
    }
}

/// <summary>A visual phase only. No entities, discovery, tracking or clock state are accepted or changed.</summary>
public readonly record struct RadarPulse(double RadiusRatio,double Opacity)
{
    public static RadarPulse Sample(long elapsedMs,bool enabled)
    {
        var phase=((elapsedMs%2500+2500)%2500)/2500.0;
        return new(phase,enabled?1-phase:0);
    }
}
