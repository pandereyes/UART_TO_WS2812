using System;
using System.Threading;

namespace UART_TO_WS2812
{
    internal class display_func_ambilight : IShowable, IDisposable
    {
        private ScreenColorSampler colorSampler;
        private Thread samplingThread;
        private volatile bool isRunning;
        private readonly object updateLock = new object();
        private DateTime lastSampleTime = DateTime.MinValue;
        private volatile bool hasNewData = false;

        public display_func_ambilight()
        {
            colorSampler = new ScreenColorSampler();
            isRunning = true;

            // 启动采样线程
            samplingThread = new Thread(ColorSamplingLoop);
            samplingThread.IsBackground = true;
            samplingThread.Start();
        }

        private void ColorSamplingLoop()
        {
            while (isRunning)
            {
                try
                {
                    DateTime now = DateTime.Now;
                    int interval = DisplayConfig.AmbiLightSettings.SampleInterval;
                    
                    // 检查是否到了采样时间
                    if ((now - lastSampleTime).TotalMilliseconds >= interval)
                    {
                        lock (updateLock)
                        {
                            colorSampler.SampleScreenColors();
                            hasNewData = true;
                            lastSampleTime = now;
                        }
                    }
                    
                    // 等待5ms再检查，减少CPU占用
                    Thread.Sleep(5);
                }
                catch (Exception)
                {
                    // 忽略错误，继续采样
                    Thread.Sleep(100);
                }
            }
        }

        public void show()
        {
            try
            {
                var config = DisplayConfig.AmbiLightSettings;
                
                // 只有在有新数据时才更新显示，减少不必要的计算
                if (hasNewData)
                {
                    lock (updateLock)
                    {
                        // 确保显示数据数组大小正确
                        if (display_global_define.g_display_data.Length < config.TotalLEDs)
                        {
                            // 清空显示，等待配置更新完成
                            Array.Clear(display_global_define.g_display_data, 0, display_global_define.g_display_data.Length);
                            return;
                        }

                        // 将采样的颜色映射到LED灯带
                        int maxIndex = Math.Min(config.TotalLEDs, display_global_define.g_display_data.Length);
                        for (int i = 0; i < maxIndex; i++)
                        {
                            display_global_define.g_display_data[i] = colorSampler.GetLEDColor(i);
                        }
                        
                        hasNewData = false;
                    }
                }
            }
            catch (Exception)
            {
                // 发生错误时清空显示
                try
                {
                    Array.Clear(display_global_define.g_display_data, 0, display_global_define.g_display_data.Length);
                }
                catch { }
            }
        }

        public void Dispose()
        {
            isRunning = false;
            if (samplingThread != null)
            {
                samplingThread.Join(1000); // 等待采样线程结束
                samplingThread = null;
            }
            colorSampler?.Dispose();
            colorSampler = null;
        }

        // 更新配置
        public void UpdateConfig()
        {
            lock (updateLock)
            {
                colorSampler.UpdateConfig();
            }
        }
    }
}