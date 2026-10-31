# Pet Avı — 0.8.0

## 0.8.4 otomatik takip ve kalıcı çıkarma

Kullanıcı yakındaki eksik petlerin otomatik gelmesini, isteyenin takipten
çıkarabilmesini seçti. Doğrulanmış eksik ruhla yakındaki pet otomatik eklenir;
çıkarma kalıcı exclusion'dır, yeniden işaretleme açıkça geri alır. Ana eksik ruh
görünümü takip durumundan bağımsızdır. Son NPC ölümü gecikmeli ruhu kaybettirmez.

## 0.8.3 devam eden hedef ve MAX filtresi

Kullanıcı Seviye 3 petin mini listeden kalkmasını ve Seviye 2 sonrası 75 ruh
hedefinin görünmesini istedi. Doğrulanan rank hedefe yetişince sonraki hedefe
ilerlenir; MAX filtresi hem ana hem mini listede aynı anlamı taşır.

## 0.8.2 kompakt liste

Kullanıcı büyük kartın fazla alan kapladığını belirterek DPS metre tarzı çerçevesiz
liste ve yarı saydamlık istedi. Mini pencerede iki satırlı pet kayıtları, içerik
yüksekliğine sığma, %40–100 görünürlük ayarı ve kenardan boyutlandırma uygulanır.
Menü ···/sağ tık ile açılır. Önceki hedef, sayım, bağımsız pencere ve konum korunur.

## 0.8.1 mini pencere

Kullanıcı mini radar gibi bir pet sayacı ve kesim/kalan miktarını istedi.
Pet Avı > Mini pet, bağımsız top-level ve varsayılan üstte duran pencere açar.
Ana uygulama küçülünce görünür/güncel kalır; kapanınca temizlenir. Konum,
boyut ve üstte tut tercihi pets.json'da saklanır. Birden fazla takip kartı kaydırılır.
Üstte toplam oturum kesimi; her kartta ruh kazanımı, kalan ruh ve doğrulanabilen
pet kesimi bulunur. NPC adı başlangıçta alınmadıysa toplam kesim yine sayılır,
pet eşlemesi uydurulmaz. Gözlenen kendi saldırısı + taze NPC ölümü katılım sayılır;
bu, son vuruş veya sunucunun ödül kredisi garantisi değildir. Kesim ruh eklemez.
Corpse/despawn, başka aktör ve tekrarlanan ölüm sayılmaz. Tam koleksiyon yenileme
aynı akışın oturum sayaçlarını silmez. Kesinti sayacı kısmi işaretler.

Kullanıcı 30.10.2026 tarihinde araştırmadaki pet takibi kapsamını onayladı.
Mevcut WinForms uygulamasına Pet Avı sayfası eklenir. Aynı pasif yakalama
kullanılır; oyun girdisi, ikinci ağ dinleyicisi veya görüntü tanıma eklenmez.

## Kabul koşulları

- Girişte koleksiyon görüntüsü, ruh ödülü/harcaması ve seviye değişimi ayrıştırılır.
- Bilinmeyen, bozuk veya eksik veri sıfır sayılmaz. Bağlantı/veri kaybında eski
  sayaç kesin kabul edilmez; yeni tam görüntü eşitler. TCP tekrarları çift sayılmaz.
- Yakındaki NPC'ler doğrulanmış kaynak tablosuyla petlere eşlenir; ölüm/despawn
  çevre listesinden çıkarır. Harita değişimi çevre listesini temizler.
- Seçilen pet için hedef seviye, kalan ruh ve taze veride mesafe/yön gösterilir.
  13/25 ve Lv2 hedefi 12 ruh gösterir. Seviye 3 için sonraki 75 ruh da hesaba katılır.
- Takip/hedef ve filtreler yerel saklanır. Pet bakiyeleri yeni oturumda tekrar
  eşitlenir; başka karakterin eski bakiyesi otomatik kullanılmaz.
- Gerçek ruh kazanımı hedefi doldurunca bir tamamlanma uyarısı verilir. Girişte,
  hedef seçerken veya sessizde geçen tamamlanma sonradan tekrar seslendirilmez.
- Yeni sayfa mevcut koyu arayüzü, erişilebilir kontrolleri ve sabit grid satırlarını
  kullanır. Küp, boss, mini radar ve ses denetimleri korunur.

Core/Pets ve Protocol/PetMessages: veri ve protokol; Tr/PetPanel ve PetPreferences:
sunum/yerel tercih; CaptureController: mevcut akışa bağlama. Yeni bağımlılık yok.
Kaynak verisi 0Fox2Give/PetTracker GPL-3.0; sürüm/hash ve bildirim birlikte saklanır.

Sıra: protokol/ilerleme testleri → aynı yakalamaya bağlama → native sayfa/uyarı
→ tam test ve native etkileşim kanıtı → canlı BABA kontrolü → 0.8.0 paketi.

Test: `dotnet test CubeRadar.slnx -c Release --no-restore`
Build: `dotnet build src/CubeRadar.Tr -c Release --no-restore`
Native proof: `CubeRadarTR.exe --self-test <absolute-output.png>`
Canlı kontrol yalnız pet ve NPC alanlarını içerir; anahtar/sohbet/ham trafik kaydı yok.
