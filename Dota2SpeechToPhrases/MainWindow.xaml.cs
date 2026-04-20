using DotaVoiceAssistant;
using NAudio.Wave;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;



namespace Dota2SpeechToPhrases
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            LoadDevices();
        }

        private AudioProcessor _processor = new AudioProcessor();

        private void StartButton_Click(object sender, RoutedEventArgs e)
        {
            // Вызываем метод именно у объекта _processor
            var indices = _processor.GetDeviceIndices();

            if (indices.micId != -1 && indices.cableId != -1)
            {
                _processor.Start(indices.micId, indices.cableId);
            }
        }

        public class AudioDevice
        {
            public int Id { get; set; }
            public string Name { get; set; } = "";
        }

        private void LoadDevices()
        {
            // Очистим на всякий случай
            MicComboBox.Items.Clear();
            CableComboBox.Items.Clear();

            // Явно говорим, что показывать Имя
            MicComboBox.DisplayMemberPath = "Name";
            CableComboBox.DisplayMemberPath = "Name";
            // Заполняем микрофоны
            for (int i = 0; i < WaveIn.DeviceCount; i++)
            {
                MicComboBox.Items.Add(new AudioDevice { Id = i, Name = WaveIn.GetCapabilities(i).ProductName });
            }

            // Заполняем выходы (динамики/кабели)
            for (int i = 0; i < WaveOut.DeviceCount; i++)
            {
                CableComboBox.Items.Add(new AudioDevice { Id = i, Name = WaveOut.GetCapabilities(i).ProductName });
            }

            // Пытаемся выбрать твои девайсы автоматически
            MicComboBox.SelectedIndex = MicComboBox.Items.Cast<AudioDevice>()
                .ToList().FindIndex(d => d.Name.Contains("HyperX"));

            CableComboBox.SelectedIndex = CableComboBox.Items.Cast<AudioDevice>()
                .ToList().FindIndex(d => d.Name.Contains("CABLE Input"));
        }

        private void BtnStart_Click(object sender, RoutedEventArgs e)
        {
            if (MicComboBox.SelectedItem is AudioDevice mic && CableComboBox.SelectedItem is AudioDevice cable)
            {
                try
                {
                    // 1. Сначала готовим "мозги" (Vosk)
                    string modelPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "model");
                    _processor.InitVosk(modelPath);

                    // 2. Подписываемся на события ДО запуска потока данных
                    _processor.OnPhraseRecognized += (text) => {
                        this.Dispatcher.BeginInvoke(new Action(() => {
                            RecognizedText.Text = $"Слышу: {text}";
                        }));
                    };

                    // 3. ЗАПУСКАЕМ аудио-мост только ОДИН РАЗ
                    _processor.Start(mic.Id, cable.Id);

                    StatusText.Text = "Статус: РАБОТАЕТ";
                    StatusText.Foreground = Brushes.Green;
                    BtnStart.IsEnabled = false;
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Ошибка инициализации: {ex.Message}");
                }
            }
        }

        private void BtnTestSound_Click(object sender, RoutedEventArgs e)
        {
            // Укажи здесь путь к любому короткому звуку на диске
            string testFile = @"E:\Lock\wav_dataset\Winwyv_ally_01_ru.mp3.wav";
            if (System.IO.File.Exists(testFile))
            {
                _processor.PlaySound(testFile);
            }
            else
            {
                MessageBox.Show("Тестовый файл не найден. Укажи свой путь в коде!");
            }
        }
    }
}