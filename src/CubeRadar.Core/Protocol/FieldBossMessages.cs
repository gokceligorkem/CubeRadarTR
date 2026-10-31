// Field layout derived from cyberbadger6969/aion2-dps-meter PacketParser.cs (GPL-3.0),
// retrieved 2026-10-30. Changes: atomic complete-list parsing, bounds/finite/duplicate checks.
using System.Buffers.Binary;

namespace CubeRadar.Core.Protocol;

public sealed record FieldBossSlot(int SlotId,bool Alive,long AtMs,float X=0,float Y=0,float Z=0);
public sealed record FieldBossList(int MapId,long ObservedMs,IReadOnlyList<FieldBossSlot> Slots);

public static class FieldBossMessages
{
    public const ushort Opcode=0x0191;
    public static bool TryParse(ReadOnlySpan<byte> body,long observedMs,out FieldBossList list)
    {
        list=null!;
        if(body.Length is <10 or >65535||body[0]!=0||body[1]!=0)return false;
        var map=BinaryPrimitives.ReadInt32LittleEndian(body[2..]);var count=body[6];
        if(map is <1 or >9_999_999||count is <1 or >99)return false;
        var slots=new List<FieldBossSlot>();var budget=400;
        if(!ReadSlots(body,7,map,count,slots,ref budget))return false;
        list=new(map,observedMs,slots);return true;
    }
    private static bool ReadSlots(ReadOnlySpan<byte> b,int offset,int map,int left,List<FieldBossSlot> slots,ref int budget)
    {
        if(--budget<0)return false;
        if(left==0)return b.Length-offset<=8&&b[offset..].IndexOfAnyExcept((byte)0)<0;
        if(!Header(b,offset,map,out var alive,out var id,out var at)||slots.Any(s=>s.SlotId==id))return false;
        float x=0,y=0,z=0;
        if(alive)
        {
            if(at+12>b.Length)return false;
            x=BinaryPrimitives.ReadSingleLittleEndian(b[at..]);y=BinaryPrimitives.ReadSingleLittleEndian(b[(at+4)..]);z=BinaryPrimitives.ReadSingleLittleEndian(b[(at+8)..]);at+=12;
            if(!float.IsFinite(x)||!float.IsFinite(y)||!float.IsFinite(z)||Math.Abs(x)>1e7||Math.Abs(y)>1e7||Math.Abs(z)>1e7)return false;
        }
        for(var extra=0;extra<=1;extra++)
        {
            var timeAt=at+extra;if(timeAt+8>b.Length)continue;
            var time=BinaryPrimitives.ReadInt64LittleEndian(b[timeAt..]);
            if(time!=0&&(time<1_600_000_000_000||time>2_600_000_000_000))continue;
            slots.Add(new(id,alive,time,x,y,z));
            if(ReadSlots(b,timeAt+8,map,left-1,slots,ref budget))return true;
            slots.RemoveAt(slots.Count-1);
        }
        return false;
    }
    private static bool Header(ReadOnlySpan<byte> b,int offset,int map,out bool alive,out int id,out int next)
    {
        alive=false;id=0;next=offset;
        if(offset>=b.Length||b[offset]>1||!Wire.TryVarint(b,offset+1,out var raw,out var length)||raw<=map*100L||raw>=map*100L+100)return false;
        alive=b[offset]==1;id=(int)raw;next=offset+1+length;return true;
    }
}
