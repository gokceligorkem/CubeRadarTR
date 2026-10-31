using System.Text;
using System.Text.Json;
using CubeRadar.Cli;
using CubeRadar.Core.Radar;
using Orientation=CubeRadar.Core.Radar.Orientation;

namespace CubeRadar.Tr;

internal sealed partial class RadarForm : Form
{
    private readonly CaptureController _controller;
    private readonly MotionBridge _motion=new();
    private readonly System.Windows.Forms.Timer _timer=new() { Interval=100 };
    private readonly MovementHeadingTracker _headingTracker=new();
    private readonly AlertCadence _cadence=new();
    private readonly string[] _states=new string[6];
    private readonly Label _caption=new(),_headline=new(),_detail=new(),_traffic=new(),_mode=new(),_range=new();
    private readonly HudButton _legend=new() {Tone=ButtonTone.Ghost,TextAlign=ContentAlignment.MiddleLeft,AccessibleName="Yakındaki küpleri göster veya gizle"};
    private readonly HudButton _start=new(),_demo=new(),_motionButton=new();
    private readonly CheckBox _sound=Option("Sesli uyarı",true),_proximity=Option("Yakınlık bipi"),_top=Option("Üstte tut"),
        _outside=Option("Menzil dışını göster"),_movement=Option("Geçici hareket yönü"),_debugToggle=Option("Tanı bilgileri");
    private readonly DataGridView _list=new HudGrid();
    private readonly RadarCanvas _radar=new();
    private readonly Panel _debugPanel=new() { Dock=DockStyle.Right,Width=340,Visible=false,Padding=new Padding(12) };
    private readonly TextBox _debug=new() { Dock=DockStyle.Fill,Multiline=true,ReadOnly=true,ScrollBars=ScrollBars.Vertical,BorderStyle=BorderStyle.None };
    private CaptureView? _capture;
    private CubeCompanionView? _state;
    private HeadingView _heading=new(null,HeadingSource.Unknown,0);
    private bool _allowClose,_closing,_busy,_updating;
    private uint? _selected;
    private string? _audioError;
    private readonly bool _previewMode;
    private RadarSnapshot? _previewSnapshot;
    private bool _previewDirectionMissing;
    private double _previewHeading;
    internal static readonly Color Gold=UiTheme.Accent,Bg=UiTheme.Background,Fg=UiTheme.Text,Muted=UiTheme.Muted;

    public RadarForm(CaptureController controller,bool previewMode=false,bool startMotion=false,bool keepOnTop=false,bool proximity=false,bool movementFallback=false)
    {
        _previewMode=previewMode; _controller=controller;
        _preferences=new(previewMode);
        Text="Cube Radar TR 0.8.11";Font=new Font("Segoe UI",10);
        BackColor=Bg;ForeColor=Fg;DoubleBuffered=true;AutoScaleMode=AutoScaleMode.Dpi;AutoScaleDimensions=new(96,96);
        ClientSize=new Size(1080,840);MinimumSize=new Size(680,700);StartPosition=FormStartPosition.CenterScreen;
        BuildLayout();_motionRequested=startMotion;
        _timer.Tick+=(_,_)=>{_bosses.Observe(_controller.TakeBossUpdates());_bosses.Tick(DateTimeOffset.UtcNow,_sound.Checked&&!_previewMode);UpdateView();};_timer.Start();CubeSound.Prepare();UpdateView();
        _top.Checked=keepOnTop||_preferences.Value.TopMost;_proximity.Checked=proximity||_preferences.Value.Proximity;
        _movement.Checked=movementFallback||_preferences.Value.Movement;_sound.Checked=_preferences.Value.Sound;_outside.Checked=_preferences.Value.Outside;
        _radar.RangeM=_preferences.Value.RangeM;_timer.Interval=_preferences.Value.RefreshMs;_preferencesReady=true;
        if(_preferences.Value.AutoStart&&!_previewMode&&!_controller.Running)Shown+=(_,_)=>StartCapture();
        if(startMotion&&!previewMode)Shown+=async(_,_)=>await _motion.StartAsync();
    }
    protected override void OnShown(EventArgs e)
    {
        if(!_previewMode)
        {
            var area=Screen.FromControl(this).WorkingArea;
            MinimumSize=new(Math.Min(MinimumSize.Width,area.Width),Math.Min(MinimumSize.Height,area.Height));
            Size=new(Math.Min(Width,area.Width),Math.Min(Height,area.Height));
        }
        base.OnShown(e);
    }

