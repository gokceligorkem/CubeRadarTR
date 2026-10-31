using CubeRadar.Cli;

namespace CubeRadar.Tr;

internal sealed partial class RadarForm
{
    private readonly Label _distance=UiTheme.Label("—",30,UiTheme.Accent,FontStyle.Bold);
    private readonly TrackBar _volume=new() {Minimum=0,Maximum=100,Value=85,TickStyle=TickStyle.None,Width=220,Height=36};
    private readonly Panel _pageHost=new() {Dock=DockStyle.Fill};
    private readonly HudButton[] _navigation=[new(),new(),new(),new()];
    private readonly Dictionary<uint,DataGridViewRow> _cubeRows=[];
    private readonly List<Control> _pages=[];
    private TableLayoutPanel _radarBody=null!;
    private TableLayoutPanel _nearbyLayout=null!;
    private SurfaceCard _radarCard=null!,_nearbyCard=null!,_advancedCard=null!;
    private UiPreferencesStore _preferences=null!;
    private bool _preferencesReady;
    private int _page;
    private readonly ToggleSwitch _miniCompact=new() {Text="Kompakt görünüm"},_miniPass=new() {Text="Tıklamaları oyuna geçir"},_miniTop=new() {Text="Üstte tut",Checked=true};
    private bool? _stackedListExpanded;
    private readonly ComboBox _radarOrientation=new() {DropDownStyle=ComboBoxStyle.DropDownList,Width=180,AccessibleName="Radar yönü"};
    private readonly CheckBox _sonar=Option("Sonar efekti",true);
    private readonly CheckBox _showCreatures=Option("Yaratıkları göster",true);

