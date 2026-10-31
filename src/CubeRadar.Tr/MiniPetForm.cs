using System.Runtime.InteropServices;
using CubeRadar.Core.Pets;

namespace CubeRadar.Tr;

internal sealed class MiniPetForm:Form
{
    readonly bool _preview;
    readonly Label _status=UiTheme.Label("Eşitleme bekleniyor",8,UiTheme.Muted),_totals=UiTheme.Label("— kesim",9,UiTheme.Text,FontStyle.Bold);
    readonly FlowLayoutPanel _list=new() {Dock=DockStyle.Fill,FlowDirection=FlowDirection.TopDown,WrapContents=false,AutoScroll=true,Margin=Padding.Empty};
    readonly Label _empty=UiTheme.Label("Pet Avı'ndan takip etmek istediklerini işaretle.",9,UiTheme.Muted);
    readonly Dictionary<int,PetMiniRow> _rows=[];
    readonly ContextMenuStrip _menu=UiTheme.Menu();
    readonly ToolTip _tip=new();
    readonly ToolStripMenuItem _topItem=new("Üstte tut") {CheckOnClick=true},_fitItem=new("Yüksekliği listeye sığdır") {CheckOnClick=true},_translucent=new("Yarı saydam");
    readonly ToolStripMenuItem _untrackItem=new("Takipten çıkar");
    readonly ToolStripSeparator _untrackSeparator=new();
    readonly TrackBar _opacitySlider=new() {Minimum=40,Maximum=100,TickFrequency=15,SmallChange=5,LargeChange=10,AutoSize=false,Size=new(190,36),BackColor=UiTheme.Raised,AccessibleName="Mini pet görünürlüğü"};
    readonly ToolStripLabel _opacityLabel=new();
    readonly TableLayoutPanel _layout;
    int _opacityPercent,_lastTranslucent=70;
    bool _changingAppearance,_autoFit,_resizing;
    Size _resizeBegin;
    int? _contextPet;
    long _nextRender;
    internal event Action? EditRequested;
    internal event Action<int>? UntrackRequested;
    internal event Action<Rectangle,bool,int,bool>? PreferencesChanged;
    internal int DisplayedCount=>_rows.Count;
    internal int DisplayedRowHeight=>_rows.Values.Select(r=>r.Height).DefaultIfEmpty(0).Max();
    internal byte? NativeAlpha=>IsHandleCreated&&GetLayeredWindowAttributes(Handle,out _,out var alpha,out _)?alpha:null;
    internal string TotalsText=>_totals.Text;
    internal (string Kills,string Remaining) ReadCard(int id)=>(_rows[id].StatsText,_rows[id].RemainingText);

