# Cube Radar TR — Oyuncu merkezli taktik radar

Güncel ürün durumu: [Otomatik Av kaldırma ve klasör temizliği](RADAR-ONLY-CLEANUP-TR.md). Aşağıdaki HuntEngine/Macro durumları ve test sayıları önceki teslimlere aittir; taktik radar korunur.

Güncel sabit duruş görünümü ve 627 testli doğrulama için [son bilinen konum kaydı](RADAR-IDLE-VISUAL-HISTORY-TR.md) esas alınır. Aşağıdaki kayıtlar önceki aşamaların tarihçesidir.

## Son düzeltme — sabit dururken konum kaybı, 1 Kasım 2026

`75ee2ee` düzeltmesinde radar sunumu, son hareket paketinin `TimeMs` alanı yerine mevcut doğrulanmış `LastObservationAt` zamanını kullanır. Açık duruş ve aynı generation/serial için doğrulanan yeni world-clock gözlemleri koordinat değişmeden konumu güncel tutabilir. Ham hareket/ilerleme zamanları yeniden yazılmaz. Gözlem 2,5 saniyeden eskiyse veya okuyucu hazır değilse konum yine geçersizdir; süre uzatma veya sınırsız son konum fallback'i eklenmedi. UI yön yaşı da aynı doğrulanmış gözlemi kullanır.

Regresyon önce mevcut kodda FAIL oldu; düzeltmeden sonra **600/600 .NET**, **22/22 Python**, solution/WinForms Release build ve WinForms self-test **PASS**. Yeni üç vaka duruşta görünür kalma, gözlem kesilince expire olma ve okuyucu hazır değilken eski koordinatı kabul etmeme davranışlarını sınar. HuntEngine, ClientPoseObservation doğrulaması, motion okuyucusu, input, Macro ve kullanıcı ayarları değiştirilmedi. Bu, radar görünümü düzeltmesidir; `FULL AUTO LIVE INPUT=BLOCKED`, `LIVE_VERIFIED=false` kalır. Açık eski uygulamaya geçiş için yeni derlemeyle yeniden başlatma gerekir; bu düzeltme turunda uygulama yeniden başlatılmadı.

## İlk taktik radar teslimi — 597 testli aşama

Aşağıdaki uygulama ve kanıt kaydı ilk teslimin `f834f3e` koduna aittir; duruş düzeltmesi ve güncel test toplamı yukarıdadır.

31 Ekim 2026. Varsayılan radar yönü **Karakter yönü**: oyuncu merkezde kalır, çevre doğrulanmış bakış yönünün tersine döner. **Kuzey sabit** seçeneği ve yalnız görsel **Sonar efekti** ayarı eklendi. Ana radar ve mini radar aynı projeksiyonu kullanır.

## Checkpoint ve çalışma düzeni

- Korunan branch: `refactor/hunt-combat-backend-20261031`; HEAD **d7542a2d15df44894ed04dd40bcf99dd2b677975**.
- Radar geliştirmesi ayrı worktree/branch'te: `feature/player-centered-radar-20261031`.
- Worktree: `C:/Users/gokce/Documents/Codex/2026-10-31/files-pasted-by-the-user-cube/work/radar-tactical-20261031/repo`.
- Yerel kod commit'leri: `46ba01a` saf projeksiyon; `f834f3e10de438418aefcc20776e497dcc1feccd` radar entegrasyonu.
- Kapanış HEAD/status ve dosya hash'leri [kanıt kaydında](C:/Users/gokce/Documents/Codex/2026-10-31/files-pasted-by-the-user-cube/outputs/radar-tactical-20261031/radar-tactical-proof.json). Orijinal branch'in bekleyen checkpoint/trace belgeleri korundu; feature branch'e karıştırılmadı. Push/release/publish/installer yapılmadı.

## Değişen dosyalar

