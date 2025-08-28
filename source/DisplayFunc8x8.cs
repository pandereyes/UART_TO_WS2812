using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using 串口驱动WS2812;


namespace 串口驱动WS2812
{

    internal class display_globle_define_8x8
    {

        // 动态显示数据数组

        public const int DISPLAY_MAX_NUM = 64;

        public static uint[] g_display_data = new uint[DISPLAY_MAX_NUM];
        


        // 动态行列数
        public static int DISPLAY_MAX_LIST_NUM => DisplayConfig.CurrentConfig.Width;
        public static int DISPLAY_MAX_LINE_NUM => DisplayConfig.CurrentConfig.Height;

        public static int g_display_func_index = 0; // 当前显示功能索引

        // 重新初始化显示数据数组
        public static void ReinitializeDisplayData()
        {
            g_display_data = new uint[DISPLAY_MAX_NUM];
        }
    }



    public interface IShowable
    {
        void show();
    }


    internal class display_refresh_8x8
    {
        private List<IShowable> showList = new List<IShowable>();
        private static System.Threading.Timer display_refresh_timer;
        private int ui_update_counter = 0; // UI更新计数器
        private volatile bool ui_updating = false; // UI更新标志

        // 应用与灯板相同的亮度转换
        public static Color ApplyBrightnessToColor(uint rgbColor)
        {
            if (ws2812.Instance == null)
                return Color.FromArgb(
                    (int)((rgbColor >> 16) & 0xFF),
                    (int)((rgbColor >> 8) & 0xFF),
                    (int)(rgbColor & 0xFF)
                );

            // 获取当前亮度级别,放大UI的显示亮度
            int temp_b = ws2812.Instance.GetBrightness() * 3;
            int brightnessLevel = temp_b > 100 ? 100 : temp_b;
            
            // 提取RGB分量
            byte red = (byte)((rgbColor >> 16) & 0xFF);
            byte green = (byte)((rgbColor >> 8) & 0xFF);
            byte blue = (byte)(rgbColor & 0xFF);
            
            // 使用与灯板相同的HSL亮度转换逻辑
            double h, s, l;
            RgbToHsl(red, green, blue, out h, out s, out l);
            
            double temp_brightness = (double)brightnessLevel / 100.0;
            l = temp_brightness * l;
            
            // 确保亮度不超过最大值
            if (l > 1) l = 1;
            
            // 转换回RGB
            HslToRgb(h, s, l, out red, out green, out blue);
            
            return Color.FromArgb(red, green, blue);
        }
        
        // RGB转HSL（与ws2812_driver.cs保持一致）
        private static void RgbToHsl(byte r, byte g, byte b, out double h, out double s, out double l)
        {
            double rf = r / 255.0;
            double gf = g / 255.0;
            double bf = b / 255.0;
            
            double max = Math.Max(rf, Math.Max(gf, bf));
            double min = Math.Min(rf, Math.Min(gf, bf));
            double delta = max - min;
            
            // 亮度
            l = (max + min) / 2.0;
            
            if (delta == 0)
            {
                h = s = 0; // 灰色
            }
            else
            {
                // 饱和度
                s = l > 0.5 ? delta / (2.0 - max - min) : delta / (max + min);
                
                // 色相
                if (max == rf)
                    h = (gf - bf) / delta + (gf < bf ? 6 : 0);
                else if (max == gf)
                    h = (bf - rf) / delta + 2;
                else
                    h = (rf - gf) / delta + 4;
                
                h /= 6.0;
            }
        }
        
        // HSL转RGB（与ws2812_driver.cs保持一致）
        private static void HslToRgb(double h, double s, double l, out byte r, out byte g, out byte b)
        {
            double rf, gf, bf;
            
            if (s == 0)
            {
                rf = gf = bf = l; // 灰色
            }
            else
            {
                double q = l < 0.5 ? l * (1 + s) : l + s - l * s;
                double p = 2 * l - q;
                
                rf = HueToRgb(p, q, h + 1.0 / 3.0);
                gf = HueToRgb(p, q, h);
                bf = HueToRgb(p, q, h - 1.0 / 3.0);
            }
            
            r = (byte)Math.Round(rf * 255);
            g = (byte)Math.Round(gf * 255);
            b = (byte)Math.Round(bf * 255);
        }
        