    internal MiniPetForm(bool preview,PetPreferences preferences)
    {
        _preview=preview;_autoFit=preferences.MiniAutoFit;Text="Pet Avı · Mini";Font=new("Segoe UI",9);BackColor=UiTheme.Background;ForeColor=UiTheme.Text;
        FormBorderStyle=FormBorderStyle.None;ShowInTaskbar=false;TopMost=preferences.MiniTopMost;DoubleBuffered=true;
        AutoScaleMode=AutoScaleMode.Dpi;AutoScaleDimensions=new(96,96);MinimumSize=new(240,80);MaximumSize=new(1000,1200);StartPosition=FormStartPosition.Manual;
        var area=Screen.PrimaryScreen!.WorkingArea;
        var bounds=new Rectangle(preferences.MiniLeft??area.Right-preferences.MiniWidth-24,preferences.MiniTop??80,preferences.MiniWidth,preferences.MiniHeight);
        if(!Screen.AllScreens.Any(s=>Rectangle.Intersect(s.WorkingArea,bounds) is {Width:>=100,Height:>=60}))bounds.Location=new(area.Right-bounds.Width-24,area.Top+60);
        bounds.Width=Math.Min(bounds.Width,area.Width);bounds.Height=Math.Min(bounds.Height,area.Height);Bounds=bounds;
        var layout=_layout=new TableLayoutPanel {Dock=DockStyle.Fill,Padding=new(4),ColumnCount=1,RowCount=3,Margin=Padding.Empty};
        layout.RowStyles.Add(new(SizeType.Absolute,26));layout.RowStyles.Add(new(SizeType.Percent,100));layout.RowStyles.Add(new(SizeType.Absolute,17));Controls.Add(layout);
        var header=new TableLayoutPanel {Dock=DockStyle.Fill,ColumnCount=4,Margin=Padding.Empty};
        header.ColumnStyles.Add(new(SizeType.Percent,100));header.ColumnStyles.Add(new(SizeType.Absolute,86));header.ColumnStyles.Add(new(SizeType.Absolute,25));header.ColumnStyles.Add(new(SizeType.Absolute,25));
        var title=UiTheme.Label("PET AVI",9,UiTheme.Muted,FontStyle.Bold);_totals.TextAlign=ContentAlignment.MiddleRight;
        header.Controls.Add(title,0,0);header.Controls.Add(_totals,1,0);
        var options=new HudButton {Text="···",Dock=DockStyle.Fill,Tone=ButtonTone.Ghost,Margin=Padding.Empty,AccessibleName="Mini pet ayarları"};
        var close=new HudButton {Text="×",Dock=DockStyle.Fill,Tone=ButtonTone.Ghost,Margin=Padding.Empty,AccessibleName="Mini peti kapat"};
        options.Click+=(_,_)=>{_contextPet=null;_menu.Show(options,new Point(0,options.Height));};close.Click+=(_,_)=>Close();header.Controls.Add(options,2,0);header.Controls.Add(close,3,0);
        layout.Controls.Add(header,0,0);layout.Controls.Add(_list,0,1);layout.Controls.Add(_status,0,2);_empty.Size=new(280,44);_empty.Margin=new(5);_list.Controls.Add(_empty);
        _tip.SetToolTip(options,"Görünürlük, üstte tut ve liste boyutu");_tip.SetToolTip(_totals,"Bu oturumda saldırıp öldüğünü gördüğün yaratıklar");
        BuildMenu();_topItem.Checked=TopMost;_fitItem.Checked=_autoFit;SetOpacityPercent(preferences.MiniOpacity);
        foreach(var control in new Control[]{this,layout,header,title,_totals,_status,_list,_empty})AttachDrag(control);
        _list.ClientSizeChanged+=(_,_)=>ResizeRows();
        ResizeBegin+=(_,_)=>{_resizing=true;_resizeBegin=Size;};
        ResizeEnd+=(_,_)=>{_resizing=false;if(Height!=_resizeBegin.Height){_autoFit=false;_fitItem.Checked=false;}SavePreferences();};
        if(preview){ShowInTaskbar=false;Location=new(-20000,-20000);}
    }
    void BuildMenu()
    {
        _menu.Items.Add(_untrackItem);_menu.Items.Add(_untrackSeparator);
        _menu.Opening+=(_,_)=>
        {
            var visible=_contextPet is {} id&&_rows.ContainsKey(id);_untrackItem.Visible=_untrackSeparator.Visible=visible;
            if(visible)_untrackItem.Text="Takipten çıkar · "+PetCatalog.Default.Pets[_contextPet!.Value].Name;
        };
        _menu.Closed+=(_,_)=>_contextPet=null;
        _untrackItem.Click+=(_,_)=>{if(_contextPet is {} id)UntrackRequested?.Invoke(id);};
        _menu.Items.Add("Takibi düzenle",null,(_,_)=>EditRequested?.Invoke());_menu.Items.Add(_topItem);
        _topItem.CheckedChanged+=(_,_)=>{TopMost=_topItem.Checked;SavePreferences();};
        _menu.Items.Add(_translucent);_translucent.Click+=(_,_)=>SetOpacityPercent(_opacityPercent<100?100:_lastTranslucent);
        _menu.Items.Add(_opacityLabel);_menu.Items.Add(new ToolStripControlHost(_opacitySlider) {AutoSize=false,Size=new(200,40)});
        _opacitySlider.ValueChanged+=(_,_)=>{if(!_changingAppearance)SetOpacityPercent(_opacitySlider.Value);};
        _menu.Items.Add(new ToolStripSeparator());_menu.Items.Add(_fitItem);
        _fitItem.CheckedChanged+=(_,_)=>{_autoFit=_fitItem.Checked;if(_autoFit)FitHeight();SavePreferences();};
        _menu.Items.Add("Miniyi kapat",null,(_,_)=>Close());ContextMenuStrip=_menu;
    }
    internal void SetOpacityPercent(int percent)
    {
        _opacityPercent=Math.Clamp(percent,40,100);if(_opacityPercent<100)_lastTranslucent=_opacityPercent;
        _changingAppearance=true;
        try{Opacity=_opacityPercent/100.0;_opacitySlider.Value=_opacityPercent;_opacityLabel.Text=$"Görünürlük: %{_opacityPercent}";_translucent.Checked=_opacityPercent<100;}
        finally{_changingAppearance=false;}
        SavePreferences();
    }
    internal void UpdateState(PetSnapshot snapshot,IReadOnlyDictionary<int,int> goals,Func<int,string> direction,long now,bool force=false,bool hideCompleted=true)
    {
        if(!force&&now<_nextRender)return;_nextRender=now+500;
        var nearby=snapshot.Nearby.Select(m=>m.PetId).ToHashSet();
        var active=goals.Where(g=>nearby.Contains(g.Key)&&(!hideCompleted||!PetGoalProgression.IsMaxed(snapshot.Progress.GetValueOrDefault(g.Key)))).ToDictionary();
        foreach(var id in _rows.Keys.Except(active.Keys).ToArray()){var row=_rows[id];_list.Controls.Remove(row);_rows.Remove(id);row.Dispose();}
        foreach(var (id,goal) in active)
        {
            if(!PetCatalog.Default.Pets.TryGetValue(id,out var pet))continue;
            if(!_rows.TryGetValue(id,out var row))
            {row=new();_rows.Add(id,row);_list.Controls.Add(row);AttachDrag(row,id);foreach(var label in row.Labels)AttachDrag(label,id);}
            row.Update(pet,goal,snapshot.Progress.GetValueOrDefault(id),snapshot.CombatKnown,snapshot.UnmatchedKills>0,direction(id));
        }
        _empty.Visible=_rows.Count==0;_empty.Text="Yakında takip edilen pet yok.";
        _totals.Text=$"{(snapshot.CombatKnown?snapshot.SessionKills.ToString():"—")} kesim";
        _status.Text=_preview?"ÖRNEK · canlı veri değil":!snapshot.Synced?"● Ruh eşitlemesi gerekli":snapshot.CombatPartial?"● Canlı · kesim sayacı kısmi":"● Canlı";
        _status.ForeColor=snapshot.Synced&&!_preview?UiTheme.Green:UiTheme.Muted;ResizeRows();if(_autoFit&&!_resizing)FitHeight();
    }
    void AttachDrag(Control control,int? petId=null)
    {
        control.ContextMenuStrip=_menu;
        control.MouseDown+=(_,e)=>{if(e.Button==MouseButtons.Right)_contextPet=petId;if(e.Button==MouseButtons.Left&&!_preview){_contextPet=null;ReleaseCapture();SendMessage(Handle,0xA1,(IntPtr)2,IntPtr.Zero);}};
    }
    internal void PreviewUntrackViaMenu(int id)
    {if(!_preview||!_rows.ContainsKey(id))throw new InvalidOperationException();_contextPet=id;_untrackItem.PerformClick();_contextPet=null;}
    void ResizeRows()
    {var width=Math.Max(210,_list.ClientSize.Width-(_list.VerticalScroll.Visible?SystemInformation.VerticalScrollBarWidth:0)-2);_empty.Width=width;foreach(var row in _rows.Values)row.Width=width;}
    void FitHeight()
    {
        var area=Screen.FromRectangle(Bounds).WorkingArea;
        var content=_rows.Count==0?UiTheme.Px(this,48):_rows.Values.Sum(r=>r.Height+r.Margin.Vertical);
        var height=Math.Clamp(content+(int)_layout.RowStyles[0].Height+(int)_layout.RowStyles[2].Height+_layout.Padding.Vertical,MinimumSize.Height,Math.Min(area.Height,UiTheme.Px(this,420)));
        if(Height==height)return;Height=height;
        if(!_preview&&Bottom>area.Bottom)Top=Math.Max(area.Top,area.Bottom-height);SavePreferences();
    }
    void SavePreferences(){if(IsHandleCreated&&!IsDisposed&&WindowState==FormWindowState.Normal)PreferencesChanged?.Invoke(Bounds,TopMost,_opacityPercent,_autoFit);}
    internal int PreviewHitTest(Point local)
    {
        if(!_preview)throw new InvalidOperationException();var point=PointToScreen(local);
        return (int)SendMessage(Handle,0x84,IntPtr.Zero,(IntPtr)((point.X&0xffff)|((point.Y&0xffff)<<16)));
    }
    protected override void WndProc(ref Message m)
    {
        base.WndProc(ref m);if(m.Msg!=0x84||m.Result!=(IntPtr)1||WindowState!=FormWindowState.Normal)return;
        var packed=m.LParam.ToInt64();var point=PointToClient(new Point(unchecked((short)(packed&0xffff)),unchecked((short)((packed>>16)&0xffff))));
        var grip=UiTheme.Px(this,5);var left=point.X<grip;var right=point.X>=ClientSize.Width-grip;var top=point.Y<grip;var bottom=point.Y>=ClientSize.Height-grip;
        m.Result=(IntPtr)(top?(left?13:right?14:12):bottom?(left?16:right?17:15):left?10:right?11:1);
    }
    protected override void Dispose(bool disposing)
    {if(disposing&&!IsDisposed){SavePreferences();_tip.Dispose();_menu.Dispose();}base.Dispose(disposing);}
    [DllImport("user32.dll")]static extern bool ReleaseCapture();
    [DllImport("user32.dll")]static extern bool GetLayeredWindowAttributes(IntPtr window,out uint color,out byte alpha,out uint flags);
    [DllImport("user32.dll")]static extern IntPtr SendMessage(IntPtr window,uint message,IntPtr wparam,IntPtr lparam);
}

