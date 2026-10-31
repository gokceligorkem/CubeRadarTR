using System.Text.Json;

namespace CubeRadar.Core.Pets;

public sealed record PetDefinition(int Id,string Name,string Family);
public sealed record PetSource(int NpcId,int PetId,string Name);
public sealed class PetCatalog
{
    public IReadOnlyDictionary<int,PetDefinition> Pets {get;}
    public IReadOnlyList<PetSource> Sources {get;}
    public IReadOnlyDictionary<int,PetSource> Npcs {get;}
    public static PetCatalog Default {get;}=Load();
    private PetCatalog(PetDefinition[] pets,PetSource[] sources)
    {Pets=pets.ToDictionary(p=>p.Id);Sources=sources;Npcs=sources.ToDictionary(p=>p.NpcId);}
    private static PetCatalog Load()
    {
        using var stream=typeof(PetCatalog).Assembly.GetManifestResourceStream("pet-catalog.json")!;
        using var doc=JsonDocument.Parse(stream);
        return new(doc.RootElement.GetProperty("Pets").Deserialize<PetDefinition[]>()!,doc.RootElement.GetProperty("Sources").Deserialize<PetSource[]>()!);
    }
    public static int Cost(int rank)=>rank switch {0=>5,1=>25,2=>75,_=>0};
    public static int CostBetween(int rank,int goal)=>Enumerable.Range(rank,Math.Max(0,goal-rank)).Sum(Cost);
}
