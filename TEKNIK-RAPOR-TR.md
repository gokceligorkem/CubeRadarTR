# Cube Radar TR 0.8.7 — teknik rapor

0.8.7: Kullanıcı ölüm sonrasında ilk açılışın Pet eşitlemesi bekleniyor mesajını
gördü. CaptureController.Reset yeni PetTracker oluşturur. Strict bağlantı BPF'i
değişince LiveSniffer aynı adaptörü de kapatıp yeniden açıyor, CaptureReset ile
bütün koleksiyonu siliyordu. EnsureDevice artık yalnız gerçek adaptör değişiminde
kapatır. Aynı adaptörün filtresi yerinde güncellenir. Npcap filter flush nedeniyle
gerçek bayt kaybı varsa mevcut TCP sequence/gap kontrolü yine geçersiz kılar.

İkinci reset mekanizması testle yeniden üretildi: farklı flow'dan selfId/map
bilgisi pet flow filtresinden önce işlendiği için koleksiyonu silebiliyordu.
Bound-flow kontrolü OnFrame başına taşındı. Invalidate isteğe bağlı flow alır;
CaptureController TCP gap ve kötü bundle olayları kendi flow ID'lerini iletir.
Koleksiyon akışındaki gerçek kayıp Verified değerini kaldırır; ilgisiz akışta
bakiyeyi/epoch'u değiştirmez. Geçerli yeni 00 90 kaydı eskisi gibi atomik eşitler.

Üç regresyon senaryosu; ilgisiz actor/map kaydıyla reset, kendi ölüm/teleport/map
geçişinde kalan ruh/epoch/gate korunması ve sonraki gerçek ödülle bir kez uyarı,
ayrıca doğru flow'a ait loss/recovery davranışını kapsar. Filtre sürücüsü donanım
gerektirdiğinden bu değişim kod incelemesiyle doğrulandı; yeni canlı ölüm ölçümü
yapılmadı. Gerçek yeni karakter/oturum/adaptör ayrımında eski bakiyeyi tahminle
onaylama eklenmedi. Zaten silinmiş RAM koleksiyonu yeni tam kayıt gerektirir.

CaptureController reset sebeplerini sabit metinlerle sayar. Yerel debug/JSON petSync
bölümü reset sayısı, son sebep, sync/epoch ve bakiye sayıları içerir; ad/karakter ID,
ham paket veya ağ adresi eklenmez. Normal kullanımda otomatik dosya/log yoktur.

0.8.6: AddPresets, ShowDialog(this) ile child UserControl HWND'sini owner
veriyordu ve TopMost almıyordu. DialogWindows gerçek FindForm owner'ı kullanır;
modal Form TopMost değerini eşler, CenterParent ve ShowInTaskbar=false uygular.
CommonDialog ve MessageBox da aynı modal scope'tan geçer. Scope, owner dışında
görünür TopMost formları geçici indirir, dispose/finally ile canlı kalanları eski
haline getirir. Persisted prefs yazılmaz; ana/mini kapanış sahipliği değiştirilmez.

Hazır liste yerel koyu tema/ölçeklemeyi ve Vazgeç/CancelButton'ı kullanır.
Ana Üstte tut checkbox'ı açık modeless BossEditor'ün TopMost değerini de günceller.
Kaydet, yeni gelen canlı boss verisini koruyan önceki merge davranışını sürdürür.

Native regression eski kodda başarısız oldu. Gerçek ShowDialog modal döngüsünde
GetWindow(GW_OWNER), IsWindowEnabled, TopMost açık/kapalı, combo seçilebilirliği,
CancelButton ve orijinal takvim/overlay restorasyonu denetlendi. CommonDialog
RunDialog owner parametresi ayrıca test edilir. WinForms Enabled özelliği native
modal disable'ı yansıtmadığı için native IsWindowEnabled kullanılır. Bağımsız
editor minimize/topmost testleri ve diğer self-test senaryoları korunur.

0.8.5: Python FrameStream, her pozitif TCP sequence farkında eşlemeyi hemen
sıfırlıyordu. Şifreli dört ardışık 01 37 kaydının parçaları sıra dışında
verildiğinde hata düzeltmeden önce 7 reset oluştu ve kayıtlar kayboldu.
Şimdi 1500 ms / 256 KiB / 256 segment sınırlı pending kuyruğu kullanılır.
Tekrar/overlap kırpılır, uint32 wraparound desteklenir. Gerçek delik timeout
veya baskı sınırında partial frame'i temizleyip cipher reset çağırır; kayıp
baytlar tahmin edilmez. Capture idle iken de expire çağrılır. Mevcut üç saat
doğrulaması, hareket timestamp/koordinat denetimi ve 2.5 sn sunum sınırı sürer.

