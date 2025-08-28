using System;
using System.Linq;
using System.Threading;
using NAudio.Wave;
using NAudio.Dsp;

namespace 串口驱动WS2812
{
    internal class AudioFFTAnalysis
    {
        // FFT配置
        private const int FftLength = 1024; // 必须是2的幂，增加到1024提高频率分辨率
        private const int TargetSampleRate = 44100; // 目标采样率44.1kHz，标准音频采样率

        // 音频捕获和缓冲区
        private static WasapiLoopbackCapture capture;
        private static volatile bool isCapturing = false;
        private static readonly int bufferSize = 4096; // 缓冲区大小
        private static float[] audioBuffer = new float[bufferSize];
        private static int bufferIndex = 0;

        // 重采样相关
        private static int inputSampleRate;
        private static float resampleRatio;
        private static float resampleAccumulator = 0;

        // 重叠处理
        private static int overlapSamples = FftLength / 2; // 50%重叠
        private static float[] previousBuffer = new float[FftLength];

        private static AudioFFTAnalysis _instance;
        public static AudioFFTAnalysis Instance
        {
            get
            {
                if (_instance == null)
                    _instance = new AudioFFTAnalysis();
                return _instance;
            }
        }

        public void StartCapture()
        {
            // 初始化音频捕获
            capture = new WasapiLoopbackCapture();
            inputSampleRate = capture.WaveFormat.SampleRate;
            resampleRatio = (float)inputSampleRate / TargetSampleRate;

            Console.WriteLine($"输入采样率: {inputSampleRate}Hz, 重采样比率: {resampleRatio:F2}");

            // 设置数据处理事件
            capture.DataAvailable += (s, e) =>
            {
                // 获取音频格式信息
                var format = capture.WaveFormat;
                var bytesPerSample = format.BitsPerSample / 8;
                var channels = format.Channels;
                var bytesPerFrame = bytesPerSample * channels;
                var framesRecorded = e.BytesRecorded / bytesPerFrame;

                // 只在第一次打印音频格式信息
                if (bufferIndex == 0)
                {
                    Console.WriteLine($"音频格式: {format.SampleRate}Hz, {format.BitsPerSample}bit, {format.Channels}声道");
                }

                for (int frame = 0; frame < framesRecorded; frame++)
                {
                    float sample32 = 0;
                    
                    // 处理多声道音频，混合为单声道
                    for (int channel = 0; channel < channels; channel++)
                    {
                        int byteOffset = frame * bytesPerFrame + channel * bytesPerSample;
                        
                        if (format.BitsPerSample == 16)
                        {
                            short sample16 = BitConverter.ToInt16(e.Buffer, byteOffset);
                            sample32 += sample16 / 32768f;
                        }
                        else if (format.BitsPerSample == 32)
                        {
                            if (format.Encoding == WaveFormatEncoding.IeeeFloat)
                            {
                                sample32 += BitConverter.ToSingle(e.Buffer, byteOffset);
                            }
                            else
                            {
                                int sample32int = BitConverter.ToInt32(e.Buffer, byteOffset);
                                sample32 += sample32int / 2147483648f;
                            }
                        }
                    }
                    
                    // 平均多声道
                    sample32 /= channels;

                    // 重采样到目标采样率
                    resampleAccumulator += 1.0f;
                    if (resampleAccumulator >= resampleRatio)
                    {
                        resampleAccumulator -= resampleRatio;

                        // 添加到缓冲区
                        audioBuffer[bufferIndex] = sample32;
                        bufferIndex++;

                        // 当缓冲区有足够数据时执行FFT
                        if (bufferIndex >= FftLength)
                        {
                            ProcessAudioData();

                            // 保留重叠部分用于下一次FFT计算
                            Array.Copy(audioBuffer, FftLength - overlapSamples, previousBuffer, 0, overlapSamples);

                            // 移动缓冲区内容，保留重叠部分
                            Array.Copy(audioBuffer, overlapSamples, audioBuffer, 0, bufferSize - overlapSamples);
                            bufferIndex = overlapSamples;
                        }
                    }
                }
            };

            capture.RecordingStopped += (s, e) =>
            {
                isCapturing = false;
                Console.WriteLine("捕获已停止");
            };

            isCapturing = true;
            capture.StartRecording();
        }

        public void StopCapture()
        {
            if (capture != null)
            {
                capture.StopRecording();
                capture.Dispose();
                capture = null;
            }
        }

        static void ProcessAudioData()
        {
            // 创建FFT输入数组
            Complex[] fftBuffer = new Complex[FftLength];

            // 应用汉宁窗并准备FFT输入
            for (int i = 0; i < FftLength; i++)
            {
                // 应用汉宁窗减少频谱泄漏
                double hanning = 0.5 * (1 - Math.Cos(2 * Math.PI * i / (FftLength - 1)));
                fftBuffer[i].X = (float)(audioBuffer[i] * hanning);
                fftBuffer[i].Y = 0; // 虚部设为0
            }

            // 执行FFT
            FastFourierTransform.FFT(true, (int)Math.Log(FftLength, 2), fftBuffer);

            // 计算幅度谱
            float[] magnitudes = new float[FftLength / 2];
            for (int i = 0; i < FftLength / 2; i++)
            {
                float real = fftBuffer[i].X;
                float imaginary = fftBuffer[i].Y;
                magnitudes[i] = (float)Math.Sqrt(real * real + imaginary * imaginary);
            }

            // 计算频率分辨率
            float frequencyResolution = (float)TargetSampleRate / FftLength;
            Console.WriteLine($"频率分辨率: {frequencyResolution:F2} Hz/bin");
           
            
            // 找到最大幅度的频率
            int maxBin = 0;
            float maxMagnitude = 0;
            for (int i = 1; i < magnitudes.Length; i++) // 跳过DC分量
            {
                if (magnitudes[i] > maxMagnitude)
                {
                    maxMagnitude = magnitudes[i];
                    maxBin = i;
                }
            }
            float peakFreq = maxBin * frequencyResolution;
            Console.WriteLine($"峰值频率: {peakFreq:F1} Hz (bin {maxBin}), 幅度: {maxMagnitude:F6}");


            // 将magnitudes放大并转化为uint整数
            for (int i = 0; i < display_global_define.g_fft_amp.Length; i++)
            {
                // 确保幅度不超过uint.MaxValue
                display_global_define.g_fft_amp[i] = (uint)Math.Min(magnitudes[i] * 10000, uint.MaxValue);
            }


        }

    }
}