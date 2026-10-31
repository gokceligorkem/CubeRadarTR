# Cube Radar TR — Otomatik Av kaldırma ve klasör temizliği

1 Kasım 2026. Kullanıcının açık isteğiyle Otomatik Av aktif uygulamadan ve bu branch'in kaynak ağacından kaldırıldı. Bu işlem eski BLOCKED özelliğini gizlemek veya manuel ava çevirmek değildir. Güncel ürün durumu **Otomatik Av: KALDIRILDI**.

## Kapsam ve geri dönüş

- Çalışma branch'i: `cleanup/radar-only-20261101`.
- Başlangıç HEAD: `ed510caf7403b9c88106a770c1b3a2aea42fccd9`.
- Önceki tam kaynak, test ve araştırmalar `feature/player-centered-radar-20261031` branch'inde bu HEAD'de korunur. Ayrı `refactor/hunt-combat-backend-20261031` checkpoint worktree'sine dokunulmaz.
- Push, release, publish veya installer işlemi yapılmaz.

## Kaldırılanlar

- Otomatik Av sayfası, rota/profil düzenleyicileri ve kayıt kontrolleri.
- HuntEngine, combat backend, otomatik hedef seçimi/doğrulama ve saldırı yürütme kodu.
- Windows sentetik giriş, tuş sahipliği ve klavye/fare hook sınıfları.
- Macro Tanılama kaydedicisi, hedef-selection aboneliği ve yalnız bunlar için kullanılan pasif combat observer API'si.
- Özelliğe özel araştırma belgeleri ve artık üretim kodu bulunmayan özelliğin testleri. Toplam **51 dosya** kaldırıldı; ortak testler aşağıdaki gibi taşındı.

## Korunanlar

Radar, Boss & Etkinlik, Pet Avı ve Ayarlar olmak üzere dört sayfa kalır. Mini radar, mini pet, küp kilidi/uyarıları, Pet kataloğu yaratık gösterimi, karakter yönü/kuzey sabit seçimi, sonar ve sabit duruşta açıkça işaretlenen **Son bilinen konum** görünümü korunur.

PetTracker'ın kendi hit/damage sayaçları, exact ölüm/yaşam kimliği kayıtları, map/session sınırları ve Pet Avı ilerlemesi korunur. Kaldırılan observer yalnız eski tanılama tüketicileri içindi. Motion okuyucusu ve Python kaynakları değiştirilmez; mevcut salt okunur helper'ın isteğe bağlı hedef kayıtları uygulama tarafından artık kullanılmaz.

`%LOCALAPPDATA%\CubeRadarTR` içindeki kullanıcı ayarları silinmez. Eski `hunt.json` içindeki Ranger profili ve 38 noktalı güncel rota diskte korunur, yeni uygulama bunları yüklemez. Ayrı `work/macro-trace-20261031/hunt.before.json` içindeki 63 noktalı checkpoint kopyası korunur; restore yapılmaz. Eski kullanıcının trace exportları temizliğe dahil değildir.

## Gerçek doğrulama

| Kontrol | Sonuç |
| --- | --- |
| .NET solution Release build | PASS, 0 uyarı / 0 hata |
| Güncel .NET test paketi | **399/399 PASS**, 0 atlanan |
| Python motion testleri | **22/22 PASS** |
| Türkçe WinForms win-x64 build | PASS, 0 uyarı / 0 hata |
| WinForms self-test | PASS; fixture/önizleme, canlı oyun testi değildir |
| Üretim assembly denetimi | Av motoru/editor/recorder, SendInput ve SetWindowsHookEx importları yok |
| Son bilinen konum regresyonu | 60 saniye görsel history; timestamp değişmez; kaldırılan yaratık silinir; yeni gözlem canlıya döner |

Önceki 627 sayısı kaldırılan Av/Macro testlerini de içeriyordu; güncel paketin sayısı 399'dur. Konum gözlemi ve Pet yaşam/ölüm kimliği için toplam 10 ortak test vakası ayrı `ClientPoseObservationTests` ve `PetLifeIdentityTests` dosyalarına taşındı. Core'da Hunting namespace'i kalmamasını doğrulayan yeni test, kaldırmadan önce FAIL, sonra PASS oldu. Eski 627 testin yeniden çalıştırıldığı iddia edilmez.

Self-test dört sayfayı, küçük/geniş ve %125/%150 arayüzü, Pet Avı/mini pet, boss ekranı, yaratık filtreleri, mini radar ve son bilinen konum davranışını doğrular. Bu tur canlı otomatik input veya Macro deneyi yapılmaz.

## Uygulama klasörü

Hedef: masaüstündeki `CubeRadarTR`. Yedek ve kanıtlar bu klasörün dışında, mevcut çalışma alanında tutulur.

- Tam eski klasör yedeği: `work/radar-tactical-20261031/desktop-before-radar-only-20261101`.
- Kullanıcı ayarı yedeği: `work/radar-tactical-20261031/settings-before-radar-only-20261101`.
- Eski klasör: 40 dosya / 44.722.676 bayt.
- Temiz Windows x64 klasörü: 17 dosya / 12.325.925 bayt.
- Azalma: 32.396.751 bayt, yaklaşık **30,9 MiB**.
- Gereksiz PDB sembolleri ve Windows x64 dışındaki platform/mimari SQLite kopyaları çıkarılır. SQLite'ın gereken x64 native DLL'i köke alınır; build çıktısındaki runtime bağımlılıkları korunur.
- Mevcut `config` dosyaları ve Canlı yön için gereken `Motion/CubeRadarMotion.exe` ile lisansı byte düzeyinde korunur. Masaüstü klasörüne kaynak/test/araştırma/önizleme/yedek dosyası konmaz.

Yerel doğrulama logları, TRX, self-test JSON'u, önce/sonra SHA-256 manifestleri ve ekran kanıtları `work/radar-tactical-20261031` altında tutulur. Bu belge güncel uygulama kapsamını tanımlar; eski radar teslim belgelerinin tarihsel test sayıları güncel sonuç olarak kullanılmamalıdır.
