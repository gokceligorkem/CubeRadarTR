namespace CubeRadar.Core.Radar;

/// <summary>Own motion from a process-owned C2S connection after cryptographic clock validation.</summary>
public sealed record ClientMotionFix(double X,double Y,double Z,double FacingYaw,double? MovementYaw,long TimeMs,string Opcode)
{
    public bool Stopped {get;init;}
    public long PoseGeneration {get;init;}
    public long PoseSerial {get;init;}
    public long ObservationAtMs {get;init;}
    public long PositionChangedAtMs {get;init;}
    public long LastObservationAt=>ObservationAtMs>0?ObservationAtMs:TimeMs;
    public long LastPositionChangedAt=>PositionChangedAtMs>0?PositionChangedAtMs:TimeMs;
}

public static class ClientMotionPresentation
{
    public static RadarSnapshot Apply(RadarSnapshot snapshot,ClientMotionFix? motion,long now,bool streamReady)
    {
        // A matched explicit stop can have fresh validated observations without a new movement packet.
        // Preserve the raw motion/progress timestamps; only presentation freshness uses the observation lease.
        var observed=motion?.LastObservationAt??0;
        if(!streamReady||motion is null||now-observed is < -1000 or >2500||
            !double.IsFinite(motion.X)||!double.IsFinite(motion.Y)||!double.IsFinite(motion.Z)||
            !double.IsFinite(motion.FacingYaw)||Math.Abs(motion.FacingYaw)>360||
            Math.Abs(motion.X)>=420000||Math.Abs(motion.Y)>=420000||Math.Abs(motion.Z)>=120000)return snapshot;
        return snapshot with
        {
            Self=new(motion.X,motion.Y,motion.Z,observed,Math.Max(0,now-observed),$"C2S 0x{motion.Opcode}"),
            SelfId=snapshot.IdentitySource is SelfIdentitySource.SelfInfo or SelfIdentitySource.OwnRecords?snapshot.SelfId:null,
            IdentitySource=SelfIdentitySource.OwnedConnection,FixFreshSeconds=2.5
        };
    }

    public static double Facing(ClientMotionFix motion,Orientation axes)
    {
        var yaw=motion.FacingYaw*Math.PI/180;
        return axes.Bearing(0,0,Math.Cos(yaw),Math.Sin(yaw));
    }
}
