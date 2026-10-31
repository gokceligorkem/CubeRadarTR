using CubeRadar.Core.Protocol;

namespace CubeRadar.Core.Radar;

/// <summary>Compatibility repairs for shipped templates; never copies another player's timers or alert history.</summary>
public static class BossProfile
{
    public static BossEvent Repair(BossEvent e,string server)
    {
        var baba=server.Split([' ','·','/','|',',',';','\t'],StringSplitOptions.RemoveEmptyEntries).Contains("BABA",StringComparer.OrdinalIgnoreCase);
        if(!baba)return e;
        var shipped=e.Source.StartsWith("AION2 Hub Global istemci verisi",StringComparison.Ordinal)||e.Source=="Oyun boss listesi ile otomatik eklendi; doğma saati oyundan alınır.";
        if(e.Name.Equals("Watcher Kaira",StringComparison.OrdinalIgnoreCase)&&e.RepeatMinutes==0&&
            (shipped||e.Id=="baba-abyss-watcher-kaira"&&e.OnceAt is not null))
        {
            // BABA phase was checked against the game at 15:00 Turkey time on 2026-10-30.
            // Global cadence is 180 min. Its game-list time is 0, so name/profile identify this repair.
            var anchor=e.OnceAt??DateTimeOffset.Parse("2026-10-30T12:00:00Z",System.Globalization.CultureInfo.InvariantCulture);
            var minute=TimeSpan.TicksPerMinute;anchor=new((anchor.UtcTicks+minute/2)/minute*minute,TimeSpan.Zero);
            return e with {OnceAt=anchor,RepeatMinutes=180,Times=[],Days=[],ClockConfirmed=true,
                Source="BABA oyun ekranında 30.10.2026 15:00 TR doğrulandı; Global istemci aralığı 180 dk. https://aion2hub.com/tools/world-bosses"};
        }
        if(shipped&&e.Name=="Abyss Siege Boss / Nahma"&&!e.ClockConfirmed&&e.Live is null)
            return e with {Name="Guardian Lord Nahma · Nokta 1",Times=[],Days=[],OnceAt=null,
                Source="Oyun listesi / Alt Reshanta · Nahma 1. doğma noktası; saat oyundan bekleniyor."};
        return e;
    }
    public static int AddMissing(List<BossEvent> events,FieldBossList list)
    {
        var added=0;
        foreach(var binding in BossCatalog.ForMap(list.MapId,list.Slots.Count))
        {
            if(events.Count>=200)break;
            if(events.Any(e=>BossCatalog.Match(e,list.MapId,list.Slots.Count)?.SlotId==binding.SlotId))continue;
            events.Add(new BossEvent {Name=binding.Name,Map=list.MapId==1110?"Altgard":"Chaotic Lower Reshanta",Enabled=false,ClockConfirmed=false,
                Location=list.MapId==20?"Abyss / Alt Reshanta · oyun haritasındaki boss noktası":"Altgard · oyun haritasındaki boss noktası",
                Source="Oyun boss listesi ile otomatik eklendi; doğma saati oyundan alınır.",
                MapUrl=list.MapId==1110?$"https://aion2.gg/map/World_D_A?marker=npc-{binding.NpcCode}":"https://aion2hub.com/maps?map=Abyss_Reshanta_A"});added++;
        }
        return added;
    }
}
