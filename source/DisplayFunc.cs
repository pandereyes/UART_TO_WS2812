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

    internal class display_globle_define
    {

        public static uint[] g_display_data = new uint[64]; // 假设8x8显示
        public static uint[] g_fft_amp = new uint[512];

        public const int DISPLAY_REFRESH_INTERVAL = 5;      // 屏幕刷新间隔5ms

        public const int DISPLAY_MAX_LIST_NUM = 8;          // 假设8列LED
        public const int DISPLAY_MAX_LINE_NUM = 8;          // 假设8行高度

        public static int g_display_func_index = 0; // 当前显示功能索引

    }



    public interface IShowable
    {
        void show();
    }


    internal class display_refresh
    {
        private List<IShowable> showList = new List<IShowable>();
        private System.Threading.Timer display_refresh_timer;
        private int ui_update_counter = 0; // UI更新计数器
        private volatile bool ui_updating = false; // UI更新标志


        private void display_func_init()
        {
            showList.Add(new display_func_draw());
            showList.Add(new display_music_spectrum());
            //showList.Add(new display_music_bar_spectrum());
            showList.Add(new display_func_picture());
            // 可以添加更多的显示功能类
        }

        public display_refresh()
        {
            display_func_init();
            // 创建高精度定时器，每5ms执行一次
            display_refresh_timer = new System.Threading.Timer(
                display_refresh_timer_cb,
                null,
                0, // 立即开始
                display_globle_define.DISPLAY_REFRESH_INTERVAL); // 间隔5ms
        }

        private void display_refresh_timer_cb(object state)
        {
            if (showList.Count > 0 && display_globle_define.g_display_func_index < showList.Count)
            {
                showList[display_globle_define.g_display_func_index].show();

                // 硬件LED更新 - 始终高频执行
                for (int i = 0; i < ws2812.WS2812_NUM; i++)
                {
                    ws2812.Instance.Ws2812SetColor(i, display_globle_define.g_display_data[i]);
                }

                // UI更新降频 - 每4次硬件更新才更新一次UI
                ui_update_counter++;
                if (ui_update_counter >= 4 && display_globle_define.g_display_func_index != 0 && !ui_updating)
                {
                    ui_update_counter = 0;
                    ui_updating = true;

                    // 异步更新UI，不阻塞定时器
                    Form1.Instance.BeginInvoke(new Action(() =>
                    {
                        try
                        {
                            for (int i = 0; i < ws2812.WS2812_NUM; i++)
                            {
                                uint colorValue = display_globle_define.g_display_data[i];

                                // UI显示使用原始颜色，灯板显示应用亮度控制
                                int r = (int)((colorValue >> 16) & 0xFF);
                                int g = (int)((colorValue >> 8) & 0xFF);
                                int b = (int)(colorValue & 0xFF);
                                Form1.buttonList[i].BackColor = Color.FromArgb(r, g, b);
                            }
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





    internal class display_func_draw : IShowable
    {

        public void show()
        {
            // 清除显示数据
            Array.Clear(display_globle_define.g_display_data, 0, display_globle_define.g_display_data.Length);

            for (int i = 0; i < Form1.buttonList.Count; i++)
            {
                Button btn = Form1.buttonList[i] as Button;
                int r = btn.BackColor.R;
                int g = btn.BackColor.G;
                int b = btn.BackColor.B;
                display_globle_define.g_display_data[i] = (UInt32)((r << 16) | (g << 8) | b);
                // ws2812.Instance.Ws2812SetColor(i, (UInt32)((g << 16) | (r << 8) | b));
                //ws2812.Instance.Ws2812SetColor(i, (UInt32)((r << 16) | (g << 8) | b));
            }
        }

    }


    internal class display_number
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
                            display_globle_define.g_display_data[(x + j) * 8 + (y + i)] = color;
                        }
                    }
                }
            }
        }
    }

    internal class music_spectrum_get_color
    {
        static byte last_line = 0;
        static int line_cnt = 7;
        static uint[] colour_arr = new uint[8]
        {
            0x33CCAE, 0x33CCAE, 0x33CCAE, 0x33CCAE,
            0x33CCAE, 0x33CCAE, 0x33CCAE, 0x33CCAE
        };
        static ushort time_cnt = 0;
        static byte select = 0;
        public static uint color(byte line, byte list)
        {
            byte colour_change_unit = 20;
            uint temp_r = 0, temp_g = 0, temp_b = 0;

            temp_r = ((colour_arr[line_cnt] & 0xFF0000) >> 16);
            temp_g = ((colour_arr[line_cnt] & 0x00FF00) >> 8);
            temp_b = (colour_arr[line_cnt] & 0x0000FF);

            // 行渐变，从右向左颜色逐渐变换
            if (last_line != line) // 不同行
            {
                last_line = line;
                time_cnt++;

                // 延时一段时间
                if (time_cnt == (500 / display_globle_define.DISPLAY_REFRESH_INTERVAL))
                {
                    time_cnt = 0;

                    line_cnt--;

                    if (line_cnt < 0)
                    {
                        line_cnt = 7;
                    }

                    switch (select)
                    {
                        case 0:  // 红变黄
                            temp_g += colour_change_unit;
                            if (temp_g > 0xFF)
                            {
                                temp_g = 0xFF;
                                select = 1;
                            }
                            break;
                        case 1:  // 黄变绿
                            if (temp_r <= colour_change_unit)
                            {
                                temp_r = 0;
                                select = 2;
                            }
                            else
                            {
                                temp_r -= colour_change_unit;
                            }
                            break;
                        case 2:  // 绿变青
                            temp_b += colour_change_unit;
                            if (temp_b > 0xFF)
                            {
                                temp_b = 0xFF;
                                select = 3;
                            }
                            break;
                        case 3:  // 青变蓝
                            if (temp_g <= colour_change_unit)
                            {
                                temp_g = 0;
                                select = 4;
                            }
                            else
                            {
                                temp_g -= colour_change_unit;
                            }
                            break;
                        case 4:  // 蓝变紫
                            temp_r += colour_change_unit;
                            if (temp_r > 0xFF)
                            {
                                temp_r = 0xFF;
                                select = 5;
                            }
                            break;
                        case 5:  // 紫变红
                            if (temp_b <= colour_change_unit)
                            {
                                temp_b = 0;
                                select = 0;
                            }
                            else
                            {
                                temp_b -= colour_change_unit;
                            }
                            break;
                    }

                    colour_arr[line_cnt] = (temp_r << 16) | (temp_g << 8) | (temp_b);
                }
            }
            return colour_arr[line];
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
        private static uint[] list_max_white_point = new uint[display_globle_define.DISPLAY_MAX_LINE_NUM];
        private static uint[] list_last_max_point = new uint[display_globle_define.DISPLAY_MAX_LIST_NUM];
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

            ushort list_unit_value = (ushort)(sensitivity_arry[sensitivity - 1] / (display_globle_define.DISPLAY_MAX_LIST_NUM - 1));

            uint temp_max = 0;
            byte temp_list_unit_num = 0;
            uint temp_unit_value = 0;

            // 清空显示数据
            Array.Clear(display_globle_define.g_display_data, 0, display_globle_define.g_display_data.Length);


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
                    display_number.show(3, 1, sensitivity, 0x0000FF);
                }
            }
            else
            {
                list_white_point_descent_time_cnt++;
                list_descent_time_cnt++;

                // 逐列处理 (8个频率段对应8列)
                for (byte i = 0; i < display_globle_define.DISPLAY_MAX_LINE_NUM; i++)
                {
                    // 获取当前频率段的bin范围
                    int start_bin = bin_ranges[i];
                    int end_bin = bin_ranges[i + 1];

                    // 在当前频率段内寻找最大幅度值
                    for (int j = start_bin; j < end_bin && j < display_globle_define.g_fft_amp.Length; j++)
                    {
                        if (display_globle_define.g_fft_amp[j] <= sensitivity_arry[sensitivity - 1] &&
                            display_globle_define.g_fft_amp[j] >= FFT_SPECTRUM_MINMIN_AMP) // 忽略过大过小的数据
                        {
                            if (temp_max < display_globle_define.g_fft_amp[j]) // 找到最大值
                            {
                                temp_max = display_globle_define.g_fft_amp[j];
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
                    for (byte j = 0; j < display_globle_define.DISPLAY_MAX_LINE_NUM; j++)
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
                            display_globle_define.g_display_data[j + i * display_globle_define.DISPLAY_MAX_LINE_NUM] = music_spectrum_get_color.color(i, j);
                        }
                        else
                        {
                            display_globle_define.g_display_data[j + i * display_globle_define.DISPLAY_MAX_LINE_NUM] = 0;
                        }
                    }

                    // 列下降的速度
                    if (list_descent_time_cnt == DISPLAY_DESCENT_SPEED)
                    {
                        list_descent_time_cnt = 0;

                        for (int j = 0; j < display_globle_define.DISPLAY_MAX_LINE_NUM; j++)
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

                        for (int j = 0; j < display_globle_define.DISPLAY_MAX_LINE_NUM; j++)
                        {
                            if (list_max_white_point[j] > 0)
                            {
                                list_max_white_point[j]--;
                            }
                        }
                    }

                    // 填充白点
                    if (list_max_white_point[i] < display_globle_define.DISPLAY_MAX_LINE_NUM)
                    {
                        display_globle_define.g_display_data[list_max_white_point[i] + i * display_globle_define.DISPLAY_MAX_LIST_NUM] = 0xFFFFFF; // 最高点填充白色
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
        
        // GIF动画相关变量
        private static List<uint[,]> gifFrames = new List<uint[,]>();
        private static int currentFrame = 0;
        private static int totalFrames = 0;
        private static bool isGif = false;
        private static DateTime lastFrameTime = DateTime.Now;
        private static int frameDelayMs = 100; // 默认帧延迟100ms
        private static object frameLock = new object(); // 线程安全锁

        // 设置图片路径并处理图片
        public static void SetImagePath(string path)
        {
            imagePath = path;
            imageLoaded = false;
            isGif = false;
            gifFrames.Clear();
            currentFrame = 0;
            totalFrames = 0;
            
            LoadAndProcessImage();
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

        // 色彩增强函数 - 极化处理增强对比度
        private static Color EnhanceColor(Color original)
        {
            // 计算亮度 (使用标准亮度公式)
            float luminance = (0.299f * original.R + 0.587f * original.G + 0.114f * original.B) / 255.0f;

            // 极化处理参数
            float threshold = 0.7f;    // 亮度阈值
            float contrast = 2.0f;     // 对比度增强系数
            float blackPoint = 0.05f;   // 黑点（避免完全黑色）
            float whitePoint = 0.95f;   // 白点（避免完全白色）

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

            // 如果是接近灰度的图像，直接映射到黑白
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
                saturation = Math.Min(1.0f, saturation * 1.3f);

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

        public void show()
        {
            if (!imageLoaded)
            {
                // 如果没有加载图片，显示空白
                Array.Clear(display_globle_define.g_display_data, 0, display_globle_define.g_display_data.Length);
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

            // 参考display_number方法的坐标映射：(x + j) * 8 + (y + i)
            // 这里x是列，y是行，对应LED矩阵的物理布局
            for (int x = 0; x < 8; x++) // 列
            {
                for (int y = 0; y < 8; y++) // 行
                {
                    // 注意：display_number使用 (4-i) 进行Y轴翻转
                    // 这里我们也需要考虑显示方向
                    int displayIndex = x * 8 + (7 - y); // Y轴翻转以匹配显示方向
                    
                    if (isGif && gifFrames.Count > 0)
                    {
                        display_globle_define.g_display_data[displayIndex] = gifFrames[currentFrame][x, y];
                    }
                    else
                    {
                        display_globle_define.g_display_data[displayIndex] = imageData[x, y];
                    }
                }
            }
        }
    }

}
            