Health kaydına helperVersion, outOfOrder, tcpGaps, pendingBytes/Segments ve
en çok 24 opcode:length sayacı eklenir. MotionBridge yalnız izinli sayısal
alanları alır; debug ve kullanıcının başlattığı yerel tanı kaydı veri yaşını,
son opcode'u ve ready nedenini içerir. Şifre/ham payload/adres loglanmaz.
Eski helper health kaydı çalışır, version bilinmiyor görünür. Dağıtımda yeni
Motion/CubeRadarMotion.exe bulunmalı; ana EXE güncellemesi tek başına yetmez.

14 Python testi geçti; sıra dışı şifreli yürüyüş, wraparound/overlap, gerçek
delik zaman aşımı ve kuyruk sınırı denetlenir. Arkadaşın bilgisayarındaki
dash/space sonrası konum+yön kaybının nedeni henüz canlı tanıyla doğrulanmadı.
Bu yama yeniden oluşturulan TCP hatasını giderir; bilinmeyen hareket formatını
tahmin ederek kabul etmez ve eski veriyi güncelmiş gibi göstermez.

0.8.4: PetTrackingPlan.Update mevcut Goals'u, kaydedilen ExcludedPets'i ve
snapshot'ı birleştirir. Exclusion önceliklidir. Yalnız Synced + Verified +
Remaining(3)>0 olan yakın ID'ler otomatik eklenir. Otomatik hedefler aynı Goals
içinde saklanır; NPC ölümü ardından ruh ödülü gelmeden hedef/gate kaybolmaz.
Veri değişmezse Save çağrılmaz. Kimlik/bakiye/protokol okuma değişmedi.

Checkbox kaldırma veya MiniPetForm.UntrackRequested, hedefi çıkarıp ID'yi
ExcludedPets HashSet'ine kaydeder. Yeniden işaretleme exclusion'ı temizler.
Eski JSON'da alan yoksa boş set kullanılır; boyut/ID/null denetimi korunur.
Ana Eksik ruhlar görünümü hedeflerden bağımsız balance okur, takip kapalıyken
hedef seçicisi yeniden pin oluşturamaz. Mini bağlam menüsü kaynak pet ID'sini
taşır; genel ··· menüsü bu bağlamı temizler.

312 .NET testi geçti. Native proof gerçek ToolStrip menü callback'iyle çıkarma,
JSON roundtrip exclusion, tekrarlanan snapshot'ta tekrar eklenmeme, ana listede
unchecked satırın kalması, yeniden işaretleme ve uzaktaki eksik ruh görünümünü
doğrular. Önceki rank/opacity/minimize kontrolleri korunur; yeni canlı ölçüm yoktur.

0.8.3: PetPanel hedefi ilk seçimde currentRank+1 olarak saklıyor ancak sonraki
rank değişiminde artırmıyordu. PetGoalProgression.Advance yalnız Synced + Verified
rank 1/2 kaydı için bitmiş hedefi rank+1'e taşır; mevcut ileri hedef değişmez.
Yeni hedef PetGoalGate içinde yeni baseline başlatır, eski tamamlanma tekrarlanmaz.
Bekleyen rank/spend çifti veya ruh eşiğinin dolması seviye yükselişi sayılmaz.

Hide completed önce yalnız ana grid'e uygulanıyordu ve hedefe hazır olmayı da
MAX gibi gizliyordu. Ortak IsMaxed yalnız Verified rank=3 kabul eder. Mini aynı
filtreyi alır ve seçili hedefler silinmeden görünür satırları günceller; auto-fit
görünür satır sayısını izler. MAX olmayan Hazır satırı kalır.

306 .NET testi geçti. Native proof Seviye 1 / 25 ruh satırının kalmasını,
Seviye 2 / 0 ruh geçişinde hedef 3 + 75 kaldı görünümünü ve Seviye 3'te iki
listenin de satırı kaldırmasını kontrol eder. Yeni canlı rank-up ölçümü istenmedi;
bu davranış protokol/model testleri ve gerçek WinForms kontrolleriyle doğrulandı.

0.8.2 yalnız mini pet sunumu ve tercihlerini değiştirir. PetMiniCard yerine düz
PetMiniRow (46 px) kullanılır. FormBorderStyle.None, başlık/satır sürükleme ve
WM_NCHITTEST kenar/köşe kodları ile taşınır/boyutlanır. Form hâlâ bağımsızdır;
ana minimize görünürlüğü ve kapanış temizliği korunur. Görev çubuğunda ek simge yoktur.

Mevcut snapshot ve hedefler aynı 500 ms güncellemeyle kullanılır. Native Form.Opacity
%40–100 aralığına kısıtlanır, menü slider'ı ve Yarı saydam seçimi aynı değeri
değiştirir. MiniOpacity/MiniAutoFit ayrı prefs alanlarıdır. MiniLayoutVersion=0
eski geniş boyutu bir kez küçültür; hedef/konum/Topmost korunur. Bozuk tercih dosyasına
yazmama ve atomik kayıt kuralları sürer. Elle dikey boyutlandırma auto-fit'i kapatır.

