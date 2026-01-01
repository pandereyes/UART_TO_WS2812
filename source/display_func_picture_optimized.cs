using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Windows.Forms;

namespace UART_TO_WS2812
{
    /// <summary>
    /// 优化的图片显示功能类，解决GIF播放速度和性能问题
    /// </summary>
    internal class display_func_picture_optimized : IShowable
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

        // 图像处理参数 - 与原版保持兼容
        private static float brightness = 1.0f;
        private static float contrast = 2.5f;
        private static float saturation = 1.3f;
        private static float threshold = 0.5f;
        private static bool forceBinarization = false;

        // 属性接口，用于配置保存
        public static float Brightness 
        { 
            get => brightness; 
            set 
            { 
                brightness = value;
                NotifyParametersChanged();
                ConfigManager.SetFloat(ConfigManager.Keys.PictureBrightness, value);
                ConfigManager.SaveConfig();
            } 
        }
        
        public static float Contrast 
        { 
            get => contrast; 
            set 
            { 
                contrast = value;
                NotifyParametersChanged();
                ConfigManager.SetFloat(ConfigManager.Keys.PictureContrast, value);
                ConfigManager.SaveConfig();
            } 
        }
        
        public static float Saturation 
        { 
            get => saturation; 
            set 
            { 
                saturation = value;
                NotifyParametersChanged();
                ConfigManager.SetFloat(ConfigManager.Keys.PictureSaturation, value);
                ConfigManager.SaveConfig();
            } 
        }
        
        public static float Threshold 
        { 
            get => threshold; 
            set 
            {
                threshold = value;
                NotifyParametersChanged();
                ConfigManager.SetFloat(ConfigManager.Keys.PictureThreshold, value);
                ConfigManager.SaveConfig();
            } 
        }
        
        public static bool ForceBinarization 
        { 
            get => forceBinarization; 
            set 
            { 
                forceBinarization = value;
                NotifyParametersChanged();
                ConfigManager.SetBool(ConfigManager.Keys.PictureForceBinarization, value);
                ConfigManager.SaveConfig();
            } 
        }

        // 从配置加载图像处理参数
        public static void LoadImageParamsFromConfig()
        {
            brightness = ConfigManager.GetFloat(ConfigManager.Keys.PictureBrightness, 1.0f);
            contrast = ConfigManager.GetFloat(ConfigManager.Keys.PictureContrast, 2.5f);
            saturation = ConfigManager.GetFloat(ConfigManager.Keys.PictureSaturation, 1.3f);
            threshold = ConfigManager.GetFloat(ConfigManager.Keys.PictureThreshold, 0.5f);
            forceBinarization = ConfigManager.GetBool(ConfigManager.Keys.PictureForceBinarization, false);
        }

        private static Bitmap resizedBitmap = null;

        // GIF动画相关变量 - 优化版本
        private static List<GifFrameInfo> gifFrames = new List<GifFrameInfo>();
        private static List<Bitmap> originalFrames = new List<Bitmap>(); // 缓存原始帧
        private static int[] frameDelays = new int[0]; // 每帧独立延迟时间
        private static int currentFrame = 0;
        private static int totalFrames = 0;
        private static bool isGif = false;
        private static Stopwatch gifTimer = new Stopwatch(); // 高精度计时器
        private static long nextFrameTime = 0; // 下一帧的时间戳
        private static object frameLock = new object();
        
        // 性能优化变量
        private static volatile bool parametersChanged = false;
        private static DateTime lastParameterChange = DateTime.MinValue;
        private static readonly TimeSpan debounceDelay = TimeSpan.FromMilliseconds(200);

        // 设置图片路径并处理图片
        public static bool SetImagePath(string path, bool showSettings = true)
        {
            imagePath = path;
            imageLoaded = false;
            isGif = false;
            ClearGifData();

            // 直接加载图片
            LoadAndProcessImage();
            return imageLoaded;
        }

