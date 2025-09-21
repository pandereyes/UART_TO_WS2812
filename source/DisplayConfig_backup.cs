using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Net.Http;
using System.Text.RegularExpressions;
using UART_TO_WS2812;

namespace UART_TO_WS2812
{
    public enum LEDBoardType
    {
        Board8x8,
        Board16x16,
        Board1x30,
        MonitorAmbiLight
    }

    // 灯带起始位置
    public enum LedStartPosition
    {
        TopLeft,        // 顶部左侧
        TopRight,       // 顶部右侧
        BottomLeft,     // 底部左侧
        BottomRight     // 底部右侧
    }

    // 灯带整体方向
    public enum LedDirection
    {
        Clockwise,          // 顺时针
        CounterClockwise    // 逆时针
    }

    // LED方向枚举
    public enum LEDDirection
    {
        LeftToRight,   // 从左到右（适用于顶部和底部）
        RightToLeft,   // 从右到左（适用于顶部和底部）
        TopToBottom,   // 从上到下（适用于左侧和右侧）
        BottomToTop    // 从下到上（适用于左侧和右侧）
    }

    public interface IShowable
    {
        void show();
    }

    /// <summary>
    /// 随机数工具类，提供类似C语言rand()的功能
    /// </summary>
    public static class RandomUtils
    {
        // 使用延迟初始化确保每次程序启动都有不同的随机性
        private static Random random = null;
        private static object lockObj = new object();

        /// <summary>
        /// 设置随机数种子（类似C语言的srand）
        /// </summary>
        /// <param name="seed">种子值</param>
        public static void SetSeed(int seed)
        {
            lock (lockObj)
            {
                random = new Random(seed);
            }
        }

        // 确保Random实例已初始化
        private static void EnsureInitialized()
        {
            if (random == null)
            {
                lock (lockObj)
                {
                    if (random == null)
                    {
                        // 使用当前时间作为默认种子，确保每次程序启动都有不同的随机性
                        random = new Random(Environment.TickCount);
                    }
                }
            }
        }

        /// <summary>
        /// 使用当前时间作为种子设置随机数生成器
        /// </summary>
        public static void SetTimeSeed()
        {
            SetSeed(Environment.TickCount);
        }

        /// <summary>
        /// 生成随机整数（类似C语言的rand）
        /// </summary>
        /// <returns>随机整数</returns>
        public static int Rand()
        {
            EnsureInitialized();
            lock (lockObj)
            {
                return random.Next();
            }
        }

        /// <summary>
        /// 生成指定范围内的随机整数
        /// </summary>
        /// <param name="min">最小值（包含）</param>
        /// <param name="max">最大值（不包含）</param>
        /// <returns>范围内的随机整数</returns>
        public static int RandRange(int min, int max)
        {
            EnsureInitialized();
            lock (lockObj)
            {
                return random.Next(min, max);
            }
        }

        /// <summary>
        /// 生成0.0到1.0之间的随机浮点数
        /// </summary>
        /// <returns>随机浮点数</returns>
        public static double RandDouble()
        {
            EnsureInitialized();
            lock (lockObj)
            {
                return random.NextDouble();
            }
        }

        /// <summary>
        /// 生成随机颜色
        /// </summary>
        /// <returns>随机RGB颜色值</returns>
        public static uint RandomColor()
        {
            EnsureInitialized();
            lock (lockObj)
            {
                byte r = (byte)random.Next(256);
                byte g = (byte)random.Next(256);
                byte b = (byte)random.Next(256);
                return (uint)((r << 16) | (g << 8) | b);
            }
        }

        /// <summary>
        /// 生成指定亮度范围内的随机颜色
        /// </summary>
        /// <param name="minBrightness">最小亮度（0-255）</param>
        /// <param name="maxBrightness">最大亮度（0-255）</param>
        /// <returns>随机RGB颜色值</returns>
        public static uint RandomColor(int minBrightness, int maxBrightness)
        {
            EnsureInitialized();
            lock (lockObj)
            {
                minBrightness = Math.Max(0, Math.Min(255, minBrightness));
                maxBrightness = Math.Max(0, Math.Min(255, maxBrightness));
                
                int brightness = random.Next(minBrightness, maxBrightness + 1);
                byte r = (byte)(brightness * random.NextDouble());
                byte g = (byte)(brightness * random.NextDouble());
                byte b = (byte)(brightness * random.NextDouble());
                
                return (uint)((r << 16) | (g << 8) | b);
            }
        }
    }

    public static class display_global_define
    {

        // 频谱幅度数据
        public static uint[] g_fft_amp = new uint[512];

        public const int DISPLAY_REFRESH_INTERVAL = 5;      // 屏幕刷新间隔5ms

