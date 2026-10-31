using System.Text.Json;
using CubeRadar.Core.Radar;

namespace CubeRadar.Tr;

internal sealed partial class RadarForm
{
    internal object VerifyTacticalRadar(Action<Form,string> save)
    {
        if(!_previewMode)throw new InvalidOperationException("Yalnız sentetik önizleme.");
        PreviewScenario();ClientSize=new(1080,840);
        var original=_previewSnapshot!;var self=original.Self!;var seed=original.Cubes[0];
        var world=original with {Cubes=[
            seed with {ObjectId=90001,X=self.X,Y=self.Y-5000,Z=self.Z},
            seed with {ObjectId=90002,X=self.X+4000,Y=self.Y,Z=self.Z+500},
            seed with {ObjectId=90003,X=self.X-4000,Y=self.Y+4000,Z=self.Z-500},
            seed with {ObjectId=90004,X=self.X+16000,Y=self.Y,Z=self.Z}]};
        var before=JsonSerializer.Serialize(world);
        var oldRange=_radar.RangeM;var oldOutside=_outside.Checked;
        var oldMode=_radarOrientation.SelectedIndex;var oldSonar=_sonar.Checked;
        var proof=new List<object>();
        try
        {
            _previewSnapshot=world;_outside.Checked=false;_radar.RangeM=75;
            _radarOrientation.SelectedIndex=0;_sonar.Checked=true;_radar.PreviewPulseMs=0;
            foreach(var heading in new[]{0,90,180})
            {
                _previewHeading=heading;UpdateView();save(this,$".radar-heading-{heading}.png");
                var center=_radar.PlayerCenter;var radius=_radar.Radius*50/75;
                var expected=heading switch {0=>new PointF(center.X,center.Y-radius),90=>new PointF(center.X-radius,center.Y),_=>new PointF(center.X,center.Y+radius)};
                var point=_radar.RenderedPoint(90001)??throw new InvalidOperationException("Kuzey hedefi çizilmedi.");
                if(Math.Abs(point.X-expected.X)>.01||Math.Abs(point.Y-expected.Y)>.01||_radar.RenderedCount!=3)
                    throw new InvalidOperationException("Radar heading/menzil dönüşümü hatalı.");
                var arrow=_radar.RenderedArrowTip??throw new InvalidOperationException("Oyuncu oku çizilmedi.");
                if(Math.Abs(arrow.X-center.X)>.01||arrow.Y>=center.Y)throw new InvalidOperationException("Karakter yönünde oyuncu oku yukarı bakmıyor.");
                proof.Add(new {heading,point,center,visible=_radar.RenderedCount,arrow});
            }
            _previewHeading=0;_radar.PreviewPulseMs=1250;UpdateView();save(this,".radar-sonar-mid.png");
            var pulsePoint=_radar.RenderedPoint(90001);var count=_radar.RenderedCount;
            _sonar.Checked=false;UpdateView();save(this,".radar-sonar-off.png");
            if(_radar.RenderedCount!=count||_radar.RenderedPoint(90001)!=pulsePoint)throw new InvalidOperationException("Sonar entity sunumunu değiştirdi.");
            _sonar.Checked=true;
            _radar.RoutePoints=new (double,double)[] {(self.X-6500,self.Y+1000),(self.X-3000,self.Y-2500),
                (self.X+500,self.Y-6000),(self.X+3500,self.Y-1500),(self.X+9000,self.Y+4000)};
            _previewHeading=45;UpdateView();save(this,".radar-route-entities.png");
            _radar.RoutePoints=Array.Empty<(double,double)>();
            _previewHeading=90;_radarOrientation.SelectedIndex=1;UpdateView();save(this,".radar-north-up.png");
            var north=_radar.RenderedPoint(90001)!.Value;var northCenter=_radar.PlayerCenter;
            if(Math.Abs(north.X-northCenter.X)>.01||north.Y>=northCenter.Y||_radar.RenderedArrowTip!.Value.X<=northCenter.X)
                throw new InvalidOperationException("Kuzey sabit görünüm/oyuncu oku hatalı.");
            _previewDirectionMissing=true;_radarOrientation.SelectedIndex=0;UpdateView();save(this,".radar-heading-unknown.png");
            if(_radar.RenderedCount!=0||_radar.RenderedArrowTip is not null)throw new InvalidOperationException("Bilinmeyen heading ile taktik veri çizildi.");
            _radarOrientation.SelectedIndex=1;UpdateView();save(this,".radar-north-unknown.png");
            if(_radar.RenderedCount!=3||_radar.RenderedArrowTip is not null)throw new InvalidOperationException("Kuzey sabit mod bilinmeyen heading davranışı hatalı.");
            _previewDirectionMissing=false;_radarOrientation.SelectedIndex=0;
            _previewSnapshot=world with {Self=self with {TimeMs=world.NowMs-60_000}};
            UpdateView();save(this,".radar-stale.png");
            if(_radar.RenderedCount!=0)throw new InvalidOperationException("Eski konumla radar çizildi.");
            _previewSnapshot=world;_previewHeading=0;ClientSize=new(680,760);UpdateView();save(this,".radar-compact.png");
            ClientSize=new(1080,840);PreviewSettings();save(this,".radar-settings.png");
            if(before!=JsonSerializer.Serialize(world))throw new InvalidOperationException("Radar kaynak entity verisini değiştirdi.");
            return new {source="Synthetic world XY centimetres, Z height; same four entities in every preview",frames=proof,
                sonarChangesEntityState=false,unknownHeadingHidden=true,stalePositionHidden=true,routeWorldCoordinatesPreserved=true,
                outsideNormalEntitiesHidden=true,orientationSwitchVerified=true};
        }
        finally
        {
            _previewHeading=0;_previewDirectionMissing=false;_previewSnapshot=original;
            _radar.PreviewPulseMs=null;_radar.RoutePoints=Array.Empty<(double,double)>();
            _radar.RangeM=oldRange;_outside.Checked=oldOutside;_radarOrientation.SelectedIndex=oldMode;_sonar.Checked=oldSonar;
            Navigate(0);ClientSize=new(1080,840);UpdateView();
        }
    }
}
