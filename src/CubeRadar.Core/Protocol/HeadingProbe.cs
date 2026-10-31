using System.Buffers.Binary;

namespace CubeRadar.Core.Protocol;

/// <summary>A bounded diagnostic field sample, NEVER a verified facing measurement.</summary>
public sealed record HeadingCandidate(long TimeMs,string PacketType,int Offset,float Value,string Note)
{
    public bool Verified => false;
}

public static class HeadingProbe
{
    public static HeadingCandidate? Read(long timeMs,ushort opcode,ReadOnlySpan<byte> body,uint? self)
    {
        if(self is null)return null;
        int offset;
        string note;
        if(opcode==0x2136)
        {
            offset=28;
            note="Map-load sonrası f32; açı birimi/eksen/yön ve sürekli dönüş doğrulanmadı.";
        }
        else if(opcode==0x0238)
        {
            var o=0;
            if(!Wire.TryVarint(body,ref o,out var actor)||actor!=self||!Wire.TryVarint(body,ref o,out _))return null;
            o+=5;
            if(o>=body.Length||body[o++]!=2||!Wire.TryVarint(body,ref o,out _))return null;
            offset=o;
            note="Kendi cast kaydında target öncesi değil, target sonrası f32; sahiplik/açı anlamı doğrulanmadı.";
        }
        else return null;
        if(offset+4>body.Length)return null;
        var value=BinaryPrimitives.ReadSingleLittleEndian(body[offset..]);
        return float.IsFinite(value)?new(timeMs,$"0x{opcode:X4}",offset,value,note):null;
    }
}
