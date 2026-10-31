using CubeRadar.Core.Pets;
using CubeRadar.Core.Radar;
using Orientation=CubeRadar.Core.Radar.Orientation;

namespace CubeRadar.Tr;

internal sealed class PetPanel:UserControl
{
    readonly bool _preview;
    readonly PetPreferencesStore _store;
    readonly PetGoalGate _gate=new();
    readonly HudGrid _grid=new();
    readonly Dictionary<int,DataGridViewRow> _rows=[];
    readonly HashSet<int> _farmable=PetCatalog.Default.Sources.Select(s=>s.PetId).ToHashSet();
    readonly TextBox _search=new() {PlaceholderText="Pet veya yaratık ara…",AccessibleName="Pet veya yaratık ara"};
    readonly ComboBox _view=new() {DropDownStyle=ComboBoxStyle.DropDownList,AccessibleName="Pet listesi kapsamı"};
    readonly ComboBox _goal=new() {DropDownStyle=ComboBoxStyle.DropDownList,AccessibleName="Seçili pet için hedef seviye"};
    readonly ToggleSwitch _hide=new() {Text="Tamamlananları gizle"};
    readonly Label _status=UiTheme.Label("Pet koleksiyonu bekleniyor",10,UiTheme.Muted);
    readonly Label _summary=UiTheme.Label("—",17,UiTheme.Text,FontStyle.Bold);
    readonly Label _detail=UiTheme.Label("İzlemek istediğin peti işaretle.",10,UiTheme.Muted);
    PetSnapshot? _snapshot;FixView? _self;Orientation _axes;double? _heading;long _now,_nextRender;
    bool _updating;
    string? _completion;long _completionUntil;
    public event Action? StartRequested;
    public event Action? MiniRequested;
    public event Action<string,bool>? Completed;
    internal PetPanel(bool preview)
    {
        _preview=preview;_store=new(preview);Dock=DockStyle.Fill;BackColor=UiTheme.Background;
        var layout=new TableLayoutPanel {Dock=DockStyle.Fill,ColumnCount=1,RowCount=7,Margin=Padding.Empty};
        foreach(var height in new[]{34,76,48,0,74,48,50})layout.RowStyles.Add(height==0?new(SizeType.Percent,100):new(SizeType.Absolute,height));
        Controls.Add(layout);var title=new TableLayoutPanel {Dock=DockStyle.Fill,ColumnCount=2,Margin=Padding.Empty};title.ColumnStyles.Add(new(SizeType.Percent,100));title.ColumnStyles.Add(new(SizeType.Absolute,126));
        title.Controls.Add(UiTheme.Label("PET AVI",11,UiTheme.Muted,FontStyle.Bold),0,0);var mini=new HudButton {Text="↗ Mini pet",Dock=DockStyle.Fill,Tone=ButtonTone.Ghost,AccessibleName="Mini pet sayacını aç"};mini.Click+=(_,_)=>MiniRequested?.Invoke();title.Controls.Add(mini,1,0);layout.Controls.Add(title,0,0);
        var card=new SurfaceCard {Dock=DockStyle.Fill,Margin=new(0,0,0,12)};_summary.Dock=DockStyle.Fill;card.Controls.Add(_summary);layout.Controls.Add(card,0,1);
        var filter=new TableLayoutPanel {Dock=DockStyle.Fill,ColumnCount=2,Margin=Padding.Empty};filter.ColumnStyles.Add(new(SizeType.Percent,100));filter.ColumnStyles.Add(new(SizeType.Absolute,200));
        _search.Dock=_view.Dock=DockStyle.Fill;UiTheme.Input(_search);UiTheme.Input(_view);_view.Items.AddRange(["Yakındakiler","Yakındaki takiplerim","Tüm pet kaynakları","Eksik ruhlar (MAX)"]);_view.SelectedIndex=_store.Value.View;
        filter.Controls.Add(_search,0,0);filter.Controls.Add(_view,1,0);layout.Controls.Add(filter,0,2);
        ConfigureGrid();layout.Controls.Add(_grid,0,3);_detail.Dock=DockStyle.Fill;_detail.Padding=new(6,10,0,0);layout.Controls.Add(_detail,0,4);
        var actions=new FlowLayoutPanel {Dock=DockStyle.Fill,WrapContents=false};
        var start=new HudButton {Text="Takibi başlat",Width=130,Tone=ButtonTone.Primary,AccessibleName="Pet ve radar takibini başlat"};start.Click+=(_,_)=>StartRequested?.Invoke();
        var goalLabel=UiTheme.Label("Seçili hedef",10,UiTheme.Muted);goalLabel.Dock=DockStyle.None;goalLabel.Size=new(86,34);goalLabel.TextAlign=ContentAlignment.MiddleLeft;
        _goal.Items.AddRange(["Seviye 1","Seviye 2","Seviye 3 / MAX"]);_goal.Width=145;UiTheme.Input(_goal);_goal.Enabled=false;
        foreach(var combo in new[]{_goal,_view})combo.FontChanged+=(_,_)=>combo.ItemHeight=Math.Max(22,combo.Font.Height+6);
        _hide.Checked=_store.Value.HideCompleted;actions.Controls.AddRange([start,goalLabel,_goal,_hide]);layout.Controls.Add(actions,0,5);
        _status.Dock=DockStyle.Fill;_status.Padding=new(4,6,0,0);layout.Controls.Add(_status,0,6);
        _search.TextChanged+=(_,_)=>Render();_view.SelectedIndexChanged+=(_,_)=>{_store.Value.View=_view.SelectedIndex;Save();Render();};
        _hide.CheckedChanged+=(_,_)=>{_store.Value.HideCompleted=_hide.Checked;Save();Render();};
        _goal.SelectedIndexChanged+=(_,_)=>
        {
            if(_updating||Selected is not {} id||_goal.SelectedIndex<0||!_store.Value.Goals.ContainsKey(id))return;
            _store.Value.Goals[id]=_goal.SelectedIndex+1;Save();BaselineGoals();Render();
        };
        VisibleChanged+=(_,_)=>{if(Visible)Render();};
    }
    int? Selected=>_grid.CurrentRow?.Tag is int id?id:null;
    void ConfigureGrid()
    {
        UiTheme.Grid(_grid);_grid.Dock=DockStyle.Fill;_grid.ReadOnly=false;_grid.AllowUserToAddRows=false;_grid.AllowUserToDeleteRows=false;_grid.AutoGenerateColumns=false;
        _grid.Columns.Add(new DataGridViewCheckBoxColumn {Name="Track",HeaderText="Takip",Width=64,Frozen=true,SortMode=DataGridViewColumnSortMode.NotSortable});
        foreach(var (name,title,width) in new[]{("Pet","Pet / Kaynak",140),("Progress","Ruh",106),("Remaining","Kalan",80),("Nearby","Yakında",72),("Direction","Mesafe / Yön",144)})
            _grid.Columns.Add(new DataGridViewTextBoxColumn {Name=name,HeaderText=title,MinimumWidth=width,FillWeight=width,AutoSizeMode=DataGridViewAutoSizeColumnMode.Fill,ReadOnly=true,SortMode=DataGridViewColumnSortMode.Automatic});
        _grid.CurrentCellDirtyStateChanged+=(_,_)=>{if(_grid.IsCurrentCellDirty)_grid.CommitEdit(DataGridViewDataErrorContexts.Commit);};
        _grid.CellValueChanged+=(_,e)=>
        {
            if(_updating||e.RowIndex<0||e.ColumnIndex!=0||_grid.Rows[e.RowIndex].Tag is not int id)return;
            if(_grid.Rows[e.RowIndex].Cells[0].Value is true)
            {
                _store.Value.ExcludedPets.Remove(id);
                var rank=_snapshot?.Progress.GetValueOrDefault(id)?.Rank;
                _store.Value.Goals[id]=Math.Clamp((rank??2)+1,1,3);
            }
            else {_store.Value.Goals.Remove(id);_store.Value.ExcludedPets.Add(id);}
            Save();BaselineGoals();_nextRender=0;ShowDetail();
        };
        _grid.SelectionChanged+=(_,_)=>ShowDetail();
    }
    void Save(){_store.Save();if(_store.Error is {} error)_status.Text=error;}
    internal void Untrack(int id)
    {
        if(!PetCatalog.Default.Pets.ContainsKey(id))return;
        _store.Value.Goals.Remove(id);_store.Value.ExcludedPets.Add(id);Save();BaselineGoals();_nextRender=0;
    }
    internal PetPreferences MiniPreferences=>_store.Value;
    internal void SaveMiniBounds(Rectangle bounds,bool topmost,int opacity,bool autoFit)
    {
        _store.Value.MiniLeft=bounds.Left;_store.Value.MiniTop=bounds.Top;_store.Value.MiniWidth=Math.Clamp(bounds.Width,240,1000);_store.Value.MiniHeight=Math.Clamp(bounds.Height,80,1200);_store.Value.MiniTopMost=topmost;
        _store.Value.MiniOpacity=opacity;_store.Value.MiniAutoFit=autoFit;_store.Value.MiniLayoutVersion=1;Save();
    }
    internal void UpdateMini(MiniPetForm mini,bool force=false)
    {if(_snapshot is {} snapshot)mini.UpdateState(snapshot,_store.Value.Goals,id=>Direction(snapshot.Nearby.Where(m=>m.PetId==id).ToArray()),_now,force,_store.Value.HideCompleted);}
    void BaselineGoals(){if(_snapshot is not null)_gate.Take(_snapshot,_store.Value.Goals);}
    internal void Tick(PetSnapshot snapshot,FixView? self,Orientation axes,double? heading,long now,bool sound,bool force=false)
    {
        snapshot=snapshot with {Nearby=snapshot.Nearby.Where(m=>IsNearby(m,self,now)).ToArray()};
        _snapshot=snapshot;_self=self;_axes=axes;_heading=heading;_now=now;
        if(PetTrackingPlan.Update(_store.Value.Goals,_store.Value.ExcludedPets,snapshot)){Save();_nextRender=0;}
        var complete=_gate.Take(snapshot,_store.Value.Goals);
        if(complete.Count>0&&!_preview)
        {
            _completion=string.Join(" · ",complete.Select(id=>PetCatalog.Default.Pets[id].Name+" hedefi için ruh hazır"));
            _completionUntil=now+20000;Completed?.Invoke(_completion,sound);
        }
        if(!force&&(!Visible||now<_nextRender))return;_nextRender=now+500;Render();
    }
    static bool IsNearby(PetMob mob,FixView? self,long now)
    {
        if(now<mob.SeenMs||now-mob.SeenMs>90000)return false;
        if(self is {} origin&&mob.X is {} x&&mob.Y is {} y&&mob.Z is {} z)
        {
            var dx=(double)x-origin.X;var dy=(double)y-origin.Y;var dz=(double)z-origin.Z;
            return double.IsFinite(dx)&&double.IsFinite(dy)&&double.IsFinite(dz)&&dx*dx+dy*dy+dz*dz<=15000d*15000d;
        }
        // Without both positions, only a recent server sighting can support presence.
        return now-mob.SeenMs<=10000;
    }
    void Render()
    {
        if(_snapshot is not {} snapshot)return;
        var query=_search.Text.Trim();var mobs=snapshot.Nearby.GroupBy(m=>m.PetId).ToDictionary(g=>g.Key,g=>g.ToArray());
        var shown=PetCatalog.Default.Pets.Values.Where(p=>
            (_farmable.Contains(p.Id)||_store.Value.Goals.ContainsKey(p.Id))&&
            (_view.SelectedIndex!=0||mobs.ContainsKey(p.Id))&&(_view.SelectedIndex!=1||mobs.ContainsKey(p.Id)&&_store.Value.Goals.ContainsKey(p.Id))&&
            (_view.SelectedIndex!=3||PetTrackingPlan.HasMissingSouls(snapshot.Progress.GetValueOrDefault(p.Id)))&&
            (!_hide.Checked||!PetGoalProgression.IsMaxed(snapshot.Progress.GetValueOrDefault(p.Id)))&&
            (query.Length==0||p.Name.Contains(query,StringComparison.OrdinalIgnoreCase)||PetCatalog.Default.Sources.Any(s=>s.PetId==p.Id&&s.Name.Contains(query,StringComparison.OrdinalIgnoreCase))))
            .OrderByDescending(p=>_store.Value.Goals.ContainsKey(p.Id)).ThenBy(p=>p.Name).ToArray();
        var ids=shown.Select(p=>p.Id).ToHashSet();_updating=true;
        try
        {
            foreach(var id in _rows.Keys.Where(id=>!ids.Contains(id)).ToArray()){_grid.Rows.Remove(_rows[id]);_rows.Remove(id);}
            foreach(var pet in shown)
            {
                var p=snapshot.Progress.GetValueOrDefault(pet.Id);var goal=_store.Value.Goals.GetValueOrDefault(pet.Id,Math.Clamp((p?.Rank??2)+1,1,3));var remaining=p?.Remaining(goal);
                if(!_rows.TryGetValue(pet.Id,out var row)){row=_grid.Rows[_grid.Rows.Add()];row.Tag=pet.Id;_rows.Add(pet.Id,row);}
                var nearby=mobs.GetValueOrDefault(pet.Id)??[];
                var progress=p is {Verified:true,Rank:{} rank,Souls:{} souls}?rank==3?"MAX":$"Sv.{rank} · {souls}/{PetCatalog.Cost(rank)}":"Eşitleme bekleniyor";
                var name=pet.Name+(nearby.FirstOrDefault() is {} mob&&mob.Name!=pet.Name?"\n"+mob.Name:"");
                row.Height=UiTheme.Px(this,52);row.Cells[1].Style.WrapMode=DataGridViewTriState.True;
                object[] values=[_store.Value.Goals.ContainsKey(pet.Id),name,progress,remaining is {} left?left==0?"Hazır":$"{left} ruh":"—",nearby.Length>0?$"{nearby.Length} yaratık":"—",Direction(nearby)];
                for(var i=0;i<values.Length;i++)if(!Equals(row.Cells[i].Value,values[i]))row.Cells[i].Value=values[i];
                row.Cells[1].ToolTipText=pet.Name+" · "+pet.Family+"\n"+string.Join(", ",nearby.Select(m=>m.Name).Distinct());
                row.Cells[0].ToolTipText=_store.Value.ExcludedPets.Contains(pet.Id)?"Takipten çıkarıldı; tekrar işaretleyene kadar otomatik eklenmez.":"Yakındaki eksik petler otomatik eklenir. İşareti kaldırarak takipten çıkar.";
                row.Cells[3].ToolTipText=$"Hedef: Seviye {goal}";
                row.Cells[3].Style.ForeColor=remaining==0?UiTheme.Green:UiTheme.Accent;
            }
        }
        finally{_updating=false;}
        _summary.Text=$"150 m içinde {snapshot.Nearby.Count} yaratık  ·  {_store.Value.Goals.Keys.Count(mobs.ContainsKey)} yakın takip";
        _status.ForeColor=_now<_completionUntil?UiTheme.Accent:UiTheme.Muted;
        _status.Text=_store.Error??(_now<_completionUntil?_completion:_preview?"ÖRNEK · canlı pet verisi değil":snapshot.Status)+(_rows.Count==0?"\nListe boşsa Tüm pet kaynakları görünümünü seç.":"");
        ShowDetail();
    }
    string Direction(PetMob[] mobs)
    {
        if(_self is not {} self||_now<self.TimeMs||_now-self.TimeMs>2500)return "Konum bekleniyor";
        var mob=mobs.Where(m=>m.X is not null&&_now>=m.PositionMs&&_now-m.PositionMs<=15000).MinBy(m=>RadarMath.CalculateDistance(m.X!.Value-self.X,m.Y!.Value-self.Y));
        if(mob is null)return mobs.Length>0?"Son görüldü":"—";
        var dx=mob.X!.Value-self.X;var dy=mob.Y!.Value-self.Y;var distance=RadarMath.CalculateDistance(dx,dy);
        var direction=_heading is {} heading?RadarMath.GetRelativeDirection(RadarMath.CalculateRelativeBearing(_axes.Bearing(self.X,self.Y,mob.X.Value,mob.Y.Value),heading)):"Yön bekleniyor";
        return $"{distance:0} m · {direction}";
    }
    void ShowDetail()
    {
        if(_updating)return;_updating=true;
        try
        {
            _goal.Enabled=Selected is {} selected&&_store.Value.Goals.ContainsKey(selected);
            if(Selected is not {} id){_detail.Text="Takip kutusunu işaretle, sonra hedef seviyeyi seç. Sayaç ruh kazanımını izler.";return;}
            var pet=PetCatalog.Default.Pets[id];var p=_snapshot?.Progress.GetValueOrDefault(id);
            _goal.SelectedIndex=_store.Value.Goals.GetValueOrDefault(id,Math.Clamp((p?.Rank??2)+1,1,3))-1;
            var sources=PetCatalog.Default.Sources.Where(s=>s.PetId==id).Select(s=>s.Name).Distinct().Take(5).ToArray();
            _detail.Text=$"{pet.Name} · {pet.Family} · Hedef: Seviye {_goal.SelectedIndex+1}\nKaynak: {(sources.Length>0?string.Join(", ",sources):"Canlı yaratık kaynağı bulunamadı")}\n"+
                $"Bu oturum: {(_snapshot?.CombatKnown is true?(p?.SessionKills??0).ToString():"—")} kesim · +{p?.SessionSouls??0} ruh. Yakın liste: 150 m.";
        }
        finally{_updating=false;}
    }
    internal void Preview()
    {
        var ids=PetCatalog.Default.Sources.Select(s=>s.PetId).Distinct().Take(3).ToArray();var now=DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        _store.Value.ExcludedPets.Clear();_store.Value.ExcludedPets.UnionWith(ids.Skip(1));
        _store.Value.Goals[ids[0]]=2;_view.SelectedIndex=0;_hide.Checked=false;
        var progress=ids.Select((id,i)=>(id,p:new PetProgress(1,13+i,true,SessionSouls:i==0?3:0,SessionKills:i==0?6:0))).ToDictionary(x=>x.id,x=>x.p);
        var mobs=ids.Select((id,i)=>new PetMob((uint)(100+i),PetCatalog.Default.Sources.First(s=>s.PetId==id).NpcId,id,"Örnek yaratık",10000+(i+1)*1000,-80000,500,now,now)).ToArray();
        Tick(new(999,1,true,"Örnek",progress,mobs,0,true,SessionKills:6),new FixView(10000,-80000,500,now,0,"Demo"),Orientation.Parse("-y"),0,now,false,true);
    }
    internal int PreviewTrackedId=>_store.Value.Goals.Keys.First();
    internal void PreviewTrackAll()
    {if(!_preview||_snapshot is null)throw new InvalidOperationException();foreach(var id in _snapshot.Progress.Keys){_store.Value.Goals[id]=3;_store.Value.ExcludedPets.Remove(id);}}
    internal void PreviewAdvanceMini()
    {
        if(!_preview||_snapshot is null)throw new InvalidOperationException();
        var values=_snapshot.Progress.ToDictionary();var id=PreviewTrackedId;var old=values[id];
        values[id]=old with {Souls=old.Souls+1,SessionKills=old.SessionKills+1,SessionSouls=old.SessionSouls+1,GainSerial=old.GainSerial+1};
        var now=_now+1000;Tick(_snapshot with {Progress=values,SessionKills=_snapshot.SessionKills+1},_self is null?null:_self with {TimeMs=now},_axes,_heading,now,false,true);
    }
    internal void VerifyRankTransitions(MiniPetForm mini)
    {
        if(!_preview||_snapshot is null)throw new InvalidOperationException();
        var original=_snapshot;var goals=new Dictionary<int,int>(_store.Value.Goals);var hidden=_hide.Checked;var id=PreviewTrackedId;var time=_now;
        void Apply(PetProgress value)
        {
            var progress=original.Progress.ToDictionary();progress[id]=value;time+=1000;
            Tick(original with {Progress=progress},_self,_axes,_heading,time,false,true);UpdateMini(mini,true);
        }
        try
        {
            _hide.Checked=true;_store.Value.Goals[id]=2;
            Apply(new(1,25,true));
            if(!_rows.ContainsKey(id)||mini.ReadCard(id).Remaining!="Hazır")throw new InvalidOperationException("MAX olmayan, yükseltmeye hazır pet erken gizlendi.");
            Apply(new(2,0,true));
            if(_store.Value.Goals[id]!=3||mini.ReadCard(id).Remaining!="75 kaldı"||!Equals(_rows[id].Cells[3].Value,"75 ruh"))throw new InvalidOperationException("Seviye 2 sonrası 75 ruh hedefi iki listede güncellenmedi.");
            Apply(new(3,0,true));
            if(mini.DisplayedCount!=0||_rows.ContainsKey(id))throw new InvalidOperationException("MAX pet ana/mini listeden gizlenmedi.");
        }
        finally
        {
            _store.Value.Goals=goals;_hide.Checked=hidden;Tick(original,_self,_axes,_heading,time+1000,false,true);UpdateMini(mini,true);
        }
    }
    internal void VerifyInteraction()
    {
        if(!_preview||_snapshot is null)throw new InvalidOperationException();
        var row=_grid.Rows[0];_grid.CurrentCell=row.Cells[1];var id=(int)row.Tag!;var current=_grid.CurrentCell;
        row.Cells[0].Value=true;_goal.SelectedIndex=2;Tick(_snapshot,_self,_axes,_heading,_now+500,false,true);
        if(!ReferenceEquals(row,_rows[id])||!ReferenceEquals(current,_grid.CurrentCell)||_store.Value.Goals[id]!=3)throw new InvalidOperationException("Pet seçimi/hedefi yenilemede korunmadı.");
        _search.Text="no-such-pet-expected";if(_grid.Rows.Count!=0)throw new InvalidOperationException("Pet araması filtrelemedi.");_search.Clear();
    }
    internal void VerifyAutomaticTracking(MiniPetForm mini)
    {
        if(!_preview||_snapshot is null)throw new InvalidOperationException();
        var original=_snapshot;var originalSelf=_self;var goals=new Dictionary<int,int>(_store.Value.Goals);var excluded=new HashSet<int>(_store.Value.ExcludedPets);var view=_view.SelectedIndex;var time=_now;
        void RefreshAt(PetSnapshot data,FixView? location){time+=1000;Tick(data,location is {} fix?fix with {TimeMs=time}:null,_axes,_heading,time,false,true);UpdateMini(mini,true);}
        void Refresh(PetSnapshot data)=>RefreshAt(data,originalSelf);
        try
        {
            _store.Value.Goals.Clear();_store.Value.ExcludedPets.Clear();_view.SelectedIndex=3;Refresh(original);
            if(mini.DisplayedCount!=3)throw new InvalidOperationException("Yakındaki eksik petler otomatik eklenmedi.");
            var id=original.Progress.Keys.First();_grid.CurrentCell=_rows[id].Cells[1];mini.PreviewUntrackViaMenu(id);Refresh(original);Refresh(original);
            var saved=System.Text.Json.JsonSerializer.Deserialize<PetPreferences>(System.Text.Json.JsonSerializer.Serialize(_store.Value))!;
            if(_store.Value.Goals.ContainsKey(id)||!saved.ExcludedPets.Contains(id)||mini.DisplayedCount!=2||!_rows.ContainsKey(id)||!Equals(_rows[id].Cells[0].Value,false)||_goal.Enabled)
                throw new InvalidOperationException("Takipten çıkarma kaydedilmedi, otomatik geri eklendi veya eksik ruh satırı kayboldu.");
            _rows[id].Cells[0].Value=true;Refresh(original);
            if(_store.Value.ExcludedPets.Contains(id)||mini.DisplayedCount!=3)throw new InvalidOperationException("Kullanıcının yeniden takip seçimi uygulanmadı.");
            _view.SelectedIndex=1;Refresh(original);var balance=original.Progress[id];var target=_store.Value.Goals[id];
            if(_rows.Count!=3||mini.DisplayedCount!=3)throw new InvalidOperationException("Yakın takip görünümü mevcut petleri göstermedi.");
            RefreshAt(original,originalSelf! with {X=originalSelf!.X+100000});
            if(_rows.Count!=0||mini.DisplayedCount!=0)throw new InvalidOperationException("Uzaklaşılan petler ana/mini yakın takip listesinden çıkmadı.");
            if(_store.Value.Goals[id]!=target||_snapshot!.Progress[id]!=balance)throw new InvalidOperationException("Uzaklaşma hedefi veya ruh bakiyesini değiştirdi.");
            Refresh(original);
            if(_rows.Count!=3||mini.DisplayedCount!=3)throw new InvalidOperationException("Yeniden yaklaşılan petler ana/mini listeye dönmedi.");
            RefreshAt(original,originalSelf! with {Z=originalSelf!.Z+20000});
            if(_rows.Count!=0||mini.DisplayedCount!=0)throw new InvalidOperationException("Dikey olarak uzak petler yakın takipte kaldı.");
            var stale=original with {Nearby=original.Nearby.Select(m=>m with {X=null,Y=null,Z=null,SeenMs=time-10000}).ToArray()};
            Refresh(stale);
            if(_rows.Count!=0||mini.DisplayedCount!=0)throw new InvalidOperationException("Konumsuz eski yaratık kaydı yakın takipte kaldı.");
            Refresh(original with {Nearby=[]});
            if(_rows.Count!=0||mini.DisplayedCount!=0)throw new InvalidOperationException("Kaybolan yaratıkların satırları yakın takipte kaldı.");
            _view.SelectedIndex=3;Refresh(original with {Nearby=[]});if(!_rows.ContainsKey(id))throw new InvalidOperationException("Eksik ruh görünümü uzak peti kaybetti.");
        }
        finally
        {
            _store.Value.Goals=goals;_store.Value.ExcludedPets=excluded;_view.SelectedIndex=view;Refresh(original);
        }
    }
}