    private void BuildLayout()
    {
        var shell=new TableLayoutPanel {Dock=DockStyle.Fill,ColumnCount=1,RowCount=3,Padding=new(20,12,20,16)};
        shell.RowStyles.Add(new(SizeType.Absolute,52));shell.RowStyles.Add(new(SizeType.Absolute,48));shell.RowStyles.Add(new(SizeType.Percent,100));Controls.Add(shell);
        var header=new TableLayoutPanel {Dock=DockStyle.Fill,ColumnCount=2,Margin=Padding.Empty};header.ColumnStyles.Add(new(SizeType.Percent,100));header.ColumnStyles.Add(new(SizeType.Absolute,220));
        header.Controls.Add(UiTheme.Label("◈  CUBE RADAR",19,Fg,FontStyle.Bold),0,0);
        header.Controls.Add(UiTheme.Label("AION 2  /  COMPANION",9,Muted),1,0);shell.Controls.Add(header,0,0);
        var nav=Flow();string[] titles=["Radar","Boss & Etkinlik","⚙  Ayarlar","Pet Avı"];
        foreach(var i in new[]{0,1,3,2}){var page=i;var button=_navigation[i];button.Text=titles[i];button.Tone=ButtonTone.Navigation;button.Width=i switch {0=>84,1=>146,2=>108,_=>94};button.Height=40;button.Click+=(_,_)=>Navigate(page);nav.Controls.Add(button);}
        shell.Controls.Add(nav,0,1);shell.Controls.Add(_pageHost,0,2);
        var radarPage=new Panel {Dock=DockStyle.Fill,BackColor=Bg};var bossPage=new Panel {Dock=DockStyle.Fill,BackColor=Bg};
        _bosses=new(_previewMode);bossPage.Controls.Add(_bosses);
        _pets=new(_previewMode);_pets.StartRequested+=()=>{if(!_controller.Running&&!_previewMode)StartCapture();};
        _pets.Completed+=(message,sound)=>{if(sound)PlayAlertSound();_eventAlert="PET HEDEFİ · "+message;_eventAlertPage=3;_eventAlertUntil=DateTimeOffset.UtcNow.AddSeconds(30);};
        _pages.AddRange([radarPage,bossPage,new Panel {Dock=DockStyle.Fill,BackColor=Bg,AutoScroll=true},_pets]);
        foreach(var page in _pages)_pageHost.Controls.Add(page);
        var layout=new TableLayoutPanel {Dock=DockStyle.Fill,ColumnCount=1,RowCount=5,Margin=Padding.Empty};
        layout.RowStyles.Add(new(SizeType.Absolute,36));layout.RowStyles.Add(new(SizeType.Absolute,128));layout.RowStyles.Add(new(SizeType.Percent,100));layout.RowStyles.Add(new(SizeType.Absolute,56));layout.RowStyles.Add(new(SizeType.Absolute,40));radarPage.Controls.Add(layout);
        var status=new TableLayoutPanel {Dock=DockStyle.Fill,ColumnCount=2,Margin=Padding.Empty};status.ColumnStyles.Add(new(SizeType.Percent,65));status.ColumnStyles.Add(new(SizeType.Percent,35));
        _mode.Dock=_range.Dock=DockStyle.Fill;_mode.Font=new("Segoe UI",10,FontStyle.Bold);_mode.TextAlign=ContentAlignment.MiddleLeft;_range.TextAlign=ContentAlignment.MiddleRight;_range.ForeColor=Muted;
        status.Controls.Add(_mode,0,0);status.Controls.Add(_range,1,0);layout.Controls.Add(status,0,0);
        var nearest=new SurfaceCard {Dock=DockStyle.Fill,Margin=new(0,0,0,12)};
        var target=new TableLayoutPanel {Dock=DockStyle.Fill,ColumnCount=2,RowCount=3,BackColor=UiTheme.Surface,Margin=Padding.Empty};
        target.ColumnStyles.Add(new(SizeType.Percent,100));target.ColumnStyles.Add(new(SizeType.Absolute,140));target.RowStyles.Add(new(SizeType.Absolute,22));target.RowStyles.Add(new(SizeType.Absolute,30));target.RowStyles.Add(new(SizeType.Percent,100));nearest.Controls.Add(target);
        _caption.Dock=_headline.Dock=_detail.Dock=DockStyle.Fill;_caption.Font=new("Segoe UI",9,FontStyle.Bold);_caption.ForeColor=Muted;
        _headline.Font=new("Segoe UI",17,FontStyle.Bold);_headline.AutoEllipsis=true;_detail.Font=new("Segoe UI",11);_detail.ForeColor=Muted;_detail.AutoEllipsis=true;
        target.Controls.Add(_caption,0,0);target.Controls.Add(_headline,0,1);target.Controls.Add(_detail,0,2);target.Controls.Add(_distance,1,0);target.SetRowSpan(_distance,3);_distance.TextAlign=ContentAlignment.MiddleRight;layout.Controls.Add(nearest,0,1);
        _radarBody=new TableLayoutPanel {Dock=DockStyle.Fill,ColumnCount=2,RowCount=2,Margin=Padding.Empty};
        _radarBody.ColumnStyles.Add(new(SizeType.Percent,60));_radarBody.ColumnStyles.Add(new(SizeType.Percent,40));_radarBody.RowStyles.Add(new(SizeType.Percent,100));_radarBody.RowStyles.Add(new(SizeType.Absolute,0));layout.Controls.Add(_radarBody,0,2);
        _radarCard=new SurfaceCard {Dock=DockStyle.Fill,Padding=new(8),Margin=new(0,0,12,0)};_radar.Dock=DockStyle.Fill;_radar.TargetSelected+=SelectTarget;_radarCard.Controls.Add(_radar);_radarBody.Controls.Add(_radarCard,0,0);
        _nearbyCard=new SurfaceCard {Dock=DockStyle.Fill,Padding=new(12),Margin=Padding.Empty};
        var side=_nearbyLayout=new TableLayoutPanel {Dock=DockStyle.Fill,ColumnCount=1,RowCount=3,Margin=Padding.Empty};side.RowStyles.Add(new(SizeType.Absolute,30));side.RowStyles.Add(new(SizeType.Percent,100));side.RowStyles.Add(new(SizeType.Absolute,40));
        _legend.Dock=DockStyle.Fill;_legend.ForeColor=Muted;_legend.Font=new("Segoe UI",9,FontStyle.Bold);side.Controls.Add(_legend,0,0);ConfigureList();UiTheme.Grid(_list);side.Controls.Add(_list,0,1);side.Controls.Add(_lockTarget,0,2);_nearbyCard.Controls.Add(side);_radarBody.Controls.Add(_nearbyCard,1,0);
        _legend.Cursor=Cursors.Hand;_legend.Click+=(_,_)=>{_stackedListExpanded=!(_stackedListExpanded??ClientSize.Height>=UiTheme.Px(this,860));ReflowRadar();};
        var actions=Flow();actions.Padding=new(0,12,0,0);
        StyleButton(_start,"RADARI BAŞLAT",async(_,_)=>{if(_controller.Running)await StopCapture();else StartCapture();},true);_start.Width=184;
        StyleButton(_demo,"Örnek göster",async(_,_)=>{if(_busy)return;await StopCapture();if(!_closing){_controller.LoadDemo();UpdateView();}});
        StyleButton(_motionButton,"Canlı yön",async(_,_)=>await ToggleMotion());
        var mini=new HudButton {Text="Mini radar",Width=112,Tone=ButtonTone.Ghost};mini.Click+=(_,_)=>OpenMini();
        actions.Controls.AddRange([_start,_demo,_motionButton,mini]);layout.Controls.Add(actions,0,3);
        _eventStrip.Dock=DockStyle.Fill;_eventStrip.ForeColor=Gold;_eventStrip.AutoEllipsis=true;_eventStrip.Cursor=Cursors.Hand;_eventStrip.Font=new("Segoe UI",10);_eventStrip.TextAlign=ContentAlignment.MiddleLeft;_eventStrip.Click+=(_,_)=>Navigate(DateTimeOffset.UtcNow<_eventAlertUntil?_eventAlertPage:1);layout.Controls.Add(_eventStrip,0,4);
        InitializeCompanion();BuildSettings((Panel)_pages[2]);SizeChanged+=(_,_)=>ReflowRadar();ReflowRadar();Navigate(0);
    }
    private async Task ToggleMotion()
    {
        if(_busy||_closing)return;_motionButton.Enabled=false;
        try{if(_motion.Active){_motionRequested=false;await _motion.StopAsync();}else{_motionRequested=true;if(!_controller.Running)StartCapture();await _motion.StartAsync();}}
        finally{if(!_closing){_motionButton.Enabled=true;UpdateView();}}
    }
    private void Navigate(int index)
    {
        _page=index;for(var i=0;i<_pages.Count;i++){_pages[i].Visible=i==index;_navigation[i].Active=i==index;_navigation[i].Invalidate();}
        _pages[index].BringToFront();if(index==2)UpdateDebug();
    }
    private void ReflowRadar()
    {
        if(_radarBody is null)return;var stacked=ClientSize.Width<UiTheme.Px(this,1000);
        _radarBody.SuspendLayout();_radarBody.SetCellPosition(_nearbyCard,new(stacked?0:1,stacked?1:0));
        _radarBody.ColumnStyles[0].SizeType=SizeType.Percent;_radarBody.ColumnStyles[0].Width=stacked?100:60;
        _radarBody.ColumnStyles[1].SizeType=SizeType.Percent;_radarBody.ColumnStyles[1].Width=stacked?0:40;
        var expanded=!stacked||(_stackedListExpanded??ClientSize.Height>=UiTheme.Px(this,860));
        _list.Visible=_lockTarget.Visible=expanded;
        _radarBody.RowStyles[1].SizeType=SizeType.Absolute;_radarBody.RowStyles[1].Height=stacked?UiTheme.Px(this,expanded?220:48):0;
        _nearbyCard.Padding=new(UiTheme.Px(this,stacked?8:12));_nearbyLayout.RowStyles[2].Height=UiTheme.Px(this,stacked?34:40);
        _list.ColumnHeadersHeight=UiTheme.Px(this,stacked?30:38);foreach(DataGridViewRow row in _list.Rows)row.Height=UiTheme.Px(this,stacked?36:44);
        _radarCard.Margin=stacked?new(0,0,0,8):new(0,0,12,0);_radarBody.ResumeLayout();
    }
    private void BuildSettings(Panel page)
    {
        var sections=new TableLayoutPanel {Dock=DockStyle.Top,AutoSize=true,AutoSizeMode=AutoSizeMode.GrowAndShrink,ColumnCount=1,Padding=new(0,8,0,0)};page.Controls.Add(sections);
        SurfaceCard Group(string title,int height)
        {
            var card=new SurfaceCard {Height=height,Dock=DockStyle.Top,Margin=new(0,0,0,12)};
            var label=UiTheme.Label(title,10,Muted,FontStyle.Bold);label.Dock=DockStyle.Top;label.Height=24;card.Controls.Add(label);
            sections.RowStyles.Add(new(SizeType.AutoSize));sections.Controls.Add(card,0,sections.RowCount++);return card;
        }
        FlowLayoutPanel Row(SurfaceCard card,int top,int height=38)
        {var row=new FlowLayoutPanel {Dock=DockStyle.None,Anchor=AnchorStyles.Left|AnchorStyles.Top|AnchorStyles.Right,Location=new(16,top),Size=new(card.Width-32,height),WrapContents=true,BackColor=UiTheme.Surface,Margin=Padding.Empty};card.Controls.Add(row);return row;}
        var general=Group("GENEL",120);var auto=new ToggleSwitch {Text="Açılışta radarı başlat",Checked=_preferences.Value.AutoStart};Row(general,44).Controls.AddRange([_top,auto]);
        var note=UiTheme.Label(_preferences.Error??"Türkçe  ·  Cube Radar TR 0.8.11",10,Muted);note.Location=new(16,82);note.Size=new(general.Width-32,24);note.Anchor=AnchorStyles.Top|AnchorStyles.Left|AnchorStyles.Right;note.Dock=DockStyle.None;general.Controls.Add(note);
        var profile=Group("SUNUCU VE SAAT",150);var profileControls=_bosses.ProfileControls();profileControls.Location=new(16,44);profileControls.Size=new(profile.Width-32,90);profileControls.Anchor=AnchorStyles.Left|AnchorStyles.Top|AnchorStyles.Right;profile.Controls.Add(profileControls);
        var radar=Group("RADAR",270);var range=new ComboBox {DropDownStyle=ComboBoxStyle.DropDownList,Width=130};range.Items.AddRange(["75 m","150 m","300 m"]);range.SelectedIndex=Array.IndexOf(new[]{75,150,300},_preferences.Value.RangeM);UiTheme.Input(range);
        var refresh=new ComboBox {DropDownStyle=ComboBoxStyle.DropDownList,Width=180};refresh.Items.AddRange(["Akıcı · 100 ms","Dengeli · 200 ms","Tasarruf · 500 ms"]);refresh.SelectedIndex=Array.IndexOf(new[]{100,200,500},_preferences.Value.RefreshMs);UiTheme.Input(refresh);
        var rangeLabel=UiTheme.Label("Görsel menzil",10,Muted);rangeLabel.Dock=DockStyle.None;rangeLabel.Size=new(110,30);var refreshLabel=UiTheme.Label("Yenileme",10,Muted);refreshLabel.Dock=DockStyle.None;refreshLabel.Size=new(85,30);
        Row(radar,44).Controls.AddRange([rangeLabel,range,refreshLabel,refresh]);Row(radar,88).Controls.AddRange([_outside,_heightFilter]);
        _radarOrientation.Items.AddRange(["Karakter yönü","Kuzey sabit"]);_radarOrientation.SelectedIndex=(int)_preferences.Value.RadarOrientation;UiTheme.Input(_radarOrientation);
        _sonar.Checked=_preferences.Value.SonarEnabled;
        var orientationLabel=UiTheme.Label("Radar yönü",10,Muted);orientationLabel.Dock=DockStyle.None;orientationLabel.Size=new(110,30);
        Row(radar,128).Controls.AddRange([orientationLabel,_radarOrientation,_sonar]);
        _showCreatures.Checked=_preferences.Value.ShowCreatures;
        var creatureScope=UiTheme.Label("Okunan Pet kaynakları",10,Muted);creatureScope.Dock=DockStyle.None;creatureScope.Size=new(210,30);
        Row(radar,168).Controls.AddRange([_showCreatures,creatureScope]);
        var lockNote=UiTheme.Label("Hedef kilidi, Radar ekranında seçtiğin küpü izler.",10,Muted);lockNote.Dock=DockStyle.None;lockNote.Location=new(16,224);lockNote.Size=new(radar.Width-32,26);lockNote.Anchor=AnchorStyles.Top|AnchorStyles.Left|AnchorStyles.Right;radar.Controls.Add(lockNote);
        var sound=Group("SES",182);Row(sound,44).Controls.AddRange([_sound,_proximity,_bosses.SoundControl]);
        _volume.Value=_preferences.Value.Volume;_volume.BackColor=UiTheme.Surface;var volumeLabel=UiTheme.Label($"%{_volume.Value}",10,Muted);volumeLabel.Dock=DockStyle.None;volumeLabel.Size=new(50,32);
        var cubeTest=new HudButton {Text="Küp sesi testi",Width=120,Height=34};cubeTest.Click+=(_,_)=>PlayAlertSound();var bossTest=new HudButton {Text="Boss sesi testi",Width=128,Height=34};bossTest.Click+=(_,_)=>PlayBossSound();Row(sound,96,48).Controls.AddRange([_volume,volumeLabel,cubeTest,bossTest]);
        var mini=Group("MİNİ RADAR",126);_miniCompact.Checked=_preferences.Value.MiniCompact;_miniPass.Checked=_preferences.Value.MiniClickThrough;_miniTop.Checked=_preferences.Value.MiniTopMost;Row(mini,44).Controls.AddRange([_miniCompact,_miniPass,_miniTop]);
        var miniNote=UiTheme.Label("Miniyi sürükleyerek taşı. Tıklama geçişini buradan kapatabilir veya Mini radar düğmesine yeniden basabilirsin.",10,Muted);miniNote.Dock=DockStyle.None;miniNote.Location=new(16,84);miniNote.Size=new(mini.Width-32,30);miniNote.Anchor=AnchorStyles.Top|AnchorStyles.Left|AnchorStyles.Right;mini.Controls.Add(miniNote);
        _miniCompact.CheckedChanged+=(_,_)=>ApplyMiniPreferences();_miniPass.CheckedChanged+=(_,_)=>ApplyMiniPreferences();_miniTop.CheckedChanged+=(_,_)=>ApplyMiniPreferences();
        _advancedCard=Group("GELİŞMİŞ",230);_movement.Text="Hareket yönünü kullan";Row(_advancedCard,44).Controls.AddRange([_movement,_debugToggle,_reconnect]);
        _traffic.Dock=DockStyle.None;_traffic.Location=new(16,90);_traffic.Size=new(_advancedCard.Width-32,66);_traffic.Anchor=AnchorStyles.Left|AnchorStyles.Top|AnchorStyles.Right;_traffic.ForeColor=Muted;_traffic.Font=new("Segoe UI",10);_traffic.AutoEllipsis=true;_advancedCard.Controls.Add(_traffic);
        _freshness.Dock=DockStyle.None;_freshness.Location=new(16,162);_freshness.Size=new(_advancedCard.Width-32,40);_freshness.Anchor=AnchorStyles.Left|AnchorStyles.Top|AnchorStyles.Right;_freshness.ForeColor=Muted;_advancedCard.Controls.Add(_freshness);
        _debugPanel.Dock=DockStyle.Bottom;_debugPanel.Height=310;_debugPanel.BackColor=UiTheme.Surface;_debug.BackColor=UiTheme.Surface;_debug.ForeColor=Fg;_debug.Font=new("Consolas",10);
        var save=new HudButton {Text="Tanıyı kaydet",Height=38,Dock=DockStyle.Bottom};save.Click+=(_,_)=>SaveDiagnostics();_debugPanel.Controls.Add(_debug);_debugPanel.Controls.Add(save);_advancedCard.Controls.Add(_debugPanel);
        _top.CheckedChanged+=(_,_)=>{TopMost=_top.Checked;_bosses.UpdateEditorTopMost(TopMost);SavePreferences();};_sound.CheckedChanged+=(_,_)=>{if(!_sound.Checked)CubeSound.Close();SavePreferences();};
        _proximity.CheckedChanged+=(_,_)=>SavePreferences();_outside.CheckedChanged+=(_,_)=>{SavePreferences();UpdateView();};_movement.CheckedChanged+=(_,_)=>{SavePreferences();UpdateView();};
        _debugToggle.CheckedChanged+=(_,_)=>ToggleDebug();auto.CheckedChanged+=(_,_)=>{_preferences.Value.AutoStart=auto.Checked;SavePreferences();};
        _volume.ValueChanged+=(_,_)=>{volumeLabel.Text=$"%{_volume.Value}";SavePreferences();};
        range.SelectedIndexChanged+=(_,_)=>{_preferences.Value.RangeM=new[]{75,150,300}[range.SelectedIndex];_radar.RangeM=_preferences.Value.RangeM;SavePreferences();UpdateView();};
        refresh.SelectedIndexChanged+=(_,_)=>{_preferences.Value.RefreshMs=new[]{100,200,500}[refresh.SelectedIndex];_timer.Interval=_preferences.Value.RefreshMs;SavePreferences();};
        _radarOrientation.SelectedIndexChanged+=(_,_)=>{_preferences.Value.RadarOrientation=(CubeRadar.Core.Radar.RadarOrientationMode)_radarOrientation.SelectedIndex;SavePreferences();UpdateView();};
        _sonar.CheckedChanged+=(_,_)=>{_preferences.Value.SonarEnabled=_sonar.Checked;SavePreferences();UpdateView();};
        _showCreatures.CheckedChanged+=(_,_)=>{_preferences.Value.ShowCreatures=_showCreatures.Checked;SavePreferences();UpdateView();};
    }
    private void SavePreferences()
    {
        if(!_preferencesReady)return;var p=_preferences.Value;p.Volume=_volume.Value;p.TopMost=_top.Checked;p.Sound=_sound.Checked;p.Proximity=_proximity.Checked;p.Outside=_outside.Checked;p.Movement=_movement.Checked;_preferences.Save();
    }
    private void ApplyMiniPreferences()
    {
        var p=_preferences.Value;p.MiniCompact=_miniCompact.Checked;p.MiniClickThrough=_miniPass.Checked;p.MiniTopMost=_miniTop.Checked;
        if(_mini is {IsDisposed:false}){_mini.SetCompact(p.MiniCompact);_mini.SetClickThrough(p.MiniClickThrough);_mini.TopMost=p.MiniTopMost;}
        SavePreferences();
    }
}
