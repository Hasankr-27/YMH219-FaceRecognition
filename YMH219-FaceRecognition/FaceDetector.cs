using OpenCvSharp;

namespace YMH219_FaceRecognition
{
    // Görüntüdeki yüzleri Haar Cascade ile bulan sınıf
    public class FaceDetector : IDisposable
    {
        private static readonly string CascadePath =
            Path.Combine(AppContext.BaseDirectory, "Data", "haarcascade_frontalface_default.xml");

        private readonly CascadeClassifier cascade;

        public FaceDetector()
        {
            if (!File.Exists(CascadePath))
                throw new FileNotFoundException("Yüz algılama dosyası bulunamadı:\n" + CascadePath);

            cascade = new CascadeClassifier(CascadePath);
        }

        // Görüntüdeki yüzlerin konumlarını (dikdörtgen) döndürür
        public Rect[] Detect(Mat image)
        {
            using var gray = new Mat();
            Cv2.CvtColor(image, gray, ColorConversionCodes.BGR2GRAY);   // renkli → gri
            Cv2.EqualizeHist(gray, gray);                               // ışığı dengele

            return cascade.DetectMultiScale(
                gray,
                scaleFactor: 1.1,      // her taramada resmi %10 küçült
                minNeighbors: 5,       // yanlış algılamaları azaltır
                flags: HaarDetectionTypes.ScaleImage,
                minSize: new OpenCvSharp.Size(60, 60));   // 60 pikselden küçük yüzleri yok say
        }

        public void Dispose()
        {
            cascade.Dispose();
        }
    }
}