        public static int g_display_func_index = 0;

        public static uint[] g_display_data = new uint[DisplayConfig.CurrentConfig.TotalLEDs]; // 显示缓冲区

        public static int DISPLAY_MAX_LIST_NUM => DisplayConfig.CurrentConfig.Height;
        public static int DISPLAY_MAX_LINE_NUM => DisplayConfig.CurrentConfig.Width;


        public static int[] frequence_bands_8x8 = { 60, 100, 200, 400, 800, 1500, 3000, 6000, 20000 };

        public static int[] frequence_bands_16x16 = { 50, 72, 106, 154, 224, 325, 473, 688, 1000, 1454, 2115, 3075, 4471, 6500, 9450, 13743, 20000 };

        public static void ReinitializeDisplayData()
        {
            g_display_data = new uint[DisplayConfig.CurrentConfig.TotalLEDs];
        }
    }


    public static class DisplayConfig
    {
        // 当前选择的灯板类型
        public static LEDBoardType CurrentBoardType { get; private set; } = LEDBoardType.Board16x16;
        
        // 灯板配置信息
        public static Dictionary<LEDBoardType, BoardConfig> BoardConfigs = new Dictionary<LEDBoardType, BoardConfig>
        {
            { LEDBoardType.Board8x8, new BoardConfig(8, 8, 64) },
            { LEDBoardType.Board16x16, new BoardConfig(16, 16, 256) },
            { LEDBoardType.Board1x30, new BoardConfig(1, 30, 30) },
            { LEDBoardType.MonitorAmbiLight, new AmbiLightConfig() }
        };

        // 环境光配置
        public static AmbiLightConfig AmbiLightSettings => (AmbiLightConfig)BoardConfigs[LEDBoardType.MonitorAmbiLight];

        // 获取当前灯板配置
        public static BoardConfig CurrentConfig => BoardConfigs[CurrentBoardType];

        // 切换灯板类型
        public static void SetBoardType(LEDBoardType boardType)
        {
            CurrentBoardType = boardType;
            // 切换后重新初始化显示数据数组
            display_global_define.ReinitializeDisplayData();
        }

        // 根据字符串选择灯板类型
        public static void SetBoardTypeFromString(string boardTypeStr)
        {
            switch (boardTypeStr)
            {
                case "8x8":
                    SetBoardType(LEDBoardType.Board8x8);
                    break;
                case "16x16":
                    SetBoardType(LEDBoardType.Board16x16);
                    break;
                case "1x30":
                    SetBoardType(LEDBoardType.Board1x30);
                    break;
                case "ambilight":
                    SetBoardType(LEDBoardType.MonitorAmbiLight);
                    break;
                default:
                    SetBoardType(LEDBoardType.Board8x8);
                    break;
            }
        }
    }

    // 环境光配置类
    public class AmbiLightConfig : BoardConfig 
    {
        public int TopLEDs { get; set; } = 30;     // 顶部LED数量
        public int BottomLEDs { get; set; } = 30;   // 底部LED数量
        public int LeftLEDs { get; set; } = 20;     // 左侧LED数量
        public int RightLEDs { get; set; } = 20;    // 右侧LED数量
        public int SampleInterval { get; set; } = 50; // 采样间隔(ms)
        public int SampleWidth { get; set; } = 20;   // 采样宽度(像素)
        
        // 起始位置配置（相对于各边的像素偏移）
        public int TopStartPos { get; set; } = 0;     // 顶部起始位置像素偏移
        public int BottomStartPos { get; set; } = 0;   // 底部起始位置像素偏移
        public int LeftStartPos { get; set; } = 0;     // 左侧起始位置像素偏移
        public int RightStartPos { get; set; } = 0;    // 右侧起始位置像素偏移
        
        // 灯带布局配置
        public LedStartPosition StartPosition { get; set; } = LedStartPosition.TopLeft; // 起始位置
        public LedDirection Direction { get; set; } = LedDirection.Clockwise;           // 整体方向
        
        // LED方向配置
        public LEDDirection TopDirection { get; set; } = LEDDirection.LeftToRight;     // 顶部LED方向（左到右）
        public LEDDirection RightDirection { get; set; } = LEDDirection.TopToBottom;   // 右侧LED方向（上到下）
        public LEDDirection BottomDirection { get; set; } = LEDDirection.RightToLeft;  // 底部LED方向（右到左）
        public LEDDirection LeftDirection { get; set; } = LEDDirection.BottomToTop;    // 左侧LED方向（下到上）

        public AmbiLightConfig() : base(1, 100, 100) // 宽度1，高度100，总数100个LED
        {
            // 根据四边LED数量计算总数
            UpdateTotalLEDs();
        }