        private static double HueToRgb(double p, double q, double t)
        {
            if (t < 0) t += 1;
            if (t > 1) t -= 1;
            if (t < 1.0 / 6.0) return p + (q - p) * 6 * t;
            if (t < 1.0 / 2.0) return q;
            if (t < 2.0 / 3.0) return p + (q - p) * (2.0 / 3.0 - t) * 6;
            return p;
        }


        private void display_func_init()
        {
            showList.Add(new display_func_draw());
            showList.Add(new display_music_spectrum());
            showList.Add(new display_func_picture());
        }


        public display_refresh_8x8()
        {
            display_func_init();

            display_refresh_timer = new System.Threading.Timer(
               display_refresh_timer_cb,
               null,
               0, // 立即开始
               display_global_define.DISPLAY_REFRESH_INTERVAL); // 间隔5ms
        }



        public static void display_refresh_8x8_dispose()
        {
            if (display_refresh_8x8.display_refresh_timer != null)
            {
                display_refresh_8x8.display_refresh_timer.Dispose();
                display_refresh_8x8.display_refresh_timer = null;
            }
        }
        
        // 停止显示刷新定时器
        public static void StopRefreshTimer()
        {
            if (display_refresh_timer != null)
            {
                // 完全停止并释放定时器
                display_refresh_timer.Change(Timeout.Infinite, Timeout.Infinite);
                display_refresh_timer.Dispose();
                display_refresh_timer = null;
            }
        }
        
        // 启动显示刷新定时器
        public static void StartRefreshTimer()
        {
            if (display_refresh_timer != null)
            {
                display_refresh_timer.Change(0, display_global_define.DISPLAY_REFRESH_INTERVAL);
            }
        }

        private void display_refresh_timer_cb(object state)
        {
            if (showList.Count > 0 && display_globle_define_8x8.g_display_func_index < showList.Count)
            {
                showList[display_globle_define_8x8.g_display_func_index].show();

                // 硬件LED更新 - 始终高频执行
                for (int i = 0; i < Math.Min(ws2812.WS2812_NUM, display_globle_define_8x8.g_display_data.Length); i++)
                {
                    ws2812.Instance.Ws2812SetColor(i, display_globle_define_8x8.g_display_data[i]);
                }

                // UI更新降频 - 根据窗口状态动态调整更新频率
                ui_update_counter++;
                
                // 动态调整UI更新频率：窗口最小化时完全跳过UI更新，正常显示时降低频率
                int updateThreshold = 4; // 正常显示时每4次硬件更新更新一次UI
                
                // 如果窗口最小化，完全跳过UI更新
                if (Form1.Instance.WindowState != FormWindowState.Minimized)
                {
                    if (ui_update_counter >= updateThreshold && display_globle_define_8x8.g_display_func_index != 0 && !ui_updating)
                    {
                        ui_update_counter = 0;
                        ui_updating = true;

                        // 异步更新UI，使用优化的批量更新方法
                        Form1.Instance.BeginInvoke(new Action(() =>
                        {
                            try
                            {
                                // 批量更新UI，减少BeginInvoke内部的开销
                                display_func_picture.UpdateUIButtons();
                            }
                            finally
                            {
                                ui_updating = false; // 重置标志
                            }
                        }));
                    }
                }                
            }
        }

    }





    internal class display_func_draw : IShowable
    {

        public void show()
        {
            // 清除显示数据
            Array.Clear(display_globle_define_8x8.g_display_data, 0, display_globle_define_8x8.g_display_data.Length);

            // 安全访问buttonList，防止索引越界
            for (int i = 0; i < Math.Min(display_globle_define_8x8.g_display_data.Length, Form1.buttonList.Count); i++)
            {
                if (Form1.buttonList[i] is Button btn)
                {
                    int r = btn.BackColor.R;
                    int g = btn.BackColor.G;
                    int b = btn.BackColor.B;

                    display_globle_define_8x8.g_display_data[i] = (UInt32)((r << 16) | (g << 8) | b);
                }
            }
        }

    }


    internal class display_number_8x8
    {

