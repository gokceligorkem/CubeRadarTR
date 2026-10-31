namespace CubeRadar.Core.Pets;

/// <summary>Only a reward since the observed baseline can complete a selected goal.</summary>
public sealed class PetGoalGate
{
    readonly Dictionary<int,(long Epoch,int Goal,int? Remaining,long Gain,bool Delivered)> _state=[];
    public IReadOnlyList<int> Take(PetSnapshot snapshot,IReadOnlyDictionary<int,int> goals)
    {
        var result=new List<int>();
        foreach(var removed in _state.Keys.Except(goals.Keys).ToArray())_state.Remove(removed);
        foreach(var (id,goal) in goals)
        {
            var p=snapshot.Progress.GetValueOrDefault(id);var remaining=p?.Remaining(goal);
            if(!_state.TryGetValue(id,out var old)||old.Epoch!=snapshot.Epoch||old.Goal!=goal)
            {_state[id]=(snapshot.Epoch,goal,remaining,p?.GainSerial??0,false);continue;}
            var ready=!old.Delivered&&old.Remaining>0&&remaining==0&&p!.GainSerial>old.Gain;
            if(ready)result.Add(id);
            // Retain the last trusted baseline during an in-flight upgrade pair.
            _state[id]=(snapshot.Epoch,goal,remaining??old.Remaining,ready?p!.GainSerial:old.Gain,old.Delivered||ready);
        }
        return result;
    }
}
