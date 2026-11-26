using OpenCvSharp;
using OpenCvSharp.Face;

namespace YMH219_FaceRecognition
{
    // Yüz örneklerini kaydeden, LBPH ile eğiten ve tahmin yapan sınıf
    public class FaceRecognizerService : IDisposable
    {
        private static readonly string FacesDir = Path.Combine(AppContext.BaseDirectory, "Faces");
        private static readonly OpenCvSharp.Size FaceSize = new OpenCvSharp.Size(200, 200);
        private const double UnknownThreshold = 80;   // uzaklık bundan büyükse "Bilinmiyor"

        private LBPHFaceRecognizer? recognizer;
        private readonly Dictionary<int, string> labelNames = new Dictionary<int, string>();
        private readonly object sync = new object();   // kamera ve buton aynı anda kullanmasın

        public bool IsTrained
        {
            get { lock (sync) return recognizer != null; }
        }

        // Kayıtlı kişi isimleri (Faces klasöründeki alt klasörler)
        public List<string> GetLabels()
        {
            if (!Directory.Exists(FacesDir))
                return new List<string>();

            return Directory.GetDirectories(FacesDir)
                            .Select(d => Path.GetFileName(d))
                            .ToList();
        }

        public int GetSampleCount(string label)
        {
            string dir = Path.Combine(FacesDir, label);
            return Directory.Exists(dir) ? Directory.GetFiles(dir, "*.png").Length : 0;
        }

        // Görüntüdeki bir yüzü verilen isimle örnek olarak kaydeder
        public void AddSample(Mat image, Rect face, string label)
        {
            if (string.IsNullOrWhiteSpace(label))
                throw new ArgumentException("Lütfen bir isim girin.");
            if (label.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
                throw new ArgumentException("İsimde kullanılamayan bir karakter var.");

            string dir = Path.Combine(FacesDir, label);
            Directory.CreateDirectory(dir);

            using Mat prepared = PrepareFace(image, face);
            Cv2.ImEncode(".png", prepared, out byte[] bytes);
            File.WriteAllBytes(Path.Combine(dir, $"{DateTime.Now:yyyyMMdd_HHmmss_fff}.png"), bytes);
        }

        // Kayıtlı tüm örneklerle modeli eğitir
        public void Train()
        {
            var images = new List<Mat>();
            var labels = new List<int>();
            var names = new Dictionary<int, string>();

            try
            {
                int id = 0;
                foreach (string dir in Directory.Exists(FacesDir) ? Directory.GetDirectories(FacesDir) : Array.Empty<string>())
                {
                    string[] files = Directory.GetFiles(dir, "*.png");
                    if (files.Length == 0) continue;

                    names[id] = Path.GetFileName(dir);
                    foreach (string file in files)
                    {
                        Mat img = Cv2.ImDecode(File.ReadAllBytes(file), ImreadModes.Grayscale);
                        if (img.Empty()) { img.Dispose(); continue; }
                        images.Add(img);
                        labels.Add(id);
                    }
                    id++;
                }

                if (names.Count < 2)
                    throw new InvalidOperationException("Eğitim için en az 2 farklı kişinin (etiketin) örneği gerekli.");

                var newRecognizer = LBPHFaceRecognizer.Create();
                newRecognizer.Train(images, labels);

                lock (sync)
                {
                    recognizer?.Dispose();
                    recognizer = newRecognizer;
                    labelNames.Clear();
                    foreach (var pair in names)
                        labelNames[pair.Key] = pair.Value;
                }
            }
            finally
            {
                foreach (Mat m in images)
                    m.Dispose();
            }
        }

        // Yüzün kime ait olduğunu ve güven skorunu (%) döndürür
        public (string Label, double Confidence) Predict(Mat image, Rect face)
        {
            lock (sync)
            {
                if (recognizer == null)
                    return ("?", 0);

                using Mat prepared = PrepareFace(image, face);
                recognizer.Predict(prepared, out int id, out double distance);

                double confidence = Math.Clamp(100 - distance, 0, 100);

                if (distance > UnknownThreshold || !labelNames.TryGetValue(id, out string? name))
                    return ("Bilinmiyor", confidence);

                return (name, confidence);
            }
        }

        // Yüzü kes, griye çevir, 200x200 yap, ışığı dengele
        private static Mat PrepareFace(Mat image, Rect face)
        {
            face = face.Intersect(new Rect(0, 0, image.Width, image.Height));   // resim dışına taşmasın

            using var crop = new Mat(image, face);
            var gray = new Mat();
            Cv2.CvtColor(crop, gray, ColorConversionCodes.BGR2GRAY);
            Cv2.Resize(gray, gray, FaceSize);
            Cv2.EqualizeHist(gray, gray);
            return gray;
        }

        public void Dispose()
        {
            lock (sync)
                recognizer?.Dispose();
        }
    }
}