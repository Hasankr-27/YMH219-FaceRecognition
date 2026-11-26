using OpenCvSharp;
using OpenCvSharp.Extensions;

namespace YMH219_FaceRecognition
{
    public partial class Form1 : Form
    {
        private FaceDetector? detector;
        private readonly FaceRecognizerService recognizer = new FaceRecognizerService();
        private readonly CameraService camera = new CameraService();
        private int busy = 0;

        // Örnek kaydetmek için son kare ve içindeki yüzler
        private readonly object frameLock = new object();
        private Mat? lastFrame;
        private Rect[] lastFaces = Array.Empty<Rect>();

        private readonly Label lblStatus;
        private readonly PictureBox picture;
        private readonly Button btnStart;
        private readonly Button btnStop;
        private readonly Button btnOpen;
        private readonly Label lblInfo;
        private readonly TextBox txtLabel;
        private readonly Button btnSample;
        private readonly Button btnTrain;
        private readonly Label lblTrain;

        public Form1()
        {
            InitializeComponent();

            Text = "Yüz Algılama ve Tanıma";
            ClientSize = new System.Drawing.Size(760, 680);
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

            // 1. satır: kamera ve resim
            btnStart = CreateButton("📷 Kamera Başlat", 20, 530);
            btnStop = CreateButton("Kamera Durdur", 200, 530);
            btnOpen = CreateButton("🖼 Görüntü Seç", 380, 530);

            lblInfo = new Label
            {
                Text = "Algılanan yüz: -",
                AutoSize = false,
                Location = new System.Drawing.Point(560, 522),
                Size = new System.Drawing.Size(190, 66),
                TextAlign = ContentAlignment.MiddleLeft
            };

            // 2. satır: örnek kaydetme ve eğitim
            var lblName = new Label { Text = "İsim:", AutoSize = true, Location = new System.Drawing.Point(20, 600) };
            txtLabel = new TextBox { Location = new System.Drawing.Point(70, 597), Width = 120 };
            btnSample = CreateButton("Örnek Kaydet (20)", 200, 590);
            btnTrain = CreateButton("Eğit", 380, 590);

            lblTrain = new Label { AutoSize = true, Location = new System.Drawing.Point(20, 645) };

            Controls.AddRange(new Control[]
            {
                lblStatus, picture, btnStart, btnStop, btnOpen, lblInfo,
                lblName, txtLabel, btnSample, btnTrain, lblTrain
            });

            btnStart.Click += (s, e) => StartCamera();
            btnStop.Click += async (s, e) => { await camera.StopAsync(); UpdateUi(); };
            btnOpen.Click += async (s, e) => await OpenImageAsync();
            btnSample.Click += async (s, e) => await CaptureSamplesAsync();
            btnTrain.Click += (s, e) => TrainModel();

            camera.FrameCaptured += OnFrameCaptured;
            camera.ErrorOccurred += message => RunOnUi(() =>
            {
                MessageBox.Show(message, "Kamera Hatası", MessageBoxButtons.OK, MessageBoxIcon.Error);
                UpdateUi();
            });

            FormClosing += (s, e) => _ = camera.StopAsync();

            InitDetector();
            AutoTrainOnStartup();
            UpdateUi();
            UpdateTrainInfo();
        }

