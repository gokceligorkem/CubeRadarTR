using System.Text.Json;
using CubeRadar.Core.Radar;
using CubeRadar.Cli;

namespace CubeRadar.Tr;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        ApplicationConfiguration.Initialize();
        try
        {
            var controller = new CaptureController();
            if(args is ["--pet-probe",var petReport])
            {
                var started=controller.Start();long previous=-1;
                try
                {
                    for(var i=0;i<600&&started;i++)
                    {
                        Thread.Sleep(1000);var now=DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();var pets=controller.ReadPets(now);
                        if(pets.Version==previous&&i%10!=0)continue;previous=pets.Version;
                        var json=JsonSerializer.Serialize(new {started,atMs=now,packets=controller.Diagnostics.Packets,pets,combat=controller.PetCombatDiagnostics,sync=controller.PetSyncDiagnostics});
                        File.WriteAllText(petReport+".tmp",json);File.Move(petReport+".tmp",petReport,true);
                    }
                }
                finally{controller.StopAsync().GetAwaiter().GetResult();}
                return;
            }
            if(args is ["--sync-boss-records",var records])
            {
                if(new FileInfo(records).Length>4*1024*1024)throw new InvalidDataException("Kayıt dosyası çok büyük.");
                var observations=new List<CubeRadar.Core.Protocol.BossObservation>();
                foreach(var line in File.ReadLines(records))
                {
                    using var json=JsonDocument.Parse(line);var root=json.RootElement;
                    if(root.GetProperty("op").GetString()!="0191"||!CubeRadar.Core.Protocol.FieldBossMessages.TryParse(Convert.FromHexString(root.GetProperty("hex").GetString()!),root.GetProperty("t").GetInt64(),out var list))throw new InvalidDataException("Boss kaydı geçersiz.");
                    observations.Add(new CubeRadar.Core.Protocol.BossListObservation(list));
                }
                using var panel=new BossPanel(false);panel.Observe(observations);
                File.WriteAllText(records+".applied.json",JsonSerializer.Serialize(new {acceptedRecords=observations.Count,source="local game field-boss capture"}));return;
            }
            if(args is ["--boss-sound-test",var soundPath])
            {
                File.WriteAllBytes(soundPath,CubeRadar.Core.Audio.BossFanfare.Create(85));
                CubeSound.Prepare();var note=CubeSound.PlayBoss(85);Thread.Sleep(2800);CubeSound.Close();
                File.WriteAllText(soundPath+".json",JsonSerializer.Serialize(new {outputAccepted=note is null,note,durationSeconds=2.6}));
                Environment.ExitCode=note is null?0:1;return;
            }
            if(args is ["--check-boss-capture",var bossReport])
            {
                var started=controller.Start();if(started)Thread.Sleep(15000);
                var updates=controller.TakeBossUpdates();var traffic=controller.Diagnostics;
                controller.StopAsync().GetAwaiter().GetResult();
                File.WriteAllText(bossReport,JsonSerializer.Serialize(new {started,packets=traffic.Packets,frames=traffic.Frames,
                    lists=updates.OfType<CubeRadar.Core.Protocol.BossListObservation>().Select(u=>new {u.List.MapId,u.List.ObservedMs,slots=u.List.Slots,
                        mapped=BossCatalog.ForMap(u.List.MapId,u.List.Slots.Count).Select(b=>new {b.SlotId,b.Name})}),
                    kills=updates.OfType<CubeRadar.Core.Protocol.BossKillObservation>()},new JsonSerializerOptions {WriteIndented=true}));return;
            }
            if(args is ["--sound-test",var soundReport])
            {
                CubeSound.Prepare();
                var note=CubeSound.Play(null,85);
                Thread.Sleep(1600);
                CubeSound.Close();
                File.WriteAllText(soundReport,JsonSerializer.Serialize(new {outputAccepted=note is null,note}));
                Environment.ExitCode=note is null?0:1;
                return;
            }
            if(args is ["--check-capture", var report])
            {
                var started=controller.Start();
                if(started) Thread.Sleep(6000);
                var status=controller.Status;
                var traffic=controller.Diagnostics;
                var snapshot=controller.Snapshot();
                Task.WhenAll(controller.StopAsync(),controller.StopAsync()).GetAwaiter().GetResult();
                File.WriteAllText(report,JsonSerializer.Serialize(new {
                    started,status,packets=traffic.Packets,frames=traffic.Frames,streams=traffic.Streams,
                    ownIdentityKnown=snapshot.SelfId is not null,ownPositionKnown=snapshot.Self is not null,
                    identitySource=snapshot.IdentitySource.ToString(),
                    cubeRecords=snapshot.Cubes.Count,headingSource="Unknown",
                    stopped=!controller.Running,npcapInstalled=CaptureController.DriverInstalled
                },new JsonSerializerOptions { WriteIndented=true }));
                Environment.ExitCode=controller.Running?1:0;
                return;
            }
            if (args is ["--self-test", var output])
            {
                controller.LoadDemo();
                var snapshot = controller.Snapshot();
                var state = CubeCompanionView.Build(snapshot, snapshot.NowMs);
                if (state.Cubes.Count < 1 || state.Nearest is null || !state.FreshPosition || controller.Running)
                    throw new InvalidOperationException("Kayıtlı küp verisi testi başarısız.");
                using var form = new RadarForm(controller, previewMode:true);
                form.StartPosition=FormStartPosition.Manual;
                form.Location=new Point(-20_000,-20_000);
                form.ShowInTaskbar=false;
                form.Show();
                Application.DoEvents();
                form.UpdateView();
                form.CreateControl();
                _ = form.Handle;
                form.PerformLayout();
                using var bitmap = new Bitmap(form.Width, form.Height);
                form.DrawToBitmap(bitmap, new Rectangle(Point.Empty,bitmap.Size));
                Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(output))!);
                bitmap.Save(output, System.Drawing.Imaging.ImageFormat.Png);
                form.PreviewDiagnostics();
                Application.DoEvents();
                using var debugBitmap=new Bitmap(form.Width,form.Height);
                form.DrawToBitmap(debugBitmap,new Rectangle(Point.Empty,debugBitmap.Size));
                debugBitmap.Save(output+".debug.png",System.Drawing.Imaging.ImageFormat.Png);
                form.PreviewScenario();
                form.VerifyRadarOnly();
                form.VerifyHudInteractions();
                var tacticalRadarProof=form.VerifyTacticalRadar((preview,suffix)=>SavePreview(preview,output+suffix));
                var creatureRadarProof=form.VerifyRadarCreatures((preview,suffix)=>SavePreview(preview,output+suffix));
                var lastKnownRadarProof=form.VerifyLastKnownRadar((preview,suffix)=>SavePreview(preview,output+suffix));
                SavePreview(form,output+".layout.png");
                form.ClientSize=new(680,760);
                SavePreview(form,output+".compact.png");
                form.ClientSize=new(700,900);form.PreviewExpandedList();
                SavePreview(form,output+".list-expanded.png");
                form.ClientSize=new(680,760);
                form.PreviewScenario(headingKnown:false);
                SavePreview(form,output+".unknown.png");
                form.PreviewEmpty();SavePreview(form,output+".empty.png");form.PreviewScenario();
                form.ClientSize=new(1080,840);
                form.PreviewEvents();SavePreview(form,output+".events-wide.png");
                form.ClientSize=new Size(760,900);
                form.PreviewEvents();
                SavePreview(form,output+".events.png");
                form.PreviewPets();SavePreview(form,output+".pets.png");
                var petMini=form.PreviewPetMini();SavePreview(petMini,output+".pet-mini.png");
                form.PreviewPetMiniList();SavePreview(petMini,output+".pet-mini-list.png");
                form.PreviewScenario();
                var mini=(MiniRadarForm)form.PreviewMini();SavePreview(mini,output+".mini.png");
                mini.SetCompact(true);SavePreview(mini,output+".mini-compact.png");mini.SetCompact(false);
                form.ClientSize=new(680,760);
                form.PreviewLockedAndSettings();
                SavePreview(form,output+".locked.png");
                form.PreviewSettings();SavePreview(form,output+".settings.png");
                form.PreviewSettingsSection(false);SavePreview(form,output+".settings-sound.png");
                form.PreviewSettingsSection(true);SavePreview(form,output+".settings-advanced.png");
                using(var editor=new BossEditor(new BossEvent {Name="ÖRNEK · Boss",Map="Altgard",Location="Örnek konum",Times=["21:30"]}))
                {
                    editor.StartPosition=FormStartPosition.Manual;editor.Location=new(-20000,-20000);editor.ShowInTaskbar=false;editor.Show();
                    SavePreview(editor,output+".editor.png");editor.Close();
                }
                foreach(var factor in new[]{1.25f,1.5f})
                {
                    var scaledController=new CaptureController();scaledController.LoadDemo();
                    using var scaled=new RadarForm(scaledController,previewMode:true);scaled.Location=new(-20000,-20000);scaled.ShowInTaskbar=false;scaled.Show();scaled.PreviewScenario();
                    RadarForm.ScalePreview(scaled,factor);SavePreview(scaled,output+$".scale-{factor*100:0}.png");
                    scaled.PreviewEvents();SavePreview(scaled,output+$".events-scale-{factor*100:0}.png");
                    scaled.PreviewPets();SavePreview(scaled,output+$".pets-scale-{factor*100:0}.png");
                    scaled.VerifyRadarOnly();scaled.Close();
                }
                File.WriteAllText(output+".json", JsonSerializer.Serialize(new {
                    result="passed", cubes=state.Cubes.Count, nearestM=state.Nearest.DistanceM,
                    tacticalRadarProof,
                    creatureRadarProof,
                    lastKnownRadarProof,
                    radarOnlyProof="Four pages: Radar, Boss, Pet, Settings. No automatic-hunt engine, editor, Macro recorder or native keyboard/input hooks in production assemblies.",
                    petInteractionProof="Pet Avı navigation, live-model demo rows, persistent goal/checkbox, stable cell/row across refresh, search and 125/150 percent scaling",
                    petMiniProof="Independent visible/topmost mini survives main minimize; selected pet shows kills, gained souls and remaining goal; shared updates continue while minimized; shutdown disposes mini",
                    petNearbyVisibilityProof="Main tracked view and mini drop distant/removed pets and return them on approach; 150m 3D proximity; stale positionless sightings hidden; balances, goals and opt-outs preserved",
                    petMiniListProof="Borderless compact rows and auto height for 1/3 tracked pets; 55 percent native layered alpha applied and 100 percent restored; appearance saved; native corner resize hit tests; legacy size migration preserves goals and position. Bitmap previews show layout without compositor opacity.",
                    petRankTransitionProof="Ready rank-1 pet remains visible; confirmed rank 2 promotes target to 3 and shows 75 souls in main/mini; confirmed MAX hides from both lists when Hide completed is enabled.",
                    petAutomaticTrackingProof="Verified nearby missing pets auto-add; real mini menu untracks and serializes exclusion; repeated updates do not re-add; main missing-souls row stays unchecked; explicit reselect resumes; missing view retains distant balances.",
                    npcapInstalled=CaptureController.DriverInstalled, captureStarted=controller.Running,
                    source="upstream recorded JSON protocol fixture; not a live-game validation",
                    layoutProof="layout/compact/unknown/locked/events/mini/editor/settings PNGs use explicitly labelled synthetic demo data",
                    bossInteractionProof="40 native grid rows: scroll, current cell, row identity, column width/order and per-boss checkbox preserved across ticks and game timestamp updates",
                    hudInteractionProof="stable cube rows; lock identity; height filter; navigation; saved volume and mute; compact/expanded list; mini compact and native click-through enable/disable; main radar continues after mini closes",
                    miniMinimizeProof="main form minimized while independent mini stays native-visible, normal and topmost; radar updates continue; main shutdown disposes mini",
                    bossEditorProof="independent non-modal editor stays above topmost main, survives main minimize, saves without overwriting newer game data",
                    dialogWindowProof="Presets are modal with a native top-level owner and matching topmost on/off; sibling overlays yield and restore; timezone selectable; Cancel preserves events; common-dialog owner checked; independent editor topmost updates",
                    freshInstallProof="legacy random-id Kaira repaired by Refresh; empty game time uses confirmed BABA cadence; all 8 Abyss slots imported; watch choice, row selection and alert history preserved",
                    scalingProof="125/150 percent deterministic control/text scaling; native WinForms PerMonitorV2 enabled; Windows display settings unchanged",
                    bossPresets=BossStore.BabaPresets()
                },new JsonSerializerOptions { WriteIndented=true }));
                form.Close();Application.DoEvents();
                if(!mini.IsDisposed)throw new InvalidOperationException("Ana uygulama kapandıktan sonra mini radar açık kaldı.");
                if(!petMini.IsDisposed)throw new InvalidOperationException("Ana uygulama kapandıktan sonra mini pet açık kaldı.");
                return;
            }
            if(args.Contains("--start")||args.Contains("--motion"))controller.Start();
            Application.Run(new RadarForm(controller,startMotion:args.Contains("--motion"),keepOnTop:args.Contains("--topmost"),
                proximity:args.Contains("--proximity"),movementFallback:args.Contains("--movement")));
        }
        catch(Exception error)
        {
            if(args.Length==2&&args[0].StartsWith("--",StringComparison.Ordinal))
            {
                File.WriteAllText(args[1]+".error.txt",error.ToString());
                Environment.ExitCode=1;
            }
            else MessageBox.Show(error.Message,"Cube Radar TR",MessageBoxButtons.OK,MessageBoxIcon.Error);
        }
    }
    private static void SavePreview(Form form,string path)
    {
        Application.DoEvents();form.PerformLayout();form.Refresh();
        using var bitmap=new Bitmap(form.Width,form.Height);
        form.DrawToBitmap(bitmap,new Rectangle(Point.Empty,bitmap.Size));
        bitmap.Save(path,System.Drawing.Imaging.ImageFormat.Png);
    }
}
