SmartAttendance - Akıllı Yoklama Sistemi
SmartAttendance, eğitim kurumları için geliştirilmiş; Yüz Tanıma (AI), QR Kod ve Konum Doğrulama yöntemlerini birleştiren modern bir yoklama otomasyonu sistemidir. Bu repo, projenin .NET 8.0 ile geliştirilen merkezi API ve Yapay Zeka servislerini içerir.

👨‍💻 Bu Projede Ne Yaptım?

Bu projede, yapay zeka destekli yoklama sisteminin backend tarafını .NET 8 kullanarak sıfırdan tasarladım ve geliştirdim.

.NET 8 Web API ile yüksek performanslı ve ölçeklenebilir bir backend mimarisi kurdum.
ONNX Runtime kullanarak yüz tanıma (Face Recognition) pipeline’ını geliştirdim ve ArcFace modeli ile yüzleri vektörel formata dönüştürdüm.
Cosine Similarity algoritması ile gerçek zamanlı yüz eşleştirme mekanizmasını implemente ettim.
SignalR kullanarak yoklama verilerinin anlık olarak istemcilere iletilmesini sağlayan gerçek zamanlı iletişim altyapısını kurdum.
Firebase Admin SDK ile yoklama sonuçlarına göre öğrencilere anlık push bildirim sistemi geliştirdim.
Entity Framework Core ile ilişkisel veritabanı modelini tasarladım ve Code-First yaklaşımıyla yönettim.
JWT tabanlı kimlik doğrulama ve rol bazlı yetkilendirme sistemi geliştirdim.
Katmanlı mimari (Application, Domain, Infrastructure, WebApi) kullanarak sürdürülebilir ve modüler bir proje yapısı oluşturdum.
OpenCvSharp kullanarak sunucu tarafında görüntü ön işleme (pre-processing) süreçlerini yönettim.
Öne Çıkan Özellikler
Panoramik Sınıf Taraması (AI): Öğretmen kamerayı sınıfa doğrulttuğunda, sistemdeki tüm yüzleri aynı anda tespit eder ve veritabanındaki öğrencilerle eşleştirerek yoklamayı otomatik alır.

Dinamik QR Kod: 12 saniyede bir yenilenen ve zaman damgası içeren güvenli QR kod sistemi.

Konum Doğrulama: GPS koordinatları üzerinden öğrencinin sınıfta olduğunu doğrulayan yapı.

Gerçek Zamanlı Bildirimler: SignalR ile yoklaması alınan öğrencinin isminin anlık olarak öğretmen ekranına düşmesi.

Firebase Entegrasyonu: Yoklama başladığında öğrencilere otomatik push bildirim gönderimi.
Kullanılan Teknolojiler
Framework: .NET 8.0 (C#)

Veritabanı: SQL Server & Entity Framework Core

Yapay Zeka (AI):

Microsoft.ML.OnnxRuntime: Modellerin CPU üzerinde yüksek performanslı çalışması için.

OpenCvSharp4: Resim işleme, kırpma ve ön işleme süreçleri için.

İletişim & Bildirim:

SignalR: Canlı veri akışı ve AR (Artırılmış Gerçeklik) tipi bildirimler için.

Firebase Admin SDK: Mobil bildirim yönetimi için.

Kimlik Doğrulama: JWT (JSON Web Token) & Rol Tabanlı Yetkilendirme.

Yapay Zeka Mimarisi
Sistem, iki aşamalı bir derin öğrenme boru hattı (pipeline) kullanır:

Face Detection (Yüz Tespiti): RetinaFace (veya mobil tarafta SSD Mobilenetv1) modeli ile resimdeki tüm insan yüzleri koordinat bazlı tespit edilir.

Face Recognition (Yüz Tanıma): ArcFace modeli, tespit edilen yüzleri 128/512 boyutlu matematiksel vektörlere (FaceEncoding) çevirir.

Doğrulama: Kameradan gelen anlık vektör ile veritabanındaki vektör arasında Cosine Similarity (Kosinüs Benzerliği) hesaplanır. Eşik değer (Threshold) hassasiyeti 0.25 olarak optimize edilmiştir.

📂 Proje Yapısı
SmartAttendance
├── SmartAttendance.Application    # Arayüzler ve İş Mantığı (Interfaces)
├── SmartAttendance.Domain         # Varlıklar (Entities)
├── SmartAttendance.Infrastructure # Persistence (EF Core), AI Servisleri, Hubs
└── SmartAttendance.WebApi         # API Controller'lar ve Program.cs
│       └── AI_Model               # .onnx Model dosyaları

⚙️ Kurulum ve Yapılandırma
1. Yapay Zeka Modelleri
Projenin çalışması için retinaface.onnx ve arcface.onnx dosyalarının AI_Model klasörü altında bulunması şarttır.

Önemli: Visual Studio'da bu dosyalara sağ tıklayıp "Copy to Output Directory" ayarını "Copy Always" yapmalısınız.

2. Firebase Kurulumu
firebase-key.json dosyasını Firebase Console'dan indirip SmartAttendance.WebApi projesine ekleyin.

Hata Notu: Eğer "Invalid JWT Signature" hatası alırsanız, anahtarı yenileyin ve sistem saatinizin güncel olduğunu kontrol edin.

3. Veritabanı
appsettings.json dosyasındaki ConnectionString'i güncelledikten sonra migration'ları uygulayın:
  dotnet ef database update

📡 API Uç Noktaları (Önemli Olanlar)
POST /api/Attendance/start: Yeni yoklama oturumu başlatır.

POST /api/Attendance/instructor/scan-crowd: Sınıf fotoğrafını işleyerek yüzleri tanır.

GET /api/Attendance/my-courses: Eğitmenin sorumlu olduğu dersleri listeler.