| Dosya | Değişiklik |
|---|---|
| src/CubeRadar.Core/Radar/RadarProjector.cs | Immutable frame, value-type sonuç, heading normalizasyonu, görsel pulse örneklemesi |
| src/CubeRadar.Core/Radar/RadarMath.cs | Mevcut WorldToRadar API'si ortak projeksiyona delege eder; nullable/clamped sözleşme korunur |
| src/CubeRadar.Tr/RadarCanvas.cs | Ortak dönüşüm, merkez oyuncu oku, modlar, sonar, isteğe bağlı salt okunur rota çizimi; çizim listeleri/brush/pen yeniden kullanımı |
| src/CubeRadar.Tr/UiPreferences.cs | Yalnız UI için RadarOrientation ve SonarEnabled; eski dosyalarda varsayılanlar |
| src/CubeRadar.Tr/RadarForm.Layout.cs | Radar yönü ve Sonar efekti kontrolleri |
| src/CubeRadar.Tr/RadarForm.cs | Radar için yalnız Packet heading, görünüm ayarları ve tanı projeksiyonu |
| src/CubeRadar.Tr/RadarForm.Companion.cs | Mini radara aynı yön/sonar ayarlarının aktarılması |
| src/CubeRadar.Tr/MiniRadarForm.cs | Ortak modlar ve kuzey sabit/heading unknown durumu |
| src/CubeRadar.Tr/RadarForm.RadarPreview.cs | Sentetik çizim, ok, sonar, stale/unknown, menzil ve rota önizleme kontrolleri |
| src/CubeRadar.Tr/Program.cs | Mevcut self-test'e taktik radar kanıtı ve PNG üretimi |
| tests/CubeRadar.Tests/RadarProjectionTests.cs | 33 projection/pulse vakası |
| tests/CubeRadar.Tests/RadarPresentationPreferencesTests.cs | 2 eski ayar/round-trip vakası |
| tests/CubeRadar.Tests/CubeRadar.Tests.csproj | UI-independent preferences dosyasının test projesine bağlanması |
| docs/RADAR-TACTICAL-PROJECTION-TR.md | Uygulama ve doğrulama kaydı |

## Matematik ve koordinat sözleşmesi

Mevcut sözleşme kullanılır: **world X/Y yatay, Z yükseklik; 100 world unit = 1 metre**. Varsayılan north=−Y, east=+X; farklı bir Orientation yapılandırılmışsa onun birim vektörleri kullanılır. Screen +X sağa, +Y aşağıdır. Heading, yapılandırılmış kuzeyden saat yönünde derecedir.

`RadarProjector.TryCreate` frame başına dönüşümü kurar; sin/cos entity başına hesaplanmaz. `TryProject` hedef world X/Y değerlerini alır ve oyuncuyu çıkarır:

```text
dx = (targetX - playerX) / 100
dy = (targetY - playerY) / 100
east = dot((dx,dy), East)
north = dot((dx,dy), North)
theta = CharacterUp ? normalize(heading) : 0
localXM = east*cos(theta) - north*sin(theta)
localYM = -(north*cos(theta) + east*sin(theta))
screenXY = screenCenter + localXY * screenRadius / radarRangeM
```

Distance planar metredir; Z yalnız mevcut yükseklik etiketleri/okları içindir. Dış çember tam radarRangeM'yi temsil eder: 75 m seçiminde halkalar **25/50/75 m**. Normalde menzil dışı küpler çizilmez; mevcut açık **Menzil dışını göster** Cube edge-indicator davranışı korunur. Kaynak entity/uzaklık verisi değiştirilmez.

0°'de kuzey hedef üstte, 90°'de solda, 180°'de altta, 270°'de sağda; 45°'de sol üsttedir. Oyuncuyla aynı konum ekran merkezine gider. Karakter yönünde ok hep yukarıdır; kuzey sabitte bilinen gerçek heading'e döner. Bilinmeyen heading'de yön uydurulmaz.

## Heading ve freshness

Kaynak mevcut `ClientMotionPresentation.Facing(packet, axes)` ve `HeadingSource.Packet` zinciridir. Yalnız doğrulanmış kendi bağlantısına ait, mevcut motion/position freshness kontrollerinden geçen veri kullanılır. `ClientMotionPresentation.Apply` ve `CubeCompanionView.FreshPosition` değiştirilmedi. Hareketten türetilen yön mevcut listelerdeki isteğe bağlı davranışını korur; taktik radarda karakter bakışı diye kullanılmaz.

`TryNormalizeHeading`, −90/360/450 ve büyük pozitif/negatif dereceleri [0,360) aralığına getirir. NaN/Infinity heading karakter yönünde frame'i reddeder; güncel heading gelene kadar entity çizilmez. Eski/bilinmeyen konumda çizim durur. Kullanıcı **Kuzey sabit** seçmişse taze konumla heading olmadan dünya gösterilebilir; yön oku yerine merkez işareti kullanılır. Bu otomatik kuzey fallback'i değildir. HuntEngine freshness kontratı değişmedi.

