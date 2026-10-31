# Canlı boss kaydı kaynakları ve doğrulama

- Field list / NPC / death layouts: https://github.com/cyberbadger6969/aion2-dps-meter/blob/main/src/AionMeter.Core/Protocol/PacketParser.cs
  (30.10.2026, file blob fcb062f0f27b36f65a41e6a31460fcde61810917, GPL-3.0).
  Yerel parser tam liste kabulü, sınırlar, sonlu koordinatlar ve tekil slot kontrolleriyle uyarlandı.
- NPC adları/kodları: aynı deponun data/npcs/en.json dosyasından sınırlı Altgard/Abyss alt kümesi.
  Kaynak bildirimi: https://github.com/cyberbadger6969/aion2-dps-meter/blob/main/data/NOTICE.md
  (NCSOFT istemci verisi, taengu/A2Tools-DPS-Meter e32dc5e, GPL-3.0).
- Altgard 1110 blok 2400 eşlemesi: aynı deponun data/field_boss_maps.json dosyası;
  tam 24 NPC koduyla eşlenir. Beklenmeyen slot sayısında isim tahmin edilmez.
- BABA 30.10.2026 gerçek 01 91 kaydı: fixtures/bosses/baba-altgard.json,
  yakalama 10:37:54.565 UTC. Gartua 22:29:56.024 UTC ve Lagta 22:33:03.274 UTC;
  yeni 12 saatlik sayaçları aynı anda oyun ekranında görüldü.
- Abyss (Chaotic Lower Reshanta) harita 20, 8 slot: fixtures/bosses/baba-map-20.json,
  yakalama 10:39:14.064 UTC. Oyun listesi sırası Argo / Kaira / Nahma×3 /
  Executor Tamasa / Argo / Kaira olarak ekranla doğrulandı (yaklaşık 10:45:55 UTC).
  Slot 2001 11:03:48.234 UTC, 2003–2005 20:10:00 UTC, 2006–2008 ertesi gün 20:40:00 UTC.
  2002 zaman=0'dır. Oyun ekranı bu slot için 15:00 TR'ye sayıyor.
  Nahma'nın üç tekil NPC kodunun konum-slot ilişkisi doğrulanmadı; ölüm eşlemesi kapalıdır.
- Watcher Kaira'nın Global istemci aralığı 3 saattir:
  https://aion2hub.com/tools/world-bosses (Global istemci verisi 19.09.2026).
  Kaynağın varsayılan saat dilimi yerine BABA oyun ekranında ayrıca doğrulanan
  15:00 TR fazı kullanılır. Bu saat paket zamanı değil, takvimdir.

Kapsam: yalnız zaten yapılandırılmış bosslar güncellenir; boss seçimi/konumu korunur.
Artifact Siege 01 91 boss listesinde yoktur ve ayrı tek seferlik etkinlik kaydı olarak kalır.
Oyun listesi veri göndermiyorsa uygulama kendiliğinden yeni sunucu saati üretemez.
Yakın gerçek ölüm + ölçülmüş aralık varsa geçici tahmin; aksi halde yeni liste beklenir.
Boss sesi kendi oluşturduğumuz motif olup oyun ses dosyası içermez.
