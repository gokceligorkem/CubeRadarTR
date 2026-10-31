using System.Text.Json;
using CubeRadar.Core.Pets;

namespace CubeRadar.Tr;

internal sealed class PetPreferences
{
    public Dictionary<int,int> Goals {get;set;}=[];
    public HashSet<int> ExcludedPets {get;set;}=[];
    public bool HideCompleted {get;set;}=true;
    public int View {get;set;}
    public int? MiniLeft {get;set;}
    public int? MiniTop {get;set;}
    public int MiniWidth {get;set;}=320;
    public int MiniHeight {get;set;}=140;
    public bool MiniTopMost {get;set;}=true;
    public int MiniOpacity {get;set;}=85;
    public bool MiniAutoFit {get;set;}=true;
    public int MiniLayoutVersion {get;set;}
}
internal sealed class PetPreferencesStore
{
    readonly string? _path;bool _canSave=true;
    internal PetPreferences Value {get;private set;}=new();
    internal string? Error {get;private set;}
    internal PetPreferencesStore(bool preview)
    {
        if(preview)return;
        _path=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"CubeRadarTR","pets.json");
        try
        {
            if(!File.Exists(_path)){Value.MiniLayoutVersion=1;return;}
            if(new FileInfo(_path).Length>64000)throw new InvalidDataException();
            var value=JsonSerializer.Deserialize<PetPreferences>(File.ReadAllText(_path))??throw new InvalidDataException();
            if(value.Goals is null||value.Goals.Count>PetCatalog.Default.Pets.Count||value.ExcludedPets is null||value.ExcludedPets.Count>PetCatalog.Default.Pets.Count||value.ExcludedPets.Any(id=>!PetCatalog.Default.Pets.ContainsKey(id))||value.View is <0 or >3||value.Goals.Any(g=>!PetCatalog.Default.Pets.ContainsKey(g.Key)||g.Value is <1 or >3)||
                value.MiniWidth is <240 or >1000||value.MiniHeight is <80 or >1200||value.MiniLeft is <-32000 or >32000||value.MiniTop is <-32000 or >32000||value.MiniOpacity is <40 or >100||value.MiniLayoutVersion is <0 or >1)throw new InvalidDataException();
            Value=value;if(MigrateMiniLayout(value))Save();
        }
        catch(Exception e)when(e is IOException or UnauthorizedAccessException or JsonException or InvalidDataException)
        {_canSave=false;Error="Pet tercihleri okunamadı; eski dosya korunuyor.";}
    }
    internal static bool MigrateMiniLayout(PetPreferences value)
    {
        if(value.MiniLayoutVersion!=0)return false;
        value.MiniWidth=Math.Min(value.MiniWidth,340);value.MiniHeight=Math.Min(value.MiniHeight,140);value.MiniLayoutVersion=1;return true;
    }
    internal void Save()
    {
        if(_path is null||!_canSave)return;
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_path)!);File.WriteAllText(_path+".tmp",JsonSerializer.Serialize(Value));File.Move(_path+".tmp",_path,true);Error=null;
        }
        catch(Exception e)when(e is IOException or UnauthorizedAccessException){Error="Pet tercihleri kaydedilemedi.";}
    }
}
