namespace CubeRadar.Core.Pets;

/// <summary>Remember nearby soul goals until completion or explicit opt-out; never infer an unknown balance.</summary>
public static class PetTrackingPlan
{
    public static bool HasMissingSouls(PetProgress? progress)=>progress?.Remaining(3) is >0;
    public static bool Update(Dictionary<int,int> goals,IReadOnlySet<int> excluded,PetSnapshot snapshot)
    {
        var changed=false;foreach(var id in excluded)changed|=goals.Remove(id);
        changed|=PetGoalProgression.Advance(goals,snapshot);
        if(!snapshot.Synced)return changed;
        foreach(var id in snapshot.Nearby.Select(m=>m.PetId).Distinct())
        {
            if(excluded.Contains(id)||!HasMissingSouls(snapshot.Progress.GetValueOrDefault(id)))continue;
            var rank=snapshot.Progress[id].Rank!.Value;
            changed|=goals.TryAdd(id,Math.Clamp(rank+1,1,3));
        }
        return changed;
    }
}
