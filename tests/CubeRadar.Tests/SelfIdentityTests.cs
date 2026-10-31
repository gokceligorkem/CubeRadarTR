using System.Buffers.Binary;
using CubeRadar.Core.Radar;

namespace CubeRadar.Tests;

public class SelfIdentityTests
{
    [Fact]
    public void Nearby_self_buffs_cannot_replace_identity_from_own_records()
    {
        var engine=new RadarEngine(new RadarConfig());
        engine.OnFrame(1000,0x4637,Synth.Varint(100));
        engine.OnFrame(1100,0x4637,Synth.Varint(100));
        for(var i=0;i<80;i++)engine.OnFrame(1200+i,0x2A38,Buff(200));
        Assert.Equal(100u,engine.SelfId);
        Assert.Null(engine.SelfFix);
        Assert.Equal(SelfIdentitySource.OwnRecords,engine.Snapshot().IdentitySource);
    }

    [Fact]
    public void Explicit_self_info_is_authoritative_until_replaced_by_new_self_info()
    {
        var engine=new RadarEngine(new RadarConfig());
        engine.OnFrame(1000,0x3336,Synth.Varint(100));
        for(var i=0;i<50;i++)
        {
            engine.OnFrame(1100+i*2,0x4A36,Synth.Varint(200));
            engine.OnFrame(1101+i*2,0x2A38,Buff(200));
        }
        Assert.Equal(100u,engine.SelfId);
        Assert.Equal(SelfIdentitySource.SelfInfo,engine.Snapshot().IdentitySource);
        engine.OnFrame(2000,0x3336,Synth.Varint(300));
        Assert.Equal(300u,engine.SelfId);
    }

    [Fact]
    public void Repeated_own_records_can_replace_a_tentative_buff_identity()
    {
        var engine=new RadarEngine(new RadarConfig());
        for(var i=0;i<40;i++)engine.OnFrame(1000+i,0x2A38,Buff(200));
        Assert.Equal(200u,engine.SelfId);
        engine.OnFrame(2000,0x4637,Synth.Varint(100));
        engine.OnFrame(2100,0x4637,Synth.Varint(100));
        Assert.Equal(100u,engine.SelfId);
    }

    [Fact]
    public void A_single_conflicting_own_record_is_not_enough_to_switch_identity()
    {
        var engine=new RadarEngine(new RadarConfig());
        engine.OnFrame(1000,0x4637,Synth.Varint(100));
        engine.OnFrame(1100,0x4637,Synth.Varint(100));
        engine.OnFrame(1200,0x4637,Synth.Varint(200));
        Assert.Equal(100u,engine.SelfId);
        engine.OnFrame(1300,0x4637,Synth.Varint(200));
        Assert.Equal(200u,engine.SelfId);
    }

    private static byte[] Buff(uint id)
    {
        var xyz=new byte[12];
        BinaryPrimitives.WriteSingleLittleEndian(xyz,20000);
        BinaryPrimitives.WriteSingleLittleEndian(xyz.AsSpan(4),10000);
        return [..Synth.Varint(id),0,..Synth.Varint(id),1,0,..xyz];
    }
}
