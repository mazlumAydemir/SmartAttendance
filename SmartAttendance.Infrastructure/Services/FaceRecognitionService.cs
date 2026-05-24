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
        // 0: Sol Göz X, 1: Sol Göz Y, 2: Sağ Göz X, 3: Sağ Göz Y
        public float[] Landmarks { get; set; }
        public float Score { get; set; }
        public float Quality { get; set; }
    }

    public class FaceRecognitionService : IFaceRecognitionService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private InferenceSession _yoloFaceSession;
        private InferenceSession _arcFaceSession;
        private Dictionary<int, float[]> _studentEncodingCache = new();
        private DateTime _cacheTime = DateTime.MinValue;
        private const int CACHE_MINUTES = 5;

        // 🔥 UZAK MESAFE İÇİN OPTİMİZE EDİLMİŞ BARAJLAR 🔥
        private const float DETECTION_THRESHOLD = 0.30f; // YOLO'nun silik yüzleri de bulması için 0.30'a indi
        private const float IOU_THRESHOLD = 0.45f;
        private const float MATCH_THRESHOLD = 0.50f;     // ArcFace eşleşme güveni

        private const int MIN_FACE_SIZE_CROWD = 25;      // 4-5 metre uzaklık için min piksel (eski: 80 -> yeni: 25)
        private const int FLEXIBLE_MIN_FACE_SIZE = 40;   // Kayıt esnasında kabul edilecek min boyut

        public FaceRecognitionService(IServiceScopeFactory scopeFactory)
        {
            _scopeFactory = scopeFactory;
            LoadModels();
        }

        private void LoadModels()
        {
            string baseDir = AppContext.BaseDirectory;
            string modelsDirectory = Path.Combine(baseDir, "AI_Model");
            string yoloPath = Path.Combine(modelsDirectory, "yolov8n-face.onnx");
            string arcfacePath = Path.Combine(modelsDirectory, "arcface.onnx");

            Console.WriteLine($"🔍 [AI] Model dizini: {modelsDirectory}");

            if (!File.Exists(yoloPath) || !File.Exists(arcfacePath))
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
                catch (Exception)
                {
                    Console.WriteLine("⚠️ [GPU] CUDA aktif değil. CPU üzerinden devam ediliyor.");
                }

                // Native ONNX Runtime ile model yükleme
                _yoloFaceSession = new InferenceSession(yoloPath, sessionOptions);
                _arcFaceSession = new InferenceSession(arcfacePath, sessionOptions);

                Console.WriteLine("✅ [SUCCESS] YOLOv8-Face ve ArcFace Modelleri Yüklendi!");
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
                    .Where(ce => courseIds.Contains(ce.CourseId) && !string.IsNullOrEmpty(ce.Student.FaceEncoding) && !ce.Student.IsDeleted)
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
                        if (encoding != null && encoding.Length == 512 && IsValidEncoding(encoding))
                        {
                            _studentEncodingCache[student.Id] = encoding;
                            validCount++;
                        }
                    }
                    catch { }
                }

                _cacheTime = DateTime.Now;
                Console.WriteLine($"✅ [CACHE] {validCount}/{students.Count} öğrenci RAM'e hazırlandı");
            }

            return _studentEncodingCache;
        }

        private bool IsValidEncoding(float[] encoding)
        {
            double norm = Math.Sqrt(encoding.Sum(x => (double)x * x));
            return norm > 0.5f && norm < 1.5f;
        }

        public async Task<List<int>> IdentifyStudentsInCrowdAsync(int sessionId, IFormFile frame)
        {
            var recognizedStudentIds = new List<int>();

            if (_yoloFaceSession == null || _arcFaceSession == null)
                return new List<int> { -101 };

            try
            {
                using var memoryStream = new MemoryStream();
                await frame.CopyToAsync(memoryStream);
                using var img = Cv2.ImDecode(memoryStream.ToArray(), ImreadModes.Color);

                if (img.Empty()) return recognizedStudentIds;

                await ProcessImage(sessionId, img, recognizedStudentIds);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ [FATAL] Genel hata: {ex.Message}");
            }

            Console.WriteLine($"✅ [RESULT] {recognizedStudentIds.Count} öğrenci tanındı");
            return recognizedStudentIds;
        }

        private async Task ProcessImage(int sessionId, Mat img, List<int> recognizedStudentIds)
        {
            float scale; int padX; int padY;
            var yoloInput = PreprocessImageForYolo(img, out scale, out padX, out padY);
            var yoloInputs = new List<NamedOnnxValue> { NamedOnnxValue.CreateFromTensor("images", yoloInput) };

            List<FaceData> faceList;
            using (var yoloResults = _yoloFaceSession.Run(yoloInputs))
            {
                faceList = ExtractFacesFromYolo(yoloResults, scale, padX, padY, img.Width, img.Height, MIN_FACE_SIZE_CROWD);
            }

            if (faceList.Count == 0) return;

            var studentEncodings = await GetCachedStudentEncodingsAsync(sessionId);
            if (studentEncodings.Count == 0) return;

            foreach (var face in faceList)
            {
                try
                {
                    using var alignedFace = AlignAndCropFace(img, face);
                    if (alignedFace.Empty()) continue;

                    float[] liveVector = GetArcFaceEmbedding(alignedFace);
                    if (liveVector == null || liveVector.Length == 0) continue;

                    var bestMatch = FindBestMatch(liveVector, studentEncodings);

                    if (bestMatch.score >= MATCH_THRESHOLD)
                    {
                        Console.WriteLine($"   ✅ EŞLEŞTİ! Öğrenci ID: {bestMatch.studentId}, Score: {bestMatch.score:F4}");
                        if (!recognizedStudentIds.Contains(bestMatch.studentId))
                            recognizedStudentIds.Add(bestMatch.studentId);
                    }
                    else
                    {
                        Console.WriteLine($"   ⛔ UNKNOWN (Score: {bestMatch.score:F4} - Baraj: {MATCH_THRESHOLD})");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"   ❌ Yüz işlemede hata: {ex.Message}");
                }
            }
        }

        private (int studentId, double score) FindBestMatch(float[] liveVector, Dictionary<int, float[]> studentEncodings)
        {
            double bestScore = 0;
            int bestId = -1;

            foreach (var kvp in studentEncodings)
            {
                double similarity = ComputeCosineSimilarity(liveVector, kvp.Value);
                if (similarity > bestScore)
                {
                    bestScore = similarity;
                    bestId = kvp.Key;
                }
            }
            return (bestId, bestScore);
        }

        // 🔥 GÖZ BAZLI HİZALAMA, SIKI KIRPMA (TIGHT CROP) VE GÖRÜNTÜ İYİLEŞTİRME 🔥
        private Mat AlignAndCropFace(Mat originalImg, FaceData face)
        {
            Mat workingImg = originalImg;
            Mat rotatedImg = new Mat();

            if (face.Landmarks != null && face.Landmarks.Length >= 4)
            {
                float leftEyeX = face.Landmarks[0];
                float leftEyeY = face.Landmarks[1];
                float rightEyeX = face.Landmarks[2];
                float rightEyeY = face.Landmarks[3];

                double dy = rightEyeY - leftEyeY;
                double dx = rightEyeX - leftEyeX;
                double angle = Math.Atan2(dy, dx) * (180.0 / Math.PI);

                if (Math.Abs(angle) > 2.0 && Math.Abs(angle) < 60.0)
                {
                    Point2f center = new Point2f(face.Rectangle.X + face.Rectangle.Width / 2f, face.Rectangle.Y + face.Rectangle.Height / 2f);
                    using var rotMat = Cv2.GetRotationMatrix2D(center, angle, 1.0);
                    Cv2.WarpAffine(originalImg, rotatedImg, rotMat, originalImg.Size());
                    workingImg = rotatedImg;
                }
            }

            // ✅ SIKI KIRPMA (TIGHT CROP): Margin %5'e düşürüldü.
            int marginX = (int)(face.Rectangle.Width * 0.05);
            int marginY = (int)(face.Rectangle.Height * 0.05);

            int size = Math.Max(face.Rectangle.Width + marginX * 2, face.Rectangle.Height + marginY * 2);
            size = Math.Max(size, 40);

            int startX = face.Rectangle.X + (face.Rectangle.Width / 2) - (size / 2);
            int startY = face.Rectangle.Y + (face.Rectangle.Height / 2) - (size / 2);

            startX = Math.Max(0, startX);
            startY = Math.Max(0, startY);
            int endX = Math.Min(workingImg.Width, startX + size);
            int endY = Math.Min(workingImg.Height, startY + size);

            int finalWidth = endX - startX;
            int finalHeight = endY - startY;

            if (finalWidth <= 0 || finalHeight <= 0) return new Mat();

            Rect squareRect = new Rect(startX, startY, finalWidth, finalHeight);
            using Mat faceRegion = new Mat(workingImg, squareRect);

            Mat aligned = new Mat();

            // 🚀 GELİŞTİRME 1: Lanczos4 algoritması ile en kaliteli şekilde büyüt (Upscale)
            Cv2.Resize(faceRegion, aligned, new Size(112, 112), 0, 0, InterpolationFlags.Lanczos4);

            // 🚀 GELİŞTİRME 2: Uzak mesafeden gelen bulanık yüzleri keskinleştir (Unsharp Mask)
            using Mat blurred = new Mat();
            Cv2.GaussianBlur(aligned, blurred, new Size(0, 0), 3);
            Cv2.AddWeighted(aligned, 1.5, blurred, -0.5, 0, aligned);

            if (!rotatedImg.Empty()) rotatedImg.Dispose();

            return aligned;
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
                    tensor[0, 0, y, x] = (pixel.Item0 / 255.0f - 0.5f) / 0.5f;
                    tensor[0, 1, y, x] = (pixel.Item1 / 255.0f - 0.5f) / 0.5f;
                    tensor[0, 2, y, x] = (pixel.Item2 / 255.0f - 0.5f) / 0.5f;
                }
            }

            using var results = _arcFaceSession.Run(new List<NamedOnnxValue>
            {
                NamedOnnxValue.CreateFromTensor("input.1", tensor)
            });

            var embedding = results.First().AsEnumerable<float>().ToArray();

            float norm = (float)Math.Sqrt(embedding.Sum(x => (double)x * x));
            if (norm > 0)
            {
                for (int i = 0; i < embedding.Length; i++)
                    embedding[i] /= norm;
            }
            return embedding;
        }

        private Tensor<float> PreprocessImageForYolo(Mat img, out float scale, out int padX, out int padY)
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

            using var rgbPadded = new Mat();
            Cv2.CvtColor(padded, rgbPadded, ColorConversionCodes.BGR2RGB);

            var tensor = new DenseTensor<float>(new[] { 1, 3, 640, 640 });

            for (int y = 0; y < 640; y++)
            {
                for (int x = 0; x < 640; x++)
                {
                    var pixel = rgbPadded.At<Vec3b>(y, x);
                    tensor[0, 0, y, x] = pixel.Item0 / 255.0f;
                    tensor[0, 1, y, x] = pixel.Item1 / 255.0f;
                    tensor[0, 2, y, x] = pixel.Item2 / 255.0f;
                }
            }

            return tensor;
        }

        // 🔥 DİNAMİK YOLO PARSER: Single Output modeline uygun (Sürüm 3)
        private List<FaceData> ExtractFacesFromYolo(IDisposableReadOnlyCollection<DisposableNamedOnnxValue> results,
            float scale, int padX, int padY, int originalWidth, int originalHeight, int minFaceSize)
        {
            var rawFaceList = new List<FaceData>();
            var tensor = results.First().AsTensor<float>();
            var dims = tensor.Dimensions;
            var rawData = tensor.ToArray();

            if (dims.Length < 3) return rawFaceList;

            int numChannels = dims[1];
            int numBoxes = dims[2];
            bool isTransposed = false;

            if (dims[1] > dims[2])
            {
                numBoxes = dims[1];
                numChannels = dims[2];
                isTransposed = true;
            }

            int scoreIdx = 4;
            bool hasLandmarks = numChannels >= 15;

            int kptStep = hasLandmarks ? (numChannels - 5) / 5 : 0;

            for (int i = 0; i < numBoxes; i++)
            {
                float conf = isTransposed
                    ? rawData[i * numChannels + scoreIdx]
                    : rawData[scoreIdx * numBoxes + i];

                if (conf > DETECTION_THRESHOLD)
                {
                    float cx = isTransposed ? rawData[i * numChannels + 0] : rawData[0 * numBoxes + i];
                    float cy = isTransposed ? rawData[i * numChannels + 1] : rawData[1 * numBoxes + i];
                    float w = isTransposed ? rawData[i * numChannels + 2] : rawData[2 * numBoxes + i];
                    float h = isTransposed ? rawData[i * numChannels + 3] : rawData[3 * numBoxes + i];

                    w = Math.Max(w, 1);
                    h = Math.Max(h, 1);

                    int width = (int)(w / scale);
                    int height = (int)(h / scale);
                    int x1 = Math.Max(0, (int)((cx - w / 2 - padX) / scale));
                    int y1 = Math.Max(0, (int)((cy - h / 2 - padY) / scale));

                    width = Math.Min(originalWidth - x1, width);
                    height = Math.Min(originalHeight - y1, height);

                    if (width >= minFaceSize && height >= minFaceSize)
                    {
                        float[] landmarksArray = null;

                        if (hasLandmarks && kptStep > 0)
                        {
                            int le_x_idx = 5;
                            int le_y_idx = 6;
                            int re_x_idx = 5 + kptStep;
                            int re_y_idx = 5 + kptStep + 1;

                            float le_x = isTransposed ? rawData[i * numChannels + le_x_idx] : rawData[le_x_idx * numBoxes + i];
                            float le_y = isTransposed ? rawData[i * numChannels + le_y_idx] : rawData[le_y_idx * numBoxes + i];
                            float re_x = isTransposed ? rawData[i * numChannels + re_x_idx] : rawData[re_x_idx * numBoxes + i];
                            float re_y = isTransposed ? rawData[i * numChannels + re_y_idx] : rawData[re_y_idx * numBoxes + i];

                            le_x = (le_x - padX) / scale;
                            le_y = (le_y - padY) / scale;
                            re_x = (re_x - padX) / scale;
                            re_y = (re_y - padY) / scale;

                            landmarksArray = new float[] { le_x, le_y, re_x, re_y };
                        }

                        rawFaceList.Add(new FaceData
                        {
                            Rectangle = new Rect(x1, y1, width, height),
                            Landmarks = landmarksArray,
                            Score = conf,
                            Quality = conf
                        });
                    }
                }
            }

            return ApplyNMS(rawFaceList, IOU_THRESHOLD);
        }

        private List<FaceData> ApplyNMS(List<FaceData> faces, float iouThreshold)
        {
            var result = new List<FaceData>();
            faces = faces.OrderByDescending(f => f.Score).ToList();

            while (faces.Count > 0)
            {
                var bestFace = faces[0];
                result.Add(bestFace);
                faces.RemoveAt(0);

                for (int i = faces.Count - 1; i >= 0; i--)
                {
                    if (CalculateIoU(bestFace.Rectangle, faces[i].Rectangle) > iouThreshold)
                    {
                        faces.RemoveAt(i);
                    }
                }
            }
            return result;
        }

        private float CalculateIoU(Rect boxA, Rect boxB)
        {
            int xA = Math.Max(boxA.X, boxB.X);
            int yA = Math.Max(boxA.Y, boxB.Y);
            int xB = Math.Min(boxA.X + boxA.Width, boxB.X + boxB.Width);
            int yB = Math.Min(boxA.Y + boxA.Height, boxB.Y + boxB.Height);

            int interArea = Math.Max(0, xB - xA) * Math.Max(0, yB - yA);
            int boxAArea = boxA.Width * boxA.Height;
            int boxBArea = boxB.Width * boxB.Height;

            float iou = (float)interArea / (float)(boxAArea + boxBArea - interArea);
            return iou;
        }

        public async Task<string> GenerateFaceEncodingAsync(byte[] imageBytes)
        {
            if (_arcFaceSession == null || _yoloFaceSession == null) return null;

            try
            {
                using Mat img = Cv2.ImDecode(imageBytes, ImreadModes.Color);
                if (img.Empty()) return null;

                Console.WriteLine($"📸 [REG] Kayıt resmi algılandı: {img.Width}x{img.Height}");

                using Mat workingImg = new Mat();
                if (img.Width > 1280 || img.Height > 1280)
                {
                    float ratio = Math.Min(1280f / img.Width, 1280f / img.Height);
                    Cv2.Resize(img, workingImg, new Size((int)(img.Width * ratio), (int)(img.Height * ratio)));
                }
                else
                {
                    img.CopyTo(workingImg);
                }

                float scale; int padX; int padY;
                var yoloInput = PreprocessImageForYolo(workingImg, out scale, out padX, out padY);
                var yoloInputs = new List<NamedOnnxValue> { NamedOnnxValue.CreateFromTensor("images", yoloInput) };

                List<FaceData> faceList;
                using (var yoloResults = _yoloFaceSession.Run(yoloInputs))
                {
                    faceList = ExtractFacesFromYolo(yoloResults, scale, padX, padY, workingImg.Width, workingImg.Height, FLEXIBLE_MIN_FACE_SIZE);
                }

                if (faceList.Count == 0)
                {
                    Console.WriteLine("⚠️ [REG] Hiçbir yüz bulunamadı! Baraj değerlerini kontrol edin.");
                    return null;
                }

                var bestFace = faceList.OrderByDescending(f => f.Quality).First();
                Console.WriteLine($"✅ [REG] Seçilen Yüz: {bestFace.Rectangle.Width}x{bestFace.Rectangle.Height} (Güven: {bestFace.Score:F4})");

                using var alignedFace = AlignAndCropFace(workingImg, bestFace);
                if (alignedFace.Empty())
                {
                    Console.WriteLine("⚠️ [REG] Yüz hizalama başarısız!");
                    return null;
                }

                float[] vector = GetArcFaceEmbedding(alignedFace);
                if (vector == null || vector.Length != 512)
                {
                    Console.WriteLine("⚠️ [REG] ArcFace embedding başarısız!");
                    return null;
                }

                string encoded = JsonSerializer.Serialize(vector);
                Console.WriteLine($"✅ [REG] Başarı! Face Encoding oluşturuldu (512 boyut)");

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
                dotProduct += (double)vectorA[i] * vectorB[i];
                normA += (double)vectorA[i] * vectorA[i];
                normB += (double)vectorB[i] * vectorB[i];
            }

            if (normA == 0 || normB == 0) return 0;
            return dotProduct / (Math.Sqrt(normA) * Math.Sqrt(normB));
        }

        public async Task<string> GenerateFaceEncodingFromMultipleAsync(List<byte[]> imageBytesList)
        {
            var validEncodings = new List<float[]>();

            foreach (var imageBytes in imageBytesList.Take(5))
            {
                var encoded = await GenerateFaceEncodingAsync(imageBytes);
                if (!string.IsNullOrEmpty(encoded))
                {
                    try
                    {
                        var encoding = JsonSerializer.Deserialize<float[]>(encoded);
                        if (encoding != null && IsValidEncoding(encoding))
                        {
                            validEncodings.Add(encoding);
                        }
                    }
                    catch { }
                }
            }

            if (validEncodings.Count >= 2)
            {
                var avgEncoding = new float[512];
                for (int i = 0; i < 512; i++)
                {
                    avgEncoding[i] = validEncodings.Average(e => e[i]);
                }

                float norm = (float)Math.Sqrt(avgEncoding.Sum(x => (double)x * x));
                if (norm > 0)
                {
                    for (int i = 0; i < 512; i++)
                        avgEncoding[i] /= norm;
                }

                return JsonSerializer.Serialize(avgEncoding);
            }

            return validEncodings.Count == 1 ? JsonSerializer.Serialize(validEncodings[0]) : null;
        }
    }
}