        // 数字0-9的3x5点阵字体定义
        private static readonly byte[,,] number_patterns = new byte[10, 5, 3]
        {
        { // 0
            {1,1,1},
            {1,0,1},
            {1,0,1},
            {1,0,1},
            {1,1,1}
        },
        { // 1
            {0,1,0},
            {1,1,0},
            {0,1,0},
            {0,1,0},
            {1,1,1}
        },
        { // 2
            {1,1,1},
            {0,0,1},
            {1,1,1},
            {1,0,0},
            {1,1,1}
        },
        { // 3
            {1,1,1},
            {0,0,1},
            {1,1,1},
            {0,0,1},
            {1,1,1}
        },
        { // 4
            {1,0,1},
            {1,0,1},
            {1,1,1},
            {0,0,1},
            {0,0,1}
        },
        { // 5
            {1,1,1},
            {1,0,0},
            {1,1,1},
            {0,0,1},
            {1,1,1}
        },
        { // 6
            {1,1,1},
            {1,0,0},
            {1,1,1},
            {1,0,1},
            {1,1,1}
        },
        { // 7
            {1,1,1},
            {0,0,1},
            {0,1,0},
            {0,1,0},
            {0,1,0}
        },
        { // 8
            {1,1,1},
            {1,0,1},
            {1,1,1},
            {1,0,1},
            {1,1,1}
        },
        { // 9
            {1,1,1},
            {1,0,1},
            {1,1,1},
            {0,0,1},
            {1,1,1}
        }
        };

        // 在指定位置显示数字
        // x,y: 显示区域左上角坐标
        // num: 要显示的数字(0-9)
        // color: 显示颜色
        public static void show(byte x, byte y, byte num, uint color)
        {
            if (num > 9) return; // 数字范围检查

            // 在指定3x5区域显示数字
            for (int i = 0; i < 5; i++)
            {
                for (int j = 0; j < 3; j++)
                {
                    if (number_patterns[num, 4 - i, j] == 1)
                    {
                        // 确保坐标在显示范围内
                        if ((x + j) < 8 && (y + i) < 8)
                        {
                            display_globle_define_8x8.g_display_data[(x + j) * 8 + (y + i)] = color;
                        }
                    }
                }
            }
        }
    }



    internal class display_music_spectrum : IShowable
    {
        // 幅度阈值(控制频谱灵敏度)
        private const uint FFT_SPECTRUM_LEV1_AMP = 300;
        private const uint FFT_SPECTRUM_LEV2_AMP = 1000;
        private const uint FFT_SPECTRUM_LEV3_AMP = 2200;
        private const uint FFT_SPECTRUM_LEV4_AMP = 3500;
        private const uint FFT_SPECTRUM_LEV5_AMP = 5000;
        private const uint FFT_SPECTRUM_MINMIN_AMP = 10;

        private const int DISPLAY_DESCENT_SPEED = 1;  // 下降速度
        private const int DISPLAY_WHITE_POINT_DESCENT_SPEED = 8; // 白点下降速度


        // 全局数据数组

        // 静态变量 (原C代码中的static变量)
        private static byte sensitivity = 3;
        private static byte state = 0;
        private static uint[] list_max_white_point = new uint[display_globle_define_8x8.DISPLAY_MAX_LINE_NUM];
        private static uint[] list_last_max_point = new uint[display_globle_define_8x8.DISPLAY_MAX_LIST_NUM];
        private static byte list_white_point_descent_time_cnt = 0;
        private static byte list_descent_time_cnt = 0;
        private static uint time_cnt = 0;


        // 灵敏度数组
        private static readonly uint[] sensitivity_arry = {
        FFT_SPECTRUM_LEV1_AMP,
        FFT_SPECTRUM_LEV2_AMP,
        FFT_SPECTRUM_LEV3_AMP,
        FFT_SPECTRUM_LEV4_AMP,
        FFT_SPECTRUM_LEV5_AMP
        };


