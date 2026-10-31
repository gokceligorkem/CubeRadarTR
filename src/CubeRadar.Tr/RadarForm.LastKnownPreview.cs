using CubeRadar.Core.Pets;
using CubeRadar.Core.Radar;

namespace CubeRadar.Tr;

internal sealed partial class RadarForm
{
    internal object VerifyLastKnownRadar(Action<Form,string> save)
    {
        if(!_previewMode)throw new InvalidOperationException();PreviewScenario();
        var world=_previewSnapshot! with {MapId=1110};var self=world.Self!;var now=world.NowMs;var axes=Core.Radar.Orientation.Parse("-y");
        var source=PetCatalog.Default.Sources[0];
        PetMob[] mobs=[new(92001,source.NpcId,source.PetId,source.Name,(float)self.X-2000,(float)self.Y-3000,(float)self.Z,now,now)
            {WireEntityId=92001,LifeId=1}];
        var motion=new ClientMotionFix(self.X,self.Y,self.Z,-90,null,now,"0137") {PoseGeneration=1,PoseSerial=2};
        var memory=new RadarPoseMemory();
        var current=ClientMotionPresentation.Apply(world,motion,now,true);var live=CubeCompanionView.Build(current,now);
        memory.Read(current,live,motion,true,true,1,123);
        var later=world with {NowMs=now+60000};var strict=CubeCompanionView.Build(later,now+60000);
        var held=memory.Read(later,strict,motion,true,true,1,123)!.Value;
        var visual=strict.WithLastKnownOrigin(held.Self,world.Orientation);
        try
        {
            _radar.PreviewPulseMs=null;_radar.SetCreatures(mobs,now+60000,true,HeightFilter.All);
            _state=strict;_radarDisplayView=visual;UpdateSummary(held.Heading,true,true);UpdateRows(null);
            _radar.SetState(visual,held.Self,axes,held.Heading,false,true);
            _mode.Text="● ÖRNEK · Son bilinen konum · güncel değil";_headline.Text="Güncel konum bekleniyor";
            _detail.Text="Soluk işaretler son okunan konumları gösterir.";
            save(this,".last-known.png");
            if(!_radar.ShowsLastKnownPosition||_radar.RenderedCount==0||_radar.RenderedLastKnownCreatureCount!=1||_radar.AnimationRunning)
                throw new InvalidOperationException("Son bilinen radar yanlış canlılık/çizim durumunda.");
            _radar.SetCreatures(Array.Empty<PetMob>(),now+60000,true,HeightFilter.All);save(this,".last-known-removed.png");
            if(_radar.RenderedCreatureCount!=0)throw new InvalidOperationException("Son bilinen yaratık kaldırılınca silinmedi.");
            var next=motion with {TimeMs=now+61000,PoseSerial=3};
            current=ClientMotionPresentation.Apply(world with {NowMs=now+61000},next,now+61000,true);
            live=CubeCompanionView.Build(current,now+61000);
            var resumed=memory.Read(current,live,next,true,true,1,123)!.Value;
            _state=live;_radarDisplayView=live;_target=live.Nearest;UpdateSummary(resumed.Heading,true,true);UpdateRows(resumed.Heading);
            _radar.SetCreatures([mobs[0] with {SeenMs=now+61000,PositionMs=now+61000}],now+61000,true,HeightFilter.All);
            _radar.SetState(live,resumed.Self,axes,resumed.Heading,false,true);_mode.Text="● ÖRNEK · Yeni gözlem geldi";
            save(this,".last-known-resumed.png");
            if(resumed.LastKnown||_radar.ShowsLastKnownPosition||_radar.RenderedLastKnownCreatureCount!=0)
                throw new InvalidOperationException("Yeni gözlem son bilinen durumunu kaldırmadı.");
            return new {heldWithoutFreshnessRenewal=true,heldSeconds=60,strictFresh=false,knownCreatureRemoved=true,
                newObservationResumesLive=true,sonarStoppedWhileHeld=true};
        }
        finally{_radar.PreviewPulseMs=null;UpdateView();}
    }
}
