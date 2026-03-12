using FaceRecognitionDotNet;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using SmartAttendance.Application.Interfaces;
using SmartAttendance.Infrastructure.Persistence;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace SmartAttendance.Infrastructure.Services
{
    public class FaceRecognitionService : IFaceRecognitionService
    {
        private readonly SmartAttendanceDbContext _context;
        private readonly FaceRecognition _faceRecognition;

        public FaceRecognitionService(SmartAttendanceDbContext context)
        {
            _context = context;

            // Yapay zeka modellerinin aranacağı klasör (Ekranda gördüğüm "AI_Model" adına göre ayarlandı)
            string modelsDirectory = Path.Combine(Directory.GetCurrentDirectory(), "AI_Model");

            if (!Directory.Exists(modelsDirectory))
                Directory.CreateDirectory(modelsDirectory);

            try
            {
                _faceRecognition = FaceRecognition.Create(modelsDirectory);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[AI MOTORU HATASI] Modeller yüklenemedi: {ex.Message}");
            }
        }

        public async Task<List<int>> IdentifyStudentsInCrowdAsync(int sessionId, IFormFile frame)
        {
            var recognizedStudentIds = new List<int>();

            // 1. Bu oturuma kayıtlı ve sistemde Yüz Vektörü (FaceEncoding) olan öğrencileri getir
            var session = await _context.AttendanceSessions
                .Include(s => s.RelatedCourses)
                .FirstOrDefaultAsync(s => s.Id == sessionId);

            if (session == null) return recognizedStudentIds;

            var courseIds = session.RelatedCourses.Select(rc => rc.CourseId).ToList();

            var enrolledStudents = await _context.CourseEnrollments
                .Include(ce => ce.Student)
                .Where(ce => courseIds.Contains(ce.CourseId) && !string.IsNullOrEmpty(ce.Student.FaceEncoding))
                .Select(ce => ce.Student)
                .Distinct()
                .ToListAsync();

            if (!enrolledStudents.Any() || _faceRecognition == null)
                return recognizedStudentIds; // Tanınacak kimse yoksa veya AI çökükse çık

            // 2. Kameradan Gelen Fotoğrafı Yükle (GEÇİCİ DOSYA TAKTİĞİ)
            var tempFilePath = Path.GetTempFileName();
            try
            {
                // Fotoğrafı geçici olarak sunucu diskine yazıyoruz
                using (var stream = new FileStream(tempFilePath, FileMode.Create))
                {
                    await frame.CopyToAsync(stream);
                }

                // Resmi doğrudan dosya yolundan yüklüyoruz (Böylece Bitmap hatalarından kurtuluyoruz)
                using var unknownImage = FaceRecognition.LoadImageFile(tempFilePath);

                // 3. Kalabalık Fotoğraftaki Tüm Yüzleri Bul
                var faceLocations = _faceRecognition.FaceLocations(unknownImage).ToArray();

                if (faceLocations.Length == 0)
                    return recognizedStudentIds; // Yüz bulunamadı

                // 4. Bulunan Yüzlerin Vektörlerini Çıkar
                var faceEncodings = _faceRecognition.FaceEncodings(unknownImage, faceLocations).ToArray();

                // 5. Kameradaki Yüzleri, Veritabanındaki Öğrencilerle Karşılaştır (Öklid Matematiği ile)
                foreach (var unknownEncoding in faceEncodings)
                {
                    int? matchedStudentId = null;
                    double bestDistance = 0.6; // Eşleşme hassasiyeti (0.6 standarttır. Düşürdükçe katılaşır)

                    // Canlı kameradan gelen yüzün 128 boyutlu sayısal dizisini alıyoruz
                    var liveVector = unknownEncoding.GetRawEncoding().ToArray();

                    foreach (var student in enrolledStudents)
                    {
                        // Veritabanındaki string "[0.12, -0.45...]" verisini double dizisine çevir
                        var dbVectorArray = JsonSerializer.Deserialize<double[]>(student.FaceEncoding);

                        // Veri bozuksa veya tam 128 boyutlu değilse atla
                        if (dbVectorArray == null || dbVectorArray.Length != 128) continue;

                        // --- KÜTÜPHANEYİ BYPASS EDİYORUZ: SAF MATEMATİK İLE ÖKLİD UZAKLIĞI HESABI ---
                        double sumOfSquares = 0;
                        for (int i = 0; i < 128; i++)
                        {
                            double diff = liveVector[i] - dbVectorArray[i];
                            sumOfSquares += diff * diff;
                        }
                        double distance = Math.Sqrt(sumOfSquares);
                        // -----------------------------------------------------------------------------

                        // Eğer 0.6'dan küçükse (benziyorsa) ve şu ana kadarki en iyi eşleşmeyse kaydet
                        if (distance < bestDistance)
                        {
                            bestDistance = distance;
                            matchedStudentId = student.Id;
                        }
                    }

                    // Eşleşen öğrenciyi listeye ekle (Mükerrer eklemeyi önle)
                    if (matchedStudentId.HasValue && !recognizedStudentIds.Contains(matchedStudentId.Value))
                    {
                        recognizedStudentIds.Add(matchedStudentId.Value);
                    }

                    unknownEncoding.Dispose();
                }

                unknownImage.Dispose();
            }
            finally
            {
                // Hata çıksa da çıkmasa da, işlem bitince geçici resmi bilgisayardan sil (Çöp bırakma)
                if (System.IO.File.Exists(tempFilePath))
                {
                    System.IO.File.Delete(tempFilePath);
                }
            }

            return recognizedStudentIds;
        }
    }
}