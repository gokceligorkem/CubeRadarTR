using System.Drawing.Drawing2D;
using CubeRadar.Core.Radar;
using Orientation=CubeRadar.Core.Radar.Orientation;

namespace CubeRadar.Tr;

internal sealed partial class RadarCanvas : Control
{
    private readonly System.Windows.Forms.Timer _animation=new() {Interval=80};
    private readonly ToolTip _tip=new();
    private readonly List<(uint Id,PointF Point,RectangleF Label,CubeView Cube,int Rank,RadarPoint Projection)> _hits=[];
    private readonly List<(CubeView Cube,int Rank,RadarPoint Projection,PointF Point)> _targets=[];
    private readonly List<RectangleF> _occupied=[];
    private readonly PointF[] _diamond=new PointF[4],_arrow=new PointF[4];
    private CubeCompanionView? _view;
    private FixView? _self;
    private Orientation _axes=Orientation.Parse("-y");
    private double? _heading;
    private bool _outside,_demo;
    private RadarOrientationMode _orientation=RadarOrientationMode.CharacterUp;
    private bool _sonar=true;
    // Deterministic visual previews only; null uses the existing UI animation clock.
    internal long? PreviewPulseMs;
    internal IReadOnlyList<(double X,double Y)> RoutePoints=Array.Empty<(double,double)>();
    internal int RenderedCount=>_hits.Count;
    internal bool ShowsLastKnownPosition=>_view?.LastKnownPosition==true;
    internal bool AnimationRunning=>_animation.Enabled;
    private PointF? _drawnArrowTip;
    internal PointF? RenderedArrowTip=>_drawnArrowTip is {} tip?new(tip.X*RenderScale,tip.Y*RenderScale):null;
    internal PointF? RenderedPoint(uint id)
    {foreach(var hit in _hits)if(hit.Id==id)return new(hit.Point.X*RenderScale,hit.Point.Y*RenderScale);return null;}
    private uint? _hover;
    internal event Action<uint>? TargetSelected;
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    internal uint? SelectedId {get;set;}
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    internal uint? LockedId {get;set;}
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    internal bool TargetOnly {get;set;}
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    internal int RangeM {get;set;}=150;
    internal float PreviewScale=1;
    private float RenderScale=>DeviceDpi/96f*PreviewScale;
    internal float Radius=>Math.Max(20,Math.Min(Width,Height)/2f-(TargetOnly?34:44)*RenderScale);
    internal PointF PlayerCenter=>new(Width/2f,Height/2f);

