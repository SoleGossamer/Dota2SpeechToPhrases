using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Linq;
using Vosk;
using Newtonsoft.Json.Linq;

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
        public bool IsListenMode { get; set; } = false;
        public void Start(int micIndex, int cableIndex)
        {
            var format = new WaveFormat(16000, 1);

            // 1. Настройка входа
            _micInput = new WaveInEvent { DeviceNumber = micIndex, WaveFormat = format };

            // 2. Настройка буфера для кабеля
            _cableBuffer = new BufferedWaveProvider(format) { DiscardOnBufferOverflow = true };

            // 3. Вывод (читает из _cableBuffer)
            _virtualOutput = new WaveOutEvent { DeviceNumber = cableIndex };
            _virtualOutput.Init(_cableBuffer);

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
                }
            };

            _virtualOutput.Play();
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

    }
}