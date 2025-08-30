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
using UART_TO_WS2812;

namespace UART_TO_WS2812
{

    public class display_1x30_init
    {
        public void showlist_init()
        {
            display_refresh.display_showlist_add(new display_func_1x30_music_spectrum());
        }
    }

    internal class display_func_1x30_music_spectrum : IShowable
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
        private static uint list_max_white_point; // 只需要一个峰值跟踪
        private static uint list_last_max_point;  // 只需要一个峰值跟踪
        private static byte list_white_point_descent_time_cnt = 0;
        private static byte list_descent_time_cnt = 0;
        private static uint time_cnt = 0;


        public static void change_sensitivity()
        {
            sensitivity++;
            if (sensitivity > 5)
            {
                sensitivity = 1;
            }
        }

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
            // 清空显示数据
            Array.Clear(display_global_define.g_display_data, 0, display_global_define.g_display_data.Length);

            ushort list_unit_value = (ushort)(sensitivity_arry[sensitivity - 1] / (display_global_define.DISPLAY_MAX_LIST_NUM - 1));

            uint temp_max = 0;
            byte temp_list_unit_num = 0;
            uint temp_unit_value = 0;


            if (state != 0)
            {
                
            }
            else
            {
                list_white_point_descent_time_cnt++;
                list_descent_time_cnt++;

                // 按幅值最大数值显示处理
                // 在整个频谱范围内寻找最大幅度值
                uint max_amplitude = 0;
                int max_bin_index = 0;
                
                // 遍历所有有效的FFT bin
                for (int j = 0; j < display_global_define.g_fft_amp.Length; j++)
                {
                    if (display_global_define.g_fft_amp[j] <= sensitivity_arry[sensitivity - 1] &&
                        display_global_define.g_fft_amp[j] >= FFT_SPECTRUM_MINMIN_AMP)
                    {
                        if (display_global_define.g_fft_amp[j] > max_amplitude)
                        {
                            max_amplitude = display_global_define.g_fft_amp[j];
                            max_bin_index = j;
                        }
                    }
                }

                temp_unit_value = list_unit_value;

                if (temp_unit_value > 0)
                {
                    temp_list_unit_num = (byte)(max_amplitude / temp_unit_value);
                }

                // 计算最大值需要占据一列中的单元数
                temp_max = 0;

                // 避免白点和频谱点超出范围
                if (temp_list_unit_num > display_global_define.DISPLAY_MAX_LIST_NUM - 2)
                {
                    temp_list_unit_num = (byte)(display_global_define.DISPLAY_MAX_LIST_NUM - 2);
                }

                // 更新峰值跟踪
                if (temp_list_unit_num > list_max_white_point)
                {
                    list_max_white_point = (uint)(temp_list_unit_num + 1); // 在最高点之上加一格,作为白点下降的起始位置
                }

                // 如果当前最高点低于上一次的最高点，则使用缓慢降落的最高点
                if (temp_list_unit_num < list_last_max_point)
                {
                    temp_list_unit_num = (byte)list_last_max_point;
                }
                else
                {
                    list_last_max_point = temp_list_unit_num;
                }
                
                // 填充频率显示区域
                for (byte i = 0; i < display_global_define.DISPLAY_MAX_LIST_NUM; i++)
                {
                    if (i <= temp_list_unit_num)
                    {
                        display_global_define.g_display_data[i] = music_spectrum_get_color.color(i, 0);
                    }
                    else
                    {
                        display_global_define.g_display_data[i] = 0;
                    }
                }

                // 列下降的速度（放在循环外部）
                if (list_descent_time_cnt == DISPLAY_DESCENT_SPEED)
                {
                    list_descent_time_cnt = 0;
                    if (list_last_max_point > 0)
                    {
                        list_last_max_point--;
                    }
                }

                // 每经过一个时间段，白点就下降一点（放在循环外部）
                if (list_white_point_descent_time_cnt == DISPLAY_WHITE_POINT_DESCENT_SPEED)
                {
                    list_white_point_descent_time_cnt = 0;
                    if (list_max_white_point > 0)
                    {
                        list_max_white_point--;
                    }
                }

                // 填充白点（确保不超出范围）
                if (list_max_white_point < display_global_define.DISPLAY_MAX_LIST_NUM)
                {
                    display_global_define.g_display_data[list_max_white_point] = 0xFFFFFF; // 最高点填充白色
                }

            }
        }
    }
}