// Body layouts adapted from 0Fox2Give/PetTracker CollectionWire.cs, GPL-3.0.
// Framing stays in the existing Cube Radar decoder. See licenses/PetTracker/SOURCE.txt.
using CubeRadar.Core.Pets;

namespace CubeRadar.Core.Protocol;

public sealed record PetCollectionData(IReadOnlyDictionary<int,int> Ranks,IReadOnlyDictionary<int,int> Souls);
public sealed record PetPoint(int PetId,int Amount);
public static class PetMessages
{
    public static bool TryCollection(ReadOnlySpan<byte> body,out PetCollectionData data)
    {
        data=null!;var pets=PetCatalog.Default.Pets;
        if(body.Length<6)return false;
        var equipped=(int)Wire.U32(body,0);if(equipped!=0&&!pets.ContainsKey(equipped))return false;
        var at=4;
        if(!Count(body,ref at,pets.Count,out var count)||count>(body.Length-at)/12)return false;
        var ranks=new Dictionary<int,int>();var souls=new Dictionary<int,int>();
        for(var i=0;i<count;i++)
        {
            var id=(int)Wire.U32(body,at);var again=(int)Wire.U32(body,at+4);var rank=(int)Wire.U32(body,at+8);at+=12;
            if(!pets.ContainsKey(id)||again!=id||rank is <1 or >3||!ranks.TryAdd(id,rank))return false;
        }
        if(equipped!=0&&!ranks.ContainsKey(equipped))return false;
        if(!Count(body,ref at,pets.Count,out count)||count>(body.Length-at)/8)return false;
        for(var i=0;i<count;i++)
        {
            var id=(int)Wire.U32(body,at);var value=(int)Wire.U32(body,at+4);at+=8;var rank=ranks.GetValueOrDefault(id);
            if(!pets.ContainsKey(id)||value<0||value>100000||rank is 1 or 2&&value>PetCatalog.Cost(rank)||!souls.TryAdd(id,value))return false;
        }
        // The login packet has other sections after the collection; never scan them for pet IDs.
        data=new(ranks,souls);return true;
    }
    public static bool TryPoints(ReadOnlySpan<byte> body,out PetPoint[] points)
    {
        points=[];var at=0;
        if(!Count(body,ref at,PetCatalog.Default.Pets.Count,out var count)||count==0||body.Length-at!=count*8)return false;
        var result=new PetPoint[count];var ids=new HashSet<int>();
        for(var i=0;i<count;i++)
        {
            var id=(int)Wire.U32(body,at);var amount=(int)Wire.U32(body,at+4);at+=8;
            if(!PetCatalog.Default.Pets.ContainsKey(id)||!ids.Add(id)||amount is <=0 or >75)return false;
            result[i]=new(id,amount);
        }
        points=result;return true;
    }
    public static bool TryRank(ReadOnlySpan<byte> body,out int id,out int rank)
    {
        id=rank=0;if(body.Length!=23||body[0]!=0||body[1]!=0)return false;
        id=(int)Wire.U32(body,2);rank=(int)Wire.U32(body,6);
        return PetCatalog.Default.Pets.ContainsKey(id)&&rank is >=1 and <=3;
    }
    // Positive hit layouts 4/6 adapted from cyberbadger6969/aion2-dps-meter PacketParser.cs (GPL-3.0).
    // The per-hit UID byte precedes damage type. Cast markers (layout 0) are not hits.
    public static bool TryHit(ReadOnlySpan<byte> body,out uint actor,out uint target)
    {
        var hits=ReadHits(body);actor=target=0;if(hits.Count==0)return false;actor=hits[0].Actor;target=hits[0].Target;return true;
    }
    public static bool TryHitNotice(ReadOnlySpan<byte> body,out uint actor,out uint target)
    {
        actor=target=0;var at=0;
        return Wire.TryVarint(body,ref at,out actor)&&actor>0&&Wire.TryU8(body,ref at,out var zero)&&zero==0&&
            Wire.TryVarint(body,ref at,out target)&&target>0&&target!=actor&&Wire.TryU32(body,ref at,out var skill)&&skill is >=1 and <=299999999;
    }
    public static IReadOnlyList<(uint Actor,uint Target)> ReadHits(ReadOnlySpan<byte> body)
    {
        var result=new List<(uint,uint)>();var at=0;
        for(var n=0;n<64&&at<body.Length;n++)
        {
            if(at+1<body.Length&&body[at]==1&&body[at+1]==0)at+=2;else if(n>0)break;
            if(!ReadHit(body,ref at,out var actor,out var target,out var clean))break;
            result.Add((actor,target));if(!clean)break;
        }
        return result;
    }
    static bool ReadHit(ReadOnlySpan<byte> b,ref int at,out uint actor,out uint target,out bool clean)
    {
        actor=target=0;clean=false;
        if(!Wire.TryVarint(b,ref at,out target)||target==0||!Wire.TryVarint(b,ref at,out var sw)||
            !Wire.TryVarint(b,ref at,out _)||!Wire.TryVarint(b,ref at,out actor)||actor==0||actor==target||at+6>b.Length)return false;
        var layout=(int)(sw&15);if(layout is not (4 or 6))return false;
        var skill=Wire.U32(b,at);at+=5; // u32 skill + u8 per-hit UID
        if(skill is <1 or >299999999||!Wire.TryVarint(b,ref at,out var type))return false;
        var block=at;
        foreach(var length in layout==4?new[]{8}:new[]{11,10})
        {
            var end=block+length;
            if(end>=b.Length||!Wire.TryVarint(b,ref end,out var scalar))continue;
            var afterScalar=end;if(!Wire.TryVarint(b,ref end,out var damage))continue;
            if(scalar==0){scalar=damage;afterScalar=end;if(!Wire.TryVarint(b,ref end,out damage))continue;}
            if(layout==6&&type==3&&scalar is >=1000 and <=5000000&&damage<=25){damage=scalar;scalar=0;end=afterScalar;}
            if(damage is <1 or >99999999||scalar!=0&&scalar is not (>=1000 and <=200000))continue;
            at=end;var tail=end;
            if(layout==4&&(!Wire.TryVarint(b,ref tail,out var field)||field is <1 or >25))return true;
            if((sw&0x20)!=0)
            {
                if(!Wire.TryVarint(b,ref tail,out var count)||count is <1 or >25)return true;
                long total=0;for(var i=0;i<count;i++){if(!Wire.TryVarint(b,ref tail,out var hit))return true;total+=hit;}
                if(total>=damage)return true;
            }
            clean=tail==b.Length||tail+1<b.Length&&b[tail]==1&&b[tail+1]==0;
            if(clean)at=tail;return true;
        }
        return false;
    }
    private static bool Count(ReadOnlySpan<byte> b,ref int at,int max,out int count)
    {
        var start=at;count=0;
        if(!Wire.TryVarint(b,ref at,out var raw)||at-start>3||raw>max||at-start>1&&raw<(1u<<(7*(at-start-1))))return false;
        count=(int)raw;return true;
    }
}
