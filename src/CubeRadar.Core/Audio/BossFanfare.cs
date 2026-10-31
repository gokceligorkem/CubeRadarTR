using System.Buffers.Binary;

namespace CubeRadar.Core.Audio;

/// <summary>Original 2.6-second war-horn and low-drum motif, generated locally; no downloaded game audio.</summary>
public static class BossFanfare
{
    public static byte[] Create(int volumePct)
    {
        const int rate=WavVolume.SampleRate;
        var count=rate*26/10;var wav=new byte[44+count*2];var b=wav.AsSpan();
        "RIFF"u8.CopyTo(b);BinaryPrimitives.WriteInt32LittleEndian(b[4..],wav.Length-8);"WAVEfmt "u8.CopyTo(b[8..]);
        BinaryPrimitives.WriteInt32LittleEndian(b[16..],16);BinaryPrimitives.WriteInt16LittleEndian(b[20..],1);BinaryPrimitives.WriteInt16LittleEndian(b[22..],1);
        BinaryPrimitives.WriteInt32LittleEndian(b[24..],rate);BinaryPrimitives.WriteInt32LittleEndian(b[28..],rate*2);
        BinaryPrimitives.WriteInt16LittleEndian(b[32..],2);BinaryPrimitives.WriteInt16LittleEndian(b[34..],16);"data"u8.CopyTo(b[36..]);BinaryPrimitives.WriteInt32LittleEndian(b[40..],count*2);
        var gain=WavVolume.Gain(volumePct);
        for(var i=0;i<count;i++)
        {
            var t=i/(double)rate;
            var sound=Horn(t-.06,110,.62)*.29+Horn(t-.40,164.8138,.65)*.25+Horn(t-.83,220,1.35)*.27
                +Horn(t-1.0,110,1.2)*.11+Drum(t)*.25+Drum(t-.28)*.2+Drum(t-.78)*.26;
            // Short, quiet room reflections rather than a sharp electronic beep.
            sound+=Horn(t-1.02,220,1.35)*.055+Horn(t-1.16,110,1.2)*.025;
            var fade=Math.Min(1,Math.Min(t/0.008,(count-1-i)/(rate*.12)));
            BinaryPrimitives.WriteInt16LittleEndian(b[(44+i*2)..],(short)Math.Round(Math.Clamp(sound*fade*gain,-.9,.9)*short.MaxValue));
        }
        return wav;
    }
    private static double Horn(double t,double hz,double duration)
    {
        if(t<0||t>=duration)return 0;
        var envelope=Math.Min(1,t/.075)*Math.Min(1,(duration-t)/.27)*Math.Exp(-t*.35);
        var phase=2*Math.PI*hz*t+.012*Math.Sin(2*Math.PI*4.3*t);
        return envelope*(.65*Math.Sin(phase)+.22*Math.Sin(phase*2)+.09*Math.Sin(phase*3)+.035*Math.Sin(phase*4));
    }
    private static double Drum(double t)=>t<0||t>.8?0:Math.Min(1,t/.004)*Math.Exp(-t*9)*Math.Sin(2*Math.PI*(55*t+12*(1-Math.Exp(-t*23))));
}