300 .NET testi geçti. Native proof tek/üç pet yüksekliğini, FormBorderStyle.None,
55% Opacity'nin gerçek layered-window alpha baytını (yaklaşık 140), %100'e dönüşü,
tercih callback'ini, yerel köşe hit testlerini ve eski tercih geçişini denetler.
Bitmap önizlemeleri layout'u gösterir; compositor saydamlığı DrawToBitmap'e dahil
değildir. Oyun protokolü, sayaçlar, boss/küp ve ses kodları değiştirilmedi.

0.8.1: MiniPetForm, Owner verilmeden Show() ile açılır. Mevcut ana timer ve
PetPanel snapshot/hedefleri kullanılır; ayrı yakalama veya alarm gate'i yoktur.
Kapanışta açık mini dispose edilir. ResizeEnd/Dispose konum/boyut/Topmost'u
mevcut pets.json dosyasına kaydeder; ekran dışı konumda görünür alana geri alınır.
Kartlar pet kimliğiyle yerinde güncellenir, 500 ms çizim sınırı vardır.

PetTracker, güvenilir SelfInfo/OwnRecords kimliğine ait 04 38 hasar veya 03 38
vuruş bildirimi ile hedefi işaretler. 30 saniye içindeki 42 36 reason=3 ölümü
bir katılım sayar ve işareti tüketir; corpse/despawn ve tekrarlar sayılmaz.
Bu, exclusive last-hit veya sunucu kill-credit çözümü değildir. Kayıt önceki
NPC görünmesini içermiyorsa toplam oturum sayısı yine artar; pet eşlemesi yapılmaz.
Pet kartı eksik eşlemede — / alt sınır N+ gösterir. Ruh bakiyesi yalnız pet
protokolünden değişir. Aynı flow koleksiyon eşitlemesi oturum sayılarını korur;
yeni flow/karakter sayıları sıfırlar, gap sayaçları kısmi işaretler.

İlk canlı deneme eski hit gövdesindeki UID farkını gösterdi; birincil GPL
PacketParser'a göre UID + layout 4/6 ve zincir alt kayıtları düzeltildi.
Tanı yalnız sayılar içerir, oyuncu kimliği/ham paket içermez. Yerel son ölçümde
kendi vuruşu, NPC ölümü ve pet 1157 için 1 ruh görüldü, ilk NPC kimliği yoktu.
Bu bulgu, pet eşlemesinden bağımsız toplamı gerektirdi; son toplam hesabı yeni
canlı kesimle sayısal karşılaştırılmadı. fixtures/pets/baba-mini-diagnostic.json.

300 .NET testi geçti. Native self-test bağımsız mini görünürlüğünü/Topmost'u,
ana pencere minimize iken 6→7 kesim ve 87→86 kalan ruh etiketlerini, kapanışta
temizliği doğrular. Önceki boss/küp/mini kanıtları korunur. Protokol atfı
licenses/PetTracker/COMBAT-SOURCE.txt içinde revision ve hash ile saklanır.

0.8.0: Core/Pets koleksiyon, yakın NPC ve hedef geçişini yönetir. PetMessages,
mevcut FrameDecoder çıktısındaki 0090 / 0D90 / 0E90 / 1490 gövdelerini okur.
Yeni framing, ikinci yakalama veya görüntü okuma yoktur. Sayım/ID/seviye/sınır,
tekillik ve bilinen koleksiyon alanları doğrulanır; ödül gövdesi tam ve atomik
uygulanır. Catalog 305 ID
içerir; av listesi yalnız tek anlamlı NPC kaynağı olan veya takip edilen petleri
gösterir. Bilinmeyen yeni ID eşitlemeyi hataya düşürür, kısmi toplam kabul edilmez.

CaptureController aynı session Tap'inde PetTracker'ı besler. Gaps ve bozuk bundle
bakiyeyi güvensiz yapar; yeni tam koleksiyon eşitler. Frame (flow,seq) tekilleme
kuyruğu 2048 ile sınırlıdır; aynı içerikli farklı ödüller ayrı sayılır. Bakiyeler
RAM'dedir, yeni oturumun benzersiz epoch değeri eski alarm tabanını kullanmaz.
Ruh harcaması ve rank değişimi credit/owed ile iki geliş sırasında da eşlenir.
Bekleyen/taşan sayaç bilinmiyor kalır. Yeni görüntü mevcut tüm bakiyeleri değiştirir.

NPC 4036/4136 kaynak eşlemesi, 4236 kaldırma ve mevcut PositionMessages konumları
kullanılır. Harita/instance değişimi çevreyi temizler. 2048 NPC üst sınırı vardır.
90 sn gözlem, 15 sn NPC konumu ve 2,5 sn oyuncu konumu sınırları ayrı tutulur.
Taze bakış yoksa yön verilmez. Metrik XY uzaklığı mevcut santimetre/metre
dönüşümünü kullanır. Canlı mesafe doğruluğu görsel olarak kontrol edilmedi.

