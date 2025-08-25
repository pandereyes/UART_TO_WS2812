using System;
using System.Threading;
using NAudio.Dsp;

namespace RGB点阵取模
{
    /// <summary>
    /// 音频捕获和FFT功能测试类
    /// </summary>
    public class AudioTest
    {
        private AudioCapture audioCapture;
        private bool isRunning = false;
        
        public void StartTest()
        {
            Console.WriteLine("开始音频捕获和FFT测试...");
            
            try
            {
                // 初始化音频捕获
                audioCapture = new AudioCapture();
                audioCapture.FFTResultAvailable += OnFFTResultAvailable;
                
                // 开始捕获
                audioCapture.StartCapture();
                isRunning = true;
                
                Console.WriteLine("音频捕获已启动，按任意键停止...");
                Console.ReadKey();
                
                // 停止捕获
                audioCapture.StopCapture();
                isRunning = false;
                
                Console.WriteLine("测试完成！");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"测试失败: {ex.Message}");
                Console.WriteLine($"详细错误: {ex.StackTrace}");
            }
            finally
            {
                audioCapture?.Dispose();
            }
        }
        
        private void OnFFTResultAvailable(object sender, FFTResultEventArgs e)
        {
            if (!isRunning) return;
            
            try
            {
                // 显示FFT结果的基本信息
                Console.WriteLine($"FFT结果长度: {e.FFTResult.Length}, 采样率: {e.SampleRate}Hz");
                
                // 找到最大幅度值及其索引
                double maxMagnitude = 0;
                int maxIndex = 0;
                
                for (int i = 0; i < Math.Min(e.FFTResult.Length / 2, 64); i++) // 只检查前64个频率点
                {
                    if (e.FFTResult[i] > maxMagnitude)
                    {
                        maxMagnitude = e.FFTResult[i];
                        maxIndex = i;
                    }
                }
                
                // 计算对应的频率
                double frequency = (double)maxIndex * e.SampleRate / (e.FFTResult.Length * 2);
                
                Console.WriteLine($"最大幅度: {maxMagnitude:F3}, 频率: {frequency:F1}Hz");
                
                // 显示前8个频率点的幅度（用于调试）
                Console.Write("前8个频率点: ");
                for (int i = 0; i < Math.Min(8, e.FFTResult.Length); i++)
                {
                    Console.Write($"{e.FFTResult[i]:F2} ");
                }
                Console.WriteLine();
                Console.WriteLine("---");
                
                // 限制输出频率，避免控制台刷屏
                Thread.Sleep(100);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"FFT结果处理错误: {ex.Message}");
            }
        }
        
        /// <summary>
        /// 静态测试方法，可以直接调用
        /// </summary>
        public static void RunTest()
        {
            var test = new AudioTest();
            test.StartTest();
        }
    }
}