        // 显示图片参数设置对话框
        public static void ShowImageSettingsDialog()
        {
            ImageSettingsForm settingsForm = new ImageSettingsForm();

            // 设置初始参数值
            settingsForm.SetInitialValues(Brightness, Contrast, Saturation, Threshold, ForceBinarization);

            // 参数变化时实时预览
            settingsForm.ParametersChanged += () =>
            {
                // 更新参数并重新处理图片
                Brightness = settingsForm.Brightness;
                Contrast = settingsForm.Contrast;
                Saturation = settingsForm.Saturation;
                Threshold = settingsForm.Threshold;
                ForceBinarization = settingsForm.ForceBinarization;

                // 实时重新处理图像（不重新加载）
                ReprocessImageWithCurrentParameters();

                // 强制立即更新显示（不等待定时器）
                ForceImmediateDisplayUpdate();
            };

            // 显示对话框并获取结果
            DialogResult result = settingsForm.ShowDialog();

            if (result == DialogResult.OK)
            {
                // 用户点击应用，更新最终参数
                Brightness = settingsForm.Brightness;
                Contrast = settingsForm.Contrast;
                Saturation = settingsForm.Saturation;
                Threshold = settingsForm.Threshold;
                ForceBinarization = settingsForm.ForceBinarization;

                // 使用最终参数重新处理图片
                if (imageLoaded)
                {
                    ReprocessImageWithCurrentParameters();
                }
                else
                {
                    LoadAndProcessImage();
                    imageLoaded = true;
                }
            }
            else if (result == DialogResult.Cancel)
            {
                // 用户点击取消，恢复默认参数
                Brightness = 1.0f;
                Contrast = 2.5f;
                Saturation = 1.3f;
                Threshold = 0.5f;
                ForceBinarization = false;

                // 恢复默认参数效果
                if (imageLoaded)
                {
                    ReprocessImageWithCurrentParameters();
                }
                else
                {
                    LoadAndProcessImage();
                }
            }
        }

        // 加载并处理图片
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
                        ProcessSingleImage(originalImage);
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

