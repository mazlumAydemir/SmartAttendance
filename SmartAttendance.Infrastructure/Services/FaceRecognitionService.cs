using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using OpenCvSharp;
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
    // Yüz verilerini ve noktalarını bir arada tutmak için yardımcı sınıf
    public class FaceData
    {
        public Rect Rectangle { get; set; }
        public float[] Landmarks { get; set; } // 5 Nokta: Sol Göz, Sağ Göz, Burun, Sol Ağız, Sağ Ağız
    }

    public class FaceRecognitionService : IFaceRecognitionService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private InferenceSession _retinaFaceSession;
        private InferenceSession _arcFaceSession;

        public FaceRecognitionService(IServiceScopeFactory scopeFactory)
        {
            _scopeFactory = scopeFactory;
            LoadModels();
        }

        private void LoadModels()
        {
            // Azure ve Canlı ortamlar için en güvenli yol tanımı
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            string modelsDirectory = Path.Combine(baseDir, "AI_Model");
            string retinaPath = Path.Combine(modelsDirectory, "retinaface.onnx");
            string arcfacePath = Path.Combine(modelsDirectory, "arcface.onnx");

            try
            {
                var sessionOptions = new SessionOptions();
                sessionOptions.AppendExecutionProvider_CPU(1);

                _retinaFaceSession = new InferenceSession(retinaPath, sessionOptions);
                _arcFaceSession = new InferenceSession(arcfacePath, sessionOptions);

                Console.WriteLine("✅ [AI MOTORU] Pipeline v2 (Alignment + High-Res) başarıyla yüklendi!");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ [AI HATASI] Modeller yüklenemedi: {ex.Message}");
            }
        }

        public async Task<List<int>> IdentifyStudentsInCrowdAsync(int sessionId, IFormFile frame)
        {
            var recognizedStudentIds = new List<int>();

            // Modeller yüklenmediyse direkt çık (Sessiz hatayı önler)
            if (_retinaFaceSession == null || _arcFaceSession == null)
            {
                throw new Exception("Kritik Hata: AI Modelleri RAM'e yüklenemedi. AI_Model klasörünü ve Azure 64-bit ayarını kontrol edin.");
            }

            using var scope = _scopeFactory.CreateScope();
            var _context = scope.ServiceProvider.GetRequiredService<SmartAttendanceDbContext>();

            var session = await _context.AttendanceSessions
                .Include(s => s.RelatedCourses)
                .FirstOrDefaultAsync(s => s.Id == sessionId);

            if (session == null) return recognizedStudentIds;

            var courseIds = session.RelatedCourses.Select(rc => rc.CourseId).ToList();
            var enrolledStudents = await _context.CourseEnrollments
                .Include(ce => ce.Student)
                .Where(ce => courseIds.Contains(ce.CourseId) && !string.IsNullOrEmpty(ce.Student.FaceEncoding))
                .Select(ce => ce.Student).Distinct().ToListAsync();

            if (!enrolledStudents.Any()) return recognizedStudentIds;

            using var memoryStream = new MemoryStream();
            await frame.CopyToAsync(memoryStream);
            using var img = Cv2.ImDecode(memoryStream.ToArray(), ImreadModes.Color);
            if (img.Empty()) return recognizedStudentIds;

            // 1. ADIM: Yüksek Hassasiyetli RetinaFace Taraması
            var retinaInput = PreprocessImageForRetinaFace(img);
            var retinaInputs = new List<NamedOnnxValue> { NamedOnnxValue.CreateFromTensor("input.1", retinaInput) };

            using var retinaResults = _retinaFaceSession.Run(retinaInputs);

            // Landmark verilerini de içeren gelişmiş çıktı çözücü
            var faceList = ExtractDetailedFaces(retinaResults, img.Width, img.Height);

            // Eğer yüz bulunamazsa can simidi olarak resmin tamamını ekle (Landmark boş kalır)
            if (faceList.Count == 0)
            {
                faceList.Add(new FaceData { Rectangle = new Rect(0, 0, img.Width, img.Height), Landmarks = null });
            }

            // 2. ADIM: Kimlik Tanıma (Alignment Dahil)
            foreach (var face in faceList)
            {
                try
                {
                    // "Using atama hatasını" önleyen temiz metot kullanımı
                    using Mat finalFaceToEmbed = PrepareFaceForArcFace(img, face);

                    float[] liveVector = GetArcFaceEmbedding(finalFaceToEmbed);

                    int? bestMatchId = null;
                    double highestSimilarity = 0.26; // Uzak mesafe için optimize edilmiş eşik

                    foreach (var student in enrolledStudents)
                    {
                        var dbVector = JsonSerializer.Deserialize<float[]>(student.FaceEncoding);
                        if (dbVector == null) continue;

                        double similarity = ComputeCosineSimilarity(liveVector, dbVector);

                        if (similarity > highestSimilarity)
                        {
                            highestSimilarity = similarity;
                            bestMatchId = student.Id;
                        }
                    }

                    if (bestMatchId.HasValue && !recognizedStudentIds.Contains(bestMatchId.Value))
                    {
                        recognizedStudentIds.Add(bestMatchId.Value);
                    }
                }
                catch { continue; }
            }

            // Teşhis için: Hiç yüz bulunmadıysa -999, bulundu ama tanınmadıysa -888 dön
            if (recognizedStudentIds.Count == 0)
            {
                if (faceList.Count == 1 && faceList[0].Landmarks == null) recognizedStudentIds.Add(-999);
                else recognizedStudentIds.Add(-888);
            }

            return recognizedStudentIds;
        }

        // =========================================================================================
        // PIPELINE FONKSİYONLARI (ALIGNMENT, PREPROCESS, EMBEDDING)
        // =========================================================================================

        // USING hatasını önleyen yardımcı metot
        private Mat PrepareFaceForArcFace(Mat originalImg, FaceData face)
        {
            using Mat rawFace = new Mat(originalImg, face.Rectangle);

            if (face.Landmarks != null)
            {
                // Yüzü Hizala (Alignment)
                return AlignFace(rawFace, face.Landmarks, face.Rectangle);
            }
            else
            {
                // Hizalama yoksa sadece yeniden boyutlandır
                Mat resizedFace = new Mat();
                Cv2.Resize(rawFace, resizedFace, new Size(112, 112));
                return resizedFace;
            }
        }

        private Mat AlignFace(Mat faceImg, float[] landmarks, Rect rect)
        {
            // RetinaFace'ten gelen 5 landmark noktasını al (Resim koordinatlarına göre)
            float lx = landmarks[0] - rect.X;
            float ly = landmarks[1] - rect.Y;
            float rx = landmarks[2] - rect.X;
            float ry = landmarks[3] - rect.Y;

            // İki göz arasındaki açıyı hesapla
            double dy = ry - ly;
            double dx = rx - lx;
            double angle = Math.Atan2(dy, dx) * 180.0 / Math.PI;

            Point2f center = new Point2f(faceImg.Width / 2, faceImg.Height / 2);
            Mat rotMatrix = Cv2.GetRotationMatrix2D(center, angle, 1.0);

            Mat rotated = new Mat();
            Cv2.WarpAffine(faceImg, rotated, rotMatrix, faceImg.Size());

            Mat finalFace = new Mat();
            Cv2.Resize(rotated, finalFace, new Size(112, 112));

            return finalFace;
        }

        private float[] GetArcFaceEmbedding(Mat faceImg)
        {
            using Mat rgbImg = new Mat();
            Cv2.CvtColor(faceImg, rgbImg, ColorConversionCodes.BGR2RGB);

            var tensor = new DenseTensor<float>(new[] { 1, 3, 112, 112 });
            for (int y = 0; y < 112; y++)
            {
                for (int x = 0; x < 112; x++)
                {
                    Vec3b pixel = rgbImg.At<Vec3b>(y, x);
                    tensor[0, 0, y, x] = (pixel.Item0 - 127.5f) / 127.5f;
                    tensor[0, 1, y, x] = (pixel.Item1 - 127.5f) / 127.5f;
                    tensor[0, 2, y, x] = (pixel.Item2 - 127.5f) / 127.5f;
                }
            }

            var inputs = new List<NamedOnnxValue> { NamedOnnxValue.CreateFromTensor("input.1", tensor) };
            using var results = _arcFaceSession.Run(inputs);
            return results.First().AsEnumerable<float>().ToArray();
        }

        private Tensor<float> PreprocessImageForRetinaFace(Mat img)
        {
            using var resized = new Mat();
            Cv2.Resize(img, resized, new Size(640, 640));

            var tensor = new DenseTensor<float>(new[] { 1, 3, 640, 640 });
            for (int y = 0; y < 640; y++)
            {
                for (int x = 0; x < 640; x++)
                {
                    var pixel = resized.At<Vec3b>(y, x);
                    tensor[0, 0, y, x] = pixel.Item2 - 104f; // B
                    tensor[0, 1, y, x] = pixel.Item1 - 117f; // G
                    tensor[0, 2, y, x] = pixel.Item0 - 123f; // R
                }
            }
            return tensor;
        }

        private List<FaceData> ExtractDetailedFaces(IDisposableReadOnlyCollection<DisposableNamedOnnxValue> results, int originalWidth, int originalHeight)
        {
            var faceList = new List<FaceData>();
            var resultsList = results.ToList();

            var bboxesRaw = resultsList.FirstOrDefault(r => r.AsEnumerable<float>().Count() % 4 == 0)?.AsEnumerable<float>().ToArray();
            var scoresRaw = resultsList.FirstOrDefault(r => r.AsEnumerable<float>().Count() % 4 != 0 && r.AsEnumerable<float>().Count() % 10 != 0)?.AsEnumerable<float>().ToArray();
            var landmarksRaw = resultsList.FirstOrDefault(r => r.AsEnumerable<float>().Count() % 10 == 0)?.AsEnumerable<float>().ToArray();

            if (bboxesRaw == null || scoresRaw == null) return faceList;

            float threshold = 0.35f; // Uzaktakiler için hassas eşik

            for (int i = 0; i < scoresRaw.Length; i++)
            {
                if (scoresRaw[i] > threshold)
                {
                    int x = (int)Math.Clamp(bboxesRaw[i * 4] * originalWidth / 640, 0, originalWidth);
                    int y = (int)Math.Clamp(bboxesRaw[i * 4 + 1] * originalHeight / 640, 0, originalHeight);
                    int w = (int)Math.Clamp((bboxesRaw[i * 4 + 2] - bboxesRaw[i * 4]) * originalWidth / 640, 10, originalWidth - x);
                    int h = (int)Math.Clamp((bboxesRaw[i * 4 + 3] - bboxesRaw[i * 4 + 1]) * originalHeight / 640, 10, originalHeight - y);

                    float[] landmarks = null;
                    if (landmarksRaw != null)
                    {
                        landmarks = new float[10];
                        for (int l = 0; l < 10; l++)
                        {
                            float scale = (l % 2 == 0) ? originalWidth : originalHeight;
                            landmarks[l] = landmarksRaw[i * 10 + l] * scale / 640;
                        }
                    }

                    faceList.Add(new FaceData { Rectangle = new Rect(x, y, w, h), Landmarks = landmarks });
                }
            }
            return faceList;
        }

        private double ComputeCosineSimilarity(float[] vectorA, float[] vectorB)
        {
            double dotProduct = 0.0, normA = 0.0, normB = 0.0;
            for (int i = 0; i < vectorA.Length; i++)
            {
                dotProduct += vectorA[i] * vectorB[i];
                normA += vectorA[i] * vectorA[i];
                normB += vectorB[i] * vectorB[i];
            }
            return (normA == 0 || normB == 0) ? 0 : dotProduct / (Math.Sqrt(normA) * Math.Sqrt(normB));
        }

        public async Task<string> GenerateFaceEncodingAsync(byte[] imageBytes)
        {
            if (_arcFaceSession == null) return null;
            using Mat img = Cv2.ImDecode(imageBytes, ImreadModes.Color);
            if (img.Empty()) return null;

            // Veritabanı Seed işlemi için basit kırpma
            using Mat resizedFace = new Mat();
            Cv2.Resize(img, resizedFace, new Size(112, 112));
            float[] vector = GetArcFaceEmbedding(resizedFace);

            return JsonSerializer.Serialize(vector);
        }
    }
}