PetPanel aynı UiTheme/HudGrid ile ayrı sayfadır; satırlar kimlikle korunur.
Gizliyken çizim/güncelleme yapılmaz, hedef gate'i çalışmaya devam eder.
PetGoalGate yalnız aynı epoch/hedef tabanından sonraki gerçek ruh kazanımıyla
tamamlanır. Login/hedef seçimi bildirmez; sessiz geçiş tüketilir, görsel kalır.
PetPreferences yalnız hedef/filtreyi ayrı, boyut ve değer denetimli atomik
pets.json dosyasında saklar; bozuk dosyanın üstüne yazmaz. Kullanıcının eski
companion.json ve ui.json dosyaları değiştirilmez.

291 .NET testi ve native self-test geçti. Paket UI kanıtı pet arama/hedef/işaret,
seçili hücre ve satırın korunması ile 125/150% render içerir. Yerel EU/BABA ölçümü
koleksiyonu ve Superior Water Spirit için 5 ruh ödülünü doğruladı: Sv2, 11/75.
fixtures/pets/baba-live-observation.json yalnız türetilmiş, kimliksiz gözlemdir;
canlı rank yükseltme ve NPC mesafe ekranı sınanmadı. Protokol/data kaynağı
0Fox2Give/PetTracker GPL-3.0; revision ve bildirimler licenses/PetTracker içindedir.

0.7.3: Önceki Kaira geçişi yalnız baba-abyss-watcher-kaira kimliğini
eşliyordu. Temiz kurulumdaki GlobalPresets kaydı rastgele kimlik ve OnceAt=null
aldığı için arkadaşın time=0 oyun kaydıyla kullanılabilir saat üretemiyordu.
BossProfile.Repair artık BABA sunucu token'ı ve dağıtılan şablon kaynağıyla
eşler; daha önce doğrulanan 30.10.2026 12:00 UTC / 180 dk fazını uygular.
Kimlik, Enabled, Live ve Delivered korunur. Özel kaynaklı kullanıcı takvimi,
başka sunucu veya mevcut RepeatMinutes değeri değiştirilmez. Geçiş idempotenttir.

BossPanel açılışta ve Yenile'de bu geçişi uygular. RefreshRequested mevcut
CaptureController'ı kullanır: durmuşsa başlatır, kuyruğu boşaltır ve Tick'i
yeniden hesaplatır. İkinci yakalama, oyuna paket/tuş gönderimi veya alarm
geçmişini sıfırlama yoktur. Son oyun kaydı ve oyun listesini açma gereği görünür.

Geçerli 24 Altgard / 8 Abyss listesi gelince BossCatalog.ForMap/Match üzerinden
eksik kayıtlar eklenir. Eski doğrulanmamış, canlı verisiz Nahma şablonu ilk
gerçek nokta adına taşınır; gelen liste her noktanın saatini ayrı doldurur.
Yeni kayıtlar Enabled=false başlar, mevcut alarm seçimleri korunur. Bilinmeyen
harita/sayım ekleme yapmaz; toplam kayıt sınırı 200'dür.

282 .NET testi geçti. Native self-test gerçek Yenile düğmesini kullanarak
rastgele kimlikli eski Kaira + sıfır oyun zamanı senaryosunu, sekiz Abyss
kaydının tamamlanmasını, üç Nahma noktasını ve seçili satır/hücre, alarm
geçmişi, kapalı uyarı seçiminin korunmasını doğrular. Arkadaşın bilgisayarında
canlı çalışma testi yapılmadı; temiz kurulum senaryosu yerelde yeniden üretildi.

0.7.2: RepeatMinutes dalı bir dakikalık başlangıç alarmı penceresinden sonra
sonraki sabit takvime ilerler; DurationMinutes=30 bu dalı artık geciktirmez.
Fresh listed SpawnAt önceliklidir; field boss ölüm/aralık mantığı değişmedi.
BABA 30.10 15:23:08 TR listesinde Shujakan slot 111017 alive=1'dir;
fixtures/bosses/baba-altgard-shujakan-alive.json ayrıştırmayı doğrular.
Kaira slot 2002 time=0 kalır; oyun UI'sı 18:00 TR'ye sayarken eski istemci
15:00 kaydını DurationMinutes boyunca gösteriyordu. Regression testi eski
kodda başarısızdır; yeni kodda aynı alarm gate'i sonraki uyarıyı tekrar kurar.

Edit artık ShowDialog(UserControl) yerine bağımsız modeless BossEditor açar.
TopMost ana formdan devralınır, ikinci açma mevcut pencereyi aktive eder.
FormClosed/OK kaydı uygular; form açıkken gelen latest.Live korunur.
BossPanel.Dispose açık editörü temizler. Önizleme editörleri etkinleştirmeden
ekran dışında açılır. Native doğrulama modeless/Owner=null/TopMost ve ana
minimize sonrası görünürlük ile yeni canlı veri üstüne kaydetmeyi denetler.
279 .NET testi geçti; mevcut mini minimize/click-through/scroll kanıtları korunur.

