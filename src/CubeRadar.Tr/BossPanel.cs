using System.Diagnostics;
using CubeRadar.Core.Radar;
using CubeRadar.Core.Protocol;

namespace CubeRadar.Tr;

internal sealed partial class BossPanel : UserControl
{
    private readonly BossStore _store;
    private readonly DataGridView _grid=new HudGrid();
    private readonly Label _status=new(),_detail=new();
    private readonly CheckBox _sound=new ToggleSwitch() {Text="Boss sesi",AutoSize=true};
    private readonly ComboBox _zone;
    private readonly TextBox _server=new();
    private long _lastSecond=-1;
    private bool _preview;
    private bool _updatingRows;
    private readonly Dictionary<string,DataGridViewRow> _rows=[];
    private Dictionary<string,BossOccurrence?> _next=[];
    private DateTimeOffset _lastLive,_lastSave;
    private bool _liveDirty;
    private BossEditor? _editor;
    private DateTimeOffset _refreshUntil;
    public event Action<string>? Alert;
    public event Action? SoundTestRequested;
    public event Action? RefreshRequested;
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    internal CheckBox SoundControl=>_sound;
    public string Summary {get;private set;}="Boss takvimi boş";

    public BossPanel(bool preview)
    {
        _preview=preview;_store=new(preview);_zone=ClockZone.Picker(_store.Settings.DisplayZone);
        if(RepairProfile()>0)_store.Save();
        BuildBossLayout();
        _sound.CheckedChanged+=(_,_)=>{_store.Settings.BossSound=_sound.Checked;Save();};
        _zone.SelectedIndexChanged+=(_,_)=>{_store.Settings.DisplayZone=((ClockZone)_zone.SelectedItem!).Id;Save();_lastSecond=-1;};
        _grid.SelectionChanged+=(_,_)=>ShowDetail();Tick(DateTimeOffset.UtcNow,false,evaluateAlerts:false);
        _lastLive=_store.Settings.Events.Where(e=>e.Live is not null).Select(e=>e.Live!.ObservedAt).DefaultIfEmpty().Max();
    }
    private static Button Button(string text,Action action)
    {
        var b=new HudButton {Text=text,Width=124,Height=36,Tone=ButtonTone.Ghost};b.Click+=(_,_)=>action();return b;
    }
    private BossEvent? Selected=>_grid.CurrentRow?.Tag as BossEvent;
    private void ConfigureGrid()
    {
        _grid.Dock=DockStyle.Fill;_grid.AllowUserToAddRows=false;_grid.AllowUserToDeleteRows=false;_grid.RowHeadersVisible=false;
        _grid.SelectionMode=DataGridViewSelectionMode.FullRowSelect;_grid.MultiSelect=false;_grid.AutoSizeColumnsMode=DataGridViewAutoSizeColumnsMode.None;
        _grid.AllowUserToOrderColumns=true;_grid.AllowUserToResizeColumns=true;_grid.AllowUserToResizeRows=false;_grid.ScrollBars=ScrollBars.Both;
        _grid.BackgroundColor=RadarForm.Bg;_grid.BorderStyle=BorderStyle.None;_grid.EnableHeadersVisualStyles=false;
        _grid.ColumnHeadersDefaultCellStyle.BackColor=Color.FromArgb(30,42,54);_grid.ColumnHeadersDefaultCellStyle.ForeColor=RadarForm.Fg;
        _grid.ColumnHeadersDefaultCellStyle.SelectionBackColor=Color.FromArgb(30,42,54);
        _grid.DefaultCellStyle.BackColor=RadarForm.Bg;_grid.DefaultCellStyle.ForeColor=RadarForm.Fg;_grid.DefaultCellStyle.SelectionBackColor=Color.FromArgb(65,57,35);_grid.DefaultCellStyle.SelectionForeColor=Color.White;
        _grid.GridColor=Color.FromArgb(43,63,77);_grid.RowTemplate.Height=42;
        _grid.Columns.Add(new DataGridViewCheckBoxColumn {Name="Alert",HeaderText="Uyarı",Width=56,Frozen=true,SortMode=DataGridViewColumnSortMode.NotSortable});
        foreach(var (name,width) in new[]{("Boss",232),("Harita",154),("Başlangıç",126),("Kalan süre",124),("Durum",168)})
        {
            var index=_grid.Columns.Add(name,name);_grid.Columns[index].Width=width;_grid.Columns[index].ReadOnly=true;
        }
        _grid.Columns[4].DefaultCellStyle.Font=new("Segoe UI",12,FontStyle.Bold);
        _grid.CurrentCellDirtyStateChanged+=(_,_)=>{if(_grid.IsCurrentCellDirty&&_grid.CurrentCell?.OwningColumn?.Name=="Alert")_grid.CommitEdit(DataGridViewDataErrorContexts.Commit);};
        _grid.CellValueChanged+=(_,e)=>
        {
            if(_updatingRows||e.RowIndex<0||e.ColumnIndex!=0||_grid.Rows[e.RowIndex].Tag is not BossEvent boss)return;
            var updated=boss with {Enabled=_grid.Rows[e.RowIndex].Cells[0].Value is true};
            var index=_store.Settings.Events.FindIndex(x=>x.Id==boss.Id);if(index<0)return;
            _store.Settings.Events[index]=updated;_grid.Rows[e.RowIndex].Tag=updated;Save();_lastSecond=-1;
        };
        _grid.SortCompare+=(_,e)=>
        {
            if(e.Column.Index is not (3 or 4))return;
            var a=(_grid.Rows[e.RowIndex1].Tag as BossEvent)?.Id;var b=(_grid.Rows[e.RowIndex2].Tag as BossEvent)?.Id;
            var at=a is null?null:_next.GetValueOrDefault(a)?.Start;var bt=b is null?null:_next.GetValueOrDefault(b)?.Start;
            e.SortResult=(at??DateTimeOffset.MaxValue).CompareTo(bt??DateTimeOffset.MaxValue);e.Handled=true;
        };
        _grid.CellDoubleClick+=(_,e)=>{if(e.RowIndex>=0&&e.ColumnIndex!=0)Edit(Selected);};
    }
    public void Tick(DateTimeOffset now,bool globalSound,bool evaluateAlerts=true)
    {
        if(now.ToUnixTimeSeconds()==_lastSecond)return;_lastSecond=now.ToUnixTimeSeconds();
        var zone=TimeZoneInfo.FindSystemTimeZoneById(_store.Settings.DisplayZone);
        var rows=_store.Settings.Events.Select(e=>(Event:e,Next:BossClock.CurrentOrNext(e,now))).OrderBy(r=>r.Next?.Start??DateTimeOffset.MaxValue).ToList();
        _next=rows.ToDictionary(r=>r.Event.Id,r=>r.Next);
        // Keep row objects, current cell, scroll and the user's sort/column layout across ticks.
        _updatingRows=true;
        var gate=new BossAlertGate(_store.Settings.Delivered);var before=_store.Settings.Delivered.Count;var alerts=new List<string>();
        try
        {
        foreach(var id in _rows.Keys.Where(id=>!_next.ContainsKey(id)).ToArray()){_grid.Rows.Remove(_rows[id]);_rows.Remove(id);}
        foreach(var (e,next) in rows)
        {
            var remaining=StatusOf(e,next,now);
            if(!_rows.TryGetValue(e.Id,out var row))
            {row=_grid.Rows[_grid.Rows.Add()];_rows.Add(e.Id,row);}
            row.Tag=e;
            var urgent=next is not null&&e.Enabled&&(next.Start-now).TotalMinutes is >=-1 and <=10;
            var status=next is not null&&next.Start<=now?"Yeni oyun kaydı bekleniyor":SourceStatus(e,now);
            var clock=e.Live is {Alive:true}?"Doğmuş":next is null?"—":next.Start<=now?"+"+Countdown(now-next.Start):Countdown(next.Start-now);
            object[] values=[e.Enabled,e.Name,e.Map,next is null?"—":TimeZoneInfo.ConvertTime(next.Start,zone).ToString("dd.MM HH:mm"),clock,(urgent?"⚠ ":"")+status];
            for(var i=0;i<values.Length;i++)if(!Equals(row.Cells[i].Value,values[i]))row.Cells[i].Value=values[i];
            row.Cells[0].ToolTipText=e.Enabled?"Bu boss için sesli uyarı açık":"Bu boss için sesli uyarı kapalı";
            row.Cells[1].ToolTipText=e.Name;
            row.Cells[5].ToolTipText=e.Live is {} live?$"Son oyun kaydı: {TimeZoneInfo.ConvertTime(live.ObservedAt,zone):dd.MM HH:mm:ss}\n"+(live.Alive?"Oyun bu bossu doğmuş bildirdi; yeni sayaç kesimden sonra gelir.":"Oyun haritasında bu bossun bulunduğu bölgenin Field Boss listesini aç."):e.Source;
            row.DefaultCellStyle.BackColor=UiTheme.Surface;row.DefaultCellStyle.ForeColor=UiTheme.Text;row.DefaultCellStyle.SelectionBackColor=UiTheme.Raised;
            row.Cells[4].Style.ForeColor=row.Cells[4].Style.SelectionForeColor=urgent?UiTheme.Accent:UiTheme.Text;row.Cells[5].Style.ForeColor=row.Cells[5].Style.SelectionForeColor=e.Live is {Alive:true}?UiTheme.Green:urgent?UiTheme.Accent:UiTheme.Muted;
            if(evaluateAlerts&&next is not null&&gate.Take(next,now,globalSound&&_sound.Checked&&!_preview))alerts.Add(e.Name+" · "+e.Map+" · "+remaining);
        }
        }
        finally {_updatingRows=false;}
        UpdateUpcoming(rows,now,globalSound&&_sound.Checked);
        if(_store.Settings.Delivered.Count!=before)Save();
        if(_liveDirty&&now-_lastSave>TimeSpan.FromSeconds(15))Save();
        if(_store.Error is null&&!_preview)
            _status.Text=(now<_refreshUntil?"Takvim yenilendi. ":"")+(_lastLive==default?"Yeni oyun verisi için Harita > Exploration > Field Boss listesini aç.":$"Son oyun kaydı: {TimeZoneInfo.ConvertTime(_lastLive,zone):HH:mm:ss} · {(_lastLive>now.AddSeconds(-5)?"liste güncelleniyor":"yeni kayıt için oyundaki boss listesini aç")}");
        var first=rows.FirstOrDefault(r=>r.Event.Enabled&&r.Next is not null);
        Summary=first.Next is {} occurrence?$"{first.Event.Name} · {Countdown(occurrence.Start-now)} · {first.Event.Map}":rows.Count==0?"Boss takvimi boş":"Yeni boss saati bekleniyor · oyun listesini aç";
        if(alerts.Count>0)Alert?.Invoke(string.Join("\n",alerts));
        ShowDetail();
    }
    public void Observe(IEnumerable<BossObservation> updates)
    {
        foreach(var update in updates.OrderBy(u=>u.TimeMs))
        {
            var important=false;
            if(update is BossListObservation incoming)
            {
                important=RepairProfile()>0;
                important|=BossProfile.AddMissing(_store.Settings.Events,incoming.List)>0;
                important|=RepairProfile()>0;
            }
            for(var i=0;i<_store.Settings.Events.Count;i++)
            {
                var e=_store.Settings.Events[i];var changed=e;
                if(update is BossListObservation listing&&BossCatalog.Match(e,listing.List.MapId,listing.List.Slots.Count) is {} binding&&listing.List.Slots.FirstOrDefault(s=>s.SlotId==binding.SlotId) is {} slot)
                    changed=BossLiveSync.Listed(e,listing.List,slot);
                else if(update is BossKillObservation kill&&e.Live is {} live&&live.MapId==kill.MapId&&live.SlotId==kill.SlotId)
                    changed=BossLiveSync.Killed(e,kill.TimeMs);
                if(changed==e)continue;
                important|=e.Live is null||e.Live.Alive!=changed.Live!.Alive||e.Live.SpawnAt!=changed.Live.SpawnAt||e.Live.LastKillAt!=changed.Live.LastKillAt||e.Live.RespawnMinutes!=changed.Live.RespawnMinutes;
                _store.Settings.Events[i]=changed;_liveDirty=true;_lastSecond=-1;
            }
            if(update is BossListObservation) _lastLive=DateTimeOffset.FromUnixTimeMilliseconds(update.TimeMs);
            if(important)Save();
        }
    }
    private int RepairProfile()
    {
        var count=0;
        for(var i=0;i<_store.Settings.Events.Count;i++)
        {
            var e=_store.Settings.Events[i];var repaired=BossProfile.Repair(e,_store.Settings.ServerName);
            if(repaired==e)continue;_store.Settings.Events[i]=repaired;count++;
        }
        return count;
    }
    internal void RefreshClocks(DateTimeOffset now,bool sound)
    {
        if(RepairProfile()>0)Save();_lastSecond=-1;_refreshUntil=now.AddSeconds(10);Tick(now,sound);
    }
    private static string StatusOf(BossEvent e,BossOccurrence? next,DateTimeOffset now)
    {
        if(e.Live is {Alive:true} live)return now-live.ObservedAt<TimeSpan.FromSeconds(5)?"CANLI · oyundan":"Son kayıt: canlı · yenile";
        if(next is null)return e.Live is not null?"Yeni süre bekleniyor":e.ClockConfirmed?"Saat girilmedi / bitti":"Saat doğrula";
        if(next.Start<=now)return "SAATİ GELDİ · yenile";
        return Countdown(next.Start-now)+(e.Live is {Estimated:true}?" · tahmin":e.RepeatMinutes>0&&e.Live?.SpawnAt is null?" · takvim":e.Live is not null?" · oyun":" · kayıt");
    }
    private void SetAllAlerts(bool enabled)
    {
        _store.Settings.Events=_store.Settings.Events.Select(e=>e with {Enabled=enabled}).ToList();Save();_lastSecond=-1;
    }
    private static string Countdown(TimeSpan time)=>time<=TimeSpan.Zero?"saati geldi":time.TotalDays>=1?$"{(int)time.TotalDays}g {time.Hours:00}:{time.Minutes:00}:{time.Seconds:00}":$"{(int)time.TotalHours:00}:{time.Minutes:00}:{time.Seconds:00}";
    private void ShowDetail()
    {
        if(Selected is not {} e){_detail.Text="Boss adı, harita ve saat ekleyebilirsin.\nOyun haritasındaki kalan süreyi tek seferlik saat olarak kaydetmek de mümkün.";return;}
        var next=BossClock.CurrentOrNext(e,DateTimeOffset.UtcNow);
        var liveText=e.Live is {} live?$"Son oyun verisi: {TimeZoneInfo.ConvertTime(live.ObservedAt,TimeZoneInfo.FindSystemTimeZoneById(_store.Settings.DisplayZone)):dd.MM HH:mm:ss} · {(live.Estimated?"kesimden tahmin":"oyun listesi")}\n":"";
        _detail.Text=$"Konum: {e.Location}\n"+liveText+$"Saat: {(next is null?"yeni kayıt bekleniyor":TimeZoneInfo.ConvertTime(next.Start,TimeZoneInfo.FindSystemTimeZoneById(e.ZoneId)).ToString("dd.MM HH:mm zzz"))}\nKaynak: {(e.Live is not null&&e.RepeatMinutes==0?"Oyunun kendi boss listesi; yakın kesim için ölçülmüş aralık varsa tahmin kullanılır.":e.Source)}";
    }
    private void Edit(BossEvent? e)
    {
        if(_editor is {IsDisposed:false} open)
        {open.TopMost=FindForm()?.TopMost??false;open.WindowState=FormWindowState.Normal;open.BringToFront();if(!_preview)open.Activate();return;}
        var editor=new BossEditor(e,_preview) {TopMost=FindForm()?.TopMost??false,StartPosition=_preview?FormStartPosition.Manual:FormStartPosition.CenterScreen};_editor=editor;
        if(_preview){editor.Location=new(-20000,-20000);editor.ShowInTaskbar=false;}
        editor.FormClosed+=(_,_)=>
        {
            if(editor.DialogResult==DialogResult.OK&&editor.Result is {} updated)
            {
                // Keep game records received while the editor was open; form fields cannot overwrite newer live data.
                var latest=_store.Settings.Events.FirstOrDefault(x=>x.Id==updated.Id);
                if(latest is not null)updated=updated with {Live=latest.Live};
                _store.Settings.Events.RemoveAll(x=>x.Id==updated.Id);_store.Settings.Events.Add(updated);Save();_lastSecond=-1;
            }
            _editor=null;
        };
        editor.Show();if(!_preview){editor.BringToFront();editor.Activate();}
    }
    private void Remove()
    {
        if(Selected is not {} e)return;
        if(DialogWindows.Message(this,e.Name+" takvimden kaldırılsın mı?","Kaydı sil",MessageBoxButtons.YesNo)!=DialogResult.Yes)return;
        _store.Settings.Events.Remove(e);Save();_lastSecond=-1;
    }
    private void AddPresets()
    {
        using var dialog=new Form {Text="Global boss saat şablonları",ClientSize=new(520,250),MinimumSize=new(520,250),
            BackColor=UiTheme.Background,ForeColor=UiTheme.Text,Font=new("Segoe UI",10),Padding=new(16),
            AutoScaleMode=AutoScaleMode.Dpi,AutoScaleDimensions=new(96,96),MinimizeBox=false,MaximizeBox=false};
        var text=UiTheme.Label("Kaynak: AION2 Hub Global istemci verisi (19.09.2026).\nSunucu saatini oyundan doğrula. Şablonlar pasif eklenir;\nDüzenle > saat dilimi ve saat doğrulaması ile açılır.\nBu şablon KR/TW takvimi değildir.");text.Dock=DockStyle.Top;text.Height=116;
        var zone=ClockZone.Picker("Tokyo Standard Time");zone.Dock=DockStyle.Top;zone.AccessibleName="Şablon saat dilimi";UiTheme.Input(zone);
        var add=Button("Pasif şablonları ekle",()=>{dialog.DialogResult=DialogResult.OK;dialog.Close();});add.Dock=DockStyle.Bottom;
        var cancel=Button("Vazgeç",()=>{dialog.DialogResult=DialogResult.Cancel;dialog.Close();});
        var actions=new FlowLayoutPanel {Dock=DockStyle.Bottom,Height=42,FlowDirection=FlowDirection.RightToLeft,WrapContents=false};
        add.Dock=DockStyle.None;add.Width=200;cancel.Width=100;actions.Controls.Add(add);actions.Controls.Add(cancel);
        dialog.AcceptButton=add;dialog.CancelButton=cancel;
        dialog.Controls.Add(zone);dialog.Controls.Add(text);dialog.Controls.Add(actions);
        if(DialogWindows.Show(dialog,this)!=DialogResult.OK)return;
        foreach(var e in BossStore.GlobalPresets(((ClockZone)zone.SelectedItem!).Id))
            if(!_store.Settings.Events.Any(x=>x.Name==e.Name&&x.Map==e.Map))_store.Settings.Events.Add(e);
        Save();_lastSecond=-1;
    }
    private void OpenMap()
    {
        if(Selected is not {} e||!Uri.TryCreate(e.MapUrl,UriKind.Absolute,out var uri)||uri.Scheme!="https")return;
        try{Process.Start(new ProcessStartInfo(uri.AbsoluteUri){UseShellExecute=true});}catch(Exception ex)when(ex is System.ComponentModel.Win32Exception or InvalidOperationException){DialogWindows.Message(this,ex.Message,"Harita açılamadı");}
    }
    internal void UpdateEditorTopMost(bool topmost)
    {
        if(_editor is {IsDisposed:false} editor)editor.TopMost=topmost;
    }
    private void Save(){if(!_store.Save())_status.Text=_store.Error;else {_lastSave=DateTimeOffset.UtcNow;_liveDirty=false;}}
    internal void Preview()
    {
        _preview=true;_store.Settings.Events=[
            new BossEvent {Name="ÖRNEK · Drakan Battalion Weapon Guruta",Map="Altgard",Location="Örnek harita noktası · canlı veri değil",OnceAt=DateTimeOffset.UtcNow.AddSeconds(58),Source="Tasarım önizlemesi"},
            new BossEvent {Name="ÖRNEK · Watcher Kaira",Map="Alt Reshanta",Location="Örnek harita noktası · canlı veri değil",OnceAt=DateTimeOffset.UtcNow.AddMinutes(54).AddSeconds(35),Source="Tasarım önizlemesi"},
            new BossEvent {Name="ÖRNEK · Veteran Shujakan",Map="Altgard",Location="Oyun haritasındaki konum",Enabled=false,OnceAt=DateTimeOffset.UtcNow.AddMinutes(54).AddSeconds(51),Source="Tasarım önizlemesi"}];
        _server.Text="ÖRNEK · BABA / Asmodian";_profile.Text=_server.Text;_status.Text="ÖRNEK TAKVİM · canlı doğma bilgisi değil · önizlemede ses kapalı";_lastSecond=-1;Tick(DateTimeOffset.UtcNow,false);
    }
    internal void VerifyGridInteraction()
    {
        if(!_preview)throw new InvalidOperationException("Yalnız önizleme doğrulaması.");
        var original=_store.Settings.Events;
        var now=DateTimeOffset.UtcNow;
        _store.Settings.Events=Enumerable.Range(0,40).Select(i=>new BossEvent {Id="proof-"+i,Name=i==25?"Immortal Gartua":$"ÖRNEK boss {i:00}",Map=i%2==0?"Altgard":"Abyss",OnceAt=now.AddMinutes(20+i)}).ToList();
        _lastSecond=-1;Tick(now,false);
        var row=_rows["proof-25"];_grid.CurrentCell=row.Cells[2];
        _grid.FirstDisplayedScrollingRowIndex=20;_grid.Columns[1].Width=245;_grid.Columns[2].DisplayIndex=4;
        var first=_grid.FirstDisplayedScrollingRowIndex;var cell=_grid.CurrentCell;
        row.Cells[0].Value=false;
        Tick(now.AddSeconds(1),false);Tick(now.AddSeconds(2),false);
        Observe([new BossListObservation(new(1110,now.AddSeconds(3).ToUnixTimeMilliseconds(),Enumerable.Range(1,24).Select(i=>new FieldBossSlot(111000+i,false,now.AddHours(12).ToUnixTimeMilliseconds())).ToArray()))]);
        Tick(now.AddSeconds(3),false);
        if(!ReferenceEquals(row,_rows["proof-25"])||!ReferenceEquals(cell,_grid.CurrentCell)||first!=_grid.FirstDisplayedScrollingRowIndex||_grid.Columns[1].Width!=245||_grid.Columns[2].DisplayIndex!=4||_store.Settings.Events.Single(e=>e.Id=="proof-25").Enabled)
            throw new InvalidOperationException("Boss listesi kaydırma/seçim/sütun/uyarı doğrulaması başarısız.");
        if(_store.Settings.Events.Single(e=>e.Id=="proof-25").Live?.SpawnAt?.ToUnixTimeMilliseconds()!=now.AddHours(12).ToUnixTimeMilliseconds())throw new InvalidOperationException("Canlı boss saati arayüze aktarılmadı.");
        row.Cells[0].Value=true;
        if(!_store.Settings.Events.Single(e=>e.Id=="proof-25").Enabled)throw new InvalidOperationException("Boss uyarı kutusu çalışmadı.");
        _store.Settings.Events=original;_grid.Columns[1].Width=232;_grid.Columns[2].DisplayIndex=2;_lastSecond=-1;Tick(now,false);
    }
    internal void VerifyEditorInteraction()
    {
        if(!_preview)throw new InvalidOperationException("Yalnız önizleme doğrulaması.");
        var original=_store.Settings.Events;var form=FindForm()!;var topmost=form.TopMost;var now=DateTimeOffset.UtcNow;
        var e=new BossEvent {Id="editor-proof",Name="Immortal Gartua",Map="Altgard",OnceAt=now.AddHours(1)};
        _store.Settings.Events=[e];form.TopMost=true;
        try
        {
            Edit(e);Application.DoEvents();var editor=_editor??throw new InvalidOperationException("Düzenleme penceresi açılmadı.");
            if(editor.Modal||editor.Owner is not null||!editor.TopMost||!editor.Visible||!form.Enabled)
                throw new InvalidOperationException("Düzenleme penceresi bağımsız ve önde açılmadı.");
            UpdateEditorTopMost(false);if(editor.TopMost)throw new InvalidOperationException("Düzenleme üstte tut kapatılamadı.");
            UpdateEditorTopMost(true);if(!editor.TopMost)throw new InvalidOperationException("Düzenleme üstte tut güncellenmedi.");
            form.WindowState=FormWindowState.Minimized;Application.DoEvents();
            if(!editor.Visible)throw new InvalidOperationException("Ana pencere küçültülünce düzenleme penceresi gizlendi.");
            form.WindowState=FormWindowState.Normal;Application.DoEvents();
            Observe([new BossListObservation(new(1110,now.ToUnixTimeMilliseconds(),Enumerable.Range(1,24).Select(i=>new FieldBossSlot(111000+i,false,now.AddHours(12).ToUnixTimeMilliseconds())).ToArray()))]);
            ((Button)editor.AcceptButton!).PerformClick();Application.DoEvents();
            if(_editor is not null||_store.Settings.Events.Single(x=>x.Id==e.Id).Live?.SpawnAt?.ToUnixTimeMilliseconds()!=now.AddHours(12).ToUnixTimeMilliseconds())
                throw new InvalidOperationException("Kaydet, açık pencere sırasında gelen canlı boss kaydını ezdi.");
        }
        finally{_editor?.Dispose();_editor=null;form.TopMost=topmost;_store.Settings.Events=original;_lastSecond=-1;Tick(now,false);}
    }
    internal void VerifyPresetDialogInteraction()
    {
        if(!_preview)throw new InvalidOperationException("Yalnız önizleme doğrulaması.");
        var owner=FindForm()!;var wasTop=owner.TopMost;var original=_store.Settings.Events.ToArray();
        using var peer=new Form {Text="ÖRNEK · mini overlay",TopMost=true,ShowInTaskbar=false,Location=new(-20000,-20000),StartPosition=FormStartPosition.Manual};peer.Show();
        try
        {
            foreach(var topmost in new[]{true,false})
            {
                owner.TopMost=topmost;Exception? failure=null;var observed=false;
                using var timer=new System.Windows.Forms.Timer {Interval=20};
                timer.Tick+=(_,_)=>
                {
                    var dialog=Application.OpenForms.Cast<Form>().FirstOrDefault(f=>f.Text=="Global boss saat şablonları");
                    if(dialog is null)return;timer.Stop();observed=true;
                    try
                    {
                        if(!dialog.Modal||NativeEnabled(owner.Handle)||dialog.TopMost!=topmost||NativeOwner(dialog.Handle,4)!=owner.Handle||peer.TopMost)
                            throw new InvalidOperationException($"Hazır liste öncelik hatası: modal={dialog.Modal}, nativeOwnerEnabled={NativeEnabled(owner.Handle)}, topmost={dialog.TopMost}/{topmost}, nativeOwner={NativeOwner(dialog.Handle,4)}/{owner.Handle}, peerTopmost={peer.TopMost}.");
                        var picker=dialog.Controls.OfType<ComboBox>().Single();
                        if(!picker.Enabled||!picker.CanSelect)throw new InvalidOperationException("Hazır liste saat seçimi kullanılamıyor.");
                        picker.SelectedIndex=0;
                    }
                    catch(Exception ex){failure=ex;}
                    finally{((Button)dialog.CancelButton!).PerformClick();}
                };
                timer.Start();AddPresets();
                if(failure is not null)throw failure;
                if(!observed||!owner.Enabled||!peer.TopMost||!original.SequenceEqual(_store.Settings.Events))
                    throw new InvalidOperationException("Hazır liste iptalinden sonra pencere/tercih/takvim durumu korunmadı.");
                using var fileDialog=new OwnerProbeDialog(handle=>
                {
                    if(handle!=owner.Handle||peer.TopMost)throw new InvalidOperationException("Dosya penceresi owner veya overlay önceliği hatalı.");
                });
                DialogWindows.Show(fileDialog,this);
                if(!fileDialog.Invoked||!peer.TopMost)throw new InvalidOperationException("Dosya penceresi kapandıktan sonra üstte tut geri yüklenmedi.");
            }
        }
        finally{owner.TopMost=wasTop;}
    }
    [System.Runtime.InteropServices.DllImport("user32.dll",EntryPoint="GetWindow")]
    private static extern IntPtr NativeOwner(IntPtr window,uint command);
    [System.Runtime.InteropServices.DllImport("user32.dll",EntryPoint="IsWindowEnabled")]
    [return:System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.Bool)]
    private static extern bool NativeEnabled(IntPtr window);
    private sealed class OwnerProbeDialog(Action<IntPtr> inspect) : CommonDialog
    {
        public bool Invoked {get;private set;}
        public override void Reset() { }
        protected override bool RunDialog(IntPtr hwndOwner){Invoked=true;inspect(hwndOwner);return false;}
    }
    internal void VerifyFreshInstallRefresh()
    {
        if(!_preview)throw new InvalidOperationException("Yalnız önizleme doğrulaması.");
        var original=_store.Settings.Events;var delivered=_store.Settings.Delivered;var server=_store.Settings.ServerName;var now=DateTimeOffset.UtcNow;
        _store.Settings.Events=BossStore.GlobalPresets("W. Europe Standard Time");_store.Settings.ServerName="EU · BABA · Asmodian";
        var kaira=_store.Settings.Events.Single(e=>e.Name=="Watcher Kaira");var id=kaira.Id;
        _store.Settings.Events[_store.Settings.Events.IndexOf(kaira)]=kaira with {Enabled=false,ClockConfirmed=true,Live=new(20,2002,now,false,null)};
        _store.Settings.Delivered=["retain-existing-alert-history"];
        try
        {
            _refreshButton.PerformClick();
            var repaired=_store.Settings.Events.Single(e=>e.Id==id);
            if(repaired.RepeatMinutes!=180||repaired.OnceAt is null||repaired.Enabled||!_store.Settings.Delivered.Contains("retain-existing-alert-history")||BossClock.CurrentOrNext(repaired,now) is null)
                throw new InvalidOperationException("İlk kurulumdaki rastgele kimlikli Kaira kaydı Yenile ile onarılmadı.");
            Observe([new BossListObservation(new(20,now.ToUnixTimeMilliseconds(),Enumerable.Range(1,8).Select(i=>new FieldBossSlot(2000+i,false,i==2?0:now.AddHours(12).ToUnixTimeMilliseconds())).ToArray()))]);
            _lastSecond=-1;Tick(now,false);var row=_rows[id];_grid.CurrentCell=row.Cells[2];var cell=_grid.CurrentCell;
            _refreshButton.PerformClick();
            if(_store.Settings.Events.Count!=8||_store.Settings.Events.Count(e=>e.Name.StartsWith("Guardian Lord Nahma",StringComparison.Ordinal))!=3||!ReferenceEquals(row,_rows[id])||!ReferenceEquals(cell,_grid.CurrentCell)||_store.Settings.Events.Single(e=>e.Id==id).Enabled)
                throw new InvalidOperationException("Yenile canlı listeyi/seçimi bozdu veya eksik Abyss kayıtları tamamlanmadı.");
        }
        finally{_store.Settings.Events=original;_store.Settings.Delivered=delivered;_store.Settings.ServerName=server;_lastSecond=-1;Tick(now,false);}
    }
    protected override void Dispose(bool disposing)
    {
        if(disposing){_editor?.Dispose();if(_liveDirty&&!_preview)Save();}base.Dispose(disposing);
    }
}
