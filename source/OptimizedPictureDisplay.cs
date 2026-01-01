using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Diagnostics;

namespace UART_TO_WS2812
{
    /// <summary>
    /// 优化的图片显示类，解决GIF播放速度和性能问题
    /// </summary>
    internal class OptimizedPictureDisplay : IShowable, IDisposable
    {
        // GIF帧信息结构
        private struct GifFrameInfo
        {
            public uint[,] FrameData;
            public int DelayMs;
            public bool IsProcessed;
        }

        private static string imagePath = "";
        private static uint[,] imageData = new uint[16, 16];
        private static bool imageLoaded = false;

        // 图像处理参数
        public static float Brightness { get; set; } = 1.0f;
        public static float Contrast { get; set; } = 2.5f;
        public static float Saturation { get; set; } = 1.3f;
        public static float Threshold { get; set; } = 0.5f;
        public static bool ForceBinarization { get; set; } = false;

        // GIF优化相关变量
        private static List<GifFrameInfo> gifFrames = new List<GifFrameInfo>();
        private static List<Bitmap> originalFrames = new List<Bitmap>(); // 缓存原始帧，避免重复读取
        private static int[] frameDelays = new int[0]; // 每帧的独立延迟时间
        private static int currentFrame = 0;
        private static int totalFrames = 0;
        private static bool isGif = false;
        private static Stopwatch gifTimer = new Stopwatch(); // 高精度计时器
        private static long nextFrameTime = 0; // 下一帧的时间戳
        private static readonly object frameLock = new object();

        // 性能优化变量
        private static volatile bool isProcessing = false;
        private static CancellationTokenSource processingCancellation = new CancellationTokenSource();
        private static readonly Queue<int> framesToProcess = new Queue<int>(); // 待处理帧队列
        private static Task backgroundProcessor;
        private static volatile bool parametersChanged = false;
        private static DateTime lastParameterChange = DateTime.MinValue;
        private static readonly TimeSpan debounceDelay = TimeSpan.FromMilliseconds(300); // 防抖延迟

        // LRU缓存管理
        private static readonly int MaxCachedFrames = 20; // 最大缓存帧数
        private static readonly LinkedList<int> frameAccessOrder = new LinkedList<int>();
        private static readonly Dictionary<int, LinkedListNode<int>> frameAccessNodes = new Dictionary<int, LinkedListNode<int>>();

        static OptimizedPictureDisplay()
        {
            // 启动后台处理线程
            StartBackgroundProcessor();
        }