0.7.1: OpenMini içindeki Show(this) yerine Show() kullanılır. WinForms owned
pencereleri owner küçülünce gizler; topmost bayrağı bu davranışı değiştirmez.
Mini artık bağımsız top-level penceredir. OnFormClosing zaten _mini.Dispose()
çağırdığından uygulama kapanışı temizdir. Mevcut update timer, capture,
motion, boss ve ses modülleri değişmedi.
HudVerification, owner minimize sonrası mini Owner=null / native
IsWindowVisible=true / WindowState normal / TopMost=true koşullarını ve
güncelleme devamını doğrular. Test eski kodda başarısız, düzeltmede başarılıdır.
Tam self-test ana pencerenin kapanışında bağımsız mininin dispose edildiğini
de denetler. Önceki tasarım/etkileşim/ölçek önizlemeleri korunur.

0.7.0 UI: UiTheme / SurfaceCard / HudButton / ToggleSwitch / HudGrid ortak
görsel bileşenleridir. RadarForm.Layout ve BossPanel.Layout sunumu yönetir;
eski yakalama, MotionBridge, Core protokol/konum/yön/boss modülleri değişmedi.
RadarCanvas aynı WorldToRadar formülünü, mevcut rangeM parametresiyle kullanır.
Radarın merkezinde oyuncu vardır; boş/yönsüz durum büyük merkez yazısıyla örtülmez.
Küp grid'i nesne kimliğiyle satırları günceller; boss grid'i önceki scroll/selection
korumasını sürdürür. Kartlar aynı BossOccurrence kayıtlarını ve mevcut alarm
gate'ini kullanır; ikinci sayaç/alarm sistemi yoktur. Ayarlar içinde ses seviyesi
aynı CubeSound.Play / PlayBoss parametresine uygulanır.

UiPreferencesStore ayrı ui.json dosyasını atomik kaydeder; boyut/değer denetimi
yapar, okuma hatasında eski dosyanın üstüne yazmaz. Başlangıç ayarı OS başlangıcı
yerine uygulama açılışında capture başlatır. Mini WS_EX_TRANSPARENT yalnız açık
seçimle uygulanır. Ana Mini radar düğmesi click-through'u geri alır. Mini, mevcut
snapshot ve hedefi kullanır; kendi yakalaması ve alarm gate'i yoktur.

276 .NET + 9 Python testi geçti. HudVerification, önizleme modunda gerçek native
kontrollerde hedef kimliği/filtre, sabit satır, gezinme, volume/mute, liste katlama,
mini compact/click-through native bayrağı ve mini kapanışı sonrası radar devamını
doğrular. Boss'un 40 satırlı grid kanıtı korunur. --self-test çıktı matrisi boş,
yön yok, geniş/dar, açılmış liste, boss, mini/compact, ayarlar ve editörü içerir.
125/150% kontroller/metin için deterministik ölçek benzetimi yapılır; bu test
Windows ekran ayarını değiştirmez ve fiziksel farklı monitör testi değildir.
Üretimde PerMonitorV2 açıktır; normal açılış pencereyi çalışma alanına sığdırır.

0.6.0: FieldBossMessages mevcut S2C yakalamasındaki 01 91 kayıtlarını atomik
olarak ayrıştırır. Harita/slot aralığı, sayım, sonlandırıcı, benzersiz slot,
zaman aralığı ve sonlu koordinatlar denetlenir. Eksik paket kısmen uygulanmaz.
Altgard harita 1110: 24 slot NPC kod sırasındadır; Abyss harita 20: 8 slot
ekrandaki sıradadır, NPC kod sırası DEĞİLDİR. BABA kayıtları fixtures/bosses
altında; alanlar ve atıflar docs/BOSS-LIVE-TR.md içindedir.

CaptureController sınırlı güncelleme kuyruğu tutar; aynı yakalama kullanılır.
BossLiveSync, oyun listesi saatini önceki tek seferlik saatten üstün tutar.
Eski/geleneksel ayarlar uyumludur; Live ve RepeatMinutes ek alanları vardır.
Taze ölüm ile sonraki oyun saatinden dakika hizalı aralık öğrenilebilir.
Bu aralıkla sonraki kesimdeki saat tahmindir, yeni oyun listesi saati üstündür.
Corpse bayrağı (1) ve despawn ölüm sayılmaz; 42 36 bayrak 3 gerekir.
Harita geçişi NPC kimliği eşlemelerini sıfırlar. Üç Nahma'nın tekil NPC
konumları doğrulanmadığından ölüm eşlemesi yapılmaz; liste saatleri alınır.

Kaira'nın paketi sıfır zaman gönderir. BABA'daki mevcut doğrulanmış ekran
ankoru en yakın dakikaya yuvarlanıp Global 180 dk takviminde kullanılır.
0.7.3'ten itibaren BABA hazır şablonları kimlikten bağımsız tanınır.
Listede oyun/takvim/tahmin ve son veri zamanı görünür. Sonlandırmada kirli
durum kaydedilir; değişmeyen saatin yalnız tazelik alanı 15 sn aralıkla yazılır.
Harita listesi kapalıyken uzaktaki kesimlerin tamamı garanti edilmez.

