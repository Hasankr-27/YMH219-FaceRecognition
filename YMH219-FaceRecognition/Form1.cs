using OpenCvSharp;
using OpenCvSharp.Extensions;

namespace YMH219_FaceRecognition
{
    public partial class Form1 : Form
    {
        private FaceDetector? detector;
        private readonly CameraService camera = new CameraService();
        private int busy = 0;   // önceki kare hâlâ işleniyorsa yenisini atlamak için

        private readonly Label lblStatus;
        private readonly PictureBox picture;
        private readonly Button btnStart;
        private readonly Button btnStop;
        private readonly Button btnOpen;
        private readonly Label lblInfo;

        public Form1()
        {
            InitializeComponent();

            Text = "Yüz Algılama ve Tanıma";
            ClientSize = new System.Drawing.Size(760, 600);
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            Font = new Font("Segoe UI", 10);

            lblStatus = new Label
            {
                AutoSize = false,
                Location = new System.Drawing.Point(20, 10),
                Size = new System.Drawing.Size(720, 36),
                TextAlign = ContentAlignment.MiddleCenter,
                Font = new Font("Segoe UI", 12, FontStyle.Bold),
                ForeColor = Color.White
            };

            picture = new PictureBox
            {
                Location = new System.Drawing.Point(20, 55),
                Size = new System.Drawing.Size(720, 460),
                SizeMode = PictureBoxSizeMode.Zoom,
                BorderStyle = BorderStyle.FixedSingle,
                BackColor = Color.Black
            };

            btnStart = CreateButton("📷 Kamera Başlat", 20);
            btnStop = CreateButton("Kamera Durdur", 200);
            btnOpen = CreateButton("🖼 Görüntü Seç", 380);

            lblInfo = new Label
            {
                Text = "Algılanan yüz: -",
                AutoSize = true,
                Location = new System.Drawing.Point(570, 542)
            };

            Controls.AddRange(new Control[] { lblStatus, picture, btnStart, btnStop, btnOpen, lblInfo });

            btnStart.Click += (s, e) => StartCamera();
            btnStop.Click += async (s, e) => { await camera.StopAsync(); UpdateUi(); };
            btnOpen.Click += async (s, e) => await OpenImageAsync();

            camera.FrameCaptured += OnFrameCaptured;
            camera.ErrorOccurred += message => RunOnUi(() =>
            {
                MessageBox.Show(message, "Kamera Hatası", MessageBoxButtons.OK, MessageBoxIcon.Error);
                UpdateUi();
            });

            FormClosing += (s, e) => _ = camera.StopAsync();

            InitDetector();
            UpdateUi();
        }

        private Button CreateButton(string text, int x)
        {
            return new Button
            {
                Text = text,
                Location = new System.Drawing.Point(x, 530),
                Size = new System.Drawing.Size(170, 45)
            };
        }

        private void InitDetector()
        {
            try
            {
                detector = new FaceDetector();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Başlatma Hatası", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void StartCamera()
        {
            try
            {
                camera.Start();
            }
            catch (InvalidOperationException ex)
            {
                MessageBox.Show(ex.Message, "Kamera Hatası", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            UpdateUi();
        }

        // Kameradan gelen her kare (arka plan iş parçacığında çalışır)
        private void OnFrameCaptured(Mat frame)
        {
            if (Interlocked.Exchange(ref busy, 1) == 1)
            {
                frame.Dispose();   // önceki kare hâlâ ekrana basılıyor, bunu atla
                return;
            }

            Bitmap bitmap;
            int faceCount;
            try
            {
                faceCount = ProcessImage(frame);
                bitmap = frame.ToBitmap();
            }
            finally
            {
                frame.Dispose();
            }

            RunOnUi(() =>
            {
                ShowImage(bitmap, faceCount);
                busy = 0;
            });
        }

        private async Task OpenImageAsync()
        {
            if (camera.IsRunning)
            {
                await camera.StopAsync();
                UpdateUi();
            }

            using var dialog = new OpenFileDialog
            {
                Title = "Görüntü Seç",
                Filter = "Resim dosyaları|*.jpg;*.jpeg;*.png;*.bmp"
            };

            if (dialog.ShowDialog() != DialogResult.OK)
                return;

            try
            {
                // Türkçe karakterli yollarda sorun çıkmasın diye dosyayı byte olarak okuyoruz
                byte[] bytes = File.ReadAllBytes(dialog.FileName);
                using Mat image = Cv2.ImDecode(bytes, ImreadModes.Color);

                if (image.Empty())
                    throw new InvalidDataException("Seçilen dosya geçerli bir resim değil.");

                int faceCount = ProcessImage(image);
                ShowImage(image.ToBitmap(), faceCount);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Resim açılamadı:\n" + ex.Message, "Hata",
                                MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        // Yüzleri bulur, etraflarına dikdörtgen çizer, yüz sayısını döndürür
        private int ProcessImage(Mat image)
        {
            if (detector == null) return 0;

            Rect[] faces = detector.Detect(image);
            foreach (var face in faces)
                Cv2.Rectangle(image, face, new Scalar(0, 255, 0), 3);   // yeşil (BGR)

            return faces.Length;
        }

        private void ShowImage(Bitmap bitmap, int faceCount)
        {
            var old = picture.Image;
            picture.Image = bitmap;
            old?.Dispose();   // eski görüntünün belleğini serbest bırak

            lblInfo.Text = $"Algılanan yüz: {faceCount}";
        }

        private void UpdateUi()
        {
            bool running = camera.IsRunning;

            lblStatus.Text = running ? "● KAMERA AÇIK" : "■ KAMERA KAPALI";
            lblStatus.BackColor = running ? Color.SeaGreen : Color.SlateGray;

            btnStart.Enabled = !running && detector != null;
            btnStop.Enabled = running;
            btnOpen.Enabled = detector != null;
        }

        private void RunOnUi(Action action)
        {
            if (IsDisposed || !IsHandleCreated) return;

            try
            {
                if (InvokeRequired)
                    BeginInvoke(action);
                else
                    action();
            }
            catch (InvalidOperationException)
            {
                // pencere kapanırken gelen son kareler, yok say
            }
        }
    }
}