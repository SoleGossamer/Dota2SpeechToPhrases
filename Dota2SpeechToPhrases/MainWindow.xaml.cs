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
        private KeyboardHook _hook = new KeyboardHook();
        public MainWindow()
        {
            InitializeComponent();
            LoadDevices();

            string savedPath = Dota2SpeechToPhrases.Properties.Settings.Default.LastPath;
            if (!string.IsNullOrEmpty(savedPath))
            {
                TxtPath.Text = savedPath;
                _processor.LoadPhrases(savedPath);
            }
        }

        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            // Отключаем хук перед выходом
            _hook.Unhook();

            // Останавливаем аудио-процессор
            _processor.Stop();

            base.OnClosing(e);
        }

        private AudioProcessor _processor = new AudioProcessor();

        private void StartButton_Click(object sender, RoutedEventArgs e)
        {
            // Берем выбранные устройства напрямую из интерфейса
            if (MicComboBox.SelectedItem is AudioDevice mic &&
                CableComboBox.SelectedItem is AudioDevice cable &&
                MonitorComboBox.SelectedItem is AudioDevice monitor)
            {
                // Передаем их ID в метод Start
                _processor.Start(mic.Id, cable.Id, monitor.Id);

                // Обновляем статус (для красоты)
                StatusText.Text = "Статус: РАБОТАЕТ";
                StatusText.Foreground = System.Windows.Media.Brushes.Green;
            }
            else
            {
                MessageBox.Show("Ошибка: Выберите все три устройства (Микрофон, Кабель и Наушники).");
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
            MonitorComboBox.Items.Clear();

            // Явно говорим, что показывать Имя
            MicComboBox.DisplayMemberPath = "Name";
            CableComboBox.DisplayMemberPath = "Name";
            MonitorComboBox.DisplayMemberPath = "Name";
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

            for (int i = 0; i < WaveOut.DeviceCount; i++)
            {
                MonitorComboBox.Items.Add(new AudioDevice { Id = i, Name = WaveOut.GetCapabilities(i).ProductName });
            }

            // Пытаемся выбрать твои девайсы автоматически
            MicComboBox.SelectedIndex = MicComboBox.Items.Cast<AudioDevice>()
                .ToList().FindIndex(d => d.Name.Contains("HyperX"));
            CableComboBox.SelectedIndex = CableComboBox.Items.Cast<AudioDevice>()
                .ToList().FindIndex(d => d.Name.Contains("CABLE Input"));
            MonitorComboBox.SelectedIndex = MonitorComboBox.Items.Cast<AudioDevice>()
                .ToList().FindIndex(d => d.Name.Contains("High Definition"));
        }

        private void BtnBrowse_Click(object sender, RoutedEventArgs e)
        {
            // Используем WinForms диалог (нужно добавить ссылку на System.Windows.Forms или Microsoft.Win32)
            var dialog = new Microsoft.Win32.OpenFolderDialog();
            if (dialog.ShowDialog() == true)
            {
                TxtPath.Text = dialog.FolderName;
                _processor.LoadPhrases(dialog.FolderName);

                // Сохраняем путь навсегда
                Dota2SpeechToPhrases.Properties.Settings.Default.LastPath = dialog.FolderName;
                Dota2SpeechToPhrases.Properties.Settings.Default.Save();
            }
        }

        private void BtnStart_Click(object sender, RoutedEventArgs e)
        {
            if (MicComboBox.SelectedItem is AudioDevice mic && CableComboBox.SelectedItem is AudioDevice cable &&
        MonitorComboBox.SelectedItem is AudioDevice monitor)
            {
                try
                {
                    // 1. Очищаем старые подписки через метод
                    _hook.ClearSubscribers();

                    // 2. Теперь подписываемся заново
                    _hook.OnKeyDown += () => { _processor.IsListenMode = true; };

                    // 2. Настраиваем логику зажатия F1
                    _hook.OnKeyDown += () => {
                        _processor.IsListenMode = true;
                    };

                    // Синхронизируем состояние тумблера перед запуском
                    _processor.IsMonitoringEnabled = ChkMonitor.IsChecked ?? false;

                    _hook.OnKeyUp += () => {
                        _processor.IsListenMode = false;

                        // Сразу забираем текст, как только отпустили кнопку
                        string finalSpeech = _processor.GetFinalText();

                        if (!string.IsNullOrEmpty(finalSpeech))
                        {
                            RecognizedText.Text = $"Финально услышал: {finalSpeech}";
                            _processor.ProcessFinalPhrase(finalSpeech); // Запускаем поиск и проигрывание
                        }
                    };

                    // 3. Готовим Vosk (модель грузится один раз)
                    string modelPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "model");
                    _processor.InitVosk(modelPath);

                    // 4. Подписка на текст (очищаем старые, чтобы не плодить дубли в UI)
                    // Примечание: если OnPhraseRecognized не поддерживает =, оставь как есть
                    _processor.OnPhraseRecognized += (text) => {
                        this.Dispatcher.BeginInvoke(new Action(() => {
                            RecognizedText.Text = $"Слышу: {text}";
                        }));
                    };

                    // 5. ЗАПУСК
                    _processor.Start(mic.Id, cable.Id, monitor.Id);
                    _hook.SetHook(); // Включаем перехват клавиш только после старта аудио

                    // UI фидбек
                    StatusText.Text = "Статус: РАБОТАЕТ (F1 активна)";
                    StatusText.Foreground = Brushes.Green;
                    BtnStart.IsEnabled = false;
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Ошибка при запуске: {ex.Message}");
                }
            }
            BtnStop.IsEnabled = true;
        }

        private void BtnStop_Click(object sender, RoutedEventArgs e)
        {
            // 1. Отключаем хук, чтобы клавиша F1 снова работала в обычном режиме
            _hook.Unhook();

            // 2. Останавливаем аудио
            _processor.Stop();

            // 3. Обновляем интерфейс
            StatusText.Text = "Статус: Остановлено";
            StatusText.Foreground = Brushes.Red;

            BtnStart.IsEnabled = true;
            BtnStop.IsEnabled = false;
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

        private void ChkMonitor_Click(object sender, RoutedEventArgs e)
        {
            // Проверяем, инициализирован ли процессор, чтобы не вылетело при запуске
            if (_processor != null)
            {
                bool isEnabled = ChkMonitor.IsChecked ?? false;
                _processor.SetMonitoring(isEnabled);

                // Визуальное подтверждение в статус-баре (по желанию)
                if (isEnabled)
                    StatusText.Text = "Мониторинг включен";
            }
        }
    }
}