        public void show()
        {
            // 频率分段定义 (Hz)
            int[] frequency_bands = { 60, 100, 200, 400, 800, 1500, 3000, 6000, 20000 };

            // 计算频率分辨率 (44100Hz / 1024点 ≈ 43Hz/bin)
            float frequency_resolution = 44100.0f / 1024.0f;

            // 计算每个频率段对应的FFT bin范围
            int[] bin_ranges = new int[9];
            for (int i = 0; i < frequency_bands.Length; i++)
            {
                bin_ranges[i] = (int)(frequency_bands[i] / frequency_resolution);
            }

            ushort list_unit_value = (ushort)(sensitivity_arry[sensitivity - 1] / (display_globle_define_8x8.DISPLAY_MAX_LIST_NUM - 1));

            uint temp_max = 0;
            byte temp_list_unit_num = 0;
            uint temp_unit_value = 0;

            // 清空显示数据
            Array.Clear(display_globle_define_8x8.g_display_data, 0, display_globle_define_8x8.g_display_data.Length);


            // 获取按键输入，up键增加阈值，down键减少阈值
            if (Form1.Instance.keyUpPressed)
            {
                sensitivity++;
                if (sensitivity > 5)
                {
                    sensitivity = 5;
                }
                state = 1; // 显示灵敏度数字
                Form1.Instance.keyUpPressed = false; // 重置按键状态
            }
            if (Form1.Instance.keyDownPressed)
            {
                sensitivity--;
                if (sensitivity < 1)
                {
                    sensitivity = 1;
                }
                state = 1; // 显示灵敏度数字
                Form1.Instance.keyDownPressed = false; // 重置按键状态
            }



            if (state != 0)
            {
                time_cnt++;
                if (time_cnt > 50)
                {
                    time_cnt = 0;
                    state = 0;
                }
                else
                {
                    display_number_8x8.show(3, 1, sensitivity, 0x0000FF);
                }
            }
            else
            {
                list_white_point_descent_time_cnt++;
                list_descent_time_cnt++;

                // 逐列处理 (8个频率段对应8列)
                for (byte i = 0; i < display_globle_define_8x8.DISPLAY_MAX_LINE_NUM; i++)
                {
                    // 获取当前频率段的bin范围
                    int start_bin = bin_ranges[i];
                    int end_bin = bin_ranges[i + 1];

                    // 在当前频率段内寻找最大幅度值
                    for (int j = start_bin; j < end_bin && j < display_global_define.g_fft_amp.Length; j++)
                    {
                        if (display_global_define.g_fft_amp[j] <= sensitivity_arry[sensitivity - 1] &&
                            display_global_define.g_fft_amp[j] >= FFT_SPECTRUM_MINMIN_AMP) // 忽略过大过小的数据
                        {
                            if (temp_max < display_global_define.g_fft_amp[j]) // 找到最大值
                            {
                                temp_max = display_global_define.g_fft_amp[j];
                            }
                        }
                    }

                    temp_list_unit_num = 0;

                    //// 根据不同列设置不同的单位值
                    //switch (i)
                    //{
                    //    case 0: temp_unit_value = list_unit_value * 8 / 20; break;
                    //    case 1: temp_unit_value = list_unit_value * 8 / 15; break;
                    //    case 2: temp_unit_value = list_unit_value * 8 / 12; break;
                    //    case 3: temp_unit_value = list_unit_value * 8 / 10; break;
                    //    case 4: temp_unit_value = list_unit_value; break;
                    //    case 5: temp_unit_value = list_unit_value; break;
                    //    case 6: temp_unit_value = list_unit_value; break;
                    //    case 7: temp_unit_value = list_unit_value; break;
                    //    default: break;
                    //}

                    temp_unit_value = list_unit_value;

                    if (temp_unit_value > 0)
                    {
                        temp_list_unit_num = (byte)(temp_max / temp_unit_value);
                    }

                    // 计算最大值需要占据一列中的单元数
                    temp_max = 0;

                    // 避免白点和频谱点超出范围
                    if (temp_list_unit_num > 6)
                    {
                        temp_list_unit_num = 6;
                    }

                    if (temp_list_unit_num > list_max_white_point[i])
                    {
                        list_max_white_point[i] = (uint)(temp_list_unit_num + 1); // 在最高点之上加一格,作为白点下降的起始位置
                    }

                    // 填充频率显示区域
                    for (byte j = 0; j < display_globle_define_8x8.DISPLAY_MAX_LINE_NUM; j++)
                    {
                        // 如果当前最高点低于上一次的最高点，则使用缓慢降落的最高点
                        if (temp_list_unit_num < list_last_max_point[i])
                        {
                            temp_list_unit_num = (byte)list_last_max_point[i];
                        }
                        else
                        {
                            list_last_max_point[i] = temp_list_unit_num;
                        }

                        if (j <= temp_list_unit_num)
                        {
                            display_globle_define_8x8.g_display_data[j + i * display_globle_define_8x8.DISPLAY_MAX_LINE_NUM] = music_spectrum_get_color.color(i, j);
                        }
                        else
                        {
                            display_globle_define_8x8.g_display_data[j + i * display_globle_define_8x8.DISPLAY_MAX_LINE_NUM] = 0;
                        }
                    }

                    // 列下降的速度
                    if (list_descent_time_cnt == DISPLAY_DESCENT_SPEED)
                    {
                        list_descent_time_cnt = 0;

                        for (int j = 0; j < display_globle_define_8x8.DISPLAY_MAX_LINE_NUM; j++)
                        {
                            if (list_last_max_point[j] > 0)
                            {
                                list_last_max_point[j]--;
                            }
                        }
                    }

                    // 每经过一个时间段，白点就下降一点
                    if (list_white_point_descent_time_cnt == DISPLAY_WHITE_POINT_DESCENT_SPEED)
                    {
                        list_white_point_descent_time_cnt = 0;

                        for (int j = 0; j < display_globle_define_8x8.DISPLAY_MAX_LINE_NUM; j++)
                        {
                            if (list_max_white_point[j] > 0)
                            {
                                list_max_white_point[j]--;
                            }
                        }
                    }

                    // 填充白点
                    if (list_max_white_point[i] < display_globle_define_8x8.DISPLAY_MAX_LINE_NUM)
                    {
                        display_globle_define_8x8.g_display_data[list_max_white_point[i] + i * display_globle_define_8x8.DISPLAY_MAX_LIST_NUM] = 0xFFFFFF; // 最高点填充白色
                    }
                }
            }
        }
    }


