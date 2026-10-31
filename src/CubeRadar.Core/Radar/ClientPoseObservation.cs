using System.Text.Json;

namespace CubeRadar.Core.Radar;

/// <summary>Only a matched explicit stop can be renewed by a validated same-stream world clock.</summary>
public static class ClientPoseObservation
{
    public static ClientMotionFix? Apply(ClientMotionFix? motion,JsonElement value,long receivedMs)
    {
        if(motion is null||value.ValueKind!=JsonValueKind.Object)return null;
        bool Number(string key,out long result)
        {result=0;return value.TryGetProperty(key,out var field)&&field.ValueKind==JsonValueKind.Number&&field.TryGetInt64(out result);}
        if(!Number("motionAtMs",out var at)||at!=motion.TimeMs||!Number("observationAtMs",out var observed)||observed<at||observed>receivedMs||
            !Number("positionChangedAtMs",out var changed)||changed<=0||changed>at||changed!=motion.LastPositionChangedAt||
            !Number("generation",out var generation)||generation!=motion.PoseGeneration||generation<0||
            !Number("serial",out var serial)||serial<=0||serial!=motion.PoseSerial||
            !value.TryGetProperty("stopped",out var stopped)||stopped.ValueKind is not (JsonValueKind.True or JsonValueKind.False)||
            stopped.GetBoolean()!=motion.Stopped||observed>at&&!motion.Stopped||observed<motion.LastObservationAt)return null;
        return motion with {ObservationAtMs=observed};
    }
}
