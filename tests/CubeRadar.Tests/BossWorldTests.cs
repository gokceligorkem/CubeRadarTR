using CubeRadar.Core.Protocol;
using CubeRadar.Core.Radar;

namespace CubeRadar.Tests;

public class BossWorldTests
{
    private static byte[] Var(uint value)
    {
        var bytes=new List<byte>();do {var b=(byte)(value&127);value>>=7;bytes.Add((byte)(b|(value>0?128:0)));}while(value>0);return bytes.ToArray();
    }
    private static byte[] Spawn(uint entity,int code)=>[..Var(entity),0x0c,0x20,0,(byte)code,(byte)(code>>8),(byte)(code>>16),0,0,2,..new byte[12]];
    [Fact]
    public void Only_a_known_boss_real_death_restarts_once_and_corpse_or_despawn_is_ignored()
    {
        var tracker=new BossWorldTracker();var spawn=Spawn(12345,2400800);
        Assert.Null(tracker.OnFrame(0x4136,spawn,1000,1110));
        Assert.Null(tracker.OnFrame(0x4236,[..Var(12345),0,1],2000,1110));
        Assert.Null(tracker.OnFrame(0x3536,[..Var(12345),4],2000,1110));
        Assert.Equal(new BossKillObservation(1110,111021,3000),tracker.OnFrame(0x4236,[..Var(12345),0,3],3000,1110));
        Assert.Null(tracker.OnFrame(0x4236,[..Var(12345),0,3],3100,1110));
        Assert.Null(tracker.OnFrame(0x4236,[..Var(5555),0,3],3200,1110));
        tracker.OnFrame(0x4136,spawn,600000,1110);
        Assert.Equal(new BossKillObservation(1110,111021,601000),tracker.OnFrame(0x4236,[..Var(12345),0,3],601000,1110));
    }
    [Fact]
    public void Map_changes_and_instance_reuse_cannot_report_an_old_world_boss_kill()
    {
        var tracker=new BossWorldTracker();tracker.OnFrame(0x4136,Spawn(12345,2400800),1000,1110);
        Assert.Null(tracker.OnFrame(0x4236,[..Var(12345),0,3],2000,600001));
        tracker.OnFrame(0x4136,Spawn(12345,2400800),3000,600001);
        Assert.Null(tracker.OnFrame(0x4236,[..Var(12345),0,3],4000,600001));
    }
    [Fact]
    public void The_abyss_list_has_its_own_order_and_an_unknown_slot_count_does_not_guess_names()
    {
        Assert.Equal("Watcher Kaira",BossCatalog.ForMap(20,8).Single(b=>b.SlotId==2002).Name);
        Assert.Equal("Guardian Lord Nahma · Nokta 1",BossCatalog.ForMap(20,8).Single(b=>b.SlotId==2003).Name);
        Assert.Empty(BossCatalog.ForMap(20,9));Assert.Empty(BossCatalog.ForMap(1110,23));
        Assert.Null(BossCatalog.ForNpc(2600084));
    }
}
