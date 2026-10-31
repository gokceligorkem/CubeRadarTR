using CubeRadar.Core.Protocol;

namespace CubeRadar.Core.Radar;

public sealed record BossLiveState(int MapId,int SlotId,DateTimeOffset ObservedAt,bool Alive,DateTimeOffset? SpawnAt,
    DateTimeOffset? LastKillAt=null,int RespawnMinutes=0,bool Estimated=false);

/// <summary>Game-list times are authoritative; kill + a measured interval remains explicitly an estimate.</summary>
public static class BossLiveSync
{
    public static BossEvent Listed(BossEvent e,FieldBossList list,FieldBossSlot slot)
    {
        var at=DateTimeOffset.FromUnixTimeMilliseconds(list.ObservedMs);var old=e.Live;
        if(old is not null&&old.ObservedAt>at)return e;
        DateTimeOffset? back=!slot.Alive&&slot.AtMs>0?DateTimeOffset.FromUnixTimeMilliseconds(slot.AtMs):null;
        var kill=old?.LastKillAt;var interval=old?.RespawnMinutes??0;
        if(old is {Alive:true}&&at-old.ObservedAt<TimeSpan.FromSeconds(5)&&!slot.Alive)kill=at;
        if(!slot.Alive&&back is {} next&&kill is {} died&&at>=died&&at-died<TimeSpan.FromMinutes(30))
        {
            var minutes=(int)Math.Round((next-died).TotalMinutes);
            // Only learn minute-aligned intervals near a directly observed death, never from an old schedule.
            if(minutes is >=1 and <=2880&&Math.Abs((next-died).TotalSeconds-minutes*60)<5)interval=minutes;
        }
        return e with {ClockConfirmed=true,Live=new(list.MapId,slot.SlotId,at,slot.Alive,back,kill,interval)};
    }
    public static BossEvent Killed(BossEvent e,long observedMs)
    {
        if(e.Live is not {} old)return e;
        var at=DateTimeOffset.FromUnixTimeMilliseconds(observedMs);
        if(at<=old.ObservedAt||old.LastKillAt is {} last&&at-last<TimeSpan.FromMinutes(2))return e;
        return e with {Live=old with {ObservedAt=at,Alive=false,LastKillAt=at,
            SpawnAt=old.RespawnMinutes>0?at.AddMinutes(old.RespawnMinutes):null,Estimated=true}};
    }
}