BossFanfare özgün 2,6 sn, 44.1 kHz PCM16 davul/boru sesidir. Aynı waveOut
oturumunu kullanır; boss çalarken küp bipi onu kesmez. Ses testi ayrı düğmedir.
Hareket yardımcısı/şifreleme kodu değişmedi; boss verisi açık S2C kaydıdır.

0.5.1: BossPanel Tick içindeki Rows.Clear kaldırıldı. Event id → mevcut row
eşlemesiyle yalnız değişen hücreler güncellenir; ekleme/silme yalnız veri değişince
olur. Scroll/current cell ve kullanıcı sütun sıralaması/ölçüleri değiştirilmez.
DataGridView checkbox commit'i Enabled alanına aktarılır; diğer hücreler salt
okunurdur. Veri yazarken event handler bastırılır. Saat/süre sıralaması metin
karşılaştırması yerine occurrence UTC zamanını kullanır.
Başlangıç çizimi alarm anahtarlarını tüketmez; ilk gerçek timer tick'i ses
ayarlarına göre değerlendirir. Devre dışı bossun sayacı listede kalır.
40 gerçek WinForms row ile kaydırma, row/current cell kimliği, sütun genişliği/
sırası ve kutu aç/kapat durumu ardışık tick'lerde doğrulandı. 263 .NET testi geçti.

0.5.0: Core `TargetTracker` ve `BossClock`; TR `BossStore`, `BossEditor`,
`BossPanel`, `MiniRadarForm`, `RadarForm.Companion` eklendi. Capture/şifre çözme
ve hareket yardımcısı değişmedi. Harita/oturum geçişinde hedef kilidi sıfırlanır.
Yükseklik filtresi yalnız güvenilir geometriyle çalışır. Mini form aynı snapshot'ı
kullanır; ikinci yakalama açmaz. Sahip form kapanınca mini form da temizlenir.

Takvim TimeZoneInfo ile server-local → UTC → display-local dönüştürülür.
Yaz saati geçişindeki olmayan saat atlanır; çift saat için erken UTC seçilir.
Haftalık günler sunucu takviminde değerlendirilir. Alarm anahtarı event id + UTC
occurrence ticks + threshold; sessiz eşikler tüketilir, başlangıçtan 1 dk fazla
geçmiş bir kayıt için geç ses verilmez. Dosya geçici dosyadan atomik taşınır.
Kullanıcı verisi yerel AppData'dadır; okuma hatasında dosyanın üzerine yazılmaz.
Gerçek NPC doğma/ölüm takibi yoktur; liste bir planlama/geri sayım aracıdır.

Kaynak şablonları: https://aion2hub.com/tools/world-bosses (kendi Global istemci
çıkarımı; saat dilimi konusunda diğer kaynaklarla çelişiyor, bu nedenle pasif).
Altgard/Gartua haritası: https://aion2.gg/map/World_D_A?marker=npc-2400800
ve https://aion2.gg/database/npcs/immortal-gartua-2400800 . Harita koordinatı
radara eklenmedi; kaynak harita düğmesi kullanıcı tıklamasıyla tarayıcı açar.
BABA profili kullanıcının sunucu/faction bildiriminden, Türkiye gösterimi görev
bağlamından alınır. Gartua'nın tek seferlik saati kullanıcının kalan süre
bildiriminden hesaplanır; sabit periyot veya tüm sunucunun saati varsayılmaz.

262 .NET testi geçti. Yeni altı test kilit/sıra/kayıp/harita, yükseklik/eski veri,
gece yarısı/hafta günü dönüşü, DST boşluğu/çift saat, sessiz alarm/yeniden açılış
ve tek seferlik kaydın bitmesini kapsar. WinForms normal/küçük/kilitli/mini/
event/ayarlar/düzenleme/yönsüz durumları örnek veriyle çizilerek incelendi.

0.4.1: serbest uçuş sırasında kaybolan yön için 0A37/0B37 desteği eklendi.
Kullanıcının uçuş/yürüyüş ölçümünde bu iki aile gözlendi; eski parser bunları
reddediyordu. Aynı ölçümde 95 desteklenen hareket kaydı ve sıfır TCP boşluğu vardı.
0A37 için XYZ +3, hız +15, açı +27; 0B37 için XYZ +2, hız +14, açı +26.
Kaynak: protokol deposunun protocol/opcodes.json uçuş şemaları. Kaynakta açı
alanının anlamı inferred olarak işaretlidir; yerel örnekle ayrıca karşılaştırılmalı.
Geçiş 40 byte/flags 2 veya 41 byte/flags 3/marker 3, devam 39 byte; mode 0/1.
Zaman son 8 byte; mevcut zaman/sonlu değer/koordinat kontrolleri korunur.
Üç biçim ve bozuk/eski kayıt testi önce başarısız oldu, düzeltmeyle geçti.
Yeni sürümün uçuş sırasında canlı arayüzü henüz kullanıcıyla doğrulanmadı.

