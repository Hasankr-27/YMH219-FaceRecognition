\# Yüz Algılama ve Tanıma – C# Windows Forms (OpenCvSharp)



YMH 219 Nesne Tabanlı Programlama dersi ek uygulama ödevi kapsamında geliştirilmiş, kameradan veya seçilen bir resimden yüz algılayan ve kullanıcının oluşturduğu küçük yerel örnek küme üzerinde yüz tanıma yapan bir Windows Forms uygulamasıdır.



\*\*Öğrenci:\*\* Hasan Kara – 250541015



\## Ekran Görüntüleri



| Kameradan tanıma | Tanınmayan yüz |

|---|---|

| !\[Kameradan tanıma](screenshots/kamera.png) | !\[Tanınmayan yüz](screenshots/bilinmiyor.png) |



\## Kullanılan Teknolojiler



\- C#

\- .NET 8

\- Windows Forms

\- \[OpenCvSharp4.Windows](https://www.nuget.org/packages/OpenCvSharp4.Windows) – OpenCV'nin C# sarmalayıcısı ve Windows çalışma zamanı

\- \[OpenCvSharp4.Extensions](https://www.nuget.org/packages/OpenCvSharp4.Extensions) – OpenCV görüntüsünü `Bitmap`'e çevirme

\- Haar Cascade (`haarcascade\_frontalface\_default.xml`) – yüz algılama

\- LBPH (Local Binary Patterns Histograms) – yüz tanıma



\## Kurulum ve Çalıştırma



1\. Depoyu klonlayın:

&#x20;  ```

&#x20;  git clone https://github.com/Hasankr-27/YMH219-FaceRecognition.git

&#x20;  ```

2\. `.sln` / `.slnx` dosyasını Visual Studio 2022 veya üzeriyle açın. NuGet paketleri otomatik olarak indirilir.

3\. `F5` ile çalıştırın.



Yüz algılama dosyası (`Data/haarcascade\_frontalface\_default.xml`) depoda bulunur ve derleme sırasında çıktı klasörüne kopyalanır.



\## Kullanım



1\. \*\*📷 Kamera Başlat\*\* ile kamerayı açın veya \*\*🖼 Görüntü Seç\*\* ile bir resim seçin. Algılanan yüzler dikdörtgenle işaretlenir.

2\. \*\*İsim\*\* kutusuna bir etiket yazın ve \*\*Örnek Kaydet (20)\*\* butonuna basın. Kamera açıksa yaklaşık 3 saniyede 20 yüz örneği kaydedilir (başı hafifçe oynatmak önerilir); resimden ise 1 örnek kaydedilir.

3\. En az \*\*iki farklı kişi/etiket\*\* için örnek kaydedin.

4\. \*\*Eğit\*\* butonuna basın.

5\. Bundan sonra her yüzün üzerinde tahmin edilen isim ve güven skoru gösterilir. Tanınan yüzler yeşil, tanınmayan yüzler kırmızı çerçeveyle ve \*\*Bilinmiyor\*\* etiketiyle gösterilir.



Daha önce kaydedilmiş örnekler varsa uygulama açılışta modeli otomatik olarak eğitir.



\## Özellikler



\- Kameradan canlı görüntü (arka plan iş parçacığında, arayüz donmadan)

\- Dosyadan resim seçme (Türkçe karakterli dosya yollarında da çalışır)

\- Haar Cascade ile yüz algılama ve dikdörtgenle işaretleme

\- Etiketli yüz örneği kaydetme (yüz kesilir, griye çevrilir, 200x200 boyutuna getirilir, ışığı dengelenir)

\- LBPH ile model eğitme ve tanıma

\- Tahmin edilen etiket ve güven skorunun hem görüntü üzerinde hem arayüzde gösterilmesi

\- Belirli bir benzerlik eşiğinin altındaki yüzler için \*\*Bilinmiyor\*\* sonucu

\- Kamera durum göstergesi (\*\*KAMERA AÇIK / KAMERA KAPALI\*\*) ve kayıtlı örnek sayılarının gösterimi

\- Hata yönetimi: kamera açılamaması veya bağlantının kopması, algılama dosyasının bulunamaması, geçersiz resim dosyası, isim girilmemesi, tek etiketle eğitim denemesi gibi durumlarda uygulama kapanmadan bilgilendirme yapılır



\## Proje Yapısı



| Sınıf | Görevi |

|---|---|

| `CameraService` | Kamerayı açar, arka planda sürekli kare okur ve olay (event) ile bildirir |

| `FaceDetector` | Haar Cascade ile görüntüdeki yüzlerin konumlarını bulur |

| `FaceRecognizerService` | Yüz örneklerini kaydeder, LBPH modelini eğitir, tahmin ve güven skoru üretir |

| `Form1` | Arayüz, butonlar, sonuçların çizilmesi ve gösterilmesi |



Kamera, algılama ve tanıma işlemleri arayüzden ayrı sınıflarda tutulmuştur. Kaynak kullanan nesneler (`Mat`, `CascadeClassifier`, LBPH modeli) iş bitince serbest bırakılır.



\## Güven Skoru Hakkında



LBPH, iki yüz arasındaki farkı bir \*\*uzaklık\*\* değeri olarak verir; değer küçüldükçe yüzler daha benzerdir. Arayüzde gösterilen güven skoru `100 - uzaklık` formülüyle hesaplanır (0–100 arası). Uzaklık 80'den büyükse yüz \*\*Bilinmiyor\*\* olarak etiketlenir. Bu skor bir olasılık değil, benzerlik ölçüsüdür. Eğitimde kullanılan resmin aynısı test edildiğinde skor %100'e yaklaşır.



\## Bilinen Sınırlamalar



\- Yüz örnekleri programın çalıştığı klasördeki `Faces` klasöründe tutulur ve gizlilik nedeniyle depoya eklenmez; projeyi farklı bir bilgisayarda çalıştırırken örneklerin yeniden kaydedilmesi gerekir.

\- LBPH basit bir yöntemdir; ışık, açı ve yüz ifadesindeki değişikliklerden etkilenir. Daha fazla ve çeşitli örnek kaydetmek doğruluğu artırır.

\- Haar Cascade yalnızca karşıdan bakan yüzleri güvenilir şekilde algılar.

\- OpenCV'nin yazı fonksiyonu Türkçe karakterleri desteklemediği için görüntü üzerindeki isimlerde Türkçe harfler İngilizce karşılıklarıyla gösterilir.

\- Uygulama Windows'a özeldir (x64).

