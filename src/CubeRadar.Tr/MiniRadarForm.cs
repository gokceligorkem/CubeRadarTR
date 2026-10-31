using System.Runtime.InteropServices;
using CubeRadar.Core.Radar;
using Orientation=CubeRadar.Core.Radar.Orientation;

namespace CubeRadar.Tr;

internal sealed class MiniRadarForm : Form
{
    private readonly Label _distance=UiTheme.Label("—",28,UiTheme.Accent,FontStyle.Bold),_direction=UiTheme.Label("Hedef bekleniyor",10),
        _status=UiTheme.Label("● Konum bekleniyor",10,UiTheme.Muted),_event=UiTheme.Label("Boss saati bekleniyor",10,UiTheme.Muted),_eventClock=UiTheme.Label("—",10,UiTheme.Accent,FontStyle.Bold);
    private readonly RadarCanvas _radar=new() {TargetOnly=true};
    private readonly ToolTip _tip=new();
    private bool _compact,_clickThrough;
    private readonly bool _preview;
    internal event Action<bool>? CompactChanged;
    internal bool IsCompact=>_compact;
    internal bool IsClickThrough=>_clickThrough;
    internal bool NativeClickThrough=>IsHandleCreated&&(GetWindowLong(Handle,-20)&0x20)!=0;
    internal int RenderedCreatureCount=>_radar.RenderedCreatureCount;
    internal void UpdateCreatures(IReadOnlyList<CubeRadar.Core.Pets.PetMob> mobs,long now,bool show,HeightFilter height)
        =>_radar.SetCreatures(mobs,now,show,height);
    public MiniRadarForm(bool preview=false)
    {
        _preview=preview;Text="Cube Radar · Mini";ClientSize=new(320,450);MinimumSize=new(260,360);TopMost=true;
        FormBorderStyle=FormBorderStyle.SizableToolWindow;BackColor=UiTheme.Surface;ForeColor=UiTheme.Text;Font=new("Segoe UI",10);
        AutoScaleMode=AutoScaleMode.Dpi;AutoScaleDimensions=new(96,96);DoubleBuffered=true;Opacity=.97;
        StartPosition=FormStartPosition.Manual;Location=new(Math.Max(0,Screen.PrimaryScreen!.WorkingArea.Right-Width-20),40);
        var layout=new TableLayoutPanel {Dock=DockStyle.Fill,Padding=new(12),ColumnCount=1,RowCount=5,BackColor=UiTheme.Surface};
        layout.RowStyles.Add(new(SizeType.Absolute,32));layout.RowStyles.Add(new(SizeType.Absolute,56));layout.RowStyles.Add(new(SizeType.Percent,100));layout.RowStyles.Add(new(SizeType.Absolute,28));layout.RowStyles.Add(new(SizeType.Absolute,38));Controls.Add(layout);
        var header=new TableLayoutPanel {Dock=DockStyle.Fill,ColumnCount=2,Margin=Padding.Empty};header.ColumnStyles.Add(new(SizeType.Percent,100));header.ColumnStyles.Add(new(SizeType.Absolute,34));header.Controls.Add(_status,0,0);
        var compact=new HudButton {Text="▣",Dock=DockStyle.Fill,Tone=ButtonTone.Ghost,AccessibleName="Kompakt mini radar"};compact.Click+=(_,_)=>SetCompact(!_compact);header.Controls.Add(compact,1,0);layout.Controls.Add(header,0,0);
        layout.Controls.Add(_distance,0,1);_distance.TextAlign=ContentAlignment.MiddleCenter;_direction.TextAlign=ContentAlignment.MiddleCenter;
        _radar.Dock=DockStyle.Fill;layout.Controls.Add(_radar,0,2);layout.Controls.Add(_direction,0,3);
        var bossLine=new TableLayoutPanel {Dock=DockStyle.Fill,ColumnCount=2,Margin=Padding.Empty};bossLine.ColumnStyles.Add(new(SizeType.Percent,100));bossLine.ColumnStyles.Add(new(SizeType.Absolute,84));bossLine.Controls.Add(_event,0,0);bossLine.Controls.Add(_eventClock,1,0);layout.Controls.Add(bossLine,0,4);
        _event.Dock=DockStyle.None;_event.Anchor=AnchorStyles.Left|AnchorStyles.Right;_event.Height=24;_event.MaximumSize=new(0,24);
        _event.TextAlign=ContentAlignment.MiddleLeft;_eventClock.TextAlign=ContentAlignment.MiddleRight;_event.AutoEllipsis=true;
        foreach(var control in new Control[]{_status,_distance,_direction,_radar})control.MouseDown+=(_,e)=>{if(e.Button==MouseButtons.Left&&!_preview){ReleaseCapture();SendMessage(Handle,0xA1,(IntPtr)2,IntPtr.Zero);}};
        if(preview){ShowInTaskbar=false;Location=new(-20000,-20000);}
    }
    public void Update(CubeCompanionView view,FixView? self,Orientation axes,double? heading,CubeView? target,string eventName,string eventClock,bool outside,bool demo,bool alerting,int range,
        RadarOrientationMode orientation=RadarOrientationMode.CharacterUp,bool sonar=true)
    {
        var needsFacing=orientation==RadarOrientationMode.CharacterUp;
        _status.Text=demo?"● ÖRNEK · canlı değil":!view.LiveTraffic?"● Bağlantı bekleniyor":view.LastKnownPosition?"● Son bilinen konum":!view.FreshPosition?"● Konum bekleniyor":needsFacing&&heading is null?"● Yön bekleniyor":needsFacing?"● Canlı":"● Canlı · Kuzey sabit";
        _status.ForeColor=demo||!view.FreshPosition||needsFacing&&heading is null?UiTheme.Muted:UiTheme.Green;
        _distance.Text=target is not null&&view.FreshPosition?$"{target.DistanceM:0} m":"—";
        _direction.Text=target is null?"Küp algılanmadı":heading is {} h&&target.Bearing is {} b?
            RadarForm.DirectionText(RadarMath.GetRelativeDirection(RadarMath.CalculateRelativeBearing(b,h)))+"  ·  "+RadarForm.HeightLabel(target.DzUnits):"Yön bekleniyor";
        _event.Text=(alerting?"⚠ ":"Boss: ")+eventName;_eventClock.Text=eventClock;_event.ForeColor=alerting?UiTheme.Accent:UiTheme.Muted;_tip.SetToolTip(_event,eventName+" · "+eventClock);
        var one=view with {Cubes=target is null?[]:[target],Nearest=target};_radar.RangeM=range;
        _radar.SetPresentation(orientation,sonar);_radar.SetState(one,self,axes,heading,outside,demo);_radar.SelectedId=target?.ObjectId;
    }
    internal void SetCompact(bool compact)
    {
        if(_compact==compact)return;_compact=compact;ClientSize=new(UiTheme.Px(this,compact?280:320),UiTheme.Px(this,compact?380:450));CompactChanged?.Invoke(compact);
    }
    internal void SetClickThrough(bool enabled)
    {
        _clickThrough=enabled;if(!IsHandleCreated)return;
        var style=GetWindowLong(Handle,-20);SetWindowLong(Handle,-20,enabled?style|0x20|0x80000:style&~0x20);
    }
    protected override void OnHandleCreated(EventArgs e){base.OnHandleCreated(e);SetClickThrough(_clickThrough);}
    protected override void Dispose(bool disposing){if(disposing)_tip.Dispose();base.Dispose(disposing);}
    [DllImport("user32.dll",EntryPoint="GetWindowLongW")]private static extern int GetWindowLong(IntPtr window,int index);
    [DllImport("user32.dll",EntryPoint="SetWindowLongW")]private static extern int SetWindowLong(IntPtr window,int index,int value);
    [DllImport("user32.dll")]private static extern bool ReleaseCapture();
    [DllImport("user32.dll")]private static extern IntPtr SendMessage(IntPtr window,uint message,IntPtr wparam,IntPtr lparam);
}
