namespace CubeRadar.Core.Radar;

public readonly record struct RadarPoseDisplay(FixView Self,double Heading,bool LastKnown);

/// <summary>Visual history only. Never renews a timestamp or proves a stop.</summary>
public sealed class RadarPoseMemory
{
    (int Map,long Session,int Pid,long Generation)? _context;
    RadarPoseDisplay? _display;
    ClientMotionFix? _sample;
    long _rejectedMotionAt;

    public RadarPoseDisplay? Read(RadarSnapshot snapshot,CubeCompanionView view,ClientMotionFix? motion,
        bool readerReady,bool captureHealthy,long session,int gamePid)
    {
        if(!readerReady||!captureHealthy||!view.LiveTraffic||motion is null||snapshot.MapId is not >0||
            session<=0||gamePid<=0||motion.PoseGeneration<0)
        {_context=null;_display=null;_sample=null;_rejectedMotionAt=0;return null;}
        var context=(snapshot.MapId.Value,session,gamePid,motion.PoseGeneration);
        if(_context is {} previous&&previous!=context)
        {_display=null;_sample=null;_rejectedMotionAt=motion.TimeMs;}
        _context=context;
        if(motion.TimeMs==_rejectedMotionAt)return null;
        if(view.FreshPosition&&snapshot.IdentitySource==SelfIdentitySource.OwnedConnection&&snapshot.Self is {} self&&
            self.TimeMs==motion.LastObservationAt)
        {
            var axes=new Orientation(snapshot.Orientation.NorthX,snapshot.Orientation.NorthY,snapshot.Orientation.EastX,snapshot.Orientation.EastY);
            _sample=motion;_display=new(self,ClientMotionPresentation.Facing(motion,axes),false);return _display;
        }
        if(_sample is {} saved&&_display is {} held&&saved.TimeMs==motion.TimeMs&&saved.PoseSerial==motion.PoseSerial&&
            saved.X==motion.X&&saved.Y==motion.Y&&saved.Z==motion.Z&&saved.FacingYaw==motion.FacingYaw)
            return held with {LastKnown=true,Self=held.Self with {AgeMs=Math.Max(0,snapshot.NowMs-held.Self.TimeMs)}};
        _sample=null;_display=null;return null;
    }
}