        private Button CreateButton(string text, int x, int y)
        {
            return new Button
            {
                Text = text,
                Location = new System.Drawing.Point(x, y),
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

        // Daha önce kaydedilmiş örnekler varsa açılışta otomatik eğit
        private void AutoTrainOnStartup()
        {
            try
            {
                if (recognizer.GetLabels().Count >= 2)
                    recognizer.Train();
            }
            catch
            {
                // açılışta eğitilemezse sorun değil, kullanıcı Eğit'e basabilir
            }
        }

        private void StartCamera()
        {
            busy = 0;
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

        // Kameradan gelen her kare (arka plan iş parçacığında)
        private void OnFrameCaptured(Mat frame)
        {
            if (Interlocked.Exchange(ref busy, 1) == 1)
            {
                frame.Dispose();
                return;
            }

            Bitmap bitmap;
            string summary;
            try
            {
                Rect[] faces = detector!.Detect(frame);
                RememberFrame(frame, faces);
                summary = DrawResults(frame, faces);
                bitmap = frame.ToBitmap();
            }
            finally
            {
                frame.Dispose();
            }

            RunOnUi(() =>
            {
                ShowImage(bitmap, summary);
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

            if (dialog.ShowDialog() != DialogResult.OK || detector == null)
                return;

            try
            {
                byte[] bytes = File.ReadAllBytes(dialog.FileName);
                using Mat image = Cv2.ImDecode(bytes, ImreadModes.Color);

                if (image.Empty())
                    throw new InvalidDataException("Seçilen dosya geçerli bir resim değil.");

                Rect[] faces = detector.Detect(image);
                RememberFrame(image, faces);
                string summary = DrawResults(image, faces);
                ShowImage(image.ToBitmap(), summary);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Resim açılamadı:\n" + ex.Message, "Hata",
                                MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        // Örnek kaydetmek için kareyi (dikdörtgenler çizilmeden önce) sakla
        private void RememberFrame(Mat image, Rect[] faces)
        {
            lock (frameLock)
            {
                lastFrame?.Dispose();
                lastFrame = image.Clone();
                lastFaces = faces;
            }
        }

        // Yüzleri tanır, dikdörtgen + isim çizer, bilgi metnini döndürür
        private string DrawResults(Mat image, Rect[] faces)
        {
            // Önce tahmin et (çizimler yüzü bozmasın), sonra çiz
            var results = new List<(Rect Face, string Label, double Confidence)>();
            foreach (Rect face in faces)
            {
                if (recognizer.IsTrained)
                {
                    var (label, confidence) = recognizer.Predict(image, face);
                    results.Add((face, label, confidence));
                }
                else
                {
                    results.Add((face, "", 0));
                }
            }

            foreach (var r in results)
            {
                bool unknown = r.Label == "Bilinmiyor";
                Scalar color = unknown ? new Scalar(0, 0, 255) : new Scalar(0, 255, 0);   // kırmızı / yeşil (BGR)

                Cv2.Rectangle(image, r.Face, color, 3);

                if (r.Label != "")
                {
                    string text = $"{ToAscii(r.Label)} %{r.Confidence:0}";
                    int y = Math.Max(r.Face.Y - 10, 25);
                    Cv2.PutText(image, text, new OpenCvSharp.Point(r.Face.X, y),
                                HersheyFonts.HersheySimplex, 0.8, color, 2);
                }
            }

            if (faces.Length == 0)
                return "Yüz algılanmadı";
            if (!recognizer.IsTrained)
                return $"Algılanan yüz: {faces.Length}\n(Tanıma için önce eğitin)";

            var first = results[0];
            return $"Algılanan yüz: {faces.Length}\nTahmin: {first.Label}\nGüven: %{first.Confidence:0}";
        }

        // OpenCV yazı fonksiyonu Türkçe karakter desteklemiyor
        private static string ToAscii(string text)
        {
            return text.Replace('ç', 'c').Replace('ğ', 'g').Replace('ı', 'i').Replace('ö', 'o')
                       .Replace('ş', 's').Replace('ü', 'u').Replace('Ç', 'C').Replace('Ğ', 'G')
                       .Replace('İ', 'I').Replace('Ö', 'O').Replace('Ş', 'S').Replace('Ü', 'U');
        }

        // Kameradan 20 örnek kaydeder (fotoğraftaysa 1 örnek)
        private async Task CaptureSamplesAsync()
        {
            string label = txtLabel.Text.Trim();
            if (label == "")
            {
                MessageBox.Show("Lütfen önce bir isim girin.", "Uyarı",
                                MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            btnSample.Enabled = false;
            int saved = 0;

            try
            {
                for (int i = 0; i < 20; i++)
                {
                    Mat? frame;
                    Rect[] faces;
                    lock (frameLock)
                    {
                        frame = lastFrame?.Clone();
                        faces = lastFaces;
                    }

                    if (frame != null)
                    {
                        using (frame)
                        {
                            if (faces.Length == 1)   // tek yüz varsa kaydet, karışmasın
                            {
                                recognizer.AddSample(frame, faces[0], label);
                                saved++;
                            }
                        }
                    }

                    lblTrain.Text = $"Kaydediliyor... {saved}/20 (başını hafifçe oynat)";

                    if (!camera.IsRunning) break;   // fotoğraftan tek örnek yeter
                    await Task.Delay(150);
                }
            }
            catch (Exception ex) when (ex is ArgumentException || ex is IOException || ex is UnauthorizedAccessException)
            {
                MessageBox.Show(ex.Message, "Hata", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                btnSample.Enabled = true;
            }

            if (saved == 0)
                MessageBox.Show("Örnek kaydedilemedi. Görüntüde tam olarak 1 yüz olmalı.", "Uyarı",
                                MessageBoxButtons.OK, MessageBoxIcon.Warning);

            UpdateTrainInfo();
        }

        private void TrainModel()
        {
            try
            {
                recognizer.Train();
                UpdateTrainInfo();
                MessageBox.Show("Model eğitildi. Artık yüzler tanınacak.", "Eğitim Tamamlandı",
                                MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (InvalidOperationException ex)
            {
                MessageBox.Show(ex.Message, "Uyarı", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Eğitim sırasında hata oluştu:\n" + ex.Message, "Hata",
                                MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void UpdateTrainInfo()
        {
            var labels = recognizer.GetLabels();
            if (labels.Count == 0)
            {
                lblTrain.Text = "Henüz örnek yok. Bir isim yazıp 'Örnek Kaydet'e basın.";
                return;
            }

            string list = string.Join(",  ", labels.Select(l => $"{l}: {recognizer.GetSampleCount(l)} örnek"));
            lblTrain.Text = list + (recognizer.IsTrained ? "   ✓ eğitildi" : "   (henüz eğitilmedi)");
        }

        private void ShowImage(Bitmap bitmap, string summary)
        {
            var old = picture.Image;
            picture.Image = bitmap;
            old?.Dispose();

            lblInfo.Text = summary;
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
            }
        }
    }
}