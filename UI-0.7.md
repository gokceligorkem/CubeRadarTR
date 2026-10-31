# 0.7 · Tactical companion UI

Kullanıcının 30.10.2026 tarihli ayrıntılı UI isteği uygulama sözleşmesidir.
Görsel yön: mevcut 0.6 ekranları ve kullanıcının verdiği hiyerarşi; koyu katmanlar,
az vurgu, 4/8/12/16/24 boşluk, 8 px kart köşesi. Neon, gradyan ve yer tutucu kontroller yok.

## Bileşenler / sıra

1. `UiTheme`, `HudButton`, `ToggleSwitch`, `SurfaceCard`, ortak sade grid.
2. `RadarForm.Layout`: modern gezinme, durum rozeti, ayrı en yakın küp kartı,
   radar + yakın küpler, tek Başlat/Durdur, ayarlar içinde teknik durum.
3. `RadarCanvas`: aynı projeksiyon; merkezde SEN, kısa yönler, çakışmayan halka
   etiketleri, ortada bekleme metni yok; küçük mini modda yalnız hedef blip'i.
4. `BossPanel.Layout` + `UpcomingBossCard`: en yakın 3 etkinlik, korunmuş satır
   kimlikleri/scroll/checkbox, ayrı Kalan/Durum, hafif vurgu, toplu işlemler menüsü.
5. `MiniRadarForm`: kısa durum, büyük mesafe, tek satır boss, resize/compact,
   sürükleme ve isteğe bağlı click-through; güvenli kapatma/geri alma yolu.
6. Native render + davranış doğrulaması; paketleme ve mevcut kurulumu güncelleme.

## Korunacak sınır

CaptureController, MotionBridge, Core protokol/hesap/takvim modelleri ve okuyucu
değişmeyecek. Boss Tick/Observe/gate ve kimlik/uyarı seçimleri korunacak.
Yeni ses seviyesi yalnız aynı mevcut ses oynatıcıya uygulanan UI tercihidir.
Görsel radar menzili mevcut WorldToRadar(rangeM) parametresini kullanabilir;
oyunun yakalaması veya tazelik kuralları değişmez. Başlangıç tercihi yalnız
uygulama açıldığında radar dinlemesini başlatır; Windows başlangıcını değiştirmez.
Dil Türkçedir; uygulanmamış ayar veya geçmiş/log ekranı eklenmez. Var olan
tanı ve JSON dışa aktarma Ayarlar > Gelişmiş içinden erişilir.

## Kabul / doğrulama

- Default ve dar pencere, 125/150% kontrol ölçeği, uzun isim/boş liste/yön yok,
  hedef kilitli, boss ve ayarlar görsellerinde kesilme/üst üste metin olmayacak.
- Tek ana radar düğmesi doğru çalışır; seçenekler Space/Tab ile erişilebilir.
- Radar ve mini oyuncusu daima merkezde; GDI projeksiyon formülü aynı kalır.
- Boss listesi canlı güncellemede scroll/sıralama/sütun ve kutu seçimini korur.
- Boss ilk üç kartı aynı occurrence verisini kullanır; ikinci sayaç/gate yok.
- Mini kapandığında ana radar ve sesler devam eder. Click-through kullanıcı
  ana pencereden geri alınabilir; tüm kaynaklar kapanışta temizlenir.
- Mevcut .NET testleri, okuyucu testleri ve native render/etkileşim kanıtı geçer.