        // 更新LED配置
        public void UpdateConfig(int top, int bottom, int left, int right)
        {
            TopLEDs = top;
            BottomLEDs = bottom;
            LeftLEDs = left;
            RightLEDs = right;
            UpdateTotalLEDs();
            // 重新初始化显示数据数组
            display_global_define.ReinitializeDisplayData();
        }

        // 更新LED方向配置
        public void UpdateDirections(LEDDirection topDir, LEDDirection rightDir, LEDDirection bottomDir, LEDDirection leftDir)
        {
            TopDirection = topDir;
            RightDirection = rightDir;
            BottomDirection = bottomDir;
            LeftDirection = leftDir;
        }

        // 更新布局配置
        public void UpdateLayoutConfig(LedStartPosition startPos, LedDirection dir)
        {
            StartPosition = startPos;
            Direction = dir;
        }

        // 更新起始位置配置
        public void UpdateStartPositions(int topStart, int bottomStart, int leftStart, int rightStart)
        {
            TopStartPos = topStart;
            BottomStartPos = bottomStart;
            LeftStartPos = leftStart;
            RightStartPos = rightStart;
        }

        // 更新完整配置（数量和方向）
        public void UpdateFullConfig(int top, int bottom, int left, int right, 
                                   LEDDirection topDir, LEDDirection rightDir, LEDDirection bottomDir, LEDDirection leftDir,
                                   LedStartPosition startPos = LedStartPosition.TopLeft, LedDirection dir = LedDirection.Clockwise)
        {
            UpdateConfig(top, bottom, left, right);
            UpdateDirections(topDir, rightDir, bottomDir, leftDir);
            UpdateLayoutConfig(startPos, dir);
        }

        // 更新完整配置（包含起始位置）
        public void UpdateFullConfigWithPositions(int top, int bottom, int left, int right, 
                                                int topStart, int bottomStart, int leftStart, int rightStart,
                                                LEDDirection topDir, LEDDirection rightDir, LEDDirection bottomDir, LEDDirection leftDir,
                                                LedStartPosition startPos = LedStartPosition.TopLeft, LedDirection dir = LedDirection.Clockwise)
        {
            UpdateConfig(top, bottom, left, right);
            UpdateStartPositions(topStart, bottomStart, leftStart, rightStart);
            UpdateDirections(topDir, rightDir, bottomDir, leftDir);
            UpdateLayoutConfig(startPos, dir);
        }

        // 更新总LED数量
        private void UpdateTotalLEDs()
        {
            TotalLEDs = TopLEDs + BottomLEDs + LeftLEDs + RightLEDs;
        }
    }

    public class BoardConfig
    {
        public int Width { get; }
        public int Height { get; }
        public int TotalLEDs { get; protected set; }
        public string DisplayName { get; }

        public BoardConfig(int width, int height, int totalLEDs)
        {
            Width = width;
            Height = height;
            TotalLEDs = totalLEDs;
            DisplayName = $"{width}x{height}";
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
                        if ((x + j) < DisplayConfig.CurrentConfig.Width && (y + i) < DisplayConfig.CurrentConfig.Height)
                        {
                            display_global_define.g_display_data[(x + j) * DisplayConfig.CurrentConfig.Width + (y + i)] = color;
                        }
                    }
                }
            }
        }
    }