        private static void StartBackgroundProcessor()
        {
            backgroundProcessor = Task.Run(async () =>
            {
                while (!processingCancellation.Token.IsCancellationRequested)
                {
                    try
                    {
                        await ProcessQueuedFrames();
                        await Task.Delay(16, processingCancellation.Token); // ~60FPS处理频率
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                }
            });
        }

        private static async Task ProcessQueuedFrames()
        {
            // 检查参数变化防抖
            if (parametersChanged && DateTime.Now - lastParameterChange > debounceDelay)
            {
                parametersChanged = false;
                await ProcessParameterChange();
            }

            // 处理队列中的帧
            while (framesToProcess.Count > 0 && !processingCancellation.Token.IsCancellationRequested)
            {
                int frameIndex;
                lock (frameLock)
                {
                    if (framesToProcess.Count == 0) break;
                    frameIndex = framesToProcess.Dequeue();
                }

                await ProcessSingleFrame(frameIndex);
            }
        }

        private static async Task ProcessParameterChange()
        {
            if (!isGif || originalFrames.Count == 0) return;

            await Task.Run(() =>
            {
                // 清除所有已处理的帧数据，强制重新处理
                lock (frameLock)
                {
                    for (int i = 0; i < gifFrames.Count; i++)
                    {
                        var frame = gifFrames[i];
                        frame.IsProcessed = false;
                        gifFrames[i] = frame;
                    }
                    
                    // 将当前帧和附近几帧加入高优先级处理队列
                    framesToProcess.Clear();
                    
                    // 优先处理当前帧
                    framesToProcess.Enqueue(currentFrame);
                    
                    // 然后处理前后几帧，提升播放流畅度
                    for (int i = 1; i <= 3 && i < totalFrames; i++)
                    {
                        int nextFrame = (currentFrame + i) % totalFrames;
                        int prevFrame = (currentFrame - i + totalFrames) % totalFrames;
                        
                        framesToProcess.Enqueue(nextFrame);
                        if (i <= 2) framesToProcess.Enqueue(prevFrame);
                    }
                }
            });
        }

        private static async Task ProcessSingleFrame(int frameIndex)
        {
            if (frameIndex >= originalFrames.Count) return;

            await Task.Run(() =>
            {
                lock (frameLock)
                {
                    if (frameIndex < gifFrames.Count && !gifFrames[frameIndex].IsProcessed)
                    {
                        var frame = gifFrames[frameIndex];
                        frame.FrameData = ProcessImageTo16x16(originalFrames[frameIndex]);
                        frame.IsProcessed = true;
                        gifFrames[frameIndex] = frame;

                        // 更新LRU缓存
                        UpdateFrameAccess(frameIndex);
                        
                        // 清理过多缓存
                        CleanupExcessFrames();
                    }
                }
            });
        }

        private static void UpdateFrameAccess(int frameIndex)
        {
            if (frameAccessNodes.ContainsKey(frameIndex))
            {
                // 移动到链表头部
                frameAccessOrder.Remove(frameAccessNodes[frameIndex]);
            }
            
            var node = frameAccessOrder.AddFirst(frameIndex);
            frameAccessNodes[frameIndex] = node;
        }

        private static void CleanupExcessFrames()
        {
            while (frameAccessOrder.Count > MaxCachedFrames)
            {
                int lruFrame = frameAccessOrder.Last.Value;
                frameAccessOrder.RemoveLast();
                frameAccessNodes.Remove(lruFrame);
                
                // 清理最久未使用的帧数据
                if (lruFrame < gifFrames.Count)
                {
                    var frame = gifFrames[lruFrame];
                    frame.FrameData = null;
                    frame.IsProcessed = false;
                    gifFrames[lruFrame] = frame;
                }
            }
        }

        public static bool SetImagePath(string path)
        {
            imagePath = path;
            imageLoaded = false;
            isGif = false;
            
            // 清理旧数据
            ClearGifData();
            
            LoadAndProcessImage();
            return imageLoaded;
        }

        private static void ClearGifData()
        {
            lock (frameLock)
            {
                gifFrames.Clear();
                foreach (var bitmap in originalFrames)
                {
                    bitmap?.Dispose();
                }
                originalFrames.Clear();
                frameDelays = new int[0];
                currentFrame = 0;
                totalFrames = 0;
                frameAccessOrder.Clear();
                frameAccessNodes.Clear();
                framesToProcess.Clear();
            }
        }

        private static void LoadAndProcessImage()
        {
            if (string.IsNullOrEmpty(imagePath) || !File.Exists(imagePath))
                return;

            try
            {
                string extension = Path.GetExtension(imagePath).ToLower();
                isGif = (extension == ".gif");

                if (isGif)
                {
                    ProcessGifAnimation();
                }
                else
                {
                    using (Bitmap originalImage = new Bitmap(imagePath))
                    {
                        imageData = ProcessImageTo16x16(originalImage);
                    }
                }

                imageLoaded = true;
            }
            catch (Exception)
            {
                imageLoaded = false;
                ClearImageData();
            }
        }

        private static void ProcessGifAnimation()
        {
            using (Image gifImage = Image.FromFile(imagePath))
            {
                FrameDimension dimension = new FrameDimension(gifImage.FrameDimensionsList[0]);
                totalFrames = gifImage.GetFrameCount(dimension);

                // 获取帧延迟信息
                PropertyItem frameDelayItem = null;
                try
                {
                    frameDelayItem = gifImage.GetPropertyItem(0x5100);
                }
                catch { }

                frameDelays = new int[totalFrames];
                originalFrames.Clear();
                gifFrames.Clear();

                // 加载所有原始帧和延迟时间
                for (int frameIndex = 0; frameIndex < totalFrames; frameIndex++)
                {
                    gifImage.SelectActiveFrame(dimension, frameIndex);
                    originalFrames.Add(new Bitmap(gifImage));

                    // 计算每帧的延迟时间
                    int delay = 100; // 默认延迟
                    if (frameDelayItem != null && frameIndex < frameDelayItem.Value.Length / 4)
                    {
                        delay = BitConverter.ToInt32(frameDelayItem.Value, frameIndex * 4) * 10;
                        if (delay < 20) delay = 20; // 最小延迟保护，避免过快播放
                        if (delay > 1000) delay = 1000; // 最大延迟保护
                    }
                    frameDelays[frameIndex] = delay;

                    // 初始化帧信息
                    gifFrames.Add(new GifFrameInfo
                    {
                        FrameData = null,
                        DelayMs = delay,
                        IsProcessed = false
                    });
                }

                // 重置播放状态
                currentFrame = 0;
                gifTimer.Restart();
                nextFrameTime = frameDelays[0];

                // 预加载前几帧
                lock (frameLock)
                {
                    framesToProcess.Clear();
                    for (int i = 0; i < Math.Min(5, totalFrames); i++)
                    {
                        framesToProcess.Enqueue(i);
                    }
                }
            }
        }

        private static uint[,] ProcessImageTo16x16(Bitmap sourceImage)
        {
            uint[,] result = new uint[DisplayConfig.CurrentConfig.Width, DisplayConfig.CurrentConfig.Height];

            using (Bitmap resizedImage = new Bitmap(DisplayConfig.CurrentConfig.Width, DisplayConfig.CurrentConfig.Height))
            {
                using (Graphics graphics = Graphics.FromImage(resizedImage))
                {
                    graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBilinear;
                    graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighQuality;
                    graphics.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.HighQuality;
                    graphics.CompositingQuality = System.Drawing.Drawing2D.CompositingQuality.HighQuality;

                    graphics.DrawImage(sourceImage, 0, 0, DisplayConfig.CurrentConfig.Width, DisplayConfig.CurrentConfig.Height);
                }

                for (int x = 0; x < DisplayConfig.CurrentConfig.Width; x++)
                {
                    for (int y = 0; y < DisplayConfig.CurrentConfig.Height; y++)
                    {
                        Color pixel = resizedImage.GetPixel(x, y);
                        Color enhancedPixel = EnhanceColor(pixel);
                        result[x, y] = (uint)((enhancedPixel.R << 16) | (enhancedPixel.G << 8) | enhancedPixel.B);
                    }
                }
            }

            return result;
        }

        private static Color EnhanceColor(Color original)
        {
            // 应用当前的图像处理参数
            float luminance = (0.299f * original.R + 0.587f * original.G + 0.114f * original.B) / 255.0f;
            luminance = Math.Max(0.0f, Math.Min(1.0f, luminance * Brightness));

            if (ForceBinarization)
            {
                return luminance < Threshold ? Color.FromArgb(0, 0, 0) : Color.FromArgb(255, 255, 255);
            }

            // 应用对比度和饱和度
            float h, s, l;
            RgbToHsl(original.R, original.G, original.B, out h, out s, out l);

            s = Math.Min(1.0f, s * Saturation);
            l = Math.Max(0.0f, Math.Min(1.0f, ApplyContrast(luminance, Contrast)));

            int r, g, b;
            HslToRgb(h, s, l, out r, out g, out b);
            return Color.FromArgb(r, g, b);
        }

        private static float ApplyContrast(float value, float contrast)
        {
            return Math.Max(0.0f, Math.Min(1.0f, (value - 0.5f) * contrast + 0.5f));
        }

        // RGB到HSL转换
        private static void RgbToHsl(int r, int g, int b, out float h, out float s, out float l)
        {
            float rf = r / 255.0f;
            float gf = g / 255.0f;
            float bf = b / 255.0f;

            float max = Math.Max(rf, Math.Max(gf, bf));
            float min = Math.Min(rf, Math.Min(gf, bf));
            float delta = max - min;

            l = (max + min) / 2.0f;

            if (delta == 0)
            {
                h = s = 0;
            }
            else
            {
                s = l > 0.5f ? delta / (2.0f - max - min) : delta / (max + min);

                if (max == rf)
                    h = (gf - bf) / delta + (gf < bf ? 6 : 0);
                else if (max == gf)
                    h = (bf - rf) / delta + 2;
                else
                    h = (rf - gf) / delta + 4;

                h /= 6.0f;
            }
        }

        // HSL到RGB转换
        private static void HslToRgb(float h, float s, float l, out int r, out int g, out int b)
        {
            float rf, gf, bf;

            if (s == 0)
            {
                rf = gf = bf = l;
            }
            else
            {
                float q = l < 0.5f ? l * (1 + s) : l + s - l * s;
                float p = 2 * l - q;

                rf = HueToRgb(p, q, h + 1.0f / 3.0f);
                gf = HueToRgb(p, q, h);
                bf = HueToRgb(p, q, h - 1.0f / 3.0f);
            }

            r = (int)Math.Round(rf * 255);
            g = (int)Math.Round(gf * 255);
            b = (int)Math.Round(bf * 255);

            r = Math.Max(0, Math.Min(255, r));
            g = Math.Max(0, Math.Min(255, g));
            b = Math.Max(0, Math.Min(255, b));
        }

        private static float HueToRgb(float p, float q, float t)
        {
            if (t < 0) t += 1;
            if (t > 1) t -= 1;
            if (t < 1.0f / 6.0f) return p + (q - p) * 6 * t;
            if (t < 1.0f / 2.0f) return q;
            if (t < 2.0f / 3.0f) return p + (q - p) * (2.0f / 3.0f - t) * 6;
            return p;
        }

        private static void ClearImageData()
        {
            for (int x = 0; x < DisplayConfig.CurrentConfig.Width; x++)
            {
                for (int y = 0; y < DisplayConfig.CurrentConfig.Height; y++)
                {
                    imageData[x, y] = 0;
                }
            }
        }

        // 参数改变通知
        public static void NotifyParametersChanged()
        {
            parametersChanged = true;
            lastParameterChange = DateTime.Now;
        }

        public void show()
        {
            if (!imageLoaded)
            {
                Array.Clear(display_global_define.g_display_data, 0, display_global_define.g_display_data.Length);
                return;
            }

            if (isGif && gifFrames.Count > 0)
            {
                UpdateGifFrame();
            }

            // 复制当前帧数据到显示缓冲区
            for (int x = 0; x < display_global_define.DISPLAY_MAX_LIST_NUM; x++)
            {
                for (int imageY = 0; imageY < display_global_define.DISPLAY_MAX_LINE_NUM; imageY++)
                {
                    int displayIndex = x * display_global_define.DISPLAY_MAX_LIST_NUM + (display_global_define.DISPLAY_MAX_LINE_NUM - 1 - imageY);

                    if (isGif && gifFrames.Count > 0)
                    {
                        lock (frameLock)
                        {
                            if (currentFrame < gifFrames.Count && gifFrames[currentFrame].IsProcessed && gifFrames[currentFrame].FrameData != null)
                            {
                                display_global_define.g_display_data[displayIndex] = gifFrames[currentFrame].FrameData[x, imageY];
                            }
                            else
                            {
                                display_global_define.g_display_data[displayIndex] = 0; // 黑色，等待加载
                            }
                        }
                    }
                    else
                    {
                        display_global_define.g_display_data[displayIndex] = imageData[x, imageY];
                    }
                }
            }
        }

        private static void UpdateGifFrame()
        {
            long currentTime = gifTimer.ElapsedMilliseconds;
            
            // 检查是否需要切换到下一帧
            if (currentTime >= nextFrameTime)
            {
                lock (frameLock)
                {
                    // 切换到下一帧
                    currentFrame = (currentFrame + 1) % totalFrames;
                    
                    // 计算下一帧的时间戳
                    nextFrameTime = currentTime + frameDelays[currentFrame];
                    
                    // 确保当前帧已处理，如果没有则加入队列
                    if (currentFrame < gifFrames.Count && !gifFrames[currentFrame].IsProcessed)
                    {
                        if (!framesToProcess.Contains(currentFrame))
                        {
                            framesToProcess.Enqueue(currentFrame);
                        }
                    }
                    
                    // 预加载后续帧
                    for (int i = 1; i <= 2; i++)
                    {
                        int futureFrame = (currentFrame + i) % totalFrames;
                        if (futureFrame < gifFrames.Count && !gifFrames[futureFrame].IsProcessed)
                        {
                            if (!framesToProcess.Contains(futureFrame))
                            {
                                framesToProcess.Enqueue(futureFrame);
                            }
                        }
                    }
                }
            }
        }

        public void Dispose()
        {
            processingCancellation?.Cancel();
            backgroundProcessor?.Wait(1000);
            
            ClearGifData();
            processingCancellation?.Dispose();
        }
    }
}