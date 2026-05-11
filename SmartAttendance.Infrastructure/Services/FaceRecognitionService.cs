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
    public class FaceData
    {
        public Rect Rectangle { get; set; }
        public float[] Landmarks { get; set; }
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
            string baseDir = AppContext.BaseDirectory;
            string modelsDirectory = Path.Combine(baseDir, "AI_Model");
            string retinaPath = Path.Combine(modelsDirectory, "retinaface.onnx");
            string arcfacePath = Path.Combine(modelsDirectory, "arcface.onnx");

            Console.WriteLine($"🔍 [AI TEST] Modeller aranıyor: {modelsDirectory}");

            if (!File.Exists(retinaPath) || !File.Exists(arcfacePath))
            {
                string error = $"❌ [AI HATASI] Model dosyaları bulunamadı! Yol: {retinaPath}";
                Console.WriteLine(error);
                return;
            }

            try
            {
                var sessionOptions = new SessionOptions();
                _retinaFaceSession = new InferenceSession(retinaPath, sessionOptions);
                _arcFaceSession = new InferenceSession(arcfacePath, sessionOptions);
                Console.WriteLine("✅ [AI BAŞARI] RetinaFace ve ArcFace modelleri RAM'e yüklendi.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ [AI KRİTİK HATA] Modeller yüklenirken hata oluştu: {ex.Message}");
            }
        }

        public async Task<List<int>> IdentifyStudentsInCrowdAsync(int sessionId, IFormFile frame)
        {
            var recognizedStudentIds = new List<int>();

            if (_retinaFaceSession == null || _arcFaceSession == null) return new List<int> { -101 };

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

            // 1. RetinaFace ile Yüz Tespiti
            var retinaInput = PreprocessImageForRetinaFace(img);
            var retinaInputs = new List<NamedOnnxValue> { NamedOnnxValue.CreateFromTensor("input.1", retinaInput) };
            using var retinaResults = _retinaFaceSession.Run(retinaInputs);
            var faceList = ExtractDetailedFaces(retinaResults, img.Width, img.Height);

            // 🔥 KRİTİK DÜZELTME 1: React zaten kırpılmış yüz atıyorsa RetinaFace hiçbir şey bulamayabilir.
            // Bu durumda resmin kendisinin zaten bir "Yüz" olduğunu varsayıp devam ediyoruz!
            if (faceList.Count == 0)
            {
                faceList.Add(new FaceData { Rectangle = new Rect(0, 0, img.Width, img.Height) });
            }

            // 2. ArcFace ile Karşılaştırma
            foreach (var face in faceList)
            {
                try
                {
                    using Mat finalFaceToEmbed = PrepareFaceForArcFace(img, face);
                    float[] liveVector = GetArcFaceEmbedding(finalFaceToEmbed);

                    int? bestMatchId = null;
                    double highestSimilarity = 0.45; // Benim denemelerimde 0.45 civarı iyi sonuç veriyor, çok düşük tutarsak yanlış eşleşmeler olabilir.

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
                catch (Exception ex)
                {
                    Console.WriteLine($"⚠️ [AI] Yüz işlenirken hata: {ex.Message}");
                    continue;
                }
            }

            return recognizedStudentIds;
        }

        private Mat PrepareFaceForArcFace(Mat originalImg, FaceData face)
        {
            Mat faceRegion = new Mat(originalImg, face.Rectangle);
            Mat resized = new Mat();
            Cv2.Resize(faceRegion, resized, new Size(112, 112));
            return resized;
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
                    tensor[0, 0, y, x] = pixel.Item2 - 104f;
                    tensor[0, 1, y, x] = pixel.Item1 - 117f;
                    tensor[0, 2, y, x] = pixel.Item0 - 123f;
                }
            }
            return tensor;
        }

        private List<FaceData> ExtractDetailedFaces(IDisposableReadOnlyCollection<DisposableNamedOnnxValue> results, int originalWidth, int originalHeight)
        {
            var faceList = new List<FaceData>();
            var resultsList = results.ToList();

            var bboxesRaw = resultsList.FirstOrDefault(r => r.Name == "face_bboxes")?.AsEnumerable<float>().ToArray()
                           ?? resultsList[0].AsEnumerable<float>().ToArray();
            var scoresRaw = resultsList.FirstOrDefault(r => r.Name == "face_scores")?.AsEnumerable<float>().ToArray()
                           ?? resultsList[1].AsEnumerable<float>().ToArray();

            float threshold = 0.25f;

            for (int i = 0; i < scoresRaw.Length; i++)
            {
                if (scoresRaw[i] > threshold)
                {
                    int x = (int)(bboxesRaw[i * 4] * originalWidth / 640);
                    int y = (int)(bboxesRaw[i * 4 + 1] * originalHeight / 640);
                    int x2 = (int)(bboxesRaw[i * 4 + 2] * originalWidth / 640);
                    int y2 = (int)(bboxesRaw[i * 4 + 3] * originalHeight / 640);

                    // Sınırları aşmayı engelle
                    x = Math.Max(0, x);
                    y = Math.Max(0, y);
                    x2 = Math.Min(originalWidth, x2);
                    y2 = Math.Min(originalHeight, y2);

                    if (x2 > x && y2 > y)
                    {
                        faceList.Add(new FaceData { Rectangle = new Rect(x, y, x2 - x, y2 - y) });
                    }
                }
            }
            return faceList;
        }

        // 🔥 KRİTİK DÜZELTME 2: Öğrencileri sisteme kaydederken YÜZÜ KIRPIP kaydet.
        public async Task<string> GenerateFaceEncodingAsync(byte[] imageBytes)
        {
            if (_arcFaceSession == null || _retinaFaceSession == null) return null;

            using Mat img = Cv2.ImDecode(imageBytes, ImreadModes.Color);
            if (img.Empty()) return null;

            // Önce fotoğraftaki yüzü bulalım
            var retinaInput = PreprocessImageForRetinaFace(img);
            var retinaInputs = new List<NamedOnnxValue> { NamedOnnxValue.CreateFromTensor("input.1", retinaInput) };
            using var retinaResults = _retinaFaceSession.Run(retinaInputs);
            var faceList = ExtractDetailedFaces(retinaResults, img.Width, img.Height);

            Mat faceToEmbed;
            if (faceList.Count > 0)
            {
                // Yüz bulunduysa, sadece yüzü kes (ArcFace'in istediği gibi)
                faceToEmbed = PrepareFaceForArcFace(img, faceList[0]);
            }
            else
            {
                // Çok nadir durumlarda yüz bulamazsa orijinal resmi 112x112'ye sığdır
                faceToEmbed = new Mat();
                Cv2.Resize(img, faceToEmbed, new Size(112, 112));
            }

            float[] vector = GetArcFaceEmbedding(faceToEmbed);
            return JsonSerializer.Serialize(vector);
        }
        private double ComputeCosineSimilarity(float[] vectorA, float[] vectorB)
        {
            double dotProduct = 0, normA = 0, normB = 0;
            for (int i = 0; i < vectorA.Length; i++)
            {
                dotProduct += vectorA[i] * vectorB[i];
                normA += vectorA[i] * vectorA[i];
                normB += vectorB[i] * vectorB[i];
            }
            return (normA == 0 || normB == 0) ? 0 : dotProduct / (Math.Sqrt(normA) * Math.Sqrt(normB));
        }
    }
}