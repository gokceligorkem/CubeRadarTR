using CubeRadar.Core.Pets;
using CubeRadar.Core.Radar;

namespace CubeRadar.Tr;

internal sealed partial class RadarForm
{
    private IReadOnlyList<PetMob> _radarCreatures=Array.Empty<PetMob>();
    private IReadOnlyList<PetMob>? _previewCreatures;
    private void UpdateRadarCreatures(long now)
    {
        _radarCreatures=_previewMode?_previewCreatures??Array.Empty<PetMob>():_controller.ReadRadarMobs();
        _radar.SetCreatures(_radarCreatures,now,_preferences.Value.ShowCreatures,(HeightFilter)_heightFilter.SelectedIndex);
    }
    internal object VerifyRadarCreatures(Action<Form,string> save)
    {
        if(!_previewMode)throw new InvalidOperationException("Yalnız sentetik önizleme.");
        PreviewScenario();var world=_previewSnapshot!;var self=world.Self!;var now=world.NowMs;
        var source=PetCatalog.Default.Sources[0];
        PetMob Mob(uint id,float dx,float dy,float z=0)=>new(id,source.NpcId,source.PetId,source.Name,
            (float)self.X+dx,(float)self.Y+dy,(float)self.Z+z,now,now) {WireEntityId=id,LifeId=id,BirthMs=now};
        PetMob[] mobs=[Mob(91001,0,-5000),Mob(91002,4000,-1500,500),Mob(91003,-3000,2500),Mob(91004,16000,0)];
        var before=System.Text.Json.JsonSerializer.Serialize(mobs);
        var oldMode=_radarOrientation.SelectedIndex;var oldShow=_showCreatures.Checked;var oldRange=_radar.RangeM;
        var oldOutside=_outside.Checked;var oldHeight=_heightFilter.SelectedIndex;
        try
        {
            _previewCreatures=mobs;_showCreatures.Checked=true;_radarOrientation.SelectedIndex=0;_heightFilter.SelectedIndex=0;
            _radar.RangeM=75;_outside.Checked=true;_radar.PreviewPulseMs=1250;
            foreach(var heading in new[]{0,90})
            {
                _previewHeading=heading;UpdateView();save(this,$".creatures-{heading}.png");
                if(_radar.RenderedCreatureCount!=3)throw new InvalidOperationException("Yaratıklar çizilmedi veya menzil dışındaki yaratık kenara taşındı.");
                var point=_radar.RenderedCreaturePoint(91001)!.Value;var center=_radar.PlayerCenter;
                if(heading==0&&(Math.Abs(point.X-center.X)>.02||point.Y>=center.Y)||
                    heading==90&&(point.X>=center.X||Math.Abs(point.Y-center.Y)>.02))
                    throw new InvalidOperationException("Yaratık yön dönüşümü hatalı.");
            }
            var cubeCount=_radar.RenderedCount;_showCreatures.Checked=false;UpdateView();save(this,".creatures-off.png");
            if(_radar.RenderedCreatureCount!=0||_radar.RenderedCount!=cubeCount)throw new InvalidOperationException("Yaratık ayarı küpleri etkiledi.");
            _showCreatures.Checked=true;_heightFilter.SelectedIndex=2;UpdateView();save(this,".creatures-height.png");
            if(_radar.RenderedCreatureCount!=1)throw new InvalidOperationException("Yaratık yükseklik filtresi uygulanmadı.");
            _heightFilter.SelectedIndex=0;_previewCreatures=[mobs[0] with {PositionMs=now-15001},mobs[1] with {X=null}];
            UpdateView();save(this,".creatures-stale.png");
            if(_radar.RenderedCreatureCount!=1||_radar.RenderedLastKnownCreatureCount!=1)throw new InvalidOperationException("Eski yaratık son bilinen olarak ayrılmadı veya konumsuz yaratık çizildi.");
            _previewCreatures=Array.Empty<PetMob>();UpdateView();save(this,".creatures-removed.png");
            if(_radar.RenderedCreatureCount!=0)throw new InvalidOperationException("Kaldırılan yaratık çizimde kaldı.");
            _previewCreatures=mobs;_previewDirectionMissing=true;UpdateView();save(this,".creatures-heading-unknown.png");
            if(_radar.RenderedCreatureCount!=0)throw new InvalidOperationException("Heading olmadan taktik yaratık çizildi.");
            _previewDirectionMissing=false;_previewSnapshot=world with {Self=self with {TimeMs=now-60000}};
            UpdateView();save(this,".creatures-position-stale.png");
            if(_radar.RenderedCreatureCount!=0)throw new InvalidOperationException("Eski oyuncu konumuyla yaratık çizildi.");
            _previewSnapshot=world;_previewHeading=0;ClientSize=new(680,760);UpdateView();save(this,".creatures-compact.png");
            var mini=(MiniRadarForm)PreviewMini();save(mini,".creatures-mini.png");
            if(mini.RenderedCreatureCount!=3)throw new InvalidOperationException("Mini radar yaratıkları göstermedi.");
            _showCreatures.Checked=false;UpdateView();save(mini,".creatures-mini-off.png");
            if(mini.RenderedCreatureCount!=0)throw new InvalidOperationException("Mini radar yaratık ayarını uygulamadı.");
            _showCreatures.Checked=true;ClientSize=new(1080,840);PreviewSettings();save(this,".creatures-settings.png");
            if(before!=System.Text.Json.JsonSerializer.Serialize(mobs))throw new InvalidOperationException("Yaratık world verisi değişti.");
            return new {scope="Existing PetCatalog creature observations only",visible=3,outsideHidden=true,
                headingRotation=true,togglePreservesCubes=true,heightFilter=true,removedHidden=true,staleExplicitlyLastKnown=true,sourceUnchanged=true,miniRadarVerified=true};
        }
        finally
        {
            _previewCreatures=null;_previewSnapshot=world;_previewHeading=0;_previewDirectionMissing=false;
            _radar.PreviewPulseMs=null;_radar.RangeM=oldRange;_outside.Checked=oldOutside;_heightFilter.SelectedIndex=oldHeight;
            _showCreatures.Checked=oldShow;_radarOrientation.SelectedIndex=oldMode;ClientSize=new(1080,840);UpdateView();
            Navigate(0);
        }
    }
}