## Sonar, veri otoritesi ve performans

Pulse 2.500 ms döngüde 0→1 yarıçap oranıyla büyür, alfa azalır ve dış çemberde yeniden başlar. Ayar yalnız görünümü etkiler. Mevcut 80 ms UI animasyon timer'ı kullanılır; sonar kapalı, radar görünmez veya gerekli poz/yön verisi yokken animasyon timer'ı etkin değildir. Küçültülmüş pencereye ek redraw yapılmaz. Sonar discovery, yeni entity veya tracker verisi üretmez.

RadarProjector/RadarPoint value type'ları per-entity projection allocation üretmez. Aynı frame matrisi paylaşılır; çizim listeleri/ikon noktaları yeniden kullanılır, tooltip metni hover'da hazırlanır, hedef başına yeni brush/pen oluşturulmaz. Yeni paket, input kaynağı, hareket takip sistemi veya feature-flag altyapısı yoktur.

Mevcut dairesel RadarCanvas küpleri çiziyordu; Pet/Boss/NPC ve hedef filtrelerinin veri otoritesi/ayrı panelleri değiştirilmedi. Yeni entity verisi uydurulmadı. Hedef kilidi, yükseklik filtreleri ve Cube edge göstergesi korunur.

## Rota projeksiyonu ve ayar koruması

Çiziciye sağlanan salt okunur world X/Y rota noktaları aynı frame ile dönüştürülür; çizgiler radar çemberine kırpılır, dış rota noktaları kenara sabitlenmez. Rota + entity önizlemesi sentetiktir. Önceki production radar kayıtlı av rotasını çizmiyordu; bu görevde HuntPanel/route logic'e yeni bağlantı eklenmedi. Mevcut 38 noktalı rotanın radar üzerinde gösterildiği iddia edilmez. Projection/render yolu sağlanan rota noktaları için testlidir; JSON world verisi yeniden yazılmaz.

- **Current user route: 38 points** — Yeni devriye, map 1110.
- **Checkpoint preserved route: 63 points** — restore edilmedi.
- **Ranger**: aynı profil kimliği ve bütün atamalar; MouseLeft/Mouse4 dahil, checkpoint profil kopyasıyla eşit.
- Güncel hunt.json SHA-256: `2b6ae6e2f5a3f59d8e16e869197beeade9c4003a607ba02b941aba69e2372571`.
- Korunan checkpoint SHA-256: `9c1409957f705387f3cb4bb16e8daf21692710412a84d1cd03a36bca4f7200eb`.

Özel rota/profil içeriği çıktı paketine alınmadı. Self-test preview preferences store'u kullanır; gerçek UI/hunt ayarlarına yazmaz.

## Gerçek doğrulama sonuçları

| Kontrol | Sonuç |
|---|---|
| Solution Release build | **PASS**, 0 hata / 0 uyarı |
| CubeRadar.Tr Release build | **PASS**, 0 hata / 0 uyarı |
| Tüm .NET testleri | **597/597 PASS**, failed=0, skipped=0; **35 yeni vaka** |
| Python Motion | **22/22 PASS** |
| WinForms self-test | **PASS**, exit 0, result=passed, captureStarted=false |
| Kod incelemesi / whitespace | Beş eksende inceleme ve görsel kontrol; git diff --check PASS |
| Kontrollü test mutasyonu | 75 m sınırındaki ≤ koşulu < yapılınca sınır testi FAIL; kaynak birebir geri yüklendi; son build/testler restore sonrasında PASS |

Projection testleri önce yazıldı; eksik API nedeniyle ilk RED alındı. Ardından 31 odaklı test ve mevcut suite geçti. Taşınan oyuncu, yapılandırılmış world north ve eski UI ayarı uyumuyla 35 yeni vaka oldu. Önceki **562 vakanın** display-name çoklukları korunuyor; TRX'te aynı görünen ada sahip ayrı vakalar bulunduğundan benzersiz ad sayısı 558→593'tür. Yeni toplam 597'dir.