    protected override bool ShowWithoutActivation=>_previewMode;
    private static CheckBox Option(string text,bool on=false)=>new ToggleSwitch() { Text=text,Checked=on,AutoSize=true,Margin=new Padding(0,5,14,0) };
    private static FlowLayoutPanel Flow()=>new() { Dock=DockStyle.Fill,WrapContents=false,Margin=Padding.Empty,Padding=new Padding(0,4,0,0) };
    private static void StyleButton(Button b,string text,EventHandler action,bool primary=false)
    {
        b.Text=text;b.Width=140;b.Height=34;b.FlatStyle=FlatStyle.Flat;b.FlatAppearance.BorderSize=0;
        if(b is HudButton hud)hud.Tone=primary?ButtonTone.Primary:ButtonTone.Ghost;
        b.BackColor=primary?Gold:UiTheme.Raised;b.ForeColor=primary?Bg:Fg;b.Click+=action;b.Margin=new Padding(0,0,10,0);
    }
    private void ConfigureList()
    {
        _list.Dock=DockStyle.Fill;_list.ReadOnly=true;_list.AllowUserToAddRows=false;_list.AllowUserToDeleteRows=false;
        _list.AllowUserToResizeRows=false;_list.RowHeadersVisible=false;_list.BackgroundColor=Bg;_list.BorderStyle=BorderStyle.None;
        _list.EnableHeadersVisualStyles=false;_list.ColumnHeadersDefaultCellStyle.BackColor=Color.FromArgb(30,42,54);
        _list.ColumnHeadersDefaultCellStyle.SelectionBackColor=Color.FromArgb(30,42,54);
        _list.ColumnHeadersDefaultCellStyle.ForeColor=Fg;_list.DefaultCellStyle.BackColor=Bg;_list.DefaultCellStyle.ForeColor=Fg;
        _list.DefaultCellStyle.SelectionBackColor=Color.FromArgb(55,67,75);_list.GridColor=Color.FromArgb(39,51,63);
        _list.AutoSizeColumnsMode=DataGridViewAutoSizeColumnsMode.Fill;_list.SelectionMode=DataGridViewSelectionMode.FullRowSelect;_list.MultiSelect=false;
        foreach(var name in new[]{"#","Mesafe","Yön","Yükseklik","Durum"})_list.Columns.Add(name,name);
        _list.Columns[0].AutoSizeMode=DataGridViewAutoSizeColumnMode.None;_list.Columns[0].Width=44;
        float[] weights=[0,75,120,95,100];int[] minimums=[44,62,94,76,80];
        for(var i=1;i<_list.Columns.Count;i++){_list.Columns[i].FillWeight=weights[i];_list.Columns[i].MinimumWidth=minimums[i];}
        _list.SelectionChanged+=(_,_)=> { if(_updating)return;_selected=_list.CurrentRow?.Tag as uint?;if(_lockTarget.Checked)_targetTracker.Lock(_selected);_radar.SelectedId=_selected;UpdateDebug(); };
    }
    private void StartCapture()
    {
        if(_busy)return;
        try { ResetTarget();_controller.Start(); }catch(Exception e){DialogWindows.Message(this,e.Message,"Dinleme başlatılamadı");}
        UpdateView();
    }
    private async Task StopCapture()
    {
        if(_busy)return;
        _busy=true;_start.Enabled=false;_demo.Enabled=false;_motionButton.Enabled=false;
        try { _motionRequested=false;await _motion.StopAsync();await _controller.StopAsync();ResetTarget(); }catch(Exception e){DialogWindows.Message(this,e.Message,"Dinleme durdurulamadı");}
        _busy=false;if(!_closing)UpdateView();
    }
    public void UpdateView()
    {
        if(_busy||_closing)return;
        _capture=_controller.Read();var snapshot=_previewSnapshot??_capture.Snapshot;
        var now=_controller.IsDemo?snapshot.NowMs:DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var motion=_motion.Read(now);
        if(!_controller.IsDemo)snapshot=ClientMotionPresentation.Apply(snapshot,motion.Motion,now,motion.Ready);
        _capture=_capture with {Snapshot=snapshot};
        _state=CubeCompanionView.Build(snapshot,now);
        var identified=snapshot.IdentitySource is SelfIdentitySource.SelfInfo or SelfIdentitySource.OwnRecords or SelfIdentitySource.OwnedConnection;
        var axes=new Orientation(snapshot.Orientation.NorthX,snapshot.Orientation.NorthY,snapshot.Orientation.EastX,snapshot.Orientation.EastY);
        _headingTracker.Observe(snapshot.Self,snapshot.SelfId,snapshot.MapId,axes);
        _heading=_state.FreshPosition&&snapshot.IdentitySource==SelfIdentitySource.OwnedConnection&&motion.Motion is {} packet
            ?new(ClientMotionPresentation.Facing(packet,axes),HeadingSource.Packet,packet.LastObservationAt)
            :_state.FreshPosition?_headingTracker.Read(now,_movement.Checked):new(null,HeadingSource.Unknown,0);
        // A recorded demo uses artificial 0° facing, never a claimed decoded packet heading.
        var displayHeading=_previewDirectionMissing?null:_controller.IsDemo?_previewHeading:_heading.Degrees;
        var pets=_controller.ReadPets(now);
        PrepareRadarDisplay(snapshot,motion.Motion,motion.Ready,pets.Epoch,displayHeading);
        UpdateRadarCreatures(now);
        UpdateCompanion(snapshot,axes,displayHeading,now);
        // Movement-derived travel direction remains available to existing lists; it is never character facing on the radar.
        var radarHeading=_controller.IsDemo?displayHeading:_heading.Source==HeadingSource.Packet?_heading.Degrees:null;
        _radar.SetPresentation(_preferences.Value.RadarOrientation,_preferences.Value.SonarEnabled);
        _radar.SetState(_radarDisplayView??_state,_radarDisplaySelf,axes,_radarDisplayHeading,_outside.Checked,_controller.IsDemo);
        _start.Enabled=true;_start.Text=_controller.Running?"RADARI DURDUR":"RADARI BAŞLAT";_demo.Enabled=true;
        _motionButton.Enabled=true;
        _motionButton.Text=_motion.Active?"Yönü kapat":"Canlı yön";
        State(0,"Npcap",CaptureController.DriverInstalled?"✓ hazır":"eksik");
        State(1,"Ağ adaptörü",_controller.Status.StartsWith("Dinleniyor")?"✓ dinleniyor":_controller.IsDemo?"örnek":"bekleniyor");
        State(2,"Oyun trafiği",_controller.IsDemo?"örnek":_state.LiveTraffic?"✓ bulundu":"bekleniyor");
        State(3,"Oyuncu konumu",!identified?"kimlik bekleniyor":_state.FreshPosition?"✓ güncel":snapshot.Self is null?"bekleniyor":$"eski ({_state.PositionAgeMs/1000}s)");
        State(4,"Oyuncu yönü",_controller.IsDemo?"örnek":_heading.Source==HeadingSource.Packet?"✓ Packet":_heading.Degrees is null?"Unknown":"≈ hareket");
        State(5,"Küp verisi",_state.Cubes.Count>0?$"✓ {_state.Cubes.Count} kayıt":"bekleniyor");
        UpdateSummary(radarHeading,identified,motion.Ready);
        UpdateRows(displayHeading);
        var traffic=_capture.Traffic;
        _traffic.Text=$"{traffic.Packets} paket · {traffic.Frames} kayıt · {traffic.Streams} akış · {snapshot.Zone??"Bölge bekleniyor"}\n"+
            (_controller.IsDemo?"Kayıtlı örnek; ağ dinlemesi ve otomatik ses kapalı.":_motion.Active||_motion.Status!="Paket yönü kapalı"?_motion.Status+" · "+_controller.Status:_controller.Status);
        bool newCube=false;while(_controller.TakeAlert(out var id))newCube|=_state.LiveTraffic&&_state.Cubes.Any(c=>c.ObjectId==id);
        if(_cadence.ShouldPlay(now,_sound.Checked,_state.LiveTraffic&&!_controller.IsDemo,_proximity.Checked,_target?.DistanceM,newCube))PlayAlertSound();
        if(_debugPanel.Visible)UpdateDebug();
    }
    private void State(int i,string name,string value)
    { _states[i]=$"{name}: {value}"; }