    internal class display_func_picture : IShowable
    {
        private static string imagePath = "";
        private static uint[,] imageData = new uint[8, 8]; // 8x8图像数据缓存
        private static bool imageLoaded = false;
        
        // 图像处理参数
        public static float Brightness { get; set; } = 1.0f;
        public static float Contrast { get; set; } = 2.5f;
        public static float Saturation { get; set; } = 1.3f;
        public static float Threshold { get; set; } = 0.5f;
        public static bool ForceBinarization { get; set; } = false;
        private static Bitmap resizedBitmap = null;
        
        // GIF动画相关变量
        private static List<uint[,]> gifFrames = new List<uint[,]>();
        private static int currentFrame = 0;
        private static int totalFrames = 0;
        private static bool isGif = false;
        private static DateTime lastFrameTime = DateTime.Now;
        private static int frameDelayMs = 100; // 默认帧延迟100ms
        private static object frameLock = new object(); // 线程安全锁

        // 设置图片路径并处理图片
        // 返回true表示图片已成功加载，false表示加载失败或用户取消
        public static bool SetImagePath(string path, bool showSettings = true)
        {
            imagePath = path;
            imageLoaded = false;
            isGif = false;
            gifFrames.Clear();
            currentFrame = 0;
            totalFrames = 0;
            
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
                // 无论之前是否加载图片，都需要重新处理以恢复默认参数效果
                if (imageLoaded)
                {
                    ReprocessImageWithCurrentParameters();
                }
                else
                {
                    LoadAndProcessImage();
                    imageLoaded = true; // 标记图片已加载
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

                // 无论之前是否加载图片，都需要重新处理以恢复默认参数效果
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

        // 加载并处理图片为8x8像素数据
        private static void LoadAndProcessImage()
        {
            if (string.IsNullOrEmpty(imagePath) || !File.Exists(imagePath))
            {
                return;
            }

            try
            {
                // 检查是否为GIF文件
                string extension = Path.GetExtension(imagePath).ToLower();
                isGif = (extension == ".gif");
                
                if (isGif)
                {
                    // 处理GIF动画
                    ProcessGifAnimation();
                }
                else
                {
                    // 处理静态图片
                    using (Bitmap originalImage = new Bitmap(imagePath))
                    {
                        ProcessSingleImage(originalImage);
                    }
                }
                
                imageLoaded = true;
            }
            catch (Exception ex)
            {
                // 图片加载失败，清空数据
                for (int x = 0; x < 8; x++)
                {
                    for (int y = 0; y < 8; y++)
                    {
                        imageData[x, y] = 0;
                    }
                }
                imageLoaded = false;
            }
        }
        
        // 处理GIF动画
        private static void ProcessGifAnimation()
        {
            gifFrames.Clear();
            
            using (Image gifImage = Image.FromFile(imagePath))
            {
                FrameDimension dimension = new FrameDimension(gifImage.FrameDimensionsList[0]);
                totalFrames = gifImage.GetFrameCount(dimension);
                
                // 获取帧延迟信息
                PropertyItem frameDelayItem = null;
                try
                {
                    frameDelayItem = gifImage.GetPropertyItem(0x5100); // PropertyTagFrameDelay
                }
                catch
                {
                    // 如果无法获取帧延迟，使用默认值
                }
                
                // 处理每一帧
                for (int frameIndex = 0; frameIndex < totalFrames; frameIndex++)
                {
                    gifImage.SelectActiveFrame(dimension, frameIndex);
                    
                    using (Bitmap frameBitmap = new Bitmap(gifImage))
                    {
                        uint[,] frameData = ProcessImageTo8x8(frameBitmap);
                        gifFrames.Add(frameData);
                    }
                    
                    // 设置帧延迟（如果有）
                    if (frameDelayItem != null && frameIndex < frameDelayItem.Value.Length / 4)
                    {
                        int delay = BitConverter.ToInt32(frameDelayItem.Value, frameIndex * 4) * 10; // 转换为毫秒
                        if (delay < 20) delay = 100; // 最小延迟保护
                        frameDelayMs = delay;
                    }
                }
            }
            
            currentFrame = 0;
            lastFrameTime = DateTime.Now;
        }
        
        // 处理单张图片
        private static void ProcessSingleImage(Bitmap originalImage)
        {
            imageData = ProcessImageTo8x8(originalImage);
        }
        
        // 通用图片处理到8x8
        private static uint[,] ProcessImageTo8x8(Bitmap sourceImage)
        {
            uint[,] result = new uint[8, 8];
            
            // 使用双线性插值缩放到8x8
            using (Bitmap resizedImage = new Bitmap(8, 8))
            {
                using (Graphics graphics = Graphics.FromImage(resizedImage))
                {
                    // 设置高质量插值模式
                    graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBilinear;
                    graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighQuality;
                    graphics.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.HighQuality;
                    graphics.CompositingQuality = System.Drawing.Drawing2D.CompositingQuality.HighQuality;
                    
                    // 绘制缩放后的图像
                    graphics.DrawImage(sourceImage, 0, 0, 8, 8);
                }
                
                // 缓存缩放后的图像用于实时预览
                resizedBitmap?.Dispose();
                resizedBitmap = new Bitmap(resizedImage);
                
                // 提取8x8像素数据
                for (int x = 0; x < 8; x++)
                {
                    for (int y = 0; y < 8; y++)
                    {
                        Color pixel = resizedImage.GetPixel(x, y);
                        
                        // 应用色彩增强和对比度调整
                        Color enhancedPixel = EnhanceColor(pixel);
                        
                        // 转换为RGB格式 (R<<16 | G<<8 | B)
                        result[x, y] = (uint)((enhancedPixel.R << 16) | (enhancedPixel.G << 8) | enhancedPixel.B);
                    }
                }
            }
            
            return result;
        }
        
        // 使用当前参数重新处理图像（用于实时预览）
        public static void ReprocessImageWithCurrentParameters()
        {
            if (resizedBitmap == null) return;
            
            // 重新处理8x8像素数据
            for (int x = 0; x < 8; x++)
            {
                for (int y = 0; y < 8; y++)
                {
                    Color pixel = resizedBitmap.GetPixel(x, y);
                    
                    // 应用色彩增强和对比度调整
                    Color enhancedPixel = EnhanceColor(pixel);
                    
                    // 转换为RGB格式 (R<<16 | G<<8 | B)
                    imageData[x, y] = (uint)((enhancedPixel.R << 16) | (enhancedPixel.G << 8) | enhancedPixel.B);
                }
            }
            
            // 如果是GIF动画，重新处理所有帧
            if (isGif && gifFrames.Count > 0)
            {
                ReprocessGifFrames();
            }
        }
        
        // 重新处理GIF动画的所有帧（使用当前参数）
        private static void ReprocessGifFrames()
        {
            if (string.IsNullOrEmpty(imagePath) || !File.Exists(imagePath)) return;
            
            try
            {
                using (Image gifImage = Image.FromFile(imagePath))
                {
                    FrameDimension dimension = new FrameDimension(gifImage.FrameDimensionsList[0]);
                    
                    // 重新处理每一帧
                    for (int frameIndex = 0; frameIndex < gifFrames.Count; frameIndex++)
                    {
                        gifImage.SelectActiveFrame(dimension, frameIndex);
                        
                        using (Bitmap frameBitmap = new Bitmap(gifImage))
                        {
                            // 重新处理当前帧
                            uint[,] reprocessedFrame = ProcessImageTo8x8(frameBitmap);
                            
                            // 更新GIF帧数据
                            for (int x = 0; x < 8; x++)
                            {
                                for (int y = 0; y < 8; y++)
                                {
                                    gifFrames[frameIndex][x, y] = reprocessedFrame[x, y];
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                // 处理失败，保持原有帧数据
            }
        }

        // 色彩增强函数 - 极化处理增强对比度
        private static Color EnhanceColor(Color original)
        {
            // 计算亮度 (使用标准亮度公式)
            float luminance = (0.299f * original.R + 0.587f * original.G + 0.114f * original.B) / 255.0f;

            // 应用亮度调整
            luminance = Math.Max(0.0f, Math.Min(1.0f, luminance * Brightness));

            // 强制二值化模式（直接返回黑白）
            if (ForceBinarization)
            {
                // 调试信息：输出二值化决策
                // Console.WriteLine($"二值化: 亮度={luminance:F3}, 阈值={Threshold:F3}, 结果={(luminance < Threshold ? "黑" : "白")}");
                return luminance < Threshold ? Color.FromArgb(0, 0, 0) : Color.FromArgb(255, 255, 255);
            }

            // 极化处理参数
            float threshold = Threshold;    // 亮度阈值
            float contrast = Contrast;      // 对比度增强系数
            float blackPoint = 0.05f;       // 黑点（避免完全黑色）
            float whitePoint = 0.95f;       // 白点（避免完全白色）

            // 应用极化处理
            float enhancedLuminance;
            if (luminance < threshold)
            {
                // 暗部区域：向黑色极化
                enhancedLuminance = blackPoint * Math.Max(0, luminance * contrast / threshold);
            }
            else
            {
                // 亮部区域：向白色极化
                float normalizedBright = (luminance - threshold) / (1.0f - threshold);
                enhancedLuminance = whitePoint + (1.0f - whitePoint) * Math.Min(1.0f, normalizedBright * contrast);
            }

            // 确保亮度在有效范围内
            enhancedLuminance = Math.Max(0.0f, Math.Min(1.0f, enhancedLuminance));

            // 如果是接近灰度的图像，进行特殊处理
            if (IsGrayscale(original))
            {
                // 灰度图像的极化处理
                if (enhancedLuminance < 0.3f)
                {
                    return Color.FromArgb(0, 0, 0); // 纯黑
                }
                else if (enhancedLuminance > 0.7f)
                {
                    return Color.FromArgb(255, 255, 255); // 纯白
                }
                else
                {
                    // 中间灰度根据原始亮度倾向选择黑白
                    return luminance < threshold ? Color.FromArgb(0, 0, 0) : Color.FromArgb(255, 255, 255);
                }
            }
            else
            {
                // 彩色图像：保持色相和饱和度，只调整亮度
                float hue, saturation, lightness;
                RgbToHsl(original.R, original.G, original.B, out hue, out saturation, out lightness);

                // 增强饱和度
                saturation = Math.Min(1.0f, saturation * Saturation);

                // 使用极化后的亮度
                lightness = enhancedLuminance;

                // 转换回RGB
                int r, g, b;
                HslToRgb(hue, saturation, lightness, out r, out g, out b);

                return Color.FromArgb(r, g, b);
            }
        }

        // 判断是否为灰度图像
        private static bool IsGrayscale(Color color)
        {
            int maxDiff = Math.Max(Math.Abs(color.R - color.G),
                         Math.Max(Math.Abs(color.G - color.B), Math.Abs(color.R - color.B)));
            return maxDiff < 30; // 允许小幅色彩偏差
        }

        // RGB转HSL
        private static void RgbToHsl(int r, int g, int b, out float h, out float s, out float l)
        {
            float rf = r / 255.0f;
            float gf = g / 255.0f;
            float bf = b / 255.0f;

            float max = Math.Max(rf, Math.Max(gf, bf));
            float min = Math.Min(rf, Math.Min(gf, bf));
            float delta = max - min;

            // 亮度
            l = (max + min) / 2.0f;

            if (delta == 0)
            {
                h = s = 0; // 灰色
            }
            else
            {
                // 饱和度
                s = l > 0.5f ? delta / (2.0f - max - min) : delta / (max + min);

                // 色相
                if (max == rf)
                    h = (gf - bf) / delta + (gf < bf ? 6 : 0);
                else if (max == gf)
                    h = (bf - rf) / delta + 2;
                else
                    h = (rf - gf) / delta + 4;

                h /= 6.0f;
            }
        }

        // HSL转RGB
        private static void HslToRgb(float h, float s, float l, out int r, out int g, out int b)
        {
            float rf, gf, bf;

            if (s == 0)
            {
                rf = gf = bf = l; // 灰色
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

        // 强制立即更新显示（用于实时预览）
        private static void ForceImmediateDisplayUpdate()
        {
            // 直接调用显示刷新逻辑，不等待定时器
            if (display_globle_define_8x8.g_display_func_index == 2) // 图片显示模式
            {
                // 更新显示数据
                for (int x = 0; x < display_globle_define_8x8.DISPLAY_MAX_LIST_NUM; x++)
                {
                    for (int imageY = 0; imageY < display_globle_define_8x8.DISPLAY_MAX_LINE_NUM; imageY++)
                    {
                        int displayIndex = x * display_globle_define_8x8.DISPLAY_MAX_LIST_NUM + (display_globle_define_8x8.DISPLAY_MAX_LINE_NUM - 1 - imageY);
                        display_globle_define_8x8.g_display_data[displayIndex] = imageData[x, imageY];
                    }
                }
                
                // 立即更新UI（不等待定时器），使用优化的更新方法
                Form1.Instance.BeginInvoke(new Action(() =>
                {
                    try
                    {
                        UpdateUIButtons();
                    }
                    catch (Exception ex)
                    {
                        // 忽略UI更新异常
                    }
                }));
            }
        }

        public void show()
        {
            if (!imageLoaded)
            {
                // 如果没有加载图片，显示空白
                Array.Clear(display_globle_define_8x8.g_display_data, 0, display_globle_define_8x8.g_display_data.Length);
                return;
            }

            // GIF动画处理 - 简单的时间控制
            if (isGif && gifFrames.Count > 0)
            {
                TimeSpan elapsed = DateTime.Now - lastFrameTime;
                if (elapsed.TotalMilliseconds >= frameDelayMs)
                {
                    currentFrame = (currentFrame + 1) % totalFrames;
                    lastFrameTime = DateTime.Now;
                }
            }

            // 正确的坐标映射：匹配按钮物理布局
            // 按钮命名：btn_x_y 中的y是逻辑行号（0在最下面，7在最上面）
            // 图像数据：imageData[x,y] 中的y是图像行号（0在最上面，7在最下面）
            for (int x = 0; x < display_globle_define_8x8.DISPLAY_MAX_LIST_NUM; x++) // 列
            {
                for (int imageY = 0; imageY < display_globle_define_8x8.DISPLAY_MAX_LINE_NUM; imageY++) // 图像行号（0在最上面）
                {
                    int displayIndex = x * display_globle_define_8x8.DISPLAY_MAX_LIST_NUM + (display_globle_define_8x8.DISPLAY_MAX_LINE_NUM - 1 - imageY); // Y轴翻转以匹配显示方向

                    if (isGif && gifFrames.Count > 0)
                    {
                        display_globle_define_8x8.g_display_data[displayIndex] = gifFrames[currentFrame][x, imageY];
                    }
                    else
                    {
                        display_globle_define_8x8.g_display_data[displayIndex] = imageData[x, imageY];
                    }
                }
            }
        }

        // 优化的UI按钮更新方法
        public static void UpdateUIButtons()
        {
            // 检查窗口是否最小化，如果是则跳过UI更新
            if (Form1.Instance.WindowState == FormWindowState.Minimized)
            {
                return;
            }
            
            // 批量处理所有按钮更新
            int buttonCount = Math.Min(Form1.buttonList.Count, display_globle_define_8x8.g_display_data.Length);
            
            for (int i = 0; i < buttonCount; i++)
            {
                // 只有当颜色确实发生变化时才更新，避免不必要的重绘
                Color newColor = display_refresh_8x8.ApplyBrightnessToColor(display_globle_define_8x8.g_display_data[i]);
                if (Form1.buttonList[i].BackColor != newColor)
                {
                    Form1.buttonList[i].BackColor = newColor;
                }
            }
        }
    }

}
            