0.4.0, radarın okunabilirliğini iyileştirir: büyük en yakın küp kartı, radar ve
listede eşleşen numaralar, belirgin göreli yön etiketleri ve ayrı Ayarlar/Tanı.
`RadarCanvas.cs`, `RadarForm.cs`, önizleme akışı ve sürüm/belgeler değişti.
Çekirdek geometri, yakalama, ses ve 0.3 hareket yardımcısı değiştirilmedi.
Tasarım önizlemesindeki üç hedef, açıkça örnek olarak işaretlenmiş yapay
konumlardır; kayıtlı gerçek protokol fixture doğrulaması ayrıca korunur.

Gerçek istemci hareket kayıtları çözüldü ve arayüze bağlandı. Kullanıcı 30 Ekim
2026'da yürürken `Oyuncu yönü: Packet` gördüğünü doğruladı. Yerel kamera dönüşü
ile karakterin ağda bildirilen bakış yönü birbirinden farklıdır; yeni kayıt
olmadan açı üretilmez. Hareketsiz kalınca 2,5 saniyede Unknown görülebilir.

## 1. Oyuncu konumu

Öncelik, yalnız AION2 işlemine ait etkin dünya bağlantısındaki çözülmüş **C2S**
kayıtlardadır. Aşağıdaki ofsetler opcode dahil çözülmüş body başlangıcındandır:

| Wire opcode | Doğrulanan biçim | XYZ | Bakış yaw | Diğer |
|---|---|---|---|---|
| `00 37` / `01 37` | 30 byte, flags=2 | +3/+7/+11 f32 LE | +17 f32 LE | Hareket yaw: +15 u16 × 360/65536; zaman: son 8 byte |
| `01 37` stop | 29 byte, flags=0 | aynı | aynı | Mode byte yok |
| `03 37` | 40 byte, flags=2 | +3/+7/+11 | +15 f32 LE | Havada hareket; zaman son 8 byte |
| `18 37` | 28 byte | +4/+8/+12 | +16 f32 LE | Eylem konum/açı kaydı; zaman +20 u64 LE |

Boyut, flags, son zaman alanı ve sonlu/sınırlar içindeki koordinatlar kontrol
edilir. C2S kaydı güncelse sunucu buff konumunun yerine kullanılır. Küplerin
mesafesi ve açısı yeni konumdan yeniden hesaplanır; eski geometri taşınmaz.

Eski gelen kaynaklar korunur: SelfInfo `0x3336` ilk varint; map-load `0x2136`
body +16/+20/+24 XYZ; teleport `0x2336` +6/+10/+14; `0x2A38`/`0x2B38` caster
tail XYZ; `0x0238` ve `0x0E38` kendi entity'ye ait target XYZ. Cast XYZ'si
caster konumu sayılmaz. Buff-only kimlik tahmini kesin geometri üretmez.

## 2. Heading ve şifre çözme

0.2 sürümündeki `0x0238` açı adayları yön kaynağı yapılmadı. 0.3, yukarıdaki
C2S **bakış** f32 alanını kullanır; sıkıştırılmış **hareket** açısı ayrı tutulur.
`Heading Source: Packet`, güncel ve doğrulanmış C2S hareket kaydından gelir.

İsteğe bağlı yardımcı yalnız hedef AION2 sürecini `PROCESS_VM_READ` ve sorgu
haklarıyla açar. İmza ile ağ nesnesi bulunur; iki eşleşen 214-byte anahtar ve
geçerli 256-byte RC4 permütasyonu aranır. İlk konumlandırmada daha geniş RAM
blokları taranabilir (bu bilgisayardaki ön kontrolde yaklaşık 1,8 GB okuma,
9,6 saniye); oyun belleği değiştirilmez. Anahtar/durum yardımcının RAM'inde kalır.

Standart RC4 vektörü, ileri/geri adım ve tam permütasyonla sayaç eşlemesi testlidir.
Aktif akışa ortadan katılmada, bellekteki durumdan çevresel keystream penceresi
çıkarılır. En az **üç** aynı opcode'lu, zamanlaması tutarlı kayıt doğrulanmadan
konum üretilmez. Sabit istemci/capture saat farkı öğrenilir; tek bir opcode veya
float benzerliği yeterli sayılmaz. TCP boşluğunda eşleşme iptal edilir.

GUI normal yetkiyle çalışır. Yalnız yardımcı Windows yönetici onayı ister.
Yerel named pipe ACL'si mevcut Windows hesabıyla sınırlıdır; network logon
erişimi reddedilir. Kanaldan yalnız konum, açı ve sağlık bilgisi geçer; anahtar
ve şifreleme durumu geçmez. Ağ sunucusu, veri yükleme veya anahtar dosyası yoktur.

Güncel Packet kaydı yoksa kaynak Unknown olur. İsteğe bağlı Movement-derived
yalnız yürüyüş tahminidir; Packet önceliklidir ve tahmin bakış diye gösterilmez.

## 3. Küp konumu

