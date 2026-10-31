using CubeRadar.Core.Protocol;
using CubeRadar.Core.Radar;

namespace CubeRadar.Tests;

public class BossProfileTests
{
    private static BossEvent LegacyKaira()=>new() {Id="friend-random-record-id",Name="Watcher Kaira",Map="Chaotic Lower Reshanta",ZoneId="W. Europe Standard Time",
        Source="AION2 Hub Global istemci verisi, 2026-09-19; sunucu saatini oyunda doğrula.",Times=["01:00","04:00","07:00","10:00","13:00","16:00","19:00","22:00"],
        Enabled=false,ClockConfirmed=true,Live=new(20,2002,DateTimeOffset.Parse("2026-10-30T12:40:00Z"),false,null)};
    [Fact]
    public void Fresh_install_kaira_with_random_id_and_empty_packet_time_gets_the_confirmed_baba_schedule()
    {
        var old=LegacyKaira();var fixedEvent=BossProfile.Repair(old,"EU · BABA · Asmodian");
        Assert.Equal(old.Id,fixedEvent.Id);Assert.False(fixedEvent.Enabled);Assert.Equal(old.Live,fixedEvent.Live);
        Assert.Equal(180,fixedEvent.RepeatMinutes);Assert.Empty(fixedEvent.Times);Assert.Empty(fixedEvent.Days);
        Assert.Equal(DateTimeOffset.Parse("2026-10-30T15:00:00Z"),BossClock.CurrentOrNext(fixedEvent,DateTimeOffset.Parse("2026-10-30T12:50:00Z"))!.Start);
        Assert.Equal(fixedEvent,BossProfile.Repair(fixedEvent,"EU · BABA · Asmodian"));
    }
    [Fact]
    public void Other_servers_and_custom_kaira_schedules_are_preserved()
    {
        var legacy=LegacyKaira();Assert.Equal(legacy,BossProfile.Repair(legacy,"EU · Different"));
        var custom=legacy with {Source="Kullanıcı tarafından girildi",Times=["19:45"]};Assert.Equal(custom,BossProfile.Repair(custom,"EU · BABA"));
        var confirmed=legacy with {OnceAt=DateTimeOffset.Parse("2026-10-30T13:00:00Z"),RepeatMinutes=240};Assert.Equal(confirmed,BossProfile.Repair(confirmed,"EU · BABA"));
    }
    [Fact]
    public void Legacy_nahma_placeholder_becomes_a_real_slot_and_missing_game_slots_are_added_without_more_alarms()
    {
        var legacy=new BossEvent {Id="old-nahma",Name="Abyss Siege Boss / Nahma",Map="Chaotic Lower Reshanta",ClockConfirmed=false,Source="AION2 Hub Global istemci verisi, 2026-09-19",Enabled=true,Times=["21:00"]};
        var repaired=BossProfile.Repair(legacy,"EU · BABA");Assert.Equal("Guardian Lord Nahma · Nokta 1",repaired.Name);Assert.Equal(legacy.Id,repaired.Id);Assert.True(repaired.Enabled);Assert.Empty(repaired.Times);
        var entries=new List<BossEvent> {repaired,LegacyKaira()};
        var at=DateTimeOffset.Parse("2026-10-30T12:40:00Z");var list=new FieldBossList(20,at.ToUnixTimeMilliseconds(),Enumerable.Range(1,8).Select(i=>new FieldBossSlot(2000+i,false,0)).ToArray());
        Assert.Equal(6,BossProfile.AddMissing(entries,list));Assert.Equal(8,entries.Count);Assert.True(entries.Single(e=>e.Id==legacy.Id).Enabled);
        Assert.All(entries.Where(e=>e.Id!=legacy.Id&&e.Id!="friend-random-record-id"),e=>Assert.False(e.Enabled));
        Assert.Equal(0,BossProfile.AddMissing(entries,list));Assert.Equal(8,entries.Select(e=>e.Id).Distinct().Count());
    }
}
