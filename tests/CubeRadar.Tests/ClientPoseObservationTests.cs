using System.Text.Json;
using CubeRadar.Core.Radar;

namespace CubeRadar.Tests;

public class ClientPoseObservationTests
{
    static ClientMotionFix Fix=>new(0,0,0,0,0,10500,"0137") {Stopped=true,PoseGeneration=1,PoseSerial=2,PositionChangedAtMs=10000};
    static ClientMotionFix? Observe(long now,long observed,bool stopped=true,long serial=2)
    {
        using var json=JsonDocument.Parse(JsonSerializer.Serialize(new {motionAtMs=10500,observationAtMs=observed,positionChangedAtMs=10000,stopped,generation=1,serial}));
        return ClientPoseObservation.Apply(Fix,json.RootElement,now);
    }
    [Fact] public void Same_coordinates_with_a_new_validated_observation_are_fresh_without_rewriting_the_motion_time()
    {
        var fix=Observe(14000,14000);Assert.NotNull(fix);Assert.Equal(10500,fix.TimeMs);
        Assert.Equal(14000,fix.LastObservationAt);Assert.Equal(10000,fix.LastPositionChangedAt);
        Assert.Equal(Fix.X,fix.X);
    }
    [Theory][InlineData(14000,14001,true,2)][InlineData(14000,14000,false,2)][InlineData(14000,14000,true,3)]
    public void Future_or_unmatched_observations_cannot_renew_a_retained_coordinate(long now,long observed,bool stopped,long serial)
        =>Assert.Null(Observe(now,observed,stopped,serial));
    [Fact] public void Even_a_matched_moving_pose_cannot_be_renewed_by_clock_alone()
    {
        using var json=JsonDocument.Parse("{\"motionAtMs\":10500,\"observationAtMs\":14000,\"positionChangedAtMs\":10000,\"stopped\":false,\"generation\":1,\"serial\":2}");
        Assert.Null(ClientPoseObservation.Apply(Fix with {Stopped=false},json.RootElement,14000));
    }
}
