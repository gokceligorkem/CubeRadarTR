using CubeRadar.Core.Radar;

namespace CubeRadar.Tr;

internal sealed partial class BossPanel
{
    private readonly UpcomingBossCard[] _upcoming=[new(),new(),new()];
    private readonly Label _profile=UiTheme.Label("",10,UiTheme.Muted);
    private Button _refreshButton=null!;
    private string _miniName="Boss saati bekleniyor",_miniClock="—";
    internal (string Name,string Clock) MiniNotice()=>(_miniName,_miniClock);

    private void BuildBossLayout()
    {
        BackColor=UiTheme.Background;ForeColor=UiTheme.Text;Font=new("Segoe UI",10);Dock=DockStyle.Fill;
        var layout=new TableLayoutPanel {Dock=DockStyle.Fill,ColumnCount=1,RowCount=6,Padding=new(0,4,0,0)};Controls.Add(layout);
        int[] heights=[48,156,42,100,88,48];
        for(var i=0;i<heights.Length;i++)layout.RowStyles.Add(new(i==3?SizeType.Percent:SizeType.Absolute,i==3?100:heights[i]));
        var header=new TableLayoutPanel {Dock=DockStyle.Fill,ColumnCount=2,Margin=Padding.Empty};header.ColumnStyles.Add(new(SizeType.Percent,55));header.ColumnStyles.Add(new(SizeType.Percent,45));
        header.Controls.Add(UiTheme.Label("YAKLAŞAN ETKİNLİKLER",11,UiTheme.Muted,FontStyle.Bold),0,0);_profile.Text=_store.Settings.ServerName;_profile.TextAlign=ContentAlignment.MiddleRight;header.Controls.Add(_profile,1,0);layout.Controls.Add(header,0,0);
        var cards=new TableLayoutPanel {Dock=DockStyle.Fill,ColumnCount=3,RowCount=1,Margin=Padding.Empty};
        for(var i=0;i<3;i++)
        {
            cards.ColumnStyles.Add(new(SizeType.Percent,33.333f));var card=_upcoming[i];card.Dock=DockStyle.Fill;card.Margin=new(i==0?0:6,0,i==2?0:6,8);cards.Controls.Add(card,i,0);
            card.Selected+=id=>{if(_rows.TryGetValue(id,out var row)){_grid.CurrentCell=row.Cells[1];_grid.FirstDisplayedScrollingRowIndex=row.Index;}};
        }
        layout.Controls.Add(cards,0,1);
        _status.Dock=DockStyle.Fill;_status.ForeColor=UiTheme.Muted;_status.Font=new("Segoe UI",10);_status.AutoEllipsis=true;layout.Controls.Add(_status,0,2);
        var table=new SurfaceCard {Dock=DockStyle.Fill,Padding=new(4),Margin=Padding.Empty};ConfigureGrid();UiTheme.Grid(_grid);table.Controls.Add(_grid);layout.Controls.Add(table,0,3);
        _grid.RowPostPaint+=(_,e)=>{if(e.RowIndex<_grid.Rows.Count&&_grid.Rows[e.RowIndex].Cells[4].Style.ForeColor==UiTheme.Accent){using var accent=new SolidBrush(UiTheme.Accent);e.Graphics.FillRectangle(accent,e.RowBounds.Left,e.RowBounds.Top+8,3,e.RowBounds.Height-16);}};
        _detail.Dock=DockStyle.Fill;_detail.Padding=new(8,10,8,0);_detail.ForeColor=UiTheme.Muted;_detail.Font=new("Segoe UI",9.5f);_detail.AutoEllipsis=true;layout.Controls.Add(_detail,0,4);
        var actions=new FlowLayoutPanel {Dock=DockStyle.Fill,WrapContents=false,Padding=new(0,6,0,0)};
        var add=Button("+ Boss ekle",()=>Edit(null));((HudButton)add).Tone=ButtonTone.Primary;add.Width=126;
        var menu=UiTheme.Menu();menu.Items.Add("Hazır liste…",null,(_,_)=>AddPresets());menu.Items.Add("Boss sesi testi",null,(_,_)=>SoundTestRequested?.Invoke());menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Tüm uyarıları aç",null,(_,_)=>SetAllAlerts(true));menu.Items.Add("Tüm uyarıları kapat",null,(_,_)=>SetAllAlerts(false));menu.Items.Add(new ToolStripSeparator());var remove=menu.Items.Add("Seçili bossu sil…",null,(_,_)=>Remove());remove.ForeColor=UiTheme.Danger;
        var more=Button("··· İşlemler",()=>{});more.Click+=(_,_)=>menu.Show(more,new Point(0,more.Height));more.Disposed+=(_,_)=>menu.Dispose();
        var edit=Button("Düzenle",()=>Edit(Selected));edit.Width=100;var map=Button("Haritada aç",OpenMap);map.Width=114;
        var refresh=_refreshButton=Button("↻ Yenile",()=>RefreshRequested?.Invoke());refresh.Width=96;more.Width=100;
        actions.Controls.AddRange([add,edit,map,refresh,more]);layout.Controls.Add(actions,0,5);
        _sound.Checked=_store.Settings.BossSound;_server.Text=_store.Settings.ServerName;UiTheme.Input(_server);UiTheme.Input(_zone);
    }
    internal Control ProfileControls()
    {
        var panel=new TableLayoutPanel {ColumnCount=3,RowCount=2,BackColor=UiTheme.Surface,Margin=Padding.Empty};
        panel.ColumnStyles.Add(new(SizeType.Absolute,116));panel.ColumnStyles.Add(new(SizeType.Percent,100));panel.ColumnStyles.Add(new(SizeType.Absolute,96));
        panel.RowStyles.Add(new(SizeType.Percent,50));panel.RowStyles.Add(new(SizeType.Percent,50));
        panel.Controls.Add(UiTheme.Label("Sunucu",10,UiTheme.Muted),0,0);_server.Dock=DockStyle.Fill;_server.MaxLength=120;panel.Controls.Add(_server,1,0);
        var save=Button("Kaydet",()=>{_store.Settings.ServerName=_server.Text.Trim();_profile.Text=_store.Settings.ServerName;Save();});save.Dock=DockStyle.Fill;panel.Controls.Add(save,2,0);
        panel.Controls.Add(UiTheme.Label("Gösterim saati",10,UiTheme.Muted),0,1);_zone.Dock=DockStyle.Fill;panel.Controls.Add(_zone,1,1);panel.SetColumnSpan(_zone,2);return panel;
    }
    private void UpdateUpcoming(List<(BossEvent Event,BossOccurrence? Next)> rows,DateTimeOffset now,bool sound)
    {
        var future=rows.Where(r=>r.Next is not null&&r.Next.Start>now).Take(3).ToArray();
        for(var i=0;i<3;i++)_upcoming[i].Update(i<future.Length?future[i].Event:null,i<future.Length?future[i].Next:null,now,sound);
        var next=rows.FirstOrDefault(r=>r.Event.Enabled&&r.Next is not null&&r.Next.Start>=now.AddMinutes(-1));
        _miniName=next.Next is null?"Yaklaşan boss yok":next.Event.Name;
        var left=next.Next?.Start-now;_miniClock=left is {} t&&t.TotalDays>=1?$"{(int)t.TotalDays}g {t.Hours:00}:{t.Minutes:00}":left is {} rest?Countdown(rest):"—";
    }
    private static string SourceStatus(BossEvent e,DateTimeOffset now)=>e.Live is {Alive:true} live?
        now-live.ObservedAt<TimeSpan.FromSeconds(5)?"Doğmuş · oyundan":"Son kayıt: doğmuş":e.Live is {Estimated:true}?"Tahmin":e.RepeatMinutes>0?"Takvim":e.Live is {SpawnAt:null}?"Süre bekleniyor":e.Live is not null?"Oyun kaydı":e.ClockConfirmed?"Kayıt":"Saat doğrula";
}

