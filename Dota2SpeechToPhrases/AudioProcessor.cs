using FuzzySharp;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using Vosk;

namespace DotaVoiceAssistant
{
    public class AudioProcessor
    {
        private WaveInEvent? _micInput;
        private WaveOutEvent? _virtualOutput;
        private MixingSampleProvider? _mixer;
        private WaveInProvider? _micProvider;
        private VoskRecognizer? _recognizer;
        private Model? _voskModel;

        public void InitVosk(string modelPath)
        {
            _voskModel = new Model(modelPath);
            _recognizer = new VoskRecognizer(_voskModel, 16000f);
        }

        public event Action<string>? OnPhraseRecognized;

        private BufferedWaveProvider? _cableBuffer; // Буфер для виртуального кабеля
        private BufferedWaveProvider? _monitorBuffer;
        public bool IsListenMode { get; set; } = false;

        private WaveOutEvent? _monitorOutput; // Выход на твои наушники
        public bool IsMonitoringEnabled { get; set; } = false; // Тот самый тумблер
        public void Start(int micIndex, int cableIndex, int monitorIndex)
        {
            var format = new WaveFormat(16000, 1);

            // 1. Настройка входа
            _micInput = new WaveInEvent { DeviceNumber = micIndex, WaveFormat = format };

            // 2. Настройка буферов (для виртуального кабеля и мониторинга)
            _cableBuffer = new BufferedWaveProvider(format) { DiscardOnBufferOverflow = true };
            _monitorBuffer = new BufferedWaveProvider(format) { DiscardOnBufferOverflow = true };

            // 3. Вывод (читает из _cableBuffer)
            _virtualOutput = new WaveOutEvent { DeviceNumber = cableIndex };
            _virtualOutput.Init(_cableBuffer); // Он читает из буфера виртуального кабеля

            // Инициализируем мониторинг (наушники)
            _monitorOutput = new WaveOutEvent { DeviceNumber = monitorIndex };
            _monitorOutput.Init(_monitorBuffer); // Он читает из мониторингового буфера
            _monitorOutput.Volume = IsMonitoringEnabled ? 1.0f : 0.0f; // Явная установка громкости при старте

            _micInput.DataAvailable += (s, e) =>
            {
                if (_recognizer == null) return;

                // --- ЛОГИКА ГОЛОСА ---
                if (IsListenMode)
                {
                    // РЕЖИМ ИИ: отправляем звук ТОЛЬКО в Vosk
                    if (_recognizer.AcceptWaveform(e.Buffer, e.BytesRecorded))
                    {
                        var json = _recognizer.Result();
                        var text = JObject.Parse(json)["text"]?.ToString();
                        if (!string.IsNullOrEmpty(text)) OnPhraseRecognized?.Invoke(text);
                    }
                }
                else
                {
                    // ОБЫЧНЫЙ РЕЖИМ: отправляем звук в виртуальный кабель
                    _cableBuffer.AddSamples(e.Buffer, 0, e.BytesRecorded);
                    // И одновременно в мониторинговый буфер (если мониторинг включен)
                    if (IsMonitoringEnabled)
                    {
                        _monitorBuffer.AddSamples(e.Buffer, 0, e.BytesRecorded);
                    }
                }
            };

            _virtualOutput.Play();
            _monitorOutput.Play();
            _micInput.StartRecording();
        }

        public void PlaySound(string path)
        {
            // Если микшер еще не создан, просто выходим из метода
            if (_mixer == null) return;

            var reader = new AudioFileReader(path);
            _mixer.AddMixerInput((ISampleProvider)reader);
        }

        public void Stop()
        {
            // 1. Останавливаем вывод звука в кабель
            if (_virtualOutput != null)
            {
                _virtualOutput.Stop();
                _virtualOutput.Dispose();
                _virtualOutput = null;
            }

            // 2. Останавливаем захват с микрофона
            if (_micInput != null)
            {
                _micInput.StopRecording();
                _micInput.Dispose();
                _micInput = null;
            }

            // 3. Очищаем буфер и провайдеры
            _cableBuffer = null;
            _micProvider = null;
            _mixer = null;

            // Рекогнайзер и модель (Vosk) можно не трогать, 
            // чтобы не тратить время на их повторную загрузку при следующем старте.
        }

