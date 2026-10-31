using System.Text.Json;
using CubeRadar.Core.Protocol;

namespace CubeRadar.Tests;

public class FieldBossTests
{
    [Fact]
    public void Live_shujakan_from_the_game_list_clears_the_overdue_timer_without_claiming_a_new_kill()
    {
        using var json=JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"fixtures","bosses","baba-altgard-shujakan-alive.json")));
        var root=json.RootElement;
        Assert.True(FieldBossMessages.TryParse(Convert.FromHexString(root.GetProperty("hex").GetString()!),root.GetProperty("t").GetInt64(),out var list));
        var slot=list.Slots.Single(s=>s.SlotId==111017);Assert.True(slot.Alive);
        var old=new CubeRadar.Core.Radar.BossEvent {Name="Veteran Shujakan",Map="Altgard",OnceAt=DateTimeOffset.FromUnixTimeMilliseconds(slot.AtMs)};
        var updated=CubeRadar.Core.Radar.BossLiveSync.Listed(old,list,slot);
        Assert.True(updated.Live!.Alive);Assert.Null(updated.Live.SpawnAt);Assert.Null(updated.Live.LastKillAt);
        Assert.Null(CubeRadar.Core.Radar.BossClock.CurrentOrNext(updated,DateTimeOffset.FromUnixTimeMilliseconds(list.ObservedMs)));
    }
    [Fact]
    public void Baba_live_altgard_packet_reads_all_slots_and_new_gartua_lagta_times()
    {
        using var json=JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"fixtures","bosses","baba-altgard.json")));
        var root=json.RootElement;
        Assert.True(FieldBossMessages.TryParse(Convert.FromHexString(root.GetProperty("hex").GetString()!),root.GetProperty("t").GetInt64(),out var list));
        Assert.Equal(1110,list.MapId);Assert.Equal(24,list.Slots.Count);
        Assert.Equal(24,list.Slots.Select(s=>s.SlotId).Distinct().Count());
        Assert.False(list.Slots.Single(s=>s.SlotId==111021).Alive);
        Assert.Equal(DateTimeOffset.Parse("2026-10-30T22:29:56.024Z").ToUnixTimeMilliseconds(),list.Slots.Single(s=>s.SlotId==111021).AtMs);
        Assert.True(list.Slots.Single(s=>s.SlotId==111002).Alive);
    }

    [Fact]
    public void Baba_abyss_keeps_the_empty_time_unknown_instead_of_inventing_a_respawn()
    {
        using var json=JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"fixtures","bosses","baba-map-20.json")));
        var root=json.RootElement;
        Assert.True(FieldBossMessages.TryParse(Convert.FromHexString(root.GetProperty("hex").GetString()!),root.GetProperty("t").GetInt64(),out var list));
        Assert.Equal(20,list.MapId);Assert.Equal(8,list.Slots.Count);
        Assert.Equal(0,list.Slots.Single(s=>s.SlotId==2002).AtMs);
        Assert.Equal(DateTimeOffset.Parse("2026-10-31T20:40:00Z").ToUnixTimeMilliseconds(),list.Slots.Single(s=>s.SlotId==2008).AtMs);
    }

    [Fact]
    public void Truncated_and_duplicate_slots_cannot_replace_a_good_list()
    {
        using var json=JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"fixtures","bosses","baba-altgard.json")));
        var root=json.RootElement;var body=Convert.FromHexString(root.GetProperty("hex").GetString()!);var t=root.GetProperty("t").GetInt64();
        Assert.False(FieldBossMessages.TryParse(body[..^10],t,out _));
        body[6]=25;Assert.False(FieldBossMessages.TryParse(body,t,out _));
    }
}
