using CubeRadar.Core.Radar;
using Orientation=CubeRadar.Core.Radar.Orientation;

namespace CubeRadar.Tr;

internal sealed partial class RadarForm
{
    private readonly TargetTracker _targetTracker=new();
    private readonly CheckBox _lockTarget=Option("Hedefe kilitlen");
    private readonly ComboBox _heightFilter=new() {DropDownStyle=ComboBoxStyle.DropDownList,Width=125};
    private readonly Label _freshness=new();
    private readonly HudButton _eventStrip=new() {Tone=ButtonTone.Ghost,AccessibleName="Yaklaşan boss bildirimi, etkinlikleri aç"};
    private readonly HudButton _reconnect=new();
    private BossPanel _bosses=null!;
    private PetPanel _pets=null!;
    private int _eventAlertPage=1;
    private MiniRadarForm? _mini;
    private MiniPetForm? _petMini;
    private long _lastReconnectOffer;
    private long _lastHeadingTime;
    private long _nextPetDiagnosticMs;
    private readonly string _petDiagnosticRunId=Guid.NewGuid().ToString("N");
    private DateTimeOffset _eventAlertUntil;
    private string? _eventAlert;
    private bool _motionRequested;
    private CubeView? _target;

    private void InitializeCompanion()
    {
        _heightFilter.Items.AddRange(["Her yükseklik","Aynı seviye","Yukarıdaki","Aşağıdaki"]);_heightFilter.SelectedIndex=0;UiTheme.Input(_heightFilter);
        _heightFilter.SelectedIndexChanged+=(_,_)=>UpdateView();
        _lockTarget.CheckedChanged+=(_,_)=>{_targetTracker.Lock(_lockTarget.Checked?_selected:null);UpdateView();};
        StyleButton(_reconnect,"Yeniden bağlan",async(_,_)=>await ReconnectMotion());_reconnect.Width=150;_reconnect.Height=34;_reconnect.Enabled=false;
        _bosses.Alert+=message=>{PlayBossSound();_eventAlert="BOSS UYARISI · "+message.Replace('\n',' ');_eventAlertPage=1;_eventAlertUntil=DateTimeOffset.UtcNow.AddSeconds(30);};
        _bosses.SoundTestRequested+=PlayBossSound;
        _pets.MiniRequested+=OpenPetMini;
        _bosses.RefreshRequested+=()=>
        {
            if(!_controller.Running&&!_previewMode)StartCapture();
            _bosses.Observe(_controller.TakeBossUpdates());_bosses.RefreshClocks(DateTimeOffset.UtcNow,_sound.Checked&&!_previewMode);
        };
    }
    private void ResetTarget(){_targetTracker.Lock(null);_lockTarget.Checked=false;_selected=null;_lastHeadingTime=0;}
    private void UpdateCompanion(RadarSnapshot snapshot,Orientation axes,double? heading,long now)
    {
        if(_state is null)return;
        _targetTracker.ObserveMap(snapshot.MapId);
        if(_lockTarget.Checked&&_targetTracker.LockedId is null)_lockTarget.Checked=false;
        _state=TargetTracker.Filter(_state,(HeightFilter)_heightFilter.SelectedIndex);
        _target=_targetTracker.Resolve(_state);
        _lockTarget.Enabled=_state.Cubes.Count>0||_targetTracker.LockedId is not null;
        _radar.LockedId=_targetTracker.LockedId;
        _radar.SelectedId=_targetTracker.LockedId??_selected;
        var age=_state.PositionAgeMs==long.MaxValue?"yok":$"{_state.PositionAgeMs/1000.0:0.0} sn önce";
        if(_heading.Degrees is not null)_lastHeadingTime=_heading.TimeMs;
        var headingAge=_lastHeadingTime>0?$"{Math.Max(0,now-_lastHeadingTime)/1000.0:0.0} sn önce":"yok";
        _freshness.Text=$"Konum: {age}  ·  Yön: {headingAge}"+(_state.FreshPosition?"":"  · güncelleme bekleniyor");
        _reconnect.Enabled=!_busy&&!_controller.IsDemo&&_controller.Running&&!_motion.Active;
        if(_motionRequested&&!_motion.Active)_freshness.Text+=" · Bağlantı kesildi: Ayarlar > Yeniden bağlan";
        if(_motion.Active)_reconnect.Tone=ButtonTone.Ghost;
        if(_motionRequested&&!_motion.Active&&now-_lastReconnectOffer>30000){_lastReconnectOffer=now;_reconnect.Tone=ButtonTone.Primary;}
        _bosses.Tick(DateTimeOffset.UtcNow,_sound.Checked&&!_previewMode);
        if(!_previewMode)_pets.Tick(_controller.ReadPets(now),snapshot.Self,axes,heading,now,_sound.Checked&&!_controller.IsDemo);
        SavePetDiagnostics(now);
        if(_petMini is {IsDisposed:false})_pets.UpdateMini(_petMini);
        var alerting=DateTimeOffset.UtcNow<_eventAlertUntil;
        _eventStrip.Text=alerting?_eventAlert:"BOSS · "+_bosses.Summary;
        var notice=_bosses.MiniNotice();
        if(_mini is {IsDisposed:false})
        {
            _mini.UpdateCreatures(_radarCreatures,now,_preferences.Value.ShowCreatures,(HeightFilter)_heightFilter.SelectedIndex);
            _mini.Update(_radarDisplayView??_state,_radarDisplaySelf,axes,_radarDisplayHeading,
                _target,notice.Name,notice.Clock,_outside.Checked,_controller.IsDemo,alerting,_radar.RangeM,_preferences.Value.RadarOrientation,_preferences.Value.SonarEnabled);
        }
    }
    private void SavePetDiagnostics(long now)
    {
        if(_previewMode||now<_nextPetDiagnosticMs)return;_nextPetDiagnosticMs=now+2000;
        try
        {
            var directory=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"CubeRadarTR");
            Directory.CreateDirectory(directory);var path=Path.Combine(directory,"pet-status.json");
            var state=new {eventName="pet_capture_status",entryPoint="desktop",runId=_petDiagnosticRunId,
                version=typeof(RadarForm).Assembly.GetName().Version?.ToString(3),pid=Environment.ProcessId,atMs=now,pet=_controller.PetSyncDiagnostics};
            File.WriteAllText(path+".tmp",System.Text.Json.JsonSerializer.Serialize(state));File.Move(path+".tmp",path,true);
        }
        catch(Exception error)when(error is IOException or UnauthorizedAccessException){System.Diagnostics.Debug.WriteLine(error.Message);}
    }
    private void OpenMini()
    {
        if(_mini is null||_mini.IsDisposed){_mini=new(_previewMode);_mini.CompactChanged+=value=>_miniCompact.Checked=value;}
        _miniPass.Checked=false;ApplyMiniPreferences();
        // Independent top-level window: an owned form is hidden whenever its owner is minimized.
        // Main-form shutdown still explicitly disposes _mini in OnFormClosing.
        _mini.Show();if(!_previewMode)_mini.BringToFront();UpdateView();
    }
    private void OpenPetMini()
    {
        if(_petMini is null||_petMini.IsDisposed)
        {
            _petMini=new(_previewMode,_pets.MiniPreferences);_petMini.PreferencesChanged+=_pets.SaveMiniBounds;_petMini.UntrackRequested+=_pets.Untrack;
            _petMini.EditRequested+=()=>{WindowState=FormWindowState.Normal;Show();Navigate(3);if(!_previewMode){BringToFront();Activate();}};
        }
        _petMini.Show();if(!_previewMode)_petMini.BringToFront();_pets.UpdateMini(_petMini,true);
    }
    private async Task ReconnectMotion()
    {
        if(_busy||_closing||_controller.IsDemo)return;
        _busy=true;_reconnect.Enabled=false;
        try {await _motion.StopAsync();_motionRequested=true;await _motion.StartAsync();}
        finally {_busy=false;if(!_closing)UpdateView();}
    }
    internal void PreviewEvents()
    {
        if(!_previewMode)throw new InvalidOperationException();
        _bosses.Preview();Navigate(1);_bosses.VerifyGridInteraction();_bosses.VerifyEditorInteraction();_bosses.VerifyPresetDialogInteraction();_bosses.VerifyFreshInstallRefresh();
    }
    internal Form PreviewMini()
    {
        if(!_previewMode)throw new InvalidOperationException();
        Navigate(0);OpenMini();return _mini!;
    }
    internal void PreviewLockedAndSettings()
    {
        if(!_previewMode)throw new InvalidOperationException();
        Navigate(0);_selected=90002;_lockTarget.Checked=true;UpdateView();
        if(_target?.ObjectId!=90002)throw new InvalidOperationException("Hedef kilidi önizleme doğrulaması başarısız.");
    }
    internal void PreviewSettings(){if(!_previewMode)throw new InvalidOperationException();Navigate(2);((Panel)_pages[2]).AutoScrollPosition=Point.Empty;}
    internal void PreviewPets(){if(!_previewMode)throw new InvalidOperationException();Navigate(3);_pets.Preview();_pets.VerifyInteraction();}
    internal Form PreviewPetMini()
    {
        if(!_previewMode)throw new InvalidOperationException();PreviewPets();OpenPetMini();var mini=_petMini!;
        if(mini.FormBorderStyle!=FormBorderStyle.None||mini.DisplayedRowHeight>UiTheme.Px(mini,60)||mini.Height>UiTheme.Px(mini,180))throw new InvalidOperationException("Mini pet çerçevesiz, kompakt bir liste değil.");
        mini.SetOpacityPercent(55);
        if(Math.Abs(mini.Opacity-.55)>.001||mini.NativeAlpha is not (139 or 140 or 141)||_pets.MiniPreferences.MiniOpacity!=55)throw new InvalidOperationException("Mini pet yarı saydamlık ayarı uygulanmadı/kaydedilmedi.");
        mini.SetOpacityPercent(100);if(mini.Opacity!=1)throw new InvalidOperationException("Mini pet opak görünüm geri alınmadı.");
        if(mini.PreviewHitTest(new(1,1))!=13||mini.PreviewHitTest(new(mini.ClientSize.Width-2,mini.ClientSize.Height-2))!=17)throw new InvalidOperationException("Çerçevesiz mini petin kenarlarından boyutlandırma çalışmadı.");
        var legacy=new PetPreferences {MiniWidth=360,MiniHeight=360,MiniLeft=42,MiniTop=55,MiniTopMost=false,Goals=new(){{1002,2}}};
        if(!PetPreferencesStore.MigrateMiniLayout(legacy)||legacy.MiniHeight>140||legacy.MiniLeft!=42||legacy.MiniTopMost||legacy.Goals[1002]!=2||PetPreferencesStore.MigrateMiniLayout(legacy))throw new InvalidOperationException("Eski mini pet boyutu/seçimleri korunarak kompakt görünüme taşınmadı.");
        if(mini.DisplayedCount!=1||mini.ReadCard(_pets.PreviewTrackedId).Remaining!="87 kaldı")throw new InvalidOperationException("Mini pet takip hedefini göstermedi.");
        WindowState=FormWindowState.Minimized;Application.DoEvents();
        try
        {
            if(mini.Owner is not null||!mini.Visible||!NativeWindowVisible(mini.Handle)||!mini.TopMost||mini.WindowState==FormWindowState.Minimized)throw new InvalidOperationException("Ana pencere küçülünce mini pet kayboldu.");
            _pets.PreviewAdvanceMini();UpdateView();var row=mini.ReadCard(_pets.PreviewTrackedId);
            if(row.Remaining!="86 kaldı"||!row.Kills.Contains("7 kesim",StringComparison.Ordinal)||mini.TotalsText!="7 kesim")throw new InvalidOperationException("Küçültülmüş ana pencerede mini pet sayaçları güncellenmedi.");
        }
        finally{WindowState=FormWindowState.Normal;Application.DoEvents();}
        _pets.VerifyRankTransitions(mini);
        _pets.VerifyAutomaticTracking(mini);
        return mini;
    }
    internal void PreviewPetMiniList()
    {
        if(!_previewMode||_petMini is null)throw new InvalidOperationException();_pets.PreviewTrackAll();_pets.UpdateMini(_petMini,true);
        if(_petMini.DisplayedCount!=3||_petMini.Height>UiTheme.Px(_petMini,240))throw new InvalidOperationException("Çoklu pet listesi kompakt yüksekliğe sığmadı.");
    }
}