Mevcut `0x3436` ObjectSpawn parser'ı korunur: `id varint | kind u16 | template
u32 | X/Y/Z f32 | f32 | object heading f32`. Varint bitiminden itibaren XYZ
+6/+10/+14'tedir. Object heading oyuncunun açısı sayılmaz. ObjectId ile
tekilleştirme ve `0x3536`/`0x3836`/`0x3A36` yaşam döngüsü korunur.

## 4. Eksenler

Yatay X/Y, yükseklik Z; 100 birim = 1m. Varsayılan north=-Y, east=+X.
World yaw için 0°=+X, 90°=+Y. Bakış yaw birim vektöre çevrilir ve ayardaki
north/east bazına izdüşürülür; varsayılanda compass heading=(yaw+90) mod 360.
Bu, kameranın serbest dönüş açısı değildir.

## 5. Radar formülü

```text
dx = cube.X-player.X; dy = cube.Y-player.Y; dz = cube.Z-player.Z
distanceM = sqrt(dx²+dy²)/100; heightM = dz/100
bearing = atan2(dot((dx,dy),east), dot((dx,dy),north))
relative = wrap180(bearing-playerHeading)
r = min(distanceM/150,1) * radiusPixels
screenX = centerX + sin(relative)*r
screenY = centerY - cos(relative)*r
```

150m üstü varsayılan gizlenir; kenara sabitleme açıksa gerçek mesafe korunur.
45° sektörler Ön/Sağ Ön/Sağ/Sağ Arka/Arka/Sol Arka/Sol/Sol Ön'dür. Oyuncu
üçgeni merkezde yukarı bakar. Sonar 2,5s, çizim ~29 FPS üst sınırı; GUI veri
yenilemesi 10 Hz. Ağ callback'leri UI çalıştırmaz; kilitli snapshot kullanılır.

## 6–7. Değiştirilen ve eklenen dosyalar

Değiştirilen: `RadarEngine.cs` (kimlik kaynağı/öncelik), `CubeCompanionView.cs`
(güvenilir konum ve güncel geometri), TR `RadarForm.cs`, `Program.cs`,
`CubeRadar.Tr.csproj`, kullanım/değişiklik/inceleme belgeleri.

Eklenen: Core `ClientMotion.cs`, TR `MotionBridge.cs`, `ClientMotionTests.cs`;
`src/CubeRadar.Motion/` içindeki `motion_reader.py`, `motion_wire.py`,
`passive_pcap.py`, MIT kaynaklı sınırlı `read_only_locator.py`, dokümantasyon,
lisans ve dokuz okuyucu testi. Önceki RadarMath/sonar/ses işlevleri korunur.

## 8. Testler ve canlı kanıt

- 256/256 .NET ve 9/9 Python okuyucu testi geçti; Release derlemesi hatasız.
- Kuzey/hedef/90° dönüş, merkez/ölçek/yükseklik, kimlik kayması, eski veri,
  C2S konumunun eski geometriyi değiştirmesi ve yaw/baz dönüşü testli.
- RC4 vektörü, ters adım, durum eşlemesi, clock opcode/saat farkı, TCP tekrar/
  boşluk/fragment, ground/stop/action ve geçersiz sayılar testli.
- Canlı kontrollü koşuda 421 hareket kaydı: 273 × `01 37`, 24 × `00 37`,
  122 × `03 37`, 2 × `18 37`; 1.736 zaman kaydı, TCP boşluğu 0.
- 287 ardışık konum çiftiyle hareket yönü kıyaslandı: medyan açı farkı
  0,00398°. Bakış ile hareket alanı bazı örneklerde 45° ayrıştı; geri hareket
  yapıldığını kullanıcı bildirdi, fakat yakalanan veride 180° ayrışma gözlenmedi.
- Ayrı gerçek-protokol küp fixture'ı iki küp çözümler; uygulama görüntüsü kontrol edildi.
- Son GUI kontrolünde kullanıcı yürürken Packet gördüğünü doğruladı. Tam ekran
  oyun, otomasyonun son radar görüntüsünü güvenilir yakalamasını engelledi.
- Ses backend'i kullanıcı tarafından duyularak doğrulandı. Gerçek bir küpün
  yanında canlı sesli uyarı henüz gözlemlenmedi.

## 9. Kalan sınırlar

Yeni paket gelmediğinde (özellikle dururken veya sadece kamerayı çevirirken)
2,5 saniye sonra Unknown görülmesi mümkündür. Paket dışı karakter/kamera yönü
tahmin edilmez. Yeni uçuş/dash varyantları, farklı bölgeler, IPv6 ve oyun
güncellemeleri yeniden doğrulama gerektirebilir. Cube template kapsamı rehberdedir.

Birincil kaynaklar: [protokol](https://github.com/lunixxx37/aion2-protocol-research/blob/main/docs/PROTOCOL.md),
[hareket alanları](https://github.com/lunixxx37/aion2-protocol-research/blob/main/docs/C2S_RESEARCH.md),
[küp bulguları](https://github.com/dr89mh/aion2-cube-radar/blob/main/docs/FINDINGS.md),
[Windows pipe ACL](https://learn.microsoft.com/en-us/dotnet/api/system.io.pipes.namedpipeserverstreamacl.create).