internal sealed class PetMiniRow:Panel
{
    readonly Label _name=UiTheme.Label("",10,UiTheme.Text,FontStyle.Bold),_remaining=UiTheme.Label("—",11,UiTheme.Accent,FontStyle.Bold),
        _stats=UiTheme.Label("",8.5f,UiTheme.Muted),_direction=UiTheme.Label("",8.5f,UiTheme.Muted);
    readonly ToolTip _tip=new();
    double _ratio;int _dpi;
    internal IEnumerable<Label> Labels=>[_name,_remaining,_stats,_direction];
    internal string StatsText=>_stats.Text;
    internal string RemainingText=>_remaining.Text;
    internal PetMiniRow()
    {
        DoubleBuffered=true;BackColor=UiTheme.Background;Width=300;Height=46;Margin=new(0,0,0,2);
        foreach(var label in Labels){label.Dock=DockStyle.None;Controls.Add(label);}
        _remaining.TextAlign=_direction.TextAlign=ContentAlignment.MiddleRight;SizeChanged+=(_,_)=>Reflow();
    }
    void Reflow()
    {
        var pad=UiTheme.Px(this,5);var upper=UiTheme.Px(this,21);var lower=UiTheme.Px(this,18);var right=UiTheme.Px(this,92);
        _name.SetBounds(pad,1,Math.Max(30,Width-right-pad*2),upper);_remaining.SetBounds(Width-right-pad,1,right,upper);
        _stats.SetBounds(pad,upper,Math.Max(30,Width-right-pad*2),lower);_direction.SetBounds(Width-right-pad,upper,right,lower);
    }
    internal void Update(PetDefinition pet,int goal,PetProgress? progress,bool combatKnown,bool unmatched,string direction)
    {
        if(_dpi!=DeviceDpi){_dpi=DeviceDpi;Height=UiTheme.Px(this,46);Margin=new(0,0,0,UiTheme.Px(this,2));Reflow();}
        var remaining=progress?.Remaining(goal);var kills=progress?.SessionKills??0;
        _name.Text=pet.Name;_remaining.Text=remaining is {} left?left==0?"Hazır":$"{left} kaldı":"—";_remaining.ForeColor=remaining==0?UiTheme.Green:UiTheme.Accent;
        var balance=progress is {Verified:true,Rank:{} rank,Souls:{} souls}?rank==3?"MAX":$"{souls}/{PetCatalog.Cost(rank)} ruh":"Eşitleniyor";
        var count=!combatKnown||unmatched&&kills==0?"—":kills+(unmatched?"+":"");
        _stats.Text=$"{balance} · {count} kesim · +{progress?.SessionSouls??0}";
        _direction.Text=direction.Replace("Konum bekleniyor","Konum yok").Replace("Yön bekleniyor","Yön yok").Replace("Son görüldü","Son kayıt");
        var description=$"{pet.Name} · Hedef seviye {goal}\n{(remaining is {} rest?$"{rest} ruh kaldı":"Ruh eşitlemesi bekleniyor")}\nPet kesimi: {count} · Kazanılan: +{progress?.SessionSouls??0} ruh\n{direction}";
        foreach(var label in Labels)_tip.SetToolTip(label,description);
        _ratio=remaining is {} value&&progress?.Rank is {} current?value==0?1:Math.Clamp(1-value/(double)Math.Max(1,PetCatalog.CostBetween(current,goal)),0,1):0;Invalidate();
    }
    protected override void OnPaint(PaintEventArgs e)
    {base.OnPaint(e);using var brush=new SolidBrush(UiTheme.Accent);e.Graphics.FillRectangle(brush,0,Height-2,(int)(Width*_ratio),2);}
    protected override void Dispose(bool disposing){if(disposing)_tip.Dispose();base.Dispose(disposing);}
}
