using CubeRadar.Core.Pets;
using CubeRadar.Core.Radar;

namespace CubeRadar.Tr;

internal sealed partial class RadarCanvas
{
    private IReadOnlyList<PetMob> _creatures=Array.Empty<PetMob>();
    private readonly List<RadarCreaturePoint> _creatureHits=[];
    private (uint Id,long Life)? _hoverCreature;
    private long _creatureNow;
    private bool _showCreatures=true;
    private HeightFilter _creatureHeight;
    internal int RenderedCreatureCount=>_creatureHits.Count;
    internal int RenderedLastKnownCreatureCount
    {get{var count=0;foreach(var hit in _creatureHits)if(hit.LastKnown||_view?.LastKnownPosition==true)count++;return count;}}
    internal PointF? RenderedCreaturePoint(uint id)
    {
        foreach(var hit in _creatureHits)if(hit.Mob.EntityId==id)
            return new((float)hit.Projection.ScreenX*RenderScale,(float)hit.Projection.ScreenY*RenderScale);
        return null;
    }
    internal void SetCreatures(IReadOnlyList<PetMob> creatures,long now,bool show,HeightFilter height)
    {
        _creatures=creatures;_creatureNow=now;_showCreatures=show;_creatureHeight=height;
        // Hits from the previous world frame must not survive a removal or a disabled layer.
        _creatureHits.Clear();
        if(!show&&_hoverCreature is not null){_hoverCreature=null;_tip.SetToolTip(this,null);}
        Invalidate();
    }
    private RadarCreaturePoint? CreatureHit(Point location)
    {
        var x=location.X/RenderScale;var y=location.Y/RenderScale;
        foreach(var hit in _creatureHits)
        {
            var dx=hit.Projection.ScreenX-x;var dy=hit.Projection.ScreenY-y;
            if(dx*dx+dy*dy<=81)return hit;
        }
        return null;
    }
    private void DrawCreatures(Graphics graphics,RadarProjector frame,List<RectangleF> occupied)
    {
        if(!_showCreatures||_self is null)return;
        using var fill=new SolidBrush(UiTheme.Danger);
        using var outline=new Pen(UiTheme.Surface,1.2f);
        using var ghost=new Pen(UiTheme.Muted,1.3f);
        foreach(var mob in _creatures)
        {
            if(!RadarCreaturePresentation.TryProject(mob,frame,_self.Z,_creatureHeight,_creatureNow,out var hit,showLastKnown:true))continue;
            var x=(float)hit.Projection.ScreenX;var y=(float)hit.Projection.ScreenY;
            if(hit.LastKnown||_view?.LastKnownPosition==true)graphics.DrawEllipse(ghost,x-4,y-4,8,8);
            else {graphics.FillEllipse(fill,x-4,y-4,8,8);graphics.DrawEllipse(outline,x-4,y-4,8,8);}
            if(Math.Abs(hit.HeightM)>=3)
                graphics.DrawLine(Pens.White,x-2,y+(hit.HeightM>0?-7:7),x+2,y+(hit.HeightM>0?-7:7));
            _creatureHits.Add(hit);occupied.Add(new(x-7,y-7,14,14));
        }
    }
    private void ClearMissingCreatureHover()
    {
        if(_hoverCreature is not {} hover)return;
        foreach(var hit in _creatureHits)if(hit.Mob.EntityId==hover.Id&&hit.Mob.LifeId==hover.Life)return;
        _hoverCreature=null;_tip.SetToolTip(this,null);
    }
}
