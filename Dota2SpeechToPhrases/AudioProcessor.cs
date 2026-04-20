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
        public void Start(int micIndex, int cableIndex)
        {
            // 1. Захват (пробуем захватить как есть)
            _micInput = new WaveInEvent { DeviceNumber = micIndex };
            _micInput.WaveFormat = new WaveFormat(16000, 1); // Формат захвата

            _micProvider = new WaveInProvider(_micInput);

            // 2. Микшер (IEEE Float для смешивания)
            _mixer = new MixingSampleProvider(WaveFormat.CreateIeeeFloatWaveFormat(16000, 1));
            _mixer.ReadFully = true;
            _mixer.AddMixerInput(_micProvider.ToSampleProvider());

            // 3. Вывод
            _virtualOutput = new WaveOutEvent { DeviceNumber = cableIndex };
            _virtualOutput.Init(_mixer);

            // --- ГАРАНТИРОВАННЫЙ ФОРМАТ ДЛЯ VOSK ---
            _micInput.DataAvailable += (s, e) =>
            {
                if (_recognizer == null) return;

                // Vosk ОЧЕНЬ чувствителен к размеру буфера. 
                // Если e.Buffer слишком большой или странный, он роняет процесс.
                try
                {
                    if (_recognizer.AcceptWaveform(e.Buffer, e.BytesRecorded))
                    {
                        var json = _recognizer.Result();
                        var text = JObject.Parse(json)["text"]?.ToString();
                        if (!string.IsNullOrEmpty(text))
                        {
                            OnPhraseRecognized?.Invoke(text);
                            CheckForKeywords(text);
                        }
                    }
                }
                catch { /* Игнорируем мелкие ошибки, чтобы не падал поток */ }
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
            _micInput?.StopRecording();
            _virtualOutput?.Stop();
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

        private void CheckForKeywords(string text) // Изменил имя переменной для ясности
        {
            if (text.ToLower().Contains("союзник"))
            {
                PlaySound(@"E:\Lock\wav_dataset\Winwyv_ally_01_ru.mp3.wav");
            }
        }
    }
}