using System.Globalization;

namespace CubeRadar.Core.Radar;

public sealed record BossEvent
{
    public string Id {get;init;}=Guid.NewGuid().ToString("N");
    public string Name {get;init;}="";
    public string Map {get;init;}="";
    public string Location {get;init;}="";
    public string Source {get;init;}="Elle girilen saat";
    public string MapUrl {get;init;}="";
    public string ZoneId {get;init;}="Turkey Standard Time";
    public bool Enabled {get;init;}=true;
    public bool ClockConfirmed {get;init;}=true;
    public List<DayOfWeek> Days {get;init;}=[];
    public List<string> Times {get;init;}=[];
    public DateTimeOffset? OnceAt {get;init;}
    public int DurationMinutes {get;init;}=30;
    public List<int> AlertMinutes {get;init;}=[10,5];
    public BossLiveState? Live {get;init;}
    public int RepeatMinutes {get;init;}
}

public sealed record BossOccurrence(BossEvent Event,DateTimeOffset Start)
{
    public DateTimeOffset End=>Start.AddMinutes(Event.DurationMinutes);
    public string Key=>$"{Event.Id}:{Start.UtcTicks}";
}

public static class BossClock
{
    public static string? Validate(BossEvent e)
    {
        if(string.IsNullOrWhiteSpace(e.Id)||string.IsNullOrWhiteSpace(e.Name)||string.IsNullOrWhiteSpace(e.Map))return "Boss adı ve harita gerekli.";
        if(string.IsNullOrWhiteSpace(e.ZoneId))return "Saat dilimi gerekli.";
        if(e.Location is null||e.Source is null||e.MapUrl is null||e.Times is null||e.Days is null||e.AlertMinutes is null)return "Eksik takvim alanı.";
        if(e.Name.Length>120||e.Map.Length>120||e.Location.Length>300||e.Source.Length>400)return "Metin alanları çok uzun.";
        if(e.DurationMinutes is <1 or >240||e.AlertMinutes.Any(m=>m is <1 or >120))return "Uyarı veya etkinlik süresi geçersiz.";
        if(e.OnceAt is {} once&&once> DateTimeOffset.MaxValue.AddMinutes(-e.DurationMinutes))return "Tarih çok uzak.";
        if(e.RepeatMinutes is <0 or >10080||e.RepeatMinutes>0&&e.OnceAt is null)return "Tekrar aralığı geçersiz.";
        if(e.Live is {} live&&(live.MapId is <1 or >9_999_999||live.SlotId<=live.MapId*100L||live.SlotId>=live.MapId*100L+100||live.RespawnMinutes is <0 or >2880||live.SpawnAt>DateTimeOffset.MaxValue.AddMinutes(-e.DurationMinutes)))return "Canlı boss kaydı geçersiz.";
        try {TimeZoneInfo.FindSystemTimeZoneById(e.ZoneId);}catch(Exception ex)when(ex is TimeZoneNotFoundException or InvalidTimeZoneException){return "Saat dilimi geçersiz.";}
        if(e.Times.Count>48||e.Days.Count>7||e.Days.Any(d=>(int)d is <0 or >6)||e.Times.Any(t=>!TimeOnly.TryParseExact(t,"HH:mm",CultureInfo.InvariantCulture,DateTimeStyles.None,out _)))return "Saat biçimi HH:mm olmalı.";
        if(e.MapUrl.Length>1000||e.MapUrl.Length>0&&(!Uri.TryCreate(e.MapUrl,UriKind.Absolute,out var uri)||uri.Scheme!="https"))return "Harita bağlantısı https olmalı.";
        return null;
    }

    public static BossOccurrence? CurrentOrNext(BossEvent e,DateTimeOffset now)
    {
        if(Validate(e) is not null||!e.ClockConfirmed)return null;
        if(e.RepeatMinutes>0&&e.OnceAt is {} anchor)
        {
            if(e.Live?.SpawnAt is {} listed&&listed>=now.AddMinutes(-1))return new(e,listed);
            // A fixed cadence has a next occurrence even when the game leaves its time empty.
            // Retain only the existing one-minute start-alert window, not the 30-minute event duration.
            var after=now.AddMinutes(-1);
            if(e.Live?.LastKillAt is {} kill&&kill>after)after=kill;
            var n=Math.Max(0,Math.Floor((after-anchor).TotalMinutes/e.RepeatMinutes)+1);
            return new(e,anchor.AddMinutes(n*e.RepeatMinutes));
        }
        if(e.Live is {Alive:true})return null;
        if(e.Live?.SpawnAt is {} back)return back.AddMinutes(e.DurationMinutes)>now?new(e,back):null;
        if(e.Live is not null)return null;
        if(e.OnceAt is {} once)return once.AddMinutes(e.DurationMinutes)>now?new(e,once):null;
        if(e.Times.Count==0)return null;
        var zone=TimeZoneInfo.FindSystemTimeZoneById(e.ZoneId);
        var today=TimeZoneInfo.ConvertTime(now,zone).Date;
        var starts=new List<DateTimeOffset>();
        for(var day=-1;day<=8;day++)
        {
            var date=today.AddDays(day);
            if(e.Days.Count>0&&!e.Days.Contains(date.DayOfWeek))continue;
            foreach(var text in e.Times)
            {
                var time=TimeOnly.ParseExact(text,"HH:mm",CultureInfo.InvariantCulture);
                var local=DateTime.SpecifyKind(date.Add(time.ToTimeSpan()),DateTimeKind.Unspecified);
                if(zone.IsInvalidTime(local))continue;
                var offset=zone.IsAmbiguousTime(local)?zone.GetAmbiguousTimeOffsets(local).Max():zone.GetUtcOffset(local);
                var start=new DateTimeOffset(local,offset).ToUniversalTime();
                if(start.AddMinutes(e.DurationMinutes)>now)starts.Add(start);
            }
        }
        return starts.Count==0?null:new(e,starts.Min());
    }

    public static DateTimeOffset FromLocal(DateTime value,string zoneId)
    {
        var zone=TimeZoneInfo.FindSystemTimeZoneById(zoneId);var local=DateTime.SpecifyKind(value,DateTimeKind.Unspecified);
        if(zone.IsInvalidTime(local))throw new ArgumentException("Bu saat yaz saati geçişinde yok; başka saat seç.");
        var offset=zone.IsAmbiguousTime(local)?zone.GetAmbiguousTimeOffsets(local).Max():zone.GetUtcOffset(local);
        return new DateTimeOffset(local,offset).ToUniversalTime();
    }
}

/// <summary>One alert per occurrence and threshold. Consumes muted thresholds to avoid catch-up noise.</summary>
public sealed class BossAlertGate(HashSet<string> delivered)
{
    public bool Take(BossOccurrence occurrence,DateTimeOffset now,bool sound)
    {
        var left=(occurrence.Start-now).TotalMinutes;
        if(left>120||now>=occurrence.End||!occurrence.Event.Enabled)return false;
        var thresholds=occurrence.Event.AlertMinutes.Where(m=>left<=m).Append(0).Where(m=>m!=0||left<=0).Distinct().ToArray();
        if(thresholds.Length==0)return false;
        // Consume earlier thresholds too when entering an already imminent event.
        var changed=false;
        foreach(var m in thresholds)changed|=delivered.Add($"{occurrence.Key}:{m}");
        return sound&&changed&&left>=-1;
    }
}
