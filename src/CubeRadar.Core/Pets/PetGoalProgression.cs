namespace CubeRadar.Core.Pets;

/// <summary>Keep tracked pets moving to the next confirmed rank, until MAX.</summary>
public static class PetGoalProgression
{
    public static bool IsMaxed(PetProgress? progress)=>progress is {Verified:true,Rank:3};
    public static bool Advance(Dictionary<int,int> goals,PetSnapshot snapshot)
    {
        if(!snapshot.Synced)return false;var changed=false;
        foreach(var id in goals.Keys.ToArray())
        {
            if(snapshot.Progress.GetValueOrDefault(id) is not {Verified:true,Rank:1 or 2} progress||goals[id]>progress.Rank)continue;
            goals[id]=progress.Rank.Value+1;changed=true;
        }
        return changed;
    }
}
