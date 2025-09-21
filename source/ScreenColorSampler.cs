using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Windows.Forms;

namespace UART_TO_WS2812
{
    public class ScreenColorSampler
    {
        private Rectangle monitorBounds;
        private int sampleWidth;
        private AmbiLightConfig config;
        private Bitmap screenBitmap;
        private Graphics screenGraphics;

        // LED位置映射缓存
        private struct LEDPosition
        {
            public int X;
            public int Y;
            public int Width;
            public int Height;
        }
        
        private LEDPosition[] ledPositions;

        public ScreenColorSampler()
        {
            config = DisplayConfig.AmbiLightSettings;
            
            // 获取主显示器的边界
            monitorBounds = Screen.PrimaryScreen.Bounds;
            
            // 根据百分比计算采样尺寸
            CalculateSampleSize();

            // 初始化屏幕截图对象
            screenBitmap = new Bitmap(monitorBounds.Width, monitorBounds.Height);
            screenGraphics = Graphics.FromImage(screenBitmap);

            // 计算LED位置映射
            CalculateLEDPositions();
        }
        
        // 根据百分比计算采样尺寸
        private void CalculateSampleSize()
        {
            sampleWidth = Math.Max(5, (int)(Math.Min(monitorBounds.Width, monitorBounds.Height) * config.SamplePercent / 100.0));
        }

        public void Dispose()
        {
            screenGraphics?.Dispose();
            screenBitmap?.Dispose();
        }

        public void UpdateConfig()
        {
            config = DisplayConfig.AmbiLightSettings;
            
            // 重新计算采样尺寸
            CalculateSampleSize();

            // 重新计算LED位置映射
            CalculateLEDPositions();
        }

        // 计算LED位置映射
        private void CalculateLEDPositions()
        {
            int totalLEDs = config.TotalLEDs;
            ledPositions = new LEDPosition[totalLEDs];
            
            // 根据实际LED分布计算每个LED位置
            for (int i = 0; i < totalLEDs; i++)
            {
                LEDPosition pos = CalculateLEDPositionByEdge(i);
                ledPositions[i] = pos;
            }
        }

        // 根据LED索引计算其在屏幕边缘的位置
        private LEDPosition CalculateLEDPositionByEdge(int ledIndex)
        {
            LEDPosition pos = new LEDPosition();
            pos.Width = sampleWidth;
            pos.Height = sampleWidth;
            
            // 重新映射索引考虑起始位置和方向
            int mappedIndex = RemapLEDIndexForPosition(ledIndex);
            
            // 确定LED在哪条边上
            if (mappedIndex < config.TopLEDs)
            {
                // 顶部边缘
                float progress = (float)mappedIndex / config.TopLEDs;
                pos.X = (int)(progress * monitorBounds.Width);
                pos.Y = 0;
            }
            else
            {
                mappedIndex -= config.TopLEDs;
                if (mappedIndex < config.RightLEDs)
                {
                    // 右侧边缘
                    float progress = (float)mappedIndex / config.RightLEDs;
                    pos.X = monitorBounds.Width - sampleWidth;
                    pos.Y = (int)(progress * monitorBounds.Height);
                }
                else
                {
                    mappedIndex -= config.RightLEDs;
                    if (mappedIndex < config.BottomLEDs)
                    {
                        // 底部边缘（从右到左）
                        float progress = (float)mappedIndex / config.BottomLEDs;
                        pos.X = (int)((1.0f - progress) * monitorBounds.Width);
                        pos.Y = monitorBounds.Height - sampleWidth;
                    }
                    else
                    {
                        mappedIndex -= config.BottomLEDs;
                        // 左侧边缘（从下到上）
                        float progress = (float)mappedIndex / config.LeftLEDs;
                        pos.X = 0;
                        pos.Y = (int)((1.0f - progress) * monitorBounds.Height);
                    }
                }
            }
            
            return pos;
        }

        // 重新映射LED索引考虑起始位置和方向
        private int RemapLEDIndexForPosition(int originalIndex)
        {
            int totalLEDs = config.TotalLEDs;
            int mappedIndex = originalIndex;
            
            // 根据环绕方向调整索引
            if (config.Direction == AmbiLightDirection.CounterClockwise)
            {
                mappedIndex = totalLEDs - 1 - originalIndex;
            }
            
            // 根据起始位置调整偏移
            int startOffset = GetStartPositionOffset();
            mappedIndex = (mappedIndex + startOffset) % totalLEDs;
            
            return mappedIndex;
        }

        // 获取起始位置的LED偏移量
        private int GetStartPositionOffset()
        {
            switch (config.StartPosition)
            {
                case AmbiLightStartPosition.LeftTop:
                    return 0; // 从顶部开始
                    
                case AmbiLightStartPosition.RightTop:
                    return config.TopLEDs; // 从右侧开始
                    
                case AmbiLightStartPosition.RightBottom:
                    return config.TopLEDs + config.RightLEDs; // 从底部开始
                    
                case AmbiLightStartPosition.LeftBottom:
                    return config.TopLEDs + config.RightLEDs + config.BottomLEDs; // 从左侧开始
                    
                default:
                    return 0;
            }
        }


        // 采样屏幕边缘颜色
        public void SampleScreenColors()
        {
            // 捕获屏幕
            screenGraphics.CopyFromScreen(0, 0, 0, 0, monitorBounds.Size);
        }

        // 获取指定LED的颜色
        public uint GetLEDColor(int index)
        {
            if (index < 0 || index >= ledPositions.Length)
                return 0;

            LEDPosition pos = ledPositions[index];
            return SampleAreaAverage(pos.X, pos.Y, pos.Width, pos.Height);
        }

        // 计算区域平均颜色
        private uint SampleAreaAverage(int x, int y, int width, int height)
        {
            long sumR = 0, sumG = 0, sumB = 0;
            int count = 0;

            // 确保采样区域不超出屏幕边界
            x = Math.Max(0, Math.Min(x, monitorBounds.Width - 1));
            y = Math.Max(0, Math.Min(y, monitorBounds.Height - 1));
            width = Math.Min(width, monitorBounds.Width - x);
            height = Math.Min(height, monitorBounds.Height - y);

            if (width <= 0 || height <= 0)
                return 0;

            // 锁定位图数据以提高访问速度
            BitmapData bmpData = screenBitmap.LockBits(
                new Rectangle(x, y, width, height),
                ImageLockMode.ReadOnly,
                PixelFormat.Format32bppArgb);

            try
            {
                unsafe
                {
                    byte* ptr = (byte*)bmpData.Scan0;
                    for (int row = 0; row < height; row++)
                    {
                        for (int col = 0; col < width; col++)
                        {
                            // 在ARGB格式中: Blue @ ptr[0], Green @ ptr[1], Red @ ptr[2], Alpha @ ptr[3]
                            sumB += ptr[0];
                            sumG += ptr[1];
                            sumR += ptr[2];
                            ptr += 4;
                            count++;
                        }
                        ptr += bmpData.Stride - (width * 4); // 移动到下一行
                    }
                }
            }
            finally
            {
                screenBitmap.UnlockBits(bmpData);
            }

            if (count == 0) return 0;

            // 计算平均值
            byte avgR = (byte)(sumR / count);
            byte avgG = (byte)(sumG / count);
            byte avgB = (byte)(sumB / count);

            // 转换为RGB格式 (R << 16 | G << 8 | B)
            return (uint)((avgR << 16) | (avgG << 8) | avgB);
        }
    }
}