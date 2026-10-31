using System.Net;
using CubeRadar.Core.Capture;
using CubeRadar.Core.Radar;

namespace CubeRadar.Tests;

public class CubeCompanionTests
{
    [Fact]
    public void Empty_game_connection_list_does_not_create_a_broad_capture_filter()
    {
        Assert.Null(LiveSniffer.StrictInboundFilter([]));
    }

    [Fact]
    public void Capture_is_scoped_to_exact_inbound_game_connections()
    {
        var filter = LiveSniffer.StrictInboundFilter([
            new GameConnection(IPAddress.Parse("192.0.2.10"), 55000, IPAddress.Parse("198.51.100.20"), 13328)
        ]);
        Assert.Equal("tcp and ((src host 198.51.100.20 and src port 13328 and dst host 192.0.2.10 and dst port 55000))", filter);
    }

    [Fact]
    public void Auxiliary_connection_churn_cannot_reapply_the_world_capture_filter()
    {
        var world=new GameConnection(IPAddress.Parse("192.0.2.10"),55000,IPAddress.Parse("198.51.100.20"),13328);
        var auxiliary=new GameConnection(world.LocalIp,55001,world.RemoteIp,63528);
        var replacement=auxiliary with {LocalPort=55002,RemotePort=63529};
        var expected=LiveSniffer.StrictInboundFilter([world]);
        Assert.Equal(expected,LiveSniffer.StrictInboundFilter([world,auxiliary]));
        Assert.Equal(expected,LiveSniffer.StrictInboundFilter([replacement,world]));
        Assert.Equal(expected,LiveSniffer.StrictInboundFilter([world]));
    }

    [Fact]
    public void All_owned_world_sockets_remain_scoped_when_auxiliary_sockets_are_present()
    {
        var local=IPAddress.Parse("192.0.2.10");var remote=IPAddress.Parse("198.51.100.20");
        var first=new GameConnection(local,55000,remote,13328);var second=first with {LocalPort=55001};
        var actual=LiveSniffer.StrictInboundFilter([first,second,first,new(local,55002,remote,63528)]);
        Assert.Equal(LiveSniffer.StrictInboundFilter([first,second]),actual);
        Assert.Contains("dst port 55000",actual);Assert.Contains("dst port 55001",actual);Assert.DoesNotContain("63528",actual);
    }

    [Fact]
    public void A_proxy_without_the_standard_world_port_retains_exact_owned_socket_capture()
    {
        var first=new GameConnection(IPAddress.Loopback,55000,IPAddress.Loopback,15000);var second=first with {LocalPort=55001,RemotePort=15001};
        var actual=LiveSniffer.StrictInboundFilter([first,second]);
        Assert.Contains("src port 15000",actual);Assert.Contains("src port 15001",actual);
        Assert.Contains("dst port 55000",actual);Assert.Contains("dst port 55001",actual);
        Assert.DoesNotContain("13328",actual);
    }

    [Fact]
    public void Old_packets_never_appear_as_current_nearby_cubes()
    {
        var state = CubeCompanionView.Build(Snapshot(), 30_000);
        Assert.False(state.LiveTraffic);
        Assert.Empty(state.Cubes);
        Assert.Null(state.Nearest);
    }

    [Fact]
    public void Stale_self_position_does_not_produce_an_exact_distance()
    {
        var snapshot = Snapshot() with { Self = new FixView(0,0,0,1000,9000,"test") };
        var state = CubeCompanionView.Build(snapshot, 10_001);
        Assert.True(state.LiveTraffic);
        Assert.Single(state.Cubes);
        Assert.False(state.FreshPosition);
        Assert.Null(state.Nearest);
    }

    [Fact]
    public void Opened_or_out_of_view_cubes_do_not_count_as_nearby()
    {
        var snapshot = Snapshot();
        snapshot.Cubes.Add(Cube("Opened"));
        snapshot.Cubes.Add(Cube("OutOfView"));
        var state = CubeCompanionView.Build(snapshot, 10_001);
        Assert.Single(state.Cubes);
        Assert.NotNull(state.Nearest);
    }

    [Theory]
    [InlineData(SelfIdentitySource.BuffConsensus)]
    [InlineData(SelfIdentitySource.Unknown)]
    public void A_tentative_character_identity_cannot_produce_exact_geometry(SelfIdentitySource source)
    {
        var state=CubeCompanionView.Build(Snapshot() with {IdentitySource=source},10_001);
        Assert.True(state.LiveTraffic);
        Assert.Single(state.Cubes);
        Assert.False(state.FreshPosition);
        Assert.Null(state.Nearest);
    }

    private static RadarSnapshot Snapshot() => new(10_000, 1, 1010, "Verteron",
        new FixView(0,0,0,10_000,0,"test"), null, [], [Cube("Visible")], [],
        new OrientationView(0,-1,1,0,"-y"), 5, 5) {IdentitySource=SelfIdentitySource.SelfInfo};

    private static CubeView Cube(string status) => new(1,1200039,"Verteron",null,1000,0,0,0,0,0,
        status,0,null,false,1010,10,10,90,"O",0,null,null);
}
