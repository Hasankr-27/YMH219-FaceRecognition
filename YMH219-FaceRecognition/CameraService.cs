using OpenCvSharp;

namespace YMH219_FaceRecognition
{
    // Kameradan arka planda sürekli görüntü okuyan sınıf
    public class CameraService
    {
        private VideoCapture? capture;
        private Task? loopTask;
        private volatile bool running;

        public bool IsRunning => running;

        // Her yeni karede tetiklenir (arka plan iş parçacığından). Alan taraf kareyi Dispose etmeli.
        public event Action<Mat>? FrameCaptured;
        public event Action<string>? ErrorOccurred;

        public void Start(int cameraIndex = 0)
        {
            if (running) return;

            capture = new VideoCapture(cameraIndex, VideoCaptureAPIs.DSHOW);
            if (!capture.IsOpened())
            {
                capture.Dispose();
                capture = null;
                throw new InvalidOperationException(
                    "Kamera açılamadı. Kameranın bağlı olduğundan ve başka bir uygulama tarafından kullanılmadığından emin olun.");
            }

            running = true;
            loopTask = Task.Run(CaptureLoop);   // okuma döngüsünü arka planda başlat
        }

        public async Task StopAsync()
        {
            running = false;
            if (loopTask != null)
                await loopTask;   // döngünün bitmesini bekle (pencere donmadan)
            loopTask = null;
        }

        private void CaptureLoop()
        {
            int failCount = 0;

            try
            {
                using var frame = new Mat();
                while (running)
                {
                    if (!capture!.Read(frame) || frame.Empty())
                    {
                        // Uzun süre görüntü gelmezse kamera çıkarılmış olabilir
                        if (++failCount > 100)
                        {
                            running = false;
                            ErrorOccurred?.Invoke("Kameradan görüntü alınamıyor. Kamera bağlantısı kopmuş olabilir.");
                        }
                        Thread.Sleep(10);
                        continue;
                    }

                    failCount = 0;
                    FrameCaptured?.Invoke(frame.Clone());
                    Thread.Sleep(15);
                }
            }
            catch (Exception ex)
            {
                running = false;
                ErrorOccurred?.Invoke(ex.Message);
            }
            finally
            {
                capture?.Release();   // kamerayı serbest bırak
                capture?.Dispose();
                capture = null;
            }
        }
    }
}