// 通用的音乐频谱颜色生成器，兼容8x8,16x16以及1x30灯板
internal class music_spectrum_get_color
    {
        static byte last_line = 0;
        static int line_cnt = DisplayConfig.CurrentConfig.Height - 1;
        static uint[] colour_arr = new uint[32]
        {
            0x33CCAE, 0x33CCAE, 0x33CCAE, 0x33CCAE,
            0x33CCAE, 0x33CCAE, 0x33CCAE, 0x33CCAE,
            0x33CCAE, 0x33CCAE, 0x33CCAE, 0x33CCAE,
            0x33CCAE, 0x33CCAE, 0x33CCAE, 0x33CCAE,
            0x33CCAE, 0x33CCAE, 0x33CCAE, 0x33CCAE,
            0x33CCAE, 0x33CCAE, 0x33CCAE, 0x33CCAE,
            0x33CCAE, 0x33CCAE, 0x33CCAE, 0x33CCAE,
            0x33CCAE, 0x33CCAE, 0x33CCAE, 0x33CCAE
        };

        static int period = 500;


        static ushort time_cnt = 0;
        static byte select = 0;
        public static uint color(byte line, byte list)
        {
            byte colour_change_unit = 20;
            uint temp_r = 0, temp_g = 0, temp_b = 0;

            temp_r = ((colour_arr[line_cnt] & 0xFF0000) >> 16);
            temp_g = ((colour_arr[line_cnt] & 0x00FF00) >> 8);
            temp_b = (colour_arr[line_cnt] & 0x0000FF);


            if (DisplayConfig.CurrentBoardType == LEDBoardType.Board8x8)
            {
                period = 500;
            }
            else if (DisplayConfig.CurrentBoardType == LEDBoardType.Board16x16)
            {
                period = 300;
            }

            else if (DisplayConfig.CurrentBoardType == LEDBoardType.Board1x30)
            {
                period = 500;
            }

            // 行渐变，从右向左颜色逐渐变换
            if (last_line != line) // 不同行
            {
                last_line = line;
                time_cnt++;

                // 延时一段时间
                if (time_cnt == (period / display_global_define.DISPLAY_REFRESH_INTERVAL))
                {
                    time_cnt = 0;

                    line_cnt--;

                    if (line_cnt < 0)
                    {
                        line_cnt = DisplayConfig.CurrentConfig.Height - 1;
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

    internal class display_refresh
    {
        public static List<IShowable> showList = new List<IShowable>();
        private static System.Threading.Timer display_refresh_timer;
        private int ui_update_counter = 0;          // UI更新计数器
        private volatile bool ui_updating = false;  // UI更新标志

        // 应用与灯板相同的亮度转换
        // UI亮度级别（由Form1设置）
        public static int UIBrightnessLevel { get; set; } = 90; // 默认90%
        
        public static Color ApplyBrightnessToColor(uint rgbColor)
        {
            // 使用静态的UI亮度级别
            int brightnessLevel = UIBrightnessLevel;

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

        public static void display_showlist_add(IShowable item)
        {
            showList.Add(item);
        }

        public static void display_showlist_clear()
        {
            showList.Clear();
        }


        public display_refresh()
        {
            display_refresh_timer = new System.Threading.Timer(
               display_refresh_timer_cb,
               null,
               0, // 立即开始
               display_global_define.DISPLAY_REFRESH_INTERVAL); // 间隔5ms
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
            if (showList.Count > 0 && display_global_define.g_display_func_index < showList.Count)
            {
                
                if (showList.Count > display_global_define.g_display_func_index)
                {
                    showList[display_global_define.g_display_func_index].show();
                }
                else
                {
                    return;
                }
                

                // 硬件LED更新 - 始终高频执行
                for (int i = 0; i < Math.Min(DisplayConfig.CurrentConfig.TotalLEDs, display_global_define.g_display_data.Length); i++)
                {
                    Form1.Instance.ws2812Instance.Ws2812SetColor(i, display_global_define.g_display_data[i]);
                }

                // UI更新降频 - 根据窗口状态动态调整更新频率
                ui_update_counter++;

                // 动态调整UI更新频率：窗口最小化时完全跳过UI更新，正常显示时降低频率
                int updateThreshold = 4; // 正常显示时每4次硬件更新更新一次UI

                // 如果窗口最小化，完全跳过UI更新
                if (Form1.Instance.WindowState != FormWindowState.Minimized)
                {
                    // 像素画板功能和环境光功能不适用这里的UI更新
                    if ((DisplayConfig.CurrentBoardType != LEDBoardType.Board1x30 && display_global_define.g_display_func_index == 0) ||
                        DisplayConfig.CurrentBoardType == LEDBoardType.MonitorAmbiLight)
                    {
                        return;
                    }

                    if (ui_update_counter >= updateThreshold && !ui_updating)
                    {
                        ui_update_counter = 0;
                        ui_updating = true;

                        // 异步更新UI，使用优化的批量更新方法
                        Form1.Instance.BeginInvoke(new Action(() =>
                        {
                            try
                            {
                                // 批量更新UI，减少BeginInvoke内部的开销
                                UpdateUIButtons();
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

        public static void UpdateUIButtons()
        {
            // 检查窗口是否最小化，如果是则跳过UI更新
            if (Form1.Instance.WindowState == FormWindowState.Minimized)
            {
                return;
            }

            // 环境光模式下不需要更新UI按钮
            if (DisplayConfig.CurrentBoardType == LEDBoardType.MonitorAmbiLight)
            {
                return;
            }

            // 批量处理所有按钮更新
            int buttonCount = Math.Min(Form1.buttonList.Count, DisplayConfig.CurrentConfig.TotalLEDs);

            for (int i = 0; i < buttonCount; i++)
            {
                // 只有当颜色确实发生变化时才更新，避免不必要的重绘
                Color newColor = ApplyBrightnessToColor(display_global_define.g_display_data[i]);
                if (Form1.buttonList[i].BackColor != newColor)
                {
                    Form1.buttonList[i].BackColor = newColor;
                }
            }
        }

        private static display_ambilight_init ambiLightInit = null;
        
        // 更新环境光配置
        public static void UpdateAmbiLightConfig()
        {
            if (DisplayConfig.CurrentBoardType == LEDBoardType.MonitorAmbiLight && ambiLightInit != null)
            {
                // 重新初始化显示数据数组
                display_global_define.ReinitializeDisplayData();
                
                // 重新初始化WS2812驱动的LED数组
                if (Form1.Instance?.ws2812Instance != null)
                {
                    Form1.Instance.ws2812Instance.ReinitializeLEDArrays();
                }
                
                // 更新环境光配置
                ambiLightInit.UpdateConfig();
            }
        }

        public static void InitShowList()
        {
            display_refresh.display_showlist_clear();
            if (ambiLightInit != null)
            {
                ambiLightInit.Dispose();
                ambiLightInit = null;
            }

            if (DisplayConfig.CurrentBoardType == LEDBoardType.Board8x8)
            {
                // 初始化8x8显示功能列表
                display_8x8_init displayInit = new display_8x8_init();
                displayInit.showlist_init();
            }
            else if (DisplayConfig.CurrentBoardType == LEDBoardType.Board16x16)
            {
                // 初始化16x16显示功能列表
                display_16x16_init displayInit = new display_16x16_init();
                displayInit.showlist_init();
            }
            else if (DisplayConfig.CurrentBoardType == LEDBoardType.Board1x30)
            {
                // 初始化1x30显示功能列表
                display_1x30_init displayInit = new display_1x30_init();
                displayInit.showlist_init();
            }
            else if (DisplayConfig.CurrentBoardType == LEDBoardType.MonitorAmbiLight)
            {
                // 初始化环境光显示功能列表
                ambiLightInit = new display_ambilight_init();
                ambiLightInit.showlist_init();
            }
            else
            {
                // 初始化8x8显示功能列表
                display_8x8_init displayInit = new display_8x8_init();
                displayInit.showlist_init();
            }
        }

    }

    internal class display_func_draw : IShowable
    {
        public void show()
        {
            // 清除显示数据
            Array.Clear(display_global_define.g_display_data, 0, display_global_define.g_display_data.Length);

            // 安全访问buttonList，防止索引越界
            for (int i = 0; i < Math.Min(display_global_define.g_display_data.Length, Form1.buttonList.Count); i++)
            {
                if (Form1.buttonList[i] is Button btn)
                {
                    int r = btn.BackColor.R;
                    int g = btn.BackColor.G;
                    int b = btn.BackColor.B;

                    display_global_define.g_display_data[i] = (UInt32)((r << 16) | (g << 8) | b);
                }
            }
        }
    }


    


    internal class display_func_music_spectrum : IShowable
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
        private static byte sensitivity = 1;
        private static byte state = 0;
        private static uint[] list_max_white_point = new uint[display_global_define.DISPLAY_MAX_LINE_NUM];
        private static uint[] list_last_max_point = new uint[display_global_define.DISPLAY_MAX_LIST_NUM];
        private static byte list_white_point_descent_time_cnt = 0;
        private static byte list_descent_time_cnt = 0;
        private static uint time_cnt = 0;

        private static int[] frequency_bands;

        public static void set_frequency_bands(int[] data)
        {
            frequency_bands = data;
        }

        public static void change_sensitivity()
        {
            sensitivity++;
            if (sensitivity > 5)
            {
                sensitivity = 1;
            }
            state = 1;
        }


        // 灵敏度数组
        private static readonly uint[] sensitivity_arry = {
        FFT_SPECTRUM_LEV1_AMP,
        FFT_SPECTRUM_LEV2_AMP,
        FFT_SPECTRUM_LEV3_AMP,
        FFT_SPECTRUM_LEV4_AMP,
        FFT_SPECTRUM_LEV5_AMP
        };




        public static void init_para()
        {
            list_max_white_point = new uint[display_global_define.DISPLAY_MAX_LINE_NUM];
            list_last_max_point = new uint[display_global_define.DISPLAY_MAX_LIST_NUM];
        }


        public void show()
        {

            // 计算频率分辨率 (44100Hz / 1024点 ≈ 43Hz/bin)
            float frequency_resolution = 44100.0f / 1024.0f;

            // 计算每个频率段对应的FFT bin范围
            int[] bin_ranges = new int[DisplayConfig.CurrentConfig.Width + 1];
            for (int i = 0; i < frequency_bands.Length; i++)
            {
                bin_ranges[i] = (int)(frequency_bands[i] / frequency_resolution);
            }

            ushort list_unit_value = (ushort)(sensitivity_arry[sensitivity - 1] / (display_global_define.DISPLAY_MAX_LIST_NUM - 1));

            uint temp_max = 0;
            byte temp_list_unit_num = 0;
            uint temp_unit_value = 0;

            // 清空显示数据
            Array.Clear(display_global_define.g_display_data, 0, display_global_define.g_display_data.Length);


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
                    if (DisplayConfig.CurrentBoardType == LEDBoardType.Board8x8)
                    {
                        display_number.show(3, 1, sensitivity, 0x0000FF);
                    }
                    else if (DisplayConfig.CurrentBoardType == LEDBoardType.Board16x16)
                    {
                        display_number.show(8, 5, sensitivity, 0x0000FF);
                    }                    
                }
            }
            else
            {
                list_white_point_descent_time_cnt++;
                list_descent_time_cnt++;

                // 逐列处理 (16个频率段对应16列)
                for (byte i = 0; i < display_global_define.DISPLAY_MAX_LINE_NUM; i++)
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

                    temp_unit_value = list_unit_value;

                    if (temp_unit_value > 0)
                    {
                        temp_list_unit_num = (byte)(temp_max / temp_unit_value);
                    }

                    // 计算最大值需要占据一列中的单元数
                    temp_max = 0;

                    // 避免白点和频谱点超出范围
                    if (temp_list_unit_num > 14)
                    {
                        temp_list_unit_num = 14;
                    }

                    if (temp_list_unit_num > list_max_white_point[i])
                    {
                        list_max_white_point[i] = (uint)(temp_list_unit_num + 1); // 在最高点之上加一格,作为白点下降的起始位置
                    }

                    // 填充频率显示区域
                    for (byte j = 0; j < display_global_define.DISPLAY_MAX_LINE_NUM; j++)
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
                            display_global_define.g_display_data[j + i * display_global_define.DISPLAY_MAX_LINE_NUM] = music_spectrum_get_color.color(i, j);
                        }
                        else
                        {
                            display_global_define.g_display_data[j + i * display_global_define.DISPLAY_MAX_LINE_NUM] = 0;
                        }
                    }

                    // 列下降的速度
                    if (list_descent_time_cnt == DISPLAY_DESCENT_SPEED)
                    {
                        list_descent_time_cnt = 0;

                        for (int j = 0; j < display_global_define.DISPLAY_MAX_LINE_NUM; j++)
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

                        for (int j = 0; j < display_global_define.DISPLAY_MAX_LINE_NUM; j++)
                        {
                            if (list_max_white_point[j] > 0)
                            {
                                list_max_white_point[j]--;
                            }
                        }
                    }

                    // 填充白点
                    if (list_max_white_point[i] < display_global_define.DISPLAY_MAX_LINE_NUM)
                    {
                        display_global_define.g_display_data[list_max_white_point[i] + i * display_global_define.DISPLAY_MAX_LIST_NUM] = 0xFFFFFF; // 最高点填充白色
                    }
                }
            }
        }
    }

    internal class display_func_picture : IShowable
    {
        private static string imagePath = "";
        private static uint[,] imageData = new uint[16, 16]; // 8x8图像数据缓存
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

        // 加载并处理图片为16x16像素数据
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
                for (int x = 0; x < DisplayConfig.CurrentConfig.Width; x++)
                {
                    for (int y = 0; y < DisplayConfig.CurrentConfig.Height; y++)
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
                        uint[,] frameData = ProcessImageTo16x16(frameBitmap);
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
            imageData = ProcessImageTo16x16(originalImage);
        }

        // 通用图片处理到16x16
        private static uint[,] ProcessImageTo16x16(Bitmap sourceImage)
        {
            uint[,] result = new uint[DisplayConfig.CurrentConfig.Width, DisplayConfig.CurrentConfig.Height];

            // 使用双线性插值缩放到8x8
            using (Bitmap resizedImage = new Bitmap(DisplayConfig.CurrentConfig.Width, DisplayConfig.CurrentConfig.Height))
            {
                using (Graphics graphics = Graphics.FromImage(resizedImage))
                {
                    // 设置高质量插值模式
                    graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBilinear;
                    graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighQuality;
                    graphics.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.HighQuality;
                    graphics.CompositingQuality = System.Drawing.Drawing2D.CompositingQuality.HighQuality;

                    // 绘制缩放后的图像
                    graphics.DrawImage(sourceImage, 0, 0, DisplayConfig.CurrentConfig.Width, DisplayConfig.CurrentConfig.Height);
                }

                // 缓存缩放后的图像用于实时预览
                resizedBitmap?.Dispose();
                resizedBitmap = new Bitmap(resizedImage);

                // 提取8x8像素数据
                for (int x = 0; x < DisplayConfig.CurrentConfig.Width; x++)
                {
                    for (int y = 0; y < DisplayConfig.CurrentConfig.Height; y++)
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
            for (int x = 0; x < DisplayConfig.CurrentConfig.Width; x++)
            {
                for (int y = 0; y < DisplayConfig.CurrentConfig.Height; y++)
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
                            uint[,] reprocessedFrame = ProcessImageTo16x16(frameBitmap);

                            // 更新GIF帧数据
                            for (int x = 0; x < DisplayConfig.CurrentConfig.Width; x++)
                            {
                                for (int y = 0; y < DisplayConfig.CurrentConfig.Height; y++)
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
            if (display_global_define.g_display_func_index == 2) // 图片显示模式
            {
                // 更新显示数据
                for (int x = 0; x < display_global_define.DISPLAY_MAX_LIST_NUM; x++)
                {
                    for (int imageY = 0; imageY < display_global_define.DISPLAY_MAX_LINE_NUM; imageY++)
                    {
                        int displayIndex = x * display_global_define.DISPLAY_MAX_LIST_NUM + (display_global_define.DISPLAY_MAX_LINE_NUM - 1 - imageY);
                        display_global_define.g_display_data[displayIndex] = imageData[x, imageY];
                    }
                }

                // 立即更新UI（不等待定时器），使用优化的更新方法
                Form1.Instance.BeginInvoke(new Action(() =>
                {
                    try
                    {
                        display_refresh.UpdateUIButtons();
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
                Array.Clear(display_global_define.g_display_data, 0, display_global_define.g_display_data.Length);
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
            for (int x = 0; x < display_global_define.DISPLAY_MAX_LIST_NUM; x++) // 列
            {
                for (int imageY = 0; imageY < display_global_define.DISPLAY_MAX_LINE_NUM; imageY++) // 图像行号（0在最上面）
                {
                    int displayIndex = x * display_global_define.DISPLAY_MAX_LIST_NUM + (display_global_define.DISPLAY_MAX_LINE_NUM - 1 - imageY); // Y轴翻转以匹配显示方向

                    if (isGif && gifFrames.Count > 0)
                    {
                        display_global_define.g_display_data[displayIndex] = gifFrames[currentFrame][x, imageY];
                    }
                    else
                    {
                        display_global_define.g_display_data[displayIndex] = imageData[x, imageY];
                    }
                }
            }
        }
    }

    public class random_for_tick
    {
        // 使用线程安全的Random实例，确保每次调用都有不同的随机性
        [ThreadStatic]
        private static Random ra;
        private static object lockObj = new object();

        public static int get_random(int limit)
        {
            // 确保每个线程都有独立的Random实例
            if (ra == null)
            {
                lock (lockObj)
                {
                    if (ra == null)
                    {
                        // 使用时间戳和线程ID作为种子，确保随机性
                        int seed = Environment.TickCount ^ Thread.CurrentThread.ManagedThreadId;
                        ra = new Random(seed);
                    }
                }
            }
            return ra.Next(limit);
        }
    }


    internal class display_func_code_rain : IShowable
    {
        // 代码雨效果参数
        private int canvas_upper_length = 0;

        // 每列的代码雨状态
        private int[] list_descent_time_cnt;          // 下降时间计数器
        private int[] list_descent_speed;            // 下降速度
        private int[] list_head_position;             // 雨滴头部位置
        private uint[] list_color;                    // 雨滴颜色
        private int[] list_len;                      // 雨滴长度 3-5

        private byte state = 0;
        private uint[] temp_display_data;

        public display_func_code_rain()
        {
            InitializeArrays();
        }

        private void InitializeArrays()
        {
            int maxColumns = DisplayConfig.CurrentConfig.Width;
            
            list_descent_time_cnt = new int[maxColumns];
            list_descent_speed = new int[maxColumns];
            list_head_position = new int[maxColumns];
            list_color = new uint[maxColumns];
            list_len = new int[maxColumns];
            
            temp_display_data = new uint[DisplayConfig.CurrentConfig.TotalLEDs];
        }

        // 初始化单个雨滴列
        private void InitRainColumn(int columnIndex)
        {
            
            if (DisplayConfig.CurrentBoardType == LEDBoardType.Board8x8)
            {
                canvas_upper_length = 5;
                // 初始化下降速度 (3-32)
                list_descent_speed[columnIndex] = random_for_tick.get_random(12) + 1;

                // 初始化雨滴头部位置 (0到canvas_upper_length-1)
                list_head_position[columnIndex] = random_for_tick.get_random(canvas_upper_length);

                // 雨滴长度 (4-6)
                list_len[columnIndex] = random_for_tick.get_random(3) + 4;
            }
            else if (DisplayConfig.CurrentBoardType == LEDBoardType.Board16x16)
            {
                canvas_upper_length = 9;
                // 初始化下降速度 (3-32)
                list_descent_speed[columnIndex] = random_for_tick.get_random(5) + 1;

                // 初始化雨滴头部位置 (0到canvas_upper_length-1)
                list_head_position[columnIndex] = random_for_tick.get_random(canvas_upper_length);

                // 雨滴长度 (4-6)
                list_len[columnIndex] = random_for_tick.get_random(6) + 4;
            }


            // 初始化颜色 - 绿色代码雨效果
            list_color[columnIndex] = 0x00FF00;

            
        }

        // 设置亮度级别 (简化版本)
        private uint SetBrightnessLevel(uint color, int level)
        {
            // 简单的亮度调整：根据level降低亮度
            byte r = (byte)((color >> 16) & 0xFF);
            byte g = (byte)((color >> 8) & 0xFF);
            byte b = (byte)(color & 0xFF);

            float brightness = 0;
            // 亮度递减：头部最亮，尾部渐暗
            if (DisplayConfig.CurrentBoardType == LEDBoardType.Board8x8)
            {
                brightness = 1.0f - (level * 0.3f);
                brightness = Math.Max(0.05f, brightness);
            }
            else if (DisplayConfig.CurrentBoardType == LEDBoardType.Board16x16)
            {
                brightness = 1.0f - (level * 0.15f);
                brightness = Math.Max(0.05f, brightness);
            }

            r = (byte)(r * brightness);
            g = (byte)(g * brightness);
            b = (byte)(b * brightness);

            return (uint)((r << 16) | (g << 8) | b);
        }

        public void show()
        {
            int totalColumns = DisplayConfig.CurrentConfig.Width;
            int totalRows = DisplayConfig.CurrentConfig.Height;

            // 初始化状态
            if (state == 0)
            {
                Array.Clear(temp_display_data, 0, temp_display_data.Length);
                Array.Clear(list_descent_speed, 0, list_descent_speed.Length);
                Array.Clear(list_descent_time_cnt, 0, list_descent_time_cnt.Length);

                for (int i = 0; i < totalColumns; i++)
                {
                    InitRainColumn(i);
                }
                state = 1;
            }
            else  // 开始下降
            {
                for (int i = 0; i < totalColumns; i++)
                {
                    list_descent_time_cnt[i]++;
                    
                    if (list_descent_time_cnt[i] == list_descent_speed[i]) // 达到下降时间，下降一格
                    {
                        list_descent_time_cnt[i] = 0;
                        list_head_position[i]++;

                        // 清除当前列的所有像素
                        for (int j = 0; j < totalRows; j++)
                        {
                            int pixelIndex = i * totalRows + j;
                            if (pixelIndex < temp_display_data.Length)
                            {
                                temp_display_data[pixelIndex] = 0;
                            }
                        }

                        // 绘制雨滴效果
                        // 头部
                        if (list_head_position[i] >= canvas_upper_length && 
                            list_head_position[i] < (canvas_upper_length + totalRows))
                        {
                            int displayPosition = list_head_position[i] - canvas_upper_length;
                            if (displayPosition < totalRows)
                            {
                                int pixelIndex = i * totalRows + (totalRows - 1 - displayPosition);
                                if (pixelIndex < temp_display_data.Length)
                                {
                                    temp_display_data[pixelIndex] = SetBrightnessLevel(list_color[i], 0);
                                }
                            }
                        }

                        // 身体部分
                        for (int j = 0; j < (list_len[i] - 1); j++)
                        {
                            int bodyPosition = list_head_position[i] - j - 1;
                            if (bodyPosition >= canvas_upper_length && 
                                bodyPosition < (canvas_upper_length + totalRows))
                            {
                                int displayPosition = bodyPosition - canvas_upper_length;
                                if (displayPosition < totalRows)
                                {
                                    int pixelIndex = i * totalRows + (totalRows - 1 - displayPosition);
                                    if (pixelIndex < temp_display_data.Length)
                                    {
                                        temp_display_data[pixelIndex] = SetBrightnessLevel(list_color[i], j + 1);
                                    }
                                }
                            }
                        }

                        // 如果雨滴完全离开屏幕，重新初始化
                        if (list_head_position[i] - list_len[i] > (canvas_upper_length + totalRows))
                        {
                            InitRainColumn(i);
                        }
                    }
                }

                // 复制到全局显示数据
                Array.Copy(temp_display_data, display_global_define.g_display_data, 
                          Math.Min(temp_display_data.Length, display_global_define.g_display_data.Length));
            }
        }
    }
}