        // 处理GIF动画 - 优化版本
        private static void ProcessGifAnimation()
        {
            ClearGifData();

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
                        if (delay < 10) delay = 10; // 防止过快播放
                        if (delay > 1000) delay = 1000; // 防止过慢播放
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
                ProcessFrameAsync(0); // 立即处理第一帧
                for (int i = 1; i < Math.Min(3, totalFrames); i++)
                {
                    ProcessFrameAsync(i);
                }
            }
        }

        // 处理单张图片
        private static void ProcessSingleImage(Bitmap originalImage)
        {
            imageData = ProcessImageTo16x16(originalImage);
        }

        // 通用图片处理到16x16
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

                // 缓存缩放后的图像用于实时预览
                resizedBitmap?.Dispose();
                resizedBitmap = new Bitmap(resizedImage);

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

        // 使用当前参数重新处理图像 - 优化版本
        public static void ReprocessImageWithCurrentParameters()
        {
            parametersChanged = true;
            lastParameterChange = DateTime.Now;
            
            if (resizedBitmap == null) return;

            // 重新处理静态图像
            if (!isGif)
            {
                for (int x = 0; x < DisplayConfig.CurrentConfig.Width; x++)
                {
                    for (int y = 0; y < DisplayConfig.CurrentConfig.Height; y++)
                    {
                        Color pixel = resizedBitmap.GetPixel(x, y);
                        Color enhancedPixel = EnhanceColor(pixel);
                        imageData[x, y] = (uint)((enhancedPixel.R << 16) | (enhancedPixel.G << 8) | enhancedPixel.B);
                    }
                }
            }
            else
            {
                // GIF参数变更的防抖处理，立即重新处理当前帧
                if (currentFrame < originalFrames.Count)
                {
                    ProcessFrameAsync(currentFrame);
                }
            }
        }

        // 异步处理单个帧
        private static void ProcessFrameAsync(int frameIndex)
        {
            if (frameIndex >= originalFrames.Count) return;
            
            Task.Run(() =>
            {
                lock (frameLock)
                {
                    if (frameIndex < gifFrames.Count)
                    {
                        var frame = gifFrames[frameIndex];
                        frame.FrameData = ProcessImageTo16x16(originalFrames[frameIndex]);
                        frame.IsProcessed = true;
                        gifFrames[frameIndex] = frame;
                    }
                }
            });
        }
        
        // 清理GIF数据
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
            }
        }

        // 处理参数变更的防抖逻辑
        private static void ProcessParameterChangeIfNeeded()
        {
            if (parametersChanged && DateTime.Now - lastParameterChange > debounceDelay)
            {
                parametersChanged = false;
                
                if (isGif && originalFrames.Count > 0)
                {
                    // 清除所有已处理的帧数据，强制重新处理
                    lock (frameLock)
                    {
                        for (int i = 0; i < gifFrames.Count; i++)
                        {
                            var frame = gifFrames[i];
                            frame.IsProcessed = false;
                            frame.FrameData = null;
                            gifFrames[i] = frame;
                        }
                    }
                    
                    // 优先处理当前帧和附近几帧
                    ProcessFrameAsync(currentFrame);
                    for (int i = 1; i <= 2 && i < totalFrames; i++)
                    {
                        int nextFrame = (currentFrame + i) % totalFrames;
                        ProcessFrameAsync(nextFrame);
                    }
                }
            }
        }

        // 更新GIF帧播放
        private static void UpdateGifFrame()
        {
            long currentTime = gifTimer.ElapsedMilliseconds;
            
            // 检查是否需要切换到下一帧
            if (currentTime >= nextFrameTime)
            {
                lock (frameLock)
                {
                    // 计算应该跳过多少帧（处理严重延迟情况）
                    int framesToSkip = 0;
                    long theoreticalTime = nextFrameTime;
                    
                    while (currentTime >= theoreticalTime && framesToSkip < totalFrames)
                    {
                        framesToSkip++;
                        theoreticalTime += frameDelays[(currentFrame + framesToSkip) % totalFrames];
                    }
                    
                    // 更新当前帧
                    currentFrame = (currentFrame + framesToSkip) % totalFrames;
                    
                    // ✅ 修复：使用理论时间而不是当前时间
                    nextFrameTime = theoreticalTime;
                    
                    // 确保当前帧已处理，如果没有则立即处理
                    if (currentFrame < gifFrames.Count && !gifFrames[currentFrame].IsProcessed)
                    {
                        ProcessFrameAsync(currentFrame);
                    }
                    
                    // 预加载后续帧
                    for (int i = 1; i <= 2; i++)
                    {
                        int futureFrame = (currentFrame + i) % totalFrames;
                        if (futureFrame < gifFrames.Count && !gifFrames[futureFrame].IsProcessed)
                        {
                            ProcessFrameAsync(futureFrame);
                        }
                    }
                }
            }
        }

        // 色彩增强函数
        private static Color EnhanceColor(Color original)
        {
            float luminance = (0.299f * original.R + 0.587f * original.G + 0.114f * original.B) / 255.0f;
            luminance = Math.Max(0.0f, Math.Min(1.0f, luminance * brightness));

            if (forceBinarization)
            {
                return luminance < threshold ? Color.FromArgb(0, 0, 0) : Color.FromArgb(255, 255, 255);
            }

            float threshold_param = threshold;
            float contrast_param = contrast;
            float blackPoint = 0.05f;
            float whitePoint = 0.95f;

            float enhancedLuminance;
            if (luminance < threshold_param)
            {
                enhancedLuminance = blackPoint * Math.Max(0, luminance * contrast_param / threshold_param);
            }
            else
            {
                float normalizedBright = (luminance - threshold_param) / (1.0f - threshold_param);
                enhancedLuminance = whitePoint + (1.0f - whitePoint) * Math.Min(1.0f, normalizedBright * contrast_param);
            }

            enhancedLuminance = Math.Max(0.0f, Math.Min(1.0f, enhancedLuminance));

            if (IsGrayscale(original))
            {
                if (enhancedLuminance < 0.3f)
                    return Color.FromArgb(0, 0, 0);
                else if (enhancedLuminance > 0.7f)
                    return Color.FromArgb(255, 255, 255);
                else
                    return luminance < threshold_param ? Color.FromArgb(0, 0, 0) : Color.FromArgb(255, 255, 255);
            }
            else
            {
                float h, s, l;
                RgbToHsl(original.R, original.G, original.B, out h, out s, out l);
                s = Math.Min(1.0f, s * saturation);
                l = enhancedLuminance;

                int r, g, b;
                HslToRgb(h, s, l, out r, out g, out b);
                return Color.FromArgb(r, g, b);
            }
        }

        private static bool IsGrayscale(Color color)
        {
            int maxDiff = Math.Max(Math.Abs(color.R - color.G),
                         Math.Max(Math.Abs(color.G - color.B), Math.Abs(color.R - color.B)));
            return maxDiff < 30;
        }

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

        // 强制立即更新显示
        private static void ForceImmediateDisplayUpdate()
        {
            if (display_global_define.g_display_func_index == 2) // 图片显示模式
            {
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
                            }
                        }
                        else
                        {
                            display_global_define.g_display_data[displayIndex] = imageData[x, imageY];
                        }
                    }
                }

                Form1.Instance.BeginInvoke(new Action(() =>
                {
                    try
                    {
                        display_refresh.UpdateUIButtons();
                    }
                    catch (Exception) { }
                }));
            }
        }

        // 参数改变通知
        private static void NotifyParametersChanged()
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

            // 处理参数变更防抖
            ProcessParameterChangeIfNeeded();
            
            // GIF动画处理 - 优化版本
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
    }
}