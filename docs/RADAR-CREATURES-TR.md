# Radar üzerinde yaratık gösterimi

Güncel ürün durumu: [Otomatik Av kaldırma ve klasör temizliği](RADAR-ONLY-CLEANUP-TR.md). Bu belgedeki otomatik avın korunmasına ilişkin ifadeler önceki teslimin tarihçesidir; yaratık gösterimi korunur.

Güncel sabit duruş davranışı [son bilinen konum kaydında](RADAR-IDLE-VISUAL-HISTORY-TR.md): eski konumlar güncel sayılmadan soluk/boş işaretlerle korunabilir. Aşağıdaki 614 testli ilk teslimdeki eski konumu gizleme davranışı bu açık görsel history yoluyla tamamlanmıştır; strict veri pencereleri değişmez.

1 Kasım 2026. Ana ve mini radar, mevcut PetTracker'ın bildirdiği katalog yaratıklarını mercan renkli yuvarlaklarla gösterir. Küplerin altın elmasları, hedef kilidi ve listesi korunur. Ayarlar → Radar → **Yaratıkları göster** varsayılan olarak açıktır; kayıtlı Pet Avı hedeflerinden bağımsızdır.

## Veri kapsamı

Bu katman **mevcut PetCatalog/PetTracker tarafından tanınan Pet kaynaklarıyla sınırlıdır**. Tüm oyun canavarlarının veya NPC'lerin genel radarı değildir. Yeni parser, discovery, hareket tahmini veya telemetri kaynağı eklenmedi. Kapsam radar açıklamasında ve ayarda görünürdür.

Mevcut Nearby snapshot'ı çizime aktarılır; canlı kayıtlar sentetik yaratıklarla tamamlanmaz. Kaldırılan yaratık bir sonraki snapshot'ta çizimden ve hover listesinden çıkar. Aynı entity yeniden kullanılırsa hover yaşam kimliğini de karşılaştırır. Yaratığın üzerine gelince adı, mesafesi ve yükseklik farkı görünür; işaretler oyunda hedef seçmez veya input üretmez.

## Çizim kuralları

- Cube ile aynı RadarProjector, world XY santimetre / Z yükseklik sözleşmesi ve karakter/kuzey yön modları kullanılır.
- Oyuncunun konumu eskiyse veya karakter yönü modunda geçerli yön yoksa yaratık çizilmez.
- Katalog NPC/Pet eşleşmesi ve geçerli entity/wire/life kimliği gerekir; eksik veya sonsuz koordinat çizilmez.
- Mevcut PetPanel yön gösteriminin **15 saniyelik konum penceresi** ve tracker'ın **90 saniyelik görülme penceresi** kullanılır. Sonar eski konumu yenilemez.
- Yaratık radar menzili dışındaysa gizlenir. Cube'un isteğe bağlı kenar göstergesi yaratıklara uygulanmaz.
- Mevcut Her yükseklik / Aynı seviye / Yukarıdaki / Aşağıdaki filtresi uygulanır; ±3 m sınırı Cube filtresiyle aynıdır.
- Ana ve mini radar aynı aç/kapa ayarını paylaşır. Cube kilidi/seçimi yaratık katmanından etkilenmez.
- Frame dönüşümü ve creature hit listesi yeniden kullanılır; noktalar için entity başına brush/pen oluşturulmaz.

## Doğrulama

Önce testler yazıldı; eksik presentation API nedeniyle RED alındı. Ardından **14 yeni test vakası** geçti: 0/90 derece, menzil sınırı/dışı, eski/gelecek/eksik konum, kimlik, yükseklik ve kaynak verisinin korunması. Eski UI ayarlarında varsayılan açık olması ve açık/kapalı round-trip mevcut preference testlerinde doğrulandı.

- Tüm .NET: **614/614 PASS**, failed=0, skipped=0; önceki 600 vaka korunur.
- Python Motion: **22/22 PASS**.
- Solution ve Türkçe WinForms Release build: **PASS**, 0 hata / 0 uyarı.
- WinForms self-test: **passed**, captureStarted=false. Aynı sentetik yaratık setiyle ana radar 0/90 derece, mini radar, kompakt görünüm, aç/kapa, yükseklik filtresi, kaldırılma, eski/eksik konum ve bilinmeyen yön denetlendi. Ana/mini PNG'leri görsel olarak kontrol edildi.
- Menzil koşulunu kontrollü ters çeviren mutasyonu sınır testleri yakaladı; kaynak birebir geri yüklendi ve son 614 test geçti.

Çalışma `feature/player-centered-radar-20261031` branch'indedir. Orijinal otomatik av checkpoint branch'i/HEAD'i korunur. Ranger profili, güncel 38 noktalı rota ve yerel 63 noktalı checkpoint kopyası değişmedi; restore yapılmadı. HuntEngine, motion okuyucusu, PetTracker, Macro recorder, input ve combat backend'leri değiştirilmedi. Push/release/publish/installer yapılmadı.

**GameMacroCombatBackend = NOT SAFE TO IMPLEMENT**

**FULL AUTO LIVE INPUT = BLOCKED**

**LIVE_VERIFIED = false**
