# Radar sabit duruşta son bilinen konum gösterimi

Güncel ürün durumu: [Otomatik Av kaldırma ve klasör temizliği](RADAR-ONLY-CLEANUP-TR.md). Aşağıdaki 627 testli kayıt önceki teslimin tarihçesidir; son bilinen konum davranışı korunur.

1 Kasım 2026. Kullanıcı sabit durduğunda konumun ve yaratıkların silindiğini bildirdi. Canlı kayıtta capture sağlıklı ve MotionReady=true iken son hareket 29.219 ms eski, PoseStopped=false ve observationAtMs=MotionSampleAtMs idi. Önceki düzeltme yalnız açık duruş doğrulandığında gelen yeni observation zamanını kullanıyordu; bu canlı örnekte o koşul yoktu. Yaratık katmanı da 15 saniyeyi aşan konumu gizliyordu.

## Yeni görünüm

- Bir kez güncel ve doğrulanmış kendi motion pozunu görmüş radar, aynı map/session/PID/pose-generation ve aynı motion sample'ı için güncel gözlem gelmeyince **Son bilinen konum** gösterir. Koordinat/yön dondurulur; zaman damgası yenilenmez ve Stopped=true varsayılmaz.
- Bu görünümde oyuncu/küp işaretleri soluk, yaratıklar boş gri dairedir. Üstte **SON BİLİNEN** yazar, güncel mesafe kartı sunulmaz ve sonar animasyonu durur. Tooltip son bilinen konumu açıkça belirtir.
- Yeni doğrulanmış gözlem gelirse normal canlı görünüme dönülür. İlk gözlem zaten eskiyse hafıza başlatılmaz. Okuyucu/bağlantı kaybı, map/session/PID/pose-generation değişimi veya farklı ama doğrulanmamış sample oyuncu hafızasını temizler.
- Mevcut PetTracker kayıtlarının salt okunur ReadRadarMobs görünümü kullanılır. Konumu eski yaratıklar kaynak kayıtta durduğu sürece **son bilinen** boş daire olarak çizilebilir. Bu, canlı varlık veya güncel konum iddiası değildir. Yeni keşif/tahmin yapılmaz. Ölüm/kaldırılma, kimlik yeniden kullanımı, map değişimi veya tracker reset'i kayıtları temizlemeye devam eder.
- Mevcut `PetTracker.Read(now).Nearby` 90 saniye penceresi ve strict `RadarCreaturePresentation.TryProject` 15 saniye penceresi değişmedi. Son bilinen gösterimi yalnız açık `showLastKnown` sunum yolundadır; eski kayıt güncel kayda çevrilmez.

## Otorite sınırı

HuntEngine'e hâlâ orijinal snapshot, motion ve PetSnapshot gider. Görsel klonun FreshPosition alanı **false**, LastKnownPosition alanı **true**, Nearest alanı **null** kalır. TargetTracker kilit çözümlemesi eski konumu doğrulanmış hedef olarak döndürmez. Raw TimeMs, observationAtMs, Stopped, route world coordinates, ölüm kanıtı ve av freshness kuralları değiştirilmedi. ClientPoseObservation, motion okuyucusu, input ve Macro recorder değişmedi.

Normal hedef menzili ve yükseklik filtreleri korunur. Menzil dışı yaratık kenara taşınmaz. Pet Avı'nın taze/yakın veri otoritesi ile görsel tarihin kaynağı ayrıdır; yeni tracker veya hareket tahmin sistemi eklenmedi.

## Doğrulama

- **627/627 .NET PASS**, failed=0, skipped=0: 13 yeni vaka; önceki 614 vaka korunur.
- **22/22 Python Motion PASS**.
- Solution ve Türkçe WinForms Release build: **PASS**, 0 hata / 0 uyarı.
- WinForms self-test **passed**, captureStarted=false. 60 saniyelik sentetik sessizlikte son bilinen işaretler korunur, FreshPosition false kalır, sonar durur; yaratık kaldırılması ve yeni gözlemle canlıya dönüş sınanır. Normal stale/unknown davranışını isteyen işaretsiz frame'ler yine çizilmez.
- Konum hafızasının eskiden başlatılamaması, kaynak kaybı, bağlam değişimi, farklı sample, eski yaratık koordinatı, strict Nearby ile görsel history ayrımı, ölüm ve map temizliği testlidir. Önce eksik API ile RED, ardından GREEN alındı.

Ranger ve 38 noktalı kullanıcı rotası ile 63 noktalı checkpoint kopyası korunur; restore yapılmaz. Değişiklik radar feature branch'indedir; otomatik av checkpoint branch'i korunur. Push/release/publish/installer yapılmaz.

**GameMacroCombatBackend = NOT SAFE TO IMPLEMENT**

**FULL AUTO LIVE INPUT = BLOCKED**

**LIVE_VERIFIED = false**
