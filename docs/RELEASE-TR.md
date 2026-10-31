# Şifreli TR yayını

Kişisel indirme deposu: `gokceligorkem/CubeRadarTR`. Yayın hedefi komutta açıkça verilir. Firma hesabı
varsayılan hedef olarak kullanılmaz. İndirme deposu yalnız dağıtım bilgilerini
taşır. Uygulama kaynakları ve GPL/üçüncü taraf bildirimleri, alıcıya
verilen AES-256 ZIP içinde bulunur. Yerel proje origin adresi upstream olarak
kalır; tam kaynak ağacını indirme deposuna push etmek gerekmez.

`tools/release-tr.ps1` doğrulanmış EXE klasörü ve aynı sürümün `git archive`
kaynak ZIP'ini alır. Paket beyaz listesi Motion/config/lisansları zorunlu kılar;
kullanıcı ayarları, debug sembolleri ve beklenmeyen dosyaları dışarıda bırakır.
Bütün dosyalar AES-256 ile şifrelenir ve yeniden okunarak doğrulanır. Şifre yoksa
veya kaynak/EXE sürümü uyuşmazsa yayın başarısız olur.

## Yerel paket

Python ortamına `tools/requirements-release.txt` kurulur. Windows PowerShell 7:

```powershell
./tools/release-tr.ps1 -Version 0.8.7 -ReleaseDirectory ../../outputs/CubeRadar-0.8.7 -SourceZip ../../outputs/CubeRadarTR-0.8.7-kaynak.zip -OutputDirectory ../../outputs/CubeRadarTR-0.8.7-protected -Python ../cube-env/Scripts/python.exe
```

Paket güvenlik testleri: `python -m unittest discover -s tests/release -v`.
Bu testlerin pyzipper bağımlılığı ayrı yayın ortamına aittir; uygulamanın
standart kütüphane ile çalışan eski offline testlerine bağımlılık eklemez.

Şifre ilk çalıştırmada rastgele oluşturulur ve sonraki sürümler için aynı Windows
kullanıcısına bağlı DPAPI ile `%LOCALAPPDATA%/CubeRadarPublisher/zip-password.dpapi`
dosyasında saklanır. Şifre komut satırına, Git'e veya paket çıktısına yazılmaz.
Arkadaşlara iletilebilmesi için public asset klasörünün DIŞINDA
`PRIVATE-CubeRadarTR-zip-password.txt` adlı yerel metin kopyası hazırlanır.
Bu metin kopyasını herkese açık bir yere yükleme; işi bittiğinde normal dosya
yönetimiyle kaldırabilirsin. ZIP, güncel 7-Zip veya WinRAR ile açılır.

## Yayın

GitHub CLI kullanıcı tarafından bir kez oturum açılmışsa aynı komuta
`-Publish -Repository gokceligorkem/CubeRadarTR -NotesFile <incelenmiş-notlar.md>`
eklenir. Hedef belirtilmezse yayın yapılmaz. Araç açıkça yalnız
`CubeRadarTR.zip` ve `SHA256SUMS.txt` dosyalarını taslak release'e yükler,
isim/boyutlarını doğrular ve ardından yayına alır. Var olan sürümün üzerine
yazmaz. CLI erişimi yoksa aynı iki dosya GitHub Releases arayüzünden yüklenebilir.
Şifre ve kaynak ZIP ayrıca asset olarak yüklenmez; kaynak zaten şifreli pakettedir.

Sabit bağlantı:

`https://github.com/gokceligorkem/CubeRadarTR/releases/latest/download/CubeRadarTR.zip`

ZIP parolası arşivi açmayı sınırlar. Çıkarılmış uygulamanın yeniden paylaşımını
veya lisans kapsamında alıcının haklarını değiştiren bir çalıştırma lisansı değildir.
