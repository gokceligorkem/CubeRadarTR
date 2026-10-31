# Hareket/bakış yönü incelemesi — 30 Ekim 2026

**Güncelleme 0.3.0:** Aşağıdaki 0.2.1 incelemesinden sonra kullanıcı salt okunur
yöntemi onayladı. C2S konum/bakış alanları çözüldü, arayüze bağlandı ve kullanıcı
yürürken Packet gördüğünü doğruladı. Güncel durum TEKNIK-RAPOR-TR.md'dedir.
Aşağıdaki metin önceki incelemenin tarihçesidir.

Gerçek karakter bakış yönü henüz radara bağlanmadı. İnceleme sırasında kendi
karakter kimliğinin başka bir buff caster'ına kayması bulundu ve 0.2.1'de düzeltildi.
Bu hata çözülmeden aday açıların karaktere aidiyetini doğrulamak güvenilir değildi.

## Canlı ölçüm

Kullanıcı dash, sola dönüş ve yürüyüş yaptığını bildirdi. 120 saniyelik yerel
ölçümde yalnız AION2'nin etkin dünya bağlantısı, iki yönde pasif dinlendi:

- 3.620 gelen, 3.759 giden TCP paketi; giden 50.711 payload byte.
- Giden yönde 3.307 dış çerçeve; 2.250 adet 10 byte body.
- Bu 10 byte body'lerin ilk iki byte'ı 2.214 farklı değer aldı; beklenen açık
  `01 36` zaman kaydı başlığına eşleşme 0. TCP boşluğu veya hizalama kaybı yoktu.
- Dış uzunluklar ayrıştırılabildi, içerik/opcode açık metin olarak okunamadı.
- 73 konum güncellemesi ve 142 cast açısı adayı toplandı; bunlar aralıksız,
  yerinde dönüşü izleyen bir heading kanıtı oluşturmadı.

İkinci 45 saniyelik ölçümde kimlik önce `0x4637` kaydı üzerinde seçildi,
26 saniye sonra `0x2B38` buff kaydında başka bir entity'ye geçti. İlk ölçümdeki
“own movement” sayıları bu nedenle gerçek oyuncunun hareketi sayılmaz.
Kimlik düzeltmesinden sonraki 60 saniyede yalnız BuffConsensus oluştu; bu
örnek kesin kendi-karakter yön doğrulaması için uygun sayılmadı.

Tam paketler, IP adresleri, hesap verisi veya anahtarlar kaydedilmedi. Araştırma
örnekleri yerel çalışma alanında tutuldu; kullanıcı paketine eklenmedi.

## Düzeltme ve kanıt

Eski sürüm buff ve self-only kimlik oylarını ortak kayan listede sayıyordu.
Kalabalık sahnede başka entity'nin buff'ları üstün gelince SelfId değişebiliyordu.

- SelfInfo artık yeni SelfInfo gelene kadar kesin kaynaktır.
- İki ardışık tutarlı self-only kayıt, buff tahmininden önceliklidir.
- Buff kayıtları bu iki güçlü kimlik kaynağını değiştiremez.
- Buff-only/Unknown kimliği kesin mesafe, hareket yönü veya proximity bipi üretmez.
- Tanı ekranı Identity Source ve Player Entity bilgisini gösterir.
- Eski kodda başarısız olan dört kimlik ve iki geometri regresyonu düzeltildi;
  250/250 test geçti. Mevcut küp parser'ı, Npcap filtresi ve ses backend'i korundu.

## Gerçek yön için kalan adım

Bağımsız birincil araştırma `00 37`/`01 37` giden kayıtlarda XYZ, sıkıştırılmış
hareket açısı ve ayrı float bakış açısı alanlarını belgeliyor. Bu alanların
bazı anlamları kaynakta hâlâ çıkarım düzeyinde; mevcut istemcide kontrollü
ileri/geri/yan hareket ve dönüşle ayrıca doğrulanmaları gerekiyor.
[C2S araştırması](https://github.com/lunixxx37/aion2-protocol-research/blob/main/docs/C2S_RESEARCH.md),
[alan kataloğu](https://github.com/lunixxx37/aion2-protocol-research/blob/main/docs/OPCODES.md).

Bu kayıtların body'leri sürekli RC4 ile şifreli. Sadece Npcap filtresini
genişletmek çözüm sağlamaz. Kaynakta çözüm için oyunun aktif ağ anahtarının
salt okunur süreç erişimiyle alınması ve doğru bağlantının başlangıcından
şifre durumunun eşlenmesi kullanılıyor.
[Protokol](https://github.com/lunixxx37/aion2-protocol-research/blob/main/docs/PROTOCOL.md).

Bu, 0.2.1'deki yalnız gelen trafik mimarisinden farklı bir kapsamdır.
Bu sürüme oyun belleği erişimi, anahtar çıkarma veya şifre çözme eklenmedi;
kullanıcıdan ayrıca kapsam tercihi istendi. Oturum anahtarı dosyaya veya sohbete
çıkarılmadan, yalnız RAM içinde kullanılan bir çözüm hazırlanabilir. Aktif
bağlantı başlangıcı kaçırılmışsa karakter bağlantısının yeniden açılması gerekebilir.
