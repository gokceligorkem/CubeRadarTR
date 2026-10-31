namespace CubeRadar.Core.Radar;

public enum HeightFilter { All, SameLevel, Above, Below }

/// <summary>Locks object identity rather than distance rank. Never supplies stale geometry.</summary>
public sealed class TargetTracker
{
    private int? _map;
    public uint? LockedId { get; private set; }
    public void Lock(uint? id)=>LockedId=id;
    public void ObserveMap(int? map)
    {
        if(_map!=map)LockedId=null;
        _map=map;
    }
    public CubeView? Resolve(CubeCompanionView view)=>LockedId is {} id
        ?view.FreshPosition?view.Cubes.FirstOrDefault(c=>c.ObjectId==id):null:view.Nearest;

    public static CubeCompanionView Filter(CubeCompanionView view,HeightFilter filter)
    {
        if(filter==HeightFilter.All)return view;
        var cubes=view.FreshPosition||view.LastKnownPosition?view.Cubes.Where(c=>c.DzUnits is {} z && (filter switch {
            HeightFilter.SameLevel=>Math.Abs(z)<300,HeightFilter.Above=>z>=300,_=>z<=-300})).ToList():[];
        return view with {Cubes=cubes,Nearest=view.FreshPosition?cubes.FirstOrDefault():null};
    }
}