internal sealed class UpcomingBossCard:SurfaceCard
{
    private readonly Label _name=UiTheme.Label("Yeni saat bekleniyor",11,null,FontStyle.Bold),_map=UiTheme.Label("—",10,UiTheme.Muted),
        _clock=UiTheme.Label("—",25,UiTheme.Text,FontStyle.Bold),_watch=UiTheme.Label("",9.5f,UiTheme.Muted);
    private readonly ToolTip _tip=new();
    private string? _id;
    internal event Action<string>? Selected;
    internal UpcomingBossCard()
    {
        Padding=new(12);TabStop=true;AccessibleRole=AccessibleRole.PushButton;Cursor=Cursors.Hand;
        var stack=new TableLayoutPanel {Dock=DockStyle.Fill,ColumnCount=1,RowCount=4,BackColor=UiTheme.Surface,Margin=Padding.Empty};
        foreach(var height in new[]{42,22,40,20})stack.RowStyles.Add(new(SizeType.Absolute,height));
        _name.AutoEllipsis=false;stack.Controls.Add(_name,0,0);stack.Controls.Add(_map,0,1);stack.Controls.Add(_clock,0,2);stack.Controls.Add(_watch,0,3);Controls.Add(stack);
        foreach(var c in new Control[]{this,stack,_name,_map,_clock,_watch})c.Click+=(_,_)=>{if(_id is {} id)Selected?.Invoke(id);};
        KeyDown+=(_,e)=>{if(e.KeyCode is Keys.Space or Keys.Enter&&_id is {} id){Selected?.Invoke(id);e.Handled=true;}};
    }
    internal void Update(BossEvent? boss,BossOccurrence? occurrence,DateTimeOffset now,bool sound)
    {
        _id=boss?.Id;_name.Text=boss?.Name??"Yeni saat bekleniyor";_map.Text=boss?.Map??"Oyun boss listesini aç";
        var remaining=occurrence?.Start-now;
        _clock.Text=remaining is {} time?time.TotalDays>=1?$"{(int)time.TotalDays}g {time.Hours:00}:{time.Minutes:00}":$"{(int)time.TotalHours:00}:{time.Minutes:00}:{time.Seconds:00}":"—";
        _clock.ForeColor=boss?.Enabled==true&&remaining?.TotalMinutes<=10?UiTheme.Accent:UiTheme.Text;
        _watch.Text=boss is null?"Canlı kayıt bekleniyor":!boss.Enabled?"○ Uyarı kapalı":sound?"● Uyarı açık":"○ Ses kapalı";_watch.ForeColor=boss?.Enabled==true&&sound?UiTheme.Green:UiTheme.Muted;
        AccessibleName=_name.Text+" · "+_map.Text+" · "+_clock.Text+" · "+_watch.Text;_tip.SetToolTip(_name,boss?.Name);
    }
    protected override void Dispose(bool disposing){if(disposing)_tip.Dispose();base.Dispose(disposing);}
}