Son koşuların [TRX'i](C:/Users/gokce/Documents/Codex/2026-10-31/files-pasted-by-the-user-cube/outputs/radar-tactical-20261031/radar-tactical.trx), [test logu](C:/Users/gokce/Documents/Codex/2026-10-31/files-pasted-by-the-user-cube/outputs/radar-tactical-20261031/dotnet-tests.log), [Python logu](C:/Users/gokce/Documents/Codex/2026-10-31/files-pasted-by-the-user-cube/outputs/radar-tactical-20261031/python-motion.log), [self-test JSON'u](C:/Users/gokce/Documents/Codex/2026-10-31/files-pasted-by-the-user-cube/outputs/radar-tactical-20261031/winforms-self-test.json), [mutasyon logu](C:/Users/gokce/Documents/Codex/2026-10-31/files-pasted-by-the-user-cube/outputs/radar-tactical-20261031/mutation.log) ve komut/exit-code kayıtları kanıt paketindedir.

WinForms doğrulaması gerçek render noktalarını/ok ucunu ölçer: 0/90/180 dönüşümü, sabit merkez, 75 m dışındaki hedefin gizlenmesi, mod değiştirme, heading unknown, stale pozisyon ve sonar kapatmanın noktaları/veriyi değiştirmemesi. 680×760 kompakt görünüm ve mevcut 125/150% preview regresyonları geçti. Bu tur oyunda canlı görsel test veya input denemesi yapılmadı; önizlemeler açıkça sentetiktir.

## Önizlemeler — aynı dört world entity

İlk üç görüntüde yalnız heading değişir; entity world koordinatları aynıdır. Route/pulse örneklerinde de aynı entity seti kullanılır.

| Önizleme | Dosya |
|---|---|
| Heading 0° | [PNG](C:/Users/gokce/Documents/Codex/2026-10-31/files-pasted-by-the-user-cube/outputs/radar-tactical-20261031/heading-0.png) |
| Heading 90° | [PNG](C:/Users/gokce/Documents/Codex/2026-10-31/files-pasted-by-the-user-cube/outputs/radar-tactical-20261031/heading-90.png) |
| Heading 180° | [PNG](C:/Users/gokce/Documents/Codex/2026-10-31/files-pasted-by-the-user-cube/outputs/radar-tactical-20261031/heading-180.png) |
| Sonar yarı yarıçap | [PNG](C:/Users/gokce/Documents/Codex/2026-10-31/files-pasted-by-the-user-cube/outputs/radar-tactical-20261031/sonar-mid.png) |
| Sonar kapalı | [PNG](C:/Users/gokce/Documents/Codex/2026-10-31/files-pasted-by-the-user-cube/outputs/radar-tactical-20261031/sonar-off.png) |
| Rota + entity | [PNG](C:/Users/gokce/Documents/Codex/2026-10-31/files-pasted-by-the-user-cube/outputs/radar-tactical-20261031/route-entities.png) |
| Kuzey sabit | [PNG](C:/Users/gokce/Documents/Codex/2026-10-31/files-pasted-by-the-user-cube/outputs/radar-tactical-20261031/north-up.png) |
| Heading bilinmiyor | [PNG](C:/Users/gokce/Documents/Codex/2026-10-31/files-pasted-by-the-user-cube/outputs/radar-tactical-20261031/heading-unknown.png) |
| Kuzey sabit, heading bilinmiyor | [PNG](C:/Users/gokce/Documents/Codex/2026-10-31/files-pasted-by-the-user-cube/outputs/radar-tactical-20261031/north-unknown.png) |
| Eski konum | [PNG](C:/Users/gokce/Documents/Codex/2026-10-31/files-pasted-by-the-user-cube/outputs/radar-tactical-20261031/stale.png) |
| Kompakt | [PNG](C:/Users/gokce/Documents/Codex/2026-10-31/files-pasted-by-the-user-cube/outputs/radar-tactical-20261031/compact.png) |
| Ayarlar | [PNG](C:/Users/gokce/Documents/Codex/2026-10-31/files-pasted-by-the-user-cube/outputs/radar-tactical-20261031/settings.png) |

## Otomatik av sınırı

HuntEngine, IHuntCombatBackend, DirectProfileCombatBackend, TargetVerification, DeathVerification, PetTracker, motion okuyucusu, Macro recorder ve native input dosyaları değişmedi. Yeni backend, input araştırması, SendInput/PostMessage denemesi, canlı Macro kaydı veya LIVE_VERIFIED değişikliği yapılmadı.

**GameMacroCombatBackend = NOT SAFE TO IMPLEMENT**

**FULL AUTO LIVE INPUT = BLOCKED**

**LIVE_VERIFIED = false**
