using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace 串口驱动WS2812
{
    public enum LEDBoardType
    {
        Board8x8,
        Board16x16,
        Board1x30
    }

    public static class display_global_define
    {

        // 频谱幅度数据
        public static uint[] g_fft_amp = new uint[512];

        public const int DISPLAY_REFRESH_INTERVAL = 5;      // 屏幕刷新间隔5ms
    }


    public static class DisplayConfig
    {
        // 当前选择的灯板类型
        public static LEDBoardType CurrentBoardType { get; private set; } = LEDBoardType.Board8x8;
        
        // 灯板配置信息
        public static Dictionary<LEDBoardType, BoardConfig> BoardConfigs = new Dictionary<LEDBoardType, BoardConfig>
        {
            { LEDBoardType.Board8x8, new BoardConfig(8, 8, 64) },
            { LEDBoardType.Board16x16, new BoardConfig(16, 16, 256) },
            { LEDBoardType.Board1x30, new BoardConfig(1, 30, 30) }
        };

        // 获取当前灯板配置
        public static BoardConfig CurrentConfig => BoardConfigs[CurrentBoardType];

        // 切换灯板类型
        public static void SetBoardType(LEDBoardType boardType)
        {
            CurrentBoardType = boardType;
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
                default:
                    SetBoardType(LEDBoardType.Board8x8);
                    break;
            }
        }
    }

    public class BoardConfig
    {
        public int Width { get; }
        public int Height { get; }
        public int TotalLEDs { get; }
        public string DisplayName { get; }

        public BoardConfig(int width, int height, int totalLEDs)
        {
            Width = width;
            Height = height;
            TotalLEDs = totalLEDs;
            DisplayName = $"{width}x{height}";
        }
    }

    // 通用的音乐频谱颜色生成器，兼容8x8和16x16灯板
    internal class music_spectrum_get_color
    {
        static byte last_line = 0;
        static int line_cnt = DisplayConfig.CurrentConfig.Height - 1;
        static uint[] colour_arr = new uint[16]
        {
            0x33CCAE, 0x33CCAE, 0x33CCAE, 0x33CCAE,
            0x33CCAE, 0x33CCAE, 0x33CCAE, 0x33CCAE,
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
                if (time_cnt == (500 / display_global_define.DISPLAY_REFRESH_INTERVAL))
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
}