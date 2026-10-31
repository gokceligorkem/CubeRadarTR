# 0.6 · Canlı boss kaydı ve boss sesi

Kullanıcı isteği: boss kesildikten sonra sayaç oyun kaydıyla yenilensin; boss uyarısı küp sesinden ayrı, tok bir ses olsun.

- `01 91` sunucu boss listesinden harita, sabit slot, canlılık ve doğma zamanını doğrulayarak oku. Eksik/çelişkili paket takvimi değiştirmez.
- Mevcut boss kimlikleri, konumları, uyarı seçimleri ve saat dilimi korunur. İlk canlı eşleme doğrulanmış harita/slot tablosuyla yapılır; belirsiz isim eşlemesi yapılmaz.
- Liste güncellemeleri doğma saatini ve canlılık durumunu yeniler. Canlı bossun eski saati uyarı üretmez. Aynı doğma zamanı tekrar ses üretmez; yeni doğma zamanı uyarıyı yeniden kurar.
- Yakında görülen NPC'nin ölüm kaydı eşlenebiliyorsa eski sayaç geçersiz olur. Oyun listesindeki yeni saat önceliklidir. Öğrenilmiş aralıkla hesaplanan saat açıkça tahmindir; bilinmeyen süre uydurulmaz.
- Oyun harita listesi kapalıyken uzaktaki tüm bossların kesildiği garantisi yoktur. Son veri zamanı ve güncelleme gereksinimi gösterilir.
- Boss sesi: özgün kısa davul/boru motifi, ayrı test düğmesi; mevcut ses kapatma ve boss seçimi kuralları korunur.
- Doğrulama: gerçek BABA boss paketleri ile parser testi; doğum/ölüm/yeni sayaç ve yinelenen uyarı testleri; ses PCM/tepe/volüm testi; native arayüz önizlemesi ve canlı kayıt karşılaştırması.