    private void UpdateSummary(double? heading,bool identified,bool motionReady)
    {
        if(_state is null)return;
        _range.Text=$"{_radar.RangeM} m menzil  ·  {_state.Cubes.Count} küp";
        _mode.ForeColor=Muted;
        if(_controller.IsDemo)_mode.Text="● ÖRNEK · canlı veri değil";
        else if(!_controller.Running)_mode.Text="● Radar kapalı";
        else if(!_state.LiveTraffic)_mode.Text=_capture?.Traffic.Packets>0?"● Bağlantı kesildi":"● Bağlantı bekleniyor";
        else if(_radarDisplayView?.LastKnownPosition==true){_mode.Text="● Son bilinen konum · güncelleme bekleniyor";_mode.ForeColor=Gold;}
        else if(!_state.FreshPosition){_mode.Text="● Konum bekleniyor";_mode.ForeColor=Gold;}
        else if(_preferences.Value.RadarOrientation==RadarOrientationMode.NorthUp){_mode.Text="● Canlı · Kuzey sabit";_mode.ForeColor=UiTheme.Green;}
        else if(heading is null){_mode.Text="● Canlı  ·  Yön bekleniyor";_mode.ForeColor=Gold;}
        else {_mode.Text=_heading.Source==HeadingSource.MovementDerived?"● Canlı  ·  Hareket yönü":"● Canlı";_mode.ForeColor=UiTheme.Green;}
        _legend.Text=$"YAKINDAKİ KÜPLER  ·  {_state.Cubes.Count}   ⌄";
        _caption.Text=_controller.IsDemo?"EN YAKIN KÜP · ÖRNEK":"EN YAKIN KÜP";
        _headline.ForeColor=Fg;_distance.Text="—";
        if(_radarDisplayView?.LastKnownPosition==true)
        {_caption.Text="SON BİLİNEN KONUM";_headline.Text="Güncel konum bekleniyor";_detail.Text="Soluk işaretler son okunan konumları gösterir.";}
        else if(_target is {} nearest)
        {
            _caption.Text=_targetTracker.LockedId is not null?"KİLİTLİ HEDEF":nearest.DistanceM>_radar.RangeM?"EN YAKIN KÜP · MENZİL DIŞINDA":"EN YAKIN KÜP";
            var rank=_state.Cubes.FindIndex(c=>c.ObjectId==nearest.ObjectId)+1;
            _headline.Text=$"Hidden Cube #{rank:00}";_distance.Text=$"{nearest.DistanceM:0} m";
            _detail.Text=$"{RelativeDirection(nearest,heading)}   ·   Yükseklik: {HeightLabel(nearest.DzUnits)}";
        }
        else if(_targetTracker.LockedId is not null&&_state.FreshPosition)
        {_caption.Text="KİLİTLİ HEDEF";_headline.Text="Hedef görünmüyor";_detail.Text="Kayıt yok veya yükseklik filtresinin dışında. Kilidi kaldırabilirsin.";}
        else
        {
            _headline.Text="Küp algılanmadı";
            _detail.Text=!_controller.Running&&!_controller.IsDemo?"Radarı başlat; oyun verisi burada görünecek.":!_state.LiveTraffic?"Oyun dünyasına bağlanman bekleniyor.":!identified&&!_motion.Active?"Canlı yönü aç ve konumu güncellemek için hareket et.":"Konumu güncellemek için hareket et.";
        }
        if(_audioError is not null)_detail.Text=_audioError;
    }
    private static string DirectionSentence(string direction)=>direction switch
    {
        "Ön"=>"Önünde","Sağ Ön"=>"Sağ önünde","Sağ"=>"Sağında","Sağ Arka"=>"Sağ arkanda",
        "Arka"=>"Arkanda","Sol Arka"=>"Sol arkanda","Sol"=>"Solunda",_=>"Sol önünde"
    };
    private static string HeightSentence(double? units)=>units is not {} z?"Yükseklik bekleniyor":Math.Abs(z)<300?"Seninle aynı yükseklikte":$"{(z>0?"▲":"▼")} {Math.Abs(z)/100:0} m {(z>0?"yukarıda":"aşağıda")}";
    private void UpdateRows(double? heading)
    {
        if(_state is null)return;
        _updating=true;
        try
        {
            var ids=_state.Cubes.Select(c=>c.ObjectId).ToHashSet();
            foreach(var id in _cubeRows.Keys.Where(id=>!ids.Contains(id)).ToArray()){_list.Rows.Remove(_cubeRows[id]);_cubeRows.Remove(id);}
            for(var rank=0;rank<_state.Cubes.Count;rank++)
            {
                var cube=_state.Cubes[rank];var direction=_state.FreshPosition?RelativeDirection(cube,heading):"—";
                var status=cube.Status=="Opening"?"Açılıyor":!_state.FreshPosition?"Konum eski":cube.DistanceM<=50?"● Yakın":cube.DistanceM<=_radar.RangeM?"Menzilde":"Menzil dışı";
                if(!_controller.IsVerified(cube))status+=" *";
                if(!_cubeRows.TryGetValue(cube.ObjectId,out var row)){row=_list.Rows[_list.Rows.Add()];_cubeRows.Add(cube.ObjectId,row);}
                row.Tag=cube.ObjectId;
                object[] values=[$"{rank+1:00}",_state.FreshPosition?$"{cube.DistanceM:0} m":"—",direction,_state.FreshPosition?HeightLabel(cube.DzUnits):"—",status];
                for(var i=0;i<values.Length;i++)if(!Equals(row.Cells[i].Value,values[i]))row.Cells[i].Value=values[i];
                row.DefaultCellStyle.ForeColor=rank==0&&_state.FreshPosition?Gold:Fg;
                row.Cells[2].ToolTipText=direction;
                foreach(DataGridViewCell cell in row.Cells)cell.ToolTipText=$"Küp {rank+1} · {values[1]} · {direction} · {values[3]}"+(!_controller.IsVerified(cube)?" · Deneysel küp tanımı":"");
            }
            var selected=_list.Rows.Cast<DataGridViewRow>().FirstOrDefault(r=>Equals(r.Tag,_targetTracker.LockedId??_selected));
            if(selected is not null)_list.CurrentCell=selected.Cells[0];else _selected=_list.CurrentRow?.Tag as uint?;
        }
        finally{_updating=false;}
        _radar.SelectedId=_targetTracker.LockedId??_selected;
    }
    internal static string DirectionText(string value)=>value switch
    {
        "Ön"=>"ÖN ↑","Sağ Ön"=>"SAĞ ÖN ↗","Sağ"=>"SAĞ →","Sağ Arka"=>"SAĞ ARKA ↘",
        "Arka"=>"ARKA ↓","Sol Arka"=>"SOL ARKA ↙","Sol"=>"SOL ←",_=>"SOL ÖN ↖"
    };
    private static string RelativeDirection(CubeView cube,double? heading)=>heading is {} h&&cube.Bearing is {} b?DirectionText(RadarMath.GetRelativeDirection(RadarMath.CalculateRelativeBearing(b,h))):"Yön bekleniyor";
    internal static string HeightLabel(double? units)=>units is not {} z?"—":$"{z/100:+0;-0;0} m";
    private void SelectTarget(uint id)
    { foreach(DataGridViewRow row in _list.Rows)if(Equals(row.Tag,id)){_list.CurrentCell=row.Cells[0];break;} }
    private void PlayAlertSound()=>_audioError=CubeSound.Play(null,_volume.Value) is {} note?"Ses çıkışı açılamadı: "+note:null;
    private void PlayBossSound()=>_audioError=CubeSound.PlayBoss(_volume.Value) is {} note?"Boss sesi açılamadı: "+note:null;
    private void ToggleDebug()
    {
        _debugPanel.Visible=_debugToggle.Checked;_advancedCard.Height=UiTheme.Px(this,_debugToggle.Checked?550:230);UpdateDebug();
    }
    internal void PreviewDiagnostics()
    {
        if(!_previewMode)throw new InvalidOperationException("Yalnız test penceresinde kullanılabilir.");
        _debugToggle.Checked=true;Navigate(2);((Panel)_pages[2]).ScrollControlIntoView(_advancedCard);
    }
    internal void PreviewScenario(bool headingKnown=true)
    {
        if(!_previewMode)throw new InvalidOperationException("Yalnız test penceresinde kullanılabilir.");
        _timer.Stop();_debugToggle.Checked=false;Navigate(0);
        var source=_controller.Snapshot();var self=source.Self!;var seed=source.Cubes[0];
        _previewSnapshot=source with {Cubes=[
            seed with {ObjectId=90001,X=self.X+2100,Y=self.Y-4400,Z=self.Z+1200,Status="Visible"},
            seed with {ObjectId=90002,X=self.X-8100,Y=self.Y,Z=self.Z-600,Status="Visible"},
            seed with {ObjectId=90003,X=self.X+2000,Y=self.Y+11000,Z=self.Z+100,Status="Visible"}]};
        _previewDirectionMissing=!headingKnown;_selected=90001;UpdateView();
    }
    private void UpdateDebug()
    {
        if(_capture is null||_state is null)return;
        var snap=_capture.Snapshot;var self=snap.Self;var cube=_state.Cubes.FirstOrDefault(c=>c.ObjectId==_selected);
        var b=new StringBuilder("TANI · salt okunur\r\n\r\n");
        foreach(var state in _states)b.AppendLine(state);
        b.AppendLine();
        b.AppendLine($"Player Entity: {snap.SelfId?.ToString()??"Unknown"}");
        b.AppendLine($"Identity Source: {snap.IdentitySource}");
        b.AppendLine($"Player XYZ: {self?.X:0.0} / {self?.Y:0.0} / {self?.Z:0.0}");
        b.AppendLine($"Position source: {self?.Source??"Unknown"}");b.AppendLine($"Position age: {_state.PositionAgeMs/1000.0:0.0}s");
        b.AppendLine($"Heading Degree: {_heading.Degrees?.ToString("0.00")??"Unknown"}");
        b.AppendLine($"Heading Radian: {(_heading.Degrees*Math.PI/180)?.ToString("0.000")??"Unknown"}");b.AppendLine($"Heading Source: {HeadingSourceLabel}");
        if(_controller.IsDemo)b.AppendLine("DEMO çizimi: yapay 0°; paket yönü değil.");
        if(_heading.Source==HeadingSource.Packet)
        {
            var packet=_motion.Read(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()).Motion;
            b.AppendLine($"Heading Packet: C2S 0x{packet?.Opcode}");
            b.AppendLine($"Facing world yaw: {packet?.FacingYaw:0.00}°");
            b.AppendLine($"Movement world yaw: {packet?.MovementYaw:0.00}°");
        }
        else b.AppendLine("Güncel paket bakış yönü yok.");
        b.AppendLine(_motion.Status+"\r\n");
        b.AppendLine("HAREKET OKUYUCU TANISI (ham paket içermez)");
        b.AppendLine(JsonSerializer.Serialize(_motion.ReadDiagnostics(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()),new JsonSerializerOptions {WriteIndented=true}));
        b.AppendLine("PET EŞİTLEME TANISI");
        b.AppendLine(JsonSerializer.Serialize(_controller.PetSyncDiagnostics,new JsonSerializerOptions {WriteIndented=true}));
        if(self is not null&&cube is not null)
        {
            var dx=cube.X-self.X;var dy=cube.Y-self.Y;var dz=cube.Z-self.Z;
            var axes=new Orientation(snap.Orientation.NorthX,snap.Orientation.NorthY,snap.Orientation.EastX,snap.Orientation.EastY);
            var facing=_controller.IsDemo?(_previewDirectionMissing?null:(double?)_previewHeading):_heading.Source==HeadingSource.Packet?_heading.Degrees:null;
            RadarPoint? p=_state.FreshPosition&&RadarProjector.TryCreate(self.X,self.Y,facing,axes,_radar.RangeM,_radar.Radius,
                _preferences.Value.RadarOrientation,out var frame)&&frame.TryProject(cube.X,cube.Y,out var point)&&(point.Inside||_outside.Checked)?point:null;
            b.AppendLine($"Cube ID: {cube.ObjectId}");b.AppendLine($"Cube XYZ: {cube.X:0.0} / {cube.Y:0.0} / {cube.Z:0.0}");
            b.AppendLine($"DX/DY/DZ (cm): {dx:0.0} / {dy:0.0} / {dz:0.0}");b.AppendLine($"Distance: {RadarMath.CalculateDistance(dx,dy):0.0}m");
            b.AppendLine($"Height: {RadarMath.CalculateHeightDifference(self.Z,cube.Z):+0.0;-0.0;0}m");
            var clamp=p is {Inside:false} outside?_radar.RangeM/outside.DistanceM:1;
            b.AppendLine($"Radar mode: {_preferences.Value.RadarOrientation}");
            b.AppendLine($"Radar X/Y (px merkezden): {p?.ScreenX*clamp:0.0} / {p?.ScreenY*clamp:0.0}");
        }
        else b.AppendLine("Küp detayları için listeden seç.\r\n");
        b.AppendLine($"Packet Type: {_capture.LastPacketType??"—"}");
        b.AppendLine($"Last Packet Time: {(_capture.LastFrameMs>0?DateTimeOffset.FromUnixTimeMilliseconds(_capture.LastFrameMs).ToLocalTime().ToString("HH:mm:ss.fff"):"—")}");
        b.AppendLine("\r\nDOĞRULANMAMIŞ AÇI ADAYLARI\r\n(Radar yönünde kullanılmaz)");
        foreach(var c in _capture.HeadingCandidates.TakeLast(6))b.AppendLine($"{c.PacketType} +{c.Offset}: {c.Value:0.000} @ {DateTimeOffset.FromUnixTimeMilliseconds(c.TimeMs).ToLocalTime():HH:mm:ss}");
        var text=b.ToString();if(_debug.Text!=text)_debug.Text=text;
    }
    private void SaveDiagnostics()
    {
        if(_capture is null)return;
        using var dialog=new SaveFileDialog { Filter="JSON tanı|*.json",FileName=$"cube-radar-tani-{DateTime.Now:yyyyMMdd-HHmmss}.json",Title="Tanı: kendi konumun ve açı adayları kaydedilir" };
        if(DialogWindows.Show(dialog,this)!=DialogResult.OK)return;
        try
        {
            File.WriteAllText(dialog.FileName,JsonSerializer.Serialize(new { version="0.8.11",demo=_controller.IsDemo,
                identitySource=_capture.Snapshot.IdentitySource.ToString(),
                self=_capture.Snapshot.Self,heading=_heading,headingSource=HeadingSourceLabel,selectedCube=_state?.Cubes.FirstOrDefault(c=>c.ObjectId==_selected),
                motion=_motion.ReadDiagnostics(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()),
                petSync=_controller.PetSyncDiagnostics,
                packets=_capture.Traffic.Packets,frames=_capture.Traffic.Frames,streams=_capture.Traffic.Streams,
                _capture.LastPacketType,_capture.LastFrameMs,candidates=_capture.HeadingCandidates,
                note="Candidates are unverified; no raw packets or network addresses included." },new JsonSerializerOptions { WriteIndented=true }));
        }
        catch(Exception e)when(e is IOException or UnauthorizedAccessException){DialogWindows.Message(this,e.Message,"Tanı kaydedilemedi");}
    }
    private string HeadingSourceLabel=>_heading.Source==HeadingSource.MovementDerived?"Movement-derived":_heading.Source.ToString();
    protected override async void OnFormClosing(FormClosingEventArgs e)
    {
        if(_allowClose){base.OnFormClosing(e);return;}e.Cancel=true;if(_closing)return;
        _closing=true;_timer.Stop();_radar.StopAnimation();_mini?.Dispose();_petMini?.Dispose();
        try{await _motion.StopAsync();await _controller.StopAsync();}catch(Exception error){DialogWindows.Message(this,error.Message,"Kapatma sırasında hata");}
        finally{_timer.Dispose();CubeSound.Close();_allowClose=true;Close();}
    }
}