        public void ListDevices()
        {
            // Список устройств записи (микрофоны и наш виртуальный кабель-выход)
            Debug.WriteLine("--- Устройства ЗАПИСИ (Input) ---");
            for (int i = 0; i < WaveIn.DeviceCount; i++)
            {
                var capabilities = WaveIn.GetCapabilities(i);
                Debug.WriteLine($"ID: {i} | Name: {capabilities.ProductName}");
            }

            // Список устройств воспроизведения (динамики и виртуальный кабель-вход)
            Debug.WriteLine("\n--- Устройства ВОСПРОИЗВЕДЕНИЯ (Output) ---");
            for (int i = 0; i < WaveOut.DeviceCount; i++)
            {
                var capabilities = WaveOut.GetCapabilities(i);
                Debug.WriteLine($"ID: {i} | Name: {capabilities.ProductName}");
            }
        }

        public void SetMonitoring(bool enabled)
        {
            IsMonitoringEnabled = enabled;
            if (_monitorOutput != null)
            {
                _monitorOutput.Volume = enabled ? 1.0f : 0.0f;
            }
        }

        public (int micId, int cableId) GetDeviceIndices()
        {
            int micId = -1;
            int cableId = -1;

            // Ищем микрофон HyperX среди устройств записи
            for (int i = 0; i < WaveIn.DeviceCount; i++)
            {
                var caps = WaveIn.GetCapabilities(i);
                if (caps.ProductName.Contains("HyperX QuadCast S"))
                {
                    micId = i;
                }
            }

            // Ищем CABLE Input среди устройств воспроизведения
            for (int i = 0; i < WaveOut.DeviceCount; i++)
            {
                var caps = WaveOut.GetCapabilities(i);
                if (caps.ProductName.Contains("CABLE Input"))
                {
                    cableId = i;
                }
            }

            return (micId, cableId);
        }

        public string GetFinalText()
        {
            if (_recognizer == null) return "";

            // Получаем то, что Vosk накопил до этого момента
            var json = _recognizer.FinalResult();
            var text = JObject.Parse(json)["text"]?.ToString() ?? "";

            // Важно: после FinalResult рекогнайзер сбрасывается, 
            // поэтому для следующей фразы он будет чист.
            return text;
        }

        private Dictionary<string, string> _phraseFiles = new Dictionary<string, string>();

        public void LoadPhrases(string folderPath)
        {
            _phraseFiles.Clear();
            if (!Directory.Exists(folderPath)) return;

            // Список поддерживаемых расширений
            var extensions = new[] { ".wav", ".mp3", ".mpeg" };

            var files = Directory.EnumerateFiles(folderPath, "*.*")
                                 .Where(f => extensions.Contains(Path.GetExtension(f).ToLower()));

            foreach (var file in files)
            {
                string fileName = Path.GetFileNameWithoutExtension(file).ToLower();
                _phraseFiles[fileName] = file;
            }
        }

        public void ProcessFinalPhrase(string recognizedText)
        {
            if (string.IsNullOrWhiteSpace(recognizedText) || _phraseFiles.Count == 0) return;

            // Находим лучшее совпадение среди ключей нашего словаря
            var result = FuzzySharp.Process.ExtractOne(recognizedText.ToLower(), _phraseFiles.Keys);

            // result.Score — это процент сходства (0-100)
            if (result.Score > 70) // 70% — золотой стандарт для Dota
            {
                string filePath = _phraseFiles[result.Value];

                // 1. Эмулируем нажатие клавиши G (голосовой чат в Доте)
                // 2. Проигрываем файл filePath
                PlaySound(filePath);
            }
        }
    }
}