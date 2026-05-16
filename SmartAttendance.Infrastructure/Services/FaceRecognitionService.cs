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
        private Dictionary<int, float[]> _studentEncodingCache = new();
        private DateTime _cacheTime = DateTime.MinValue;
        private const int CACHE_MINUTES = 5;

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

            Console.WriteLine($"🔍 [AI] Model dizini: {modelsDirectory}");

            if (!File.Exists(retinaPath) || !File.Exists(arcfacePath))
            {
                Console.WriteLine($"❌ [ERROR] Model dosyaları eksik!");
                return;
            }

            try
            {
                var sessionOptions = new SessionOptions();
                try
                {
                    sessionOptions.AppendExecutionProvider_CUDA(0);
                    Console.WriteLine("✅ [GPU] CUDA GPU acceleration aktif");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"⚠️ [GPU] CUDA aktivasyon başarısız: {ex.GetType().Name}");
                }

                _retinaFaceSession = new InferenceSession(retinaPath, sessionOptions);
                _arcFaceSession = new InferenceSession(arcfacePath, sessionOptions);

                Console.WriteLine("✅ [SUCCESS] Modeller yüklendi - Yüz tanıma hazır");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ [FATAL] Model yükleme hatası: {ex.Message}");
            }
        }

        private async Task<Dictionary<int, float[]>> GetCachedStudentEncodingsAsync(int sessionId)
        {
            if (_studentEncodingCache.Count == 0 || (DateTime.Now - _cacheTime).TotalMinutes > CACHE_MINUTES)
            {
                using var scope = _scopeFactory.CreateScope();
                var _context = scope.ServiceProvider.GetRequiredService<SmartAttendanceDbContext>();

                var session = await _context.AttendanceSessions
                    .Include(s => s.RelatedCourses)
                    .FirstOrDefaultAsync(s => s.Id == sessionId);

                if (session == null) return new Dictionary<int, float[]>();

                var courseIds = session.RelatedCourses.Select(rc => rc.CourseId).ToList();
                var students = await _context.CourseEnrollments
                    .Include(ce => ce.Student)
                    .Where(ce => courseIds.Contains(ce.CourseId) && !string.IsNullOrEmpty(ce.Student.FaceEncoding))
                    .Select(ce => ce.Student)
                    .Distinct()
                    .ToListAsync();

                _studentEncodingCache.Clear();
                int validCount = 0;

                foreach (var student in students)
                {
                    try
                    {
                        var encoding = JsonSerializer.Deserialize<float[]>(student.FaceEncoding);
                        if (encoding != null && encoding.Length == 512)
                        {
                            _studentEncodingCache[student.Id] = encoding;
                            validCount++;
                        }
                    }
                    catch
                    {
                        Console.WriteLine($"⚠️ [CACHE] Öğrenci {student.Id} encoding hatalı");
                    }
                }

                _cacheTime = DateTime.Now;
                Console.WriteLine($"✅ [CACHE] {validCount}/{students.Count} öğrenci hazırlandı");
            }

            return _studentEncodingCache;
        }

        public async Task<List<int>> IdentifyStudentsInCrowdAsync(int sessionId, IFormFile frame)
        {
            var recognizedStudentIds = new List<int>();

            if (_retinaFaceSession == null || _arcFaceSession == null) return new List<int> { -101 };

            try
            {
                using var memoryStream = new MemoryStream();
                await frame.CopyToAsync(memoryStream);
                using var img = Cv2.ImDecode(memoryStream.ToArray(), ImreadModes.Color);

                if (img.Empty()) return recognizedStudentIds;

                Console.WriteLine($"📸 [IMG] Resim: {img.Width}x{img.Height}");

                if (img.Width < 200 || img.Height < 200)
                {
                    Console.WriteLine("⚠️ [IMG] Resim çok küçük, büyütülüyor...");
                    using Mat enlarged = new Mat();
                    Cv2.Resize(img, enlarged, new Size(400, 400));
                    await ProcessImage(sessionId, enlarged, recognizedStudentIds);
                    return recognizedStudentIds;
                }

                await ProcessImage(sessionId, img, recognizedStudentIds);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ [FATAL] Genel hata: {ex.Message}");
            }

            Console.WriteLine($"\n✅ [RESULT] {recognizedStudentIds.Count} öğrenci tanındı");
            return recognizedStudentIds;
        }

        private async Task ProcessImage(int sessionId, Mat img, List<int> recognizedStudentIds)
        {
            Console.WriteLine("🔍 [DETECT] RetinaFace başlatılıyor...");

            float scale; int padX; int padY;
            var retinaInput = PreprocessImageForRetinaFace(img, out scale, out padX, out padY);

            var retinaInputs = new List<NamedOnnxValue> { NamedOnnxValue.CreateFromTensor("input.1", retinaInput) };

            List<FaceData> faceList;
            using (var retinaResults = _retinaFaceSession.Run(retinaInputs))
            {
                faceList = ExtractDetailedFaces(retinaResults, scale, padX, padY, img.Width, img.Height);
            }

            if (faceList.Count == 0)
            {
                Console.WriteLine("⚠️ [DETECT] Yüz bulunamadı, merkez krop kullanılıyor");
                int cropSize = (int)(Math.Min(img.Width, img.Height) * 0.85);
                int startX = (img.Width - cropSize) / 2;
                int startY = (img.Height - cropSize) / 2;

                if (startX >= 0 && startY >= 0 && cropSize > 50)
                {
                    faceList.Add(new FaceData { Rectangle = new Rect(startX, startY, cropSize, cropSize) });
                    Console.WriteLine($"   📍 Merkez Krop: {startX}, {startY} ({cropSize}x{cropSize})");
                }
            }
            else
            {
                Console.WriteLine($"✅ [DETECT] {faceList.Count} yüz bulundu");
            }

            var studentEncodings = await GetCachedStudentEncodingsAsync(sessionId);
            if (studentEncodings.Count == 0) return;

            int faceIndex = 0;
            foreach (var face in faceList)
            {
                faceIndex++;
                Console.WriteLine($"\n📌 [FACE {faceIndex}] Yüz işleniyor...");

                try
                {
                    using Mat croppedFace = PrepareFaceForArcFace(img, face);
                    if (croppedFace.Empty()) continue;

                    float[] liveVector = GetArcFaceEmbedding(croppedFace);
                    if (liveVector == null || liveVector.Length == 0) continue;

                    var similarities = new List<(int studentId, double score)>();
                    foreach (var kvp in studentEncodings)
                    {
                        similarities.Add((kvp.Key, ComputeCosineSimilarity(liveVector, kvp.Value)));
                    }

                    var best = similarities.OrderByDescending(x => x.score).FirstOrDefault();

                    // Threshold: 0.40
                    if (best.score > 0.40)
                    {
                        Console.WriteLine($"   ✅ MATCH! Öğrenci: {best.studentId}, Score: {best.score:F4}");
                        if (!recognizedStudentIds.Contains(best.studentId)) recognizedStudentIds.Add(best.studentId);
                    }
                    else
                    {
                        Console.WriteLine($"   ⚠️ Eşleşme yetersiz. En iyi: {best.score:F4} (Eşik: 0.40)");
                        foreach (var match in similarities.OrderByDescending(x => x.score).Take(2))
                            Console.WriteLine($"      - Öğrenci {match.studentId}: {match.score:F4}");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"   ❌ Yüz işlemede hata: {ex.Message}");
                }
            }
        }

        private Mat PrepareFaceForArcFace(Mat originalImg, FaceData face)
        {
            Rect r = face.Rectangle;
            r.X = Math.Max(0, Math.Min(r.X, originalImg.Width - 1));
            r.Y = Math.Max(0, Math.Min(r.Y, originalImg.Height - 1));
            r.Width = Math.Min(originalImg.Width - r.X, Math.Max(1, r.Width));
            r.Height = Math.Min(originalImg.Height - r.Y, Math.Max(1, r.Height));

            if (r.Width <= 0 || r.Height <= 0) return new Mat();

            Mat faceRegion = new Mat(originalImg, r);
            Mat resized = new Mat();
            Cv2.Resize(faceRegion, resized, new Size(112, 112), 0, 0, InterpolationFlags.Linear);
            faceRegion.Dispose();
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

            using var results = _arcFaceSession.Run(new List<NamedOnnxValue> { NamedOnnxValue.CreateFromTensor("input.1", tensor) });
            return results.First().AsEnumerable<float>().ToArray();
        }

        // ✅ DÜZELTME: Görüntü bozulmasını önlemek için "Letterbox" (Siyah bantlı) boyutlandırma
        private Tensor<float> PreprocessImageForRetinaFace(Mat img, out float scale, out int padX, out int padY)
        {
            scale = Math.Min(640f / img.Width, 640f / img.Height);
            int newW = (int)(img.Width * scale);
            int newH = (int)(img.Height * scale);

            padX = (640 - newW) / 2;
            padY = (640 - newH) / 2;

            using var resized = new Mat();
            Cv2.Resize(img, resized, new Size(newW, newH));

            using var padded = new Mat(new Size(640, 640), MatType.CV_8UC3, new Scalar(0, 0, 0));
            var roi = new Rect(padX, padY, newW, newH);
            resized.CopyTo(new Mat(padded, roi));

            var tensor = new DenseTensor<float>(new[] { 1, 3, 640, 640 });
            for (int y = 0; y < 640; y++)
            {
                for (int x = 0; x < 640; x++)
                {
                    var pixel = padded.At<Vec3b>(y, x);
                    tensor[0, 0, y, x] = pixel.Item0 - 104f;
                    tensor[0, 1, y, x] = pixel.Item1 - 117f;
                    tensor[0, 2, y, x] = pixel.Item2 - 123f;
                }
            }
            return tensor;
        }

        // ✅ DÜZELTME: Farklı ONNX Modellerine Uyumlu Dinamik Tensor Okuma
        private List<FaceData> ExtractDetailedFaces(IDisposableReadOnlyCollection<DisposableNamedOnnxValue> results, float scale, int padX, int padY, int originalWidth, int originalHeight)
        {
            var faceList = new List<FaceData>();
            float[] bboxesRaw = null;
            float[] scoresRaw = null;

            foreach (var r in results)
            {
                var dims = r.AsTensor<float>().Dimensions;
                if (dims.Length == 3 && dims[2] == 4) bboxesRaw = r.AsEnumerable<float>().ToArray();
                else if (dims.Length == 3 && dims[2] == 2)
                {
                    var raw = r.AsEnumerable<float>().ToArray();
                    scoresRaw = new float[raw.Length / 2];
                    for (int j = 0; j < scoresRaw.Length; j++) scoresRaw[j] = raw[j * 2 + 1]; // Sadece yüz (Face) olasılığını al
                }
                else if (dims.Length == 3 && dims[2] == 1) scoresRaw = r.AsEnumerable<float>().ToArray();
                else if (dims.Length == 2 && dims[1] == 4) bboxesRaw = r.AsEnumerable<float>().ToArray();
                else if (dims.Length == 2 && dims[1] == 1) scoresRaw = r.AsEnumerable<float>().ToArray();
            }

            if (bboxesRaw == null || scoresRaw == null)
            {
                Console.WriteLine("⚠️ [DETECT] ONNX Tensor okunamadı!");
                return faceList;
            }

            float threshold = 0.3f;
            Console.WriteLine($"🔍 [DETECT] RetinaFace tespit edilen en yüksek olasılık (Max Score): {(scoresRaw.Length > 0 ? scoresRaw.Max() : 0):F4}");

            for (int i = 0; i < scoresRaw.Length && faceList.Count < 10; i++)
            {
                if (scoresRaw[i] > threshold)
                {
                    int x = (int)((bboxesRaw[i * 4] - padX) / scale);
                    int y = (int)((bboxesRaw[i * 4 + 1] - padY) / scale);
                    int x2 = (int)((bboxesRaw[i * 4 + 2] - padX) / scale);
                    int y2 = (int)((bboxesRaw[i * 4 + 3] - padY) / scale);

                    x = Math.Max(0, x); y = Math.Max(0, y);
                    x2 = Math.Min(originalWidth, x2); y2 = Math.Min(originalHeight, y2);

                    if (x2 > x && y2 > y)
                    {
                        faceList.Add(new FaceData { Rectangle = new Rect(x, y, x2 - x, y2 - y) });
                    }
                }
            }
            return faceList;
        }

        public async Task<string> GenerateFaceEncodingAsync(byte[] imageBytes)
        {
            if (_arcFaceSession == null || _retinaFaceSession == null) return null;

            try
            {
                using Mat img = Cv2.ImDecode(imageBytes, ImreadModes.Color);
                if (img.Empty()) return null;

                Console.WriteLine($"📸 [REG] Kayıt resmi: {img.Width}x{img.Height}");

                Mat workingImg = img;
                if (img.Width < 200 || img.Height < 200)
                {
                    workingImg = new Mat();
                    Cv2.Resize(img, workingImg, new Size(400, 400));
                }
                else if (img.Width > 1280 || img.Height > 960)
                {
                    workingImg = new Mat();
                    Cv2.Resize(img, workingImg, new Size(640, 480));
                }

                float scale; int padX; int padY;
                var retinaInput = PreprocessImageForRetinaFace(workingImg, out scale, out padX, out padY);
                var retinaInputs = new List<NamedOnnxValue> { NamedOnnxValue.CreateFromTensor("input.1", retinaInput) };

                List<FaceData> faceList;
                using (var retinaResults = _retinaFaceSession.Run(retinaInputs))
                {
                    faceList = ExtractDetailedFaces(retinaResults, scale, padX, padY, workingImg.Width, workingImg.Height);
                }

                Mat faceToEmbed;
                if (faceList.Count > 0)
                {
                    Console.WriteLine($"✅ [REG] Yüz tespit edildi");
                    faceToEmbed = PrepareFaceForArcFace(workingImg, faceList[0]);
                }
                else
                {
                    Console.WriteLine("⚠️ [REG] Yüz bulunamadı, merkez krop kullanılıyor");
                    int cropSize = (int)(Math.Min(workingImg.Width, workingImg.Height) * 0.85);
                    int startX = (workingImg.Width - cropSize) / 2;
                    int startY = (workingImg.Height - cropSize) / 2;

                    if (startX >= 0 && startY >= 0 && cropSize > 50)
                    {
                        faceToEmbed = PrepareFaceForArcFace(workingImg, new FaceData { Rectangle = new Rect(startX, startY, cropSize, cropSize) });
                    }
                    else
                    {
                        faceToEmbed = new Mat();
                        Cv2.Resize(workingImg, faceToEmbed, new Size(112, 112));
                    }
                }

                float[] vector = GetArcFaceEmbedding(faceToEmbed);
                string encoded = JsonSerializer.Serialize(vector);

                if (workingImg != img) workingImg.Dispose();
                faceToEmbed.Dispose();

                Console.WriteLine($"✅ [REG] Encoding başarıyla oluşturuldu ({vector.Length} boyut)");
                return encoded;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ [REG] Hata: {ex.Message}");
                return null;
            }
        }

        private double ComputeCosineSimilarity(float[] vectorA, float[] vectorB)
        {
            if (vectorA.Length != vectorB.Length) return 0;

            double dotProduct = 0, normA = 0, normB = 0;
            for (int i = 0; i < vectorA.Length; i++)
            {
                dotProduct += vectorA[i] * vectorB[i];
                normA += vectorA[i] * vectorA[i];
                normB += vectorB[i] * vectorB[i];
            }

            if (normA == 0 || normB == 0) return 0;
            return dotProduct / (Math.Sqrt(normA) * Math.Sqrt(normB));
        }
    }
}