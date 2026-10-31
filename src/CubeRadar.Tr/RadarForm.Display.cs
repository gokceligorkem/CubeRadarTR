using CubeRadar.Core.Radar;

namespace CubeRadar.Tr;

internal sealed partial class RadarForm
{
    private readonly RadarPoseMemory _radarPoseMemory=new();
    private CubeCompanionView? _radarDisplayView;
    private FixView? _radarDisplaySelf;
    private double? _radarDisplayHeading;

    private void PrepareRadarDisplay(RadarSnapshot snapshot,ClientMotionFix? motion,bool ready,long session,double? demoHeading)
    {
        if(_state is null)return;
        _radarDisplayView=_state;_radarDisplaySelf=snapshot.Self;
        _radarDisplayHeading=_controller.IsDemo?demoHeading:_heading.Source==HeadingSource.Packet?_heading.Degrees:null;
        if(!_controller.IsDemo&&_radarPoseMemory.Read(snapshot,_state,motion,ready,_controller.Running,
            session,_motion.GamePid) is {LastKnown:true} held)
        {
            _radarDisplayView=_state.WithLastKnownOrigin(held.Self,snapshot.Orientation);
            _radarDisplaySelf=held.Self;_radarDisplayHeading=held.Heading;
        }
        _radarDisplayView=TargetTracker.Filter(_radarDisplayView,(HeightFilter)_heightFilter.SelectedIndex);
    }
}