    public RadarCanvas()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer|ControlStyles.UserPaint,true);
        BackColor=UiTheme.Surface;AccessibleName="Küp radarı: ön üstte, sağ sağda, sen merkezdesin";
        _animation.Tick+=(_,_)=>{if(FindForm()?.WindowState!=FormWindowState.Minimized)Invalidate();};
        MouseMove+=(_,e)=>ShowTarget(e.Location);
        MouseLeave+=(_,_)=>{_hover=null;_hoverCreature=null;_tip.SetToolTip(this,null);};
        MouseClick+=(_,e)=>{if(Hit(e.Location) is {} hit)TargetSelected?.Invoke(hit.Id);};
    }
    internal void SetState(CubeCompanionView view,FixView? self,Orientation axes,double? heading,bool outside,bool demo)
    {_view=view;_self=self;_axes=axes;_heading=heading;_outside=outside;_demo=demo;UpdateAnimation();Invalidate();}
    internal void SetPresentation(RadarOrientationMode orientation,bool sonar)
    {
        if(_orientation==orientation&&_sonar==sonar)return;
        _orientation=orientation;_sonar=sonar;
        AccessibleName=orientation==RadarOrientationMode.CharacterUp?"Küp radarı: karakter yönü üstte, sen merkezdesin":"Küp radarı: kuzey üstte, sen merkezdesin";
        UpdateAnimation();Invalidate();
    }
    void UpdateAnimation()=>_animation.Enabled=Visible&&_sonar&&PreviewPulseMs is null&&_view is {FreshPosition:true}&&(_view.LiveTraffic||_demo)&&
        (_orientation==RadarOrientationMode.NorthUp||_heading is {} h&&double.IsFinite(h));
    protected override void OnVisibleChanged(EventArgs e){base.OnVisibleChanged(e);UpdateAnimation();}
    internal void StopAnimation()=>_animation.Stop();
    private (uint Id,PointF Point,RectangleF Label,CubeView Cube,int Rank,RadarPoint Projection)? Hit(Point point)
    {
        foreach(var h in _hits)
            if(h.Label.Contains(new PointF(point.X/RenderScale,point.Y/RenderScale))||Math.Abs(h.Point.X-point.X/RenderScale)<=12&&Math.Abs(h.Point.Y-point.Y/RenderScale)<=12)return h;
        return null;
    }
    private void ShowTarget(Point point)
    {
        var hit=Hit(point);var creature=hit is null?CreatureHit(point):null;
        (uint,long)? creatureKey=creature is {} c?(c.Mob.EntityId,c.Mob.LifeId):null;
        if(hit?.Id==_hover&&creatureKey==_hoverCreature)return;
        _hoverCreature=creatureKey;
        _hover=hit?.Id;
        string? detail=null;
        if(hit is {} value&&_self is {} self)
        {
            var p=value.Projection;var height=RadarMath.CalculateHeightDifference(self.Z,value.Cube.Z);
            var direction=RadarMath.GetRelativeDirection(Math.Atan2(p.LocalXM,-p.LocalYM)*180/Math.PI);
            detail=$"Küp {value.Rank}\nMesafe: {p.DistanceM:0.0} m\n{(_orientation==RadarOrientationMode.NorthUp?"Radar yönü":"Yön")}: {direction}\nYükseklik: {height:+0.0;-0.0;0} m"+
                (!p.Inside?$"\n{RangeM} m dışında; kenara sabitlendi":"")+(_view?.LastKnownPosition==true?"\nSon bilinen oyuncu konumuna göre; güncel değil":"");
        }
        else if(creature is {} mob)
            detail=$"{mob.Mob.Name}\nPet kaynağı yaratık\nMesafe: {mob.Projection.DistanceM:0.0} m\nYükseklik: {mob.HeightM:+0.0;-0.0;0} m"+
                (mob.LastKnown?$"\nSon bilinen konum · {Math.Max(0,_creatureNow-mob.Mob.PositionMs)/1000:0} sn önce":"")+
                (_view?.LastKnownPosition==true?"\nOyuncu konumu da son bilinen; güncel değil":"");
        _tip.SetToolTip(this,detail);Cursor=hit is null?Cursors.Default:Cursors.Hand;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);var g=e.Graphics;g.SmoothingMode=SmoothingMode.AntiAlias;_hits.Clear();_creatureHits.Clear();_drawnArrowTip=null;
        var scale=RenderScale;g.ScaleTransform(scale,scale);
        var radius=Radius/scale;var center=new PointF(Width/scale/2f,Height/scale/2f);
        using var ring=new Pen(Color.FromArgb(43,63,77));
        using var outer=new Pen(Color.FromArgb(77,98,111),1.4f);
        using var muted=new SolidBrush(RadarForm.Muted);
        var lastKnown=_view?.LastKnownPosition==true;
        using var gold=new SolidBrush(lastKnown?RadarForm.Muted:RadarForm.Gold);
        using var font=new Font("Segoe UI",12,FontStyle.Regular,GraphicsUnit.Pixel);
        using var directions=new Font("Segoe UI",13,FontStyle.Bold,GraphicsUnit.Pixel);
        using var labelFont=new Font("Segoe UI",14,FontStyle.Bold,GraphicsUnit.Pixel);
        using var centered=new StringFormat {Alignment=StringAlignment.Center,LineAlignment=StringAlignment.Center};
        var occupied=_occupied;occupied.Clear();occupied.AddRange([
            new(center.X-24,center.Y-18,48,58),
            new(center.X-65,center.Y-radius-34,130,26),
            new(center.X-40,center.Y+radius+8,80,24),
            new(center.X-radius-40,center.Y-12,34,24),
            new(center.X+radius+6,center.Y-12,36,24)]);
        var frame=default(RadarProjector);
        var ready=(_view is {FreshPosition:true}||lastKnown)&&_self is {} self&&
            RadarProjector.TryCreate(self.X,self.Y,_heading,_axes,RangeM,radius,_orientation,out frame,center.X,center.Y);
        if(ready&&!lastKnown&&_orientation==RadarOrientationMode.CharacterUp)
        {
            using var front=new SolidBrush(Color.FromArgb(14,239,191,102));
            g.FillPie(front,center.X-radius,center.Y-radius,radius*2,radius*2,-112.5f,45);
        }
        for(var i=1;i<=3;i++)
        {
            var r=radius*i/3;
            g.DrawEllipse(i==3?outer:ring,center.X-r,center.Y-r,r*2,r*2);
            if(radius<65&&i<3)continue;
            var ringText=$"{i*RangeM/3} m";var ringSize=g.MeasureString(ringText,font);
            var ringPosition=new PointF(center.X+r*.5f+6,center.Y-r*.866f+2);
            g.DrawString(ringText,font,muted,ringPosition);
            occupied.Add(new RectangleF(ringPosition,ringSize));
        }
        g.DrawLine(ring,center.X-radius,center.Y,center.X+radius,center.Y);
        g.DrawLine(ring,center.X,center.Y-radius,center.X,center.Y+radius);
        var northUp=_orientation==RadarOrientationMode.NorthUp;
        g.DrawString(lastKnown?"SON BİLİNEN":northUp?"KUZEY":ready?"ÖN ↑":"YÖN BEKLENİYOR",directions,ready?gold:muted,
            new RectangleF(center.X-65,center.Y-radius-34,130,26),centered);
        g.DrawString(northUp?"GÜNEY":"ARKA",directions,muted,new RectangleF(center.X-40,center.Y+radius+8,80,24),centered);
        g.DrawString(northUp?"B":"SOL",directions,muted,new RectangleF(center.X-radius-40,center.Y-12,34,24),centered);
        g.DrawString(northUp?"D":"SAĞ",directions,muted,new RectangleF(center.X+radius+6,center.Y-12,36,24),centered);
        var pulseFrame=RadarPulse.Sample(PreviewPulseMs??Environment.TickCount64,_sonar&&ready&&!lastKnown);
        var phase=(float)pulseFrame.RadiusRatio;
        if(pulseFrame.Opacity>0)
        {
            using var sonar=new Pen(Color.FromArgb((int)(95*pulseFrame.Opacity),239,191,102),1.5f);
            var scan=radius*phase;g.DrawEllipse(sonar,center.X-scan,center.Y-scan,scan*2,scan*2);
        }

        if(ready&&_view is {} view&&_self is not null)
        {
            DrawRoute(g,frame,center,radius);
            DrawCreatures(g,frame,occupied);
            var targets=_targets;targets.Clear();
            for(var i=0;i<view.Cubes.Count;i++)
            {
                var cube=view.Cubes[i];
                if(!frame.TryProject(cube.X,cube.Y,out var p)||!p.Inside&&!_outside)continue;
                // Preserve the existing explicit Cube edge-indicator option; regular points are not clamped.
                var ratio=p.Inside?1:RangeM/p.DistanceM;
                var point=new PointF(center.X+(float)((p.ScreenX-center.X)*ratio),center.Y+(float)((p.ScreenY-center.Y)*ratio));
                targets.Add((cube,i+1,p,point));occupied.Add(new RectangleF(point.X-11,point.Y-11,22,22));
            }
            using var guide=new Pen(lastKnown?RadarForm.Muted:Color.FromArgb(85,239,191,102),1.2f) {DashStyle=DashStyle.Dot};
            using var glow=new SolidBrush(RadarForm.Gold);
            using var fill=new SolidBrush(RadarForm.Gold);
            using var textInk=new SolidBrush(RadarForm.Fg);
            foreach(var target in targets)
            {
                var (cube,rank,p,point)=target;
                var nearest=(LockedId??view.Nearest?.ObjectId)==cube.ObjectId;
                var selected=SelectedId==cube.ObjectId;
                if(nearest)
                {
                    g.DrawLine(guide,center,point);
                }
                var since=(phase-(float)Math.Min(p.DistanceM/RangeM,1)+1)%1;
                var pulse=_sonar&&!lastKnown&&since<0.10f?1-since/0.10f:0;
                var size=nearest?7:5.5f;
                glow.Color=lastKnown?Color.FromArgb(20,RadarForm.Muted):Color.FromArgb((int)(25+70*pulse),239,191,102);
                var glowSize=size+5+pulse*3;g.FillEllipse(glow,point.X-glowSize,point.Y-glowSize,glowSize*2,glowSize*2);
                _diamond[0]=new(point.X,point.Y-size);_diamond[1]=new(point.X+size,point.Y);
                _diamond[2]=new(point.X,point.Y+size);_diamond[3]=new(point.X-size,point.Y);g.FillPolygon(gold,_diamond);
                if(selected)g.DrawEllipse(Pens.White,point.X-11,point.Y-11,22,22);
                var height=RadarMath.CalculateHeightDifference(_self.Z,cube.Z);
                var arrow=Math.Abs(height)<3?"":height>0?" ▲":" ▼";
                var box=new RectangleF(point.X-12,point.Y-12,24,24);
                if(!TargetOnly&&(view.Cubes.Count<=5||nearest||selected))
                {
                    var text=$"{rank:00} · {p.DistanceM:0} m{arrow}";
                    var measured=g.MeasureString(text,labelFont);box=PlaceLabel(point,new SizeF(measured.Width+16,28),occupied);occupied.Add(box);
                    fill.Color=nearest&&!lastKnown?RadarForm.Gold:UiTheme.Raised;
                    textInk.Color=lastKnown?RadarForm.Muted:nearest?RadarForm.Bg:RadarForm.Fg;
                    using var path=Rounded(box,6);g.FillPath(fill,path);
                    g.DrawString(text,labelFont,textInk,box,centered);
                }
                _hits.Add((cube.ObjectId,point,box,cube,rank,p));

            }
        }

        ClearMissingCreatureHover();
        using var playerBack=new SolidBrush(BackColor);
        g.FillEllipse(playerBack,center.X-13,center.Y-13,26,26);
        if(ready&&frame.PlayerArrowDegrees is {} facing)
        {
            var (sin,cos)=Math.SinCos(facing*Math.PI/180);
            ReadOnlySpan<PointF> shape=[new(0,-10),new(-7,7),new(0,3),new(7,7)];
            for(var i=0;i<4;i++)_arrow[i]=new(center.X+(float)(shape[i].X*cos-shape[i].Y*sin),center.Y+(float)(shape[i].X*sin+shape[i].Y*cos));
            g.FillPolygon(lastKnown?muted:Brushes.White,_arrow);
            _drawnArrowTip=_arrow[0];
        }
        else g.DrawEllipse(Pens.Gray,center.X-5,center.Y-5,10,10);
        g.DrawString("SEN",font,muted,new RectangleF(center.X-23,center.Y+13,46,22),centered);
        if(_showCreatures&&!TargetOnly)
        {
            using var creatureInk=new SolidBrush(UiTheme.Danger);
            g.DrawString("◆ Küp",font,gold,6,Height/scale-18);
            g.DrawString("● Yaratık · Pet kaynakları",font,creatureInk,70,Height/scale-18);
            if(Width/scale>370)g.DrawString("○ Son bilinen",font,muted,260,Height/scale-18);
        }
    }

    void DrawRoute(Graphics g,RadarProjector frame,PointF center,float radius)
    {
        if(RoutePoints.Count==0)return;
        var state=g.Save();using var clip=new GraphicsPath();clip.AddEllipse(center.X-radius,center.Y-radius,radius*2,radius*2);
        g.SetClip(clip,CombineMode.Intersect);
        using var pen=new Pen(Color.FromArgb(135,UiTheme.Green),1.3f) {DashStyle=DashStyle.Dash};
        using var dot=new SolidBrush(UiTheme.Green);
        PointF? previous=null;
        foreach(var world in RoutePoints)
        {
            if(!frame.TryProject(world.X,world.Y,out var p)){previous=null;continue;}
            var point=new PointF((float)p.ScreenX,(float)p.ScreenY);
            if(!float.IsFinite(point.X)||!float.IsFinite(point.Y)){previous=null;continue;}
            if(previous is {} last)g.DrawLine(pen,last,point);
            if(p.Inside)g.FillEllipse(dot,point.X-2,point.Y-2,4,4);previous=point;
        }
        g.Restore(state);
    }

    private RectangleF PlaceLabel(PointF point,SizeF size,List<RectangleF> occupied)
    {
        ReadOnlySpan<PointF> offsets=[new(15,-35),new(-size.Width-15,-35),new(15,12),new(-size.Width-15,12),new(-size.Width-15,-69),new(15,-69),new(-size.Width-15,46),new(15,46)];
        RectangleF result=default;
        foreach(var offset in offsets)
        {
            result=new(Math.Clamp(point.X+offset.X,4,Math.Max(4,Width/RenderScale-size.Width-4)),
                Math.Clamp(point.Y+offset.Y,4,Math.Max(4,Height/RenderScale-size.Height-4)),size.Width,size.Height);
            var padded=RectangleF.Inflate(result,4,4);
            var overlaps=false;foreach(var r in occupied)if(r.IntersectsWith(padded)){overlaps=true;break;}
            if(!overlaps)return result;
        }
        return result;
    }
    private static GraphicsPath Rounded(RectangleF r,float corner)
    {
        var path=new GraphicsPath();var d=corner*2;
        path.AddArc(r.Left,r.Top,d,d,180,90);path.AddArc(r.Right-d,r.Top,d,d,270,90);
        path.AddArc(r.Right-d,r.Bottom-d,d,d,0,90);path.AddArc(r.Left,r.Bottom-d,d,d,90,90);path.CloseFigure();return path;
    }
    protected override void Dispose(bool disposing)
    {if(disposing){_animation.Dispose();_tip.Dispose();}base.Dispose(disposing);}
}
