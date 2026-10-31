using System.Text.Json;
using CubeRadar.Core.Radar;

namespace CubeRadar.Tr;

internal sealed record UiPreferences
{
    public int Volume {get;set;}=85;
    public int RangeM {get;set;}=150;
    public int RefreshMs {get;set;}=100;
    public bool AutoStart {get;set;}
    public bool TopMost {get;set;}
    public bool Sound {get;set;}=true;
    public bool Proximity {get;set;}
    public bool Outside {get;set;}
    public bool Movement {get;set;}
    public RadarOrientationMode RadarOrientation {get;set;}=RadarOrientationMode.CharacterUp;
    public bool SonarEnabled {get;set;}=true;
    public bool ShowCreatures {get;set;}=true;
    public bool MiniCompact {get;set;}
    public bool MiniClickThrough {get;set;}
    public bool MiniTopMost {get;set;}=true;
}

internal sealed class UiPreferencesStore
{
    private readonly string? _path;
    private bool _canSave=true;
    internal UiPreferences Value {get;private set;}=new();
    internal string? Error {get;private set;}
    internal UiPreferencesStore(bool preview)
    {
        if(preview)return;
        _path=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"CubeRadarTR","ui.json");
        try
        {
            if(!File.Exists(_path))return;
            if(new FileInfo(_path).Length>64000)throw new InvalidDataException("Arayüz ayarı çok büyük.");
            var value=JsonSerializer.Deserialize<UiPreferences>(File.ReadAllText(_path))??throw new InvalidDataException("Arayüz ayarı boş.");
            if(value.Volume is <0 or >100||value.RangeM is not (75 or 150 or 300)||value.RefreshMs is not (100 or 200 or 500)||
                value.RadarOrientation is not (RadarOrientationMode.CharacterUp or RadarOrientationMode.NorthUp))throw new InvalidDataException("Arayüz ayarı geçersiz.");
            Value=value;
        }
        catch(Exception ex)when(ex is IOException or UnauthorizedAccessException or JsonException or InvalidDataException)
        {_canSave=false;Error="Arayüz ayar dosyası okunamadı; eski dosya korunuyor.";}
    }
    internal void Save()
    {
        if(_path is null||!_canSave)return;
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_path)!);File.WriteAllText(_path+".tmp",JsonSerializer.Serialize(Value));File.Move(_path+".tmp",_path,true);Error=null;
        }
        catch(Exception ex)when(ex is IOException or UnauthorizedAccessException){Error="Arayüz tercihleri kaydedilemedi.";}
    }
}
