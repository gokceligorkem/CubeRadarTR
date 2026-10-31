using System.Text.Json;
using CubeRadar.Core.Radar;

namespace CubeRadar.Tr;

internal sealed record CompanionSettings
{
    public string DisplayZone {get;set;}="Turkey Standard Time";
    public string ServerName {get;set;}="EU · BABA · Asmodian";
    public List<BossEvent> Events {get;set;}=BossStore.BabaPresets();
    public HashSet<string> Delivered {get;set;}=[];
    public bool BossSound {get;set;}=true;
}

internal sealed class BossStore
{
    private readonly string? _path;
    private bool _canSave=true;
    public CompanionSettings Settings {get;private set;}=new();
    public string? Error {get;private set;}
    public BossStore(bool preview=false)
    {
        if(preview){Settings.Events=[];return;}
        _path=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"CubeRadarTR","companion.json");
        try
        {
            if(!File.Exists(_path))return;
            if(new FileInfo(_path).Length>1024*1024)throw new InvalidDataException("Ayar dosyası çok büyük.");
            var data=JsonSerializer.Deserialize<CompanionSettings>(File.ReadAllText(_path))??throw new InvalidDataException("Ayar dosyası boş.");
            if(data.Events is null||data.Delivered is null||data.Delivered.Any(k=>k is null||k.Length>180)||data.Events.Count>200||data.Events.Any(e=>e is null||BossClock.Validate(e) is not null)||data.Events.Select(e=>e.Id).Distinct().Count()!=data.Events.Count)
                throw new InvalidDataException("Takvim kayıtları geçersiz.");
            TimeZoneInfo.FindSystemTimeZoneById(data.DisplayZone);Settings=data;
        }
        catch(Exception e)when(e is IOException or InvalidDataException or UnauthorizedAccessException or JsonException or ArgumentException or TimeZoneNotFoundException or InvalidTimeZoneException)
        {_canSave=false;Error="Ayarlar okunamadı; dosya korunuyor: "+e.Message;}
    }
    public bool Save()
    {
        if(_path is null)return true;
        if(!_canSave)return false;
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
            // Retain recent occurrence keys; cap long-running local history.
            if(Settings.Delivered.Count>6000)Settings.Delivered=Settings.Delivered.OrderByDescending(k=>k.Split(':').ElementAtOrDefault(1),StringComparer.Ordinal).Take(4000).ToHashSet();
            File.WriteAllText(_path+".tmp",JsonSerializer.Serialize(Settings,new JsonSerializerOptions {WriteIndented=true}));
            File.Move(_path+".tmp",_path,true);Error=null;return true;
        }
        catch(Exception e)when(e is IOException or UnauthorizedAccessException){Error="Takvim kaydedilemedi: "+e.Message;return false;}
    }
    public static List<BossEvent> GlobalPresets(string zoneId)=>
    [
        Preset("Watcher Kaira",Enumerable.Range(0,8).Select(i=>$"{1+i*3:00}:00").ToList(),[],"2a92dd46-1fdc-8862-a624-ab8861fa7942",zoneId),
        Preset("Executor Argo",["21:30"],[DayOfWeek.Monday,DayOfWeek.Thursday,DayOfWeek.Saturday],"3ca777fc-3520-915b-9756-7a102a7170c9",zoneId),
        Preset("Executor Kaira",["21:30"],[DayOfWeek.Monday,DayOfWeek.Thursday,DayOfWeek.Saturday],"70e2c709-4c6e-c3c0-aa90-c33737270e90",zoneId),
        Preset("Executor Tamasa",["21:30"],[DayOfWeek.Monday,DayOfWeek.Thursday,DayOfWeek.Saturday],"4315f7bd-1d13-94fb-3f39-751c61708d5e",zoneId),
        Preset("Abyss Siege Boss / Nahma",["21:00"],[DayOfWeek.Friday,DayOfWeek.Sunday],"",zoneId)
    ];
    public static List<BossEvent> BabaPresets()
    {
        var events=GlobalPresets("W. Europe Standard Time");
        string[] names=["Silent Dartan","High Commander Lagta","Soul Ruler Kashapa"];
        string[] markers=["M_D1_FieldBoss_BeritraD_01_001_1@841","M_D1_FieldBoss_Lagta_02_1@3714","M_D1_FieldBoss_Kashapa_02_1@7451"];
        for(var i=0;i<names.Length;i++)events.Add(new BossEvent {Name=names[i],Map="Altgard",Location="Harita bağlantısındaki işaret; kalan süreyi oyundan gir.",ClockConfirmed=false,
            Source="AION2 Hub harita verisi; bu boss için doğrulanmış sabit saat yok.",MapUrl="https://aion2hub.com/maps?map=World_D_A&marker="+Uri.EscapeDataString(markers[i])});
        return events;
    }
    private static BossEvent Preset(string name,List<string> times,List<DayOfWeek> days,string marker,string zone)=>new()
    {
        Name=name,Map="Chaotic Lower Reshanta",Location="Abyss / Alt Reshanta · harita işaretine bak",
        Times=times,Days=days,ZoneId=zone,ClockConfirmed=false,
        Source="AION2 Hub Global istemci verisi, 2026-09-19; sunucu saatini oyunda doğrula. https://aion2hub.com/tools/world-bosses",
        MapUrl="https://aion2hub.com/maps?map=Abyss_Reshanta_A"+(marker.Length>0?"&marker="+marker:"")
    };
}

internal sealed record ClockZone(string Id,string Label)
{
    public override string ToString()=>Label;
    public static ClockZone[] Choices=>[
        new("Turkey Standard Time","Türkiye · UTC+3"),new("Tokyo Standard Time","UTC+9 · Kore / Japonya"),
        new("Taipei Standard Time","Tayvan · UTC+8"),new("W. Europe Standard Time","Avrupa · CET / CEST"),
        new("Eastern Standard Time","Amerika Doğu · ET"),new("Pacific Standard Time","Amerika Batı · PT"),new("UTC","UTC")];
    public static ComboBox Picker(string selected)
    {
        var combo=new ComboBox {DropDownStyle=ComboBoxStyle.DropDownList,Dock=DockStyle.Fill};
        foreach(var zone in Choices)combo.Items.Add(zone);
        combo.SelectedItem=combo.Items.Cast<ClockZone>().FirstOrDefault(z=>z.Id==selected)??combo.Items[0];return combo;
    }
}
