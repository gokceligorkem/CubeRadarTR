using System.Buffers.Binary;
using CubeRadar.Core.Protocol;

namespace CubeRadar.Tests;

public class HeadingProbeTests
{
    [Fact]
    public void Map_load_candidate_is_unverified_and_requires_complete_finite_field()
    {
        var body=new byte[32];
        BinaryPrimitives.WriteSingleLittleEndian(body.AsSpan(28),179.5f);
        Assert.Null(HeadingProbe.Read(1,0x2136,body.AsSpan(0,31),1));
        var candidate=HeadingProbe.Read(1,0x2136,body,1)!;
        Assert.Equal(179.5,candidate.Value);
        Assert.Equal(28,candidate.Offset);
        Assert.False(candidate.Verified);
        BinaryPrimitives.WriteSingleLittleEndian(body.AsSpan(28),float.NaN);
        Assert.Null(HeadingProbe.Read(1,0x2136,body,1));
    }

    [Fact]
    public void Other_actor_or_missing_identity_never_becomes_own_heading_candidate()
    {
        byte[] body=[2,0,0,0,0,0,0,2,1,0,0,180,66];
        Assert.Null(HeadingProbe.Read(1,0x0238,body,1));
        Assert.Null(HeadingProbe.Read(1,0x2136,new byte[32],null));
        body[0]=1;
        Assert.NotNull(HeadingProbe.Read(1,0x0238,body,1));
    }
}
