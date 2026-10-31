using System.Buffers.Binary;
using CubeRadar.Core.Audio;

namespace CubeRadar.Tests;

public class BossFanfareTests
{
    [Fact]
    public void Boss_alert_has_its_own_longer_pcm_sound_with_no_clipping_or_silent_volume_bug()
    {
        var full=BossFanfare.Create(100);var quiet=BossFanfare.Create(50);var mute=BossFanfare.Create(0);
        var layout=WavVolume.Find(full);Assert.NotNull(layout);
        Assert.InRange(layout.Value.DataLength/(2.0*WavVolume.SampleRate),2.0,3.0);
        var samples=Enumerable.Range(0,layout.Value.DataLength/2).Select(i=>(int)BinaryPrimitives.ReadInt16LittleEndian(full.AsSpan(layout.Value.DataOffset+2*i))).ToArray();
        var peak=samples.Max(s=>Math.Abs(s));Assert.InRange(peak,5000,30000);
        Assert.All(mute.Skip(44),b=>Assert.Equal(0,b));
        var quietPeak=Enumerable.Range(0,(quiet.Length-44)/2).Max(i=>Math.Abs((int)BinaryPrimitives.ReadInt16LittleEndian(quiet.AsSpan(44+2*i))));
        Assert.InRange(quietPeak,peak*.249,peak*.251);
        Assert.Equal(0,samples[0]);Assert.Equal(0,samples[^1]);
        Assert.False(full.AsSpan().SequenceEqual(WavVolume.Beep(100)));
    }
}
