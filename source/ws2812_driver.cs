using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;


namespace UART_TO_WS2812
{

    public class ws2812
    {
        // 移除单例模式，每个实例独立
        private static int instanceCount = 0;
        private int instanceId;

        // 串口对象  
        public SerialPort serialPort;

        /// <summary>  
        /// 初始化串口  
        /// </summary>  
        /// <param name="portName">串口名称，如"COM3"</param>  
        /// <param name="baudRate">波特率，常用115200</param>  
        public bool InitSerial(string portName, int baudRate = 3000000)
        {
            if (serialPort != null && serialPort.IsOpen)
            {
                serialPort.Close();
            }

            serialPort = new SerialPort(portName, baudRate, Parity.None, 8, StopBits.One);
            serialPort.ReadTimeout = 1000;
            serialPort.WriteTimeout = 1000;

            try
            {
                serialPort.Open();
                return true;
            }
            catch (Exception ex)
            {
                return false;
            }
        }

        /// <summary>  
        /// 关闭串口  
        /// </summary>  
        public void CloseSerial()
        {
            if (serialPort != null && serialPort.IsOpen)
            {
                serialPort.Close();
            }
        }


        // 动态LED数量
        public static int WS2812_NUM => DisplayConfig.CurrentConfig.TotalLEDs;

        // LED数据缓存  
        private UInt32[] leds_rgb = new UInt32[DisplayConfig.CurrentConfig.TotalLEDs];

        // LED显示缓存 
        private byte[] ws2812_display_buffer = new byte[8 * DisplayConfig.CurrentConfig.TotalLEDs];

        // 亮度控制 (0-100)
        private int brightness = 20; // 默认60%亮度

        // RGB颜色结构
        public struct WS2812Color
        {
            public byte Green;  // WS2812是GRB顺序
            public byte Red;
            public byte Blue;

            public WS2812Color(byte red, byte green, byte blue)
            {
                // WS2812使用GRB顺序，但构造函数保持RGB参数顺序以便理解
                Green = green;
                Red = red;
                Blue = blue;
            }
        }

        private WS2812Color[] ws2812_colors;

        // 构造函数
        public ws2812()
        {
            instanceId = instanceCount++;
            ReinitializeLEDArrays();
        }

        // 重新初始化LED数组
        public void ReinitializeLEDArrays()
        {
            leds_rgb = new UInt32[DisplayConfig.CurrentConfig.TotalLEDs];
            ws2812_display_buffer = new byte[8 * DisplayConfig.CurrentConfig.TotalLEDs];
            ws2812_colors = new WS2812Color[DisplayConfig.CurrentConfig.TotalLEDs];
        }

        /// <summary>
        /// 位打包函数
        /// 串口必须是8N1协议，起始位固定为0，停止位固定为1，无校验位，一个字节10bit。
        /// 将串口的TX用MOS管进行电平翻转
        /// 将D0D1D2置为1,加上起始位就是0111，翻转后可以作为0码，将D3D4D5D6置为0001，反转后就是1码。
        /// 将D0D1D2置为001，加上起始位就是0001，翻转后可以作为1码，将D3D4D5D6置为0111，反转后就是0码。
        /// 将D7置为1，加上停止位加上字节间隙作为空闲位，空闲位小于50us。
        /// 串口空闲时默认高电平，翻转后就是低电平，可以作为复位信号
        /// </summary>
        /// <param name="data">要编码的字节数据</param>
        /// <param name="output">输出缓冲区</param>
        /// <param name="bitPos">当前位位置（引用传递）</param>
        private static void WS2812PackBits(byte data, byte[] output, ref int bitPos)
        {
            for (int i = 0; i < 4; i++)
            {
                byte code = (byte)(((data & 0x80) != 0 ? 0x04 : 0x07) | ((data & 0x40) != 0 ? 0x40 : 0x70) | 0x80);
                output[bitPos] = code;
                bitPos++;
                data <<= 2;
            }
        }


        private static void WS2812EncodeLed(WS2812Color color, byte[] buffer)
        {
            if (buffer.Length < 12)
                throw new ArgumentException("Buffer must be at least 9 bytes long");

            int bitPos = 0;
            Array.Clear(buffer, 0, 12); // 

            WS2812PackBits(color.Green, buffer, ref bitPos);
            WS2812PackBits(color.Red, buffer, ref bitPos);
            WS2812PackBits(color.Blue, buffer, ref bitPos);
        }

        /// <summary>
        /// 编码多个LED颜色
        /// </summary>
        /// <param name="colors">LED颜色数组</param>
        /// <returns>编码后的字节数组</returns>
        private static byte[] WS2812EncodeColors(WS2812Color[] colors)
        {
            byte[] result = new byte[colors.Length * 12];
            byte[] tempBuffer = new byte[12];

            for (int i = 0; i < colors.Length; i++)
            {
                WS2812EncodeLed(colors[i], tempBuffer);
                Array.Copy(tempBuffer, 0, result, i * 12, 12);
            }

            return result;
        }

        /// <summary>
        /// 创建复位信号数据
        /// </summary>
        /// <param name="resetBytes">复位信号字节数（默认20字节）</param>
        /// <returns>复位信号字节数组</returns>
        private static byte[] CreateResetSignal(int resetBytes = 30)
        {
            byte[] resetData = new byte[resetBytes];
            for (int i = 0; i < resetBytes; i++)
            {
                resetData[i] = 0xFF; // 全1数组，经过MOS反转后变成低电平
            }
            return resetData;
        }

        /// <summary>
        /// 编码完整的LED数据包（包含复位信号）
        /// </summary>
        /// <param name="colors">LED颜色数组</param>
        /// <returns>完整的数据包</returns>
        private static byte[] CreateLedDataPacket(WS2812Color[] colors)
        {
            byte[] colorData = WS2812EncodeColors(colors);
            //byte[] resetData = CreateResetSignal();

            //byte[] packet = new byte[colorData.Length + resetData.Length];
            byte[] packet = new byte[colorData.Length];
            Array.Copy(colorData, 0, packet, 0, colorData.Length);
            //Array.Copy(resetData, 0, packet, colorData.Length, resetData.Length);

            return packet;
        }


        /// <summary>
        /// 设置亮度
        /// </summary>
        /// <param name="level">亮度级别 (0-100)</param>
        public void SetBrightness(int level)
        {
            if (level < 0) level = 0;
            if (level > 100) level = 100;
            brightness = level;
        }

        /// <summary>
        /// 获取当前亮度
        /// </summary>
        /// <returns>亮度级别 (0-100)</returns>
        public int GetBrightness()
        {
            return brightness;
        }

        /// <summary>
        /// 测试HSL转换（调试用）
        /// </summary>
        public void TestHslConversion()
        {
            // 测试几个基本颜色
            TestColor(255, 0, 0, "红色");
            TestColor(0, 255, 0, "绿色");
            TestColor(0, 0, 255, "蓝色");
            TestColor(255, 255, 255, "白色");
            TestColor(0, 0, 0, "黑色");
        }

        private void TestColor(byte r, byte g, byte b, string colorName)
        {
            RgbToHsl(r, g, b, out double h, out double s, out double l);
            HslToRgb(h, s, l, out byte r2, out byte g2, out byte b2);
            
            Console.WriteLine($"{colorName}: RGB({r},{g},{b}) -> HSL({h:F2},{s:F2},{l:F2}) -> RGB({r2},{g2},{b2})");
        }

        /// <summary>
        /// RGB到HSL转换
        /// </summary>
        private void RgbToHsl(byte r, byte g, byte b, out double h, out double s, out double l)
        {
            double red = r / 255.0;
            double green = g / 255.0;
            double blue = b / 255.0;

            double max = Math.Max(red, Math.Max(green, blue));
            double min = Math.Min(red, Math.Min(green, blue));

            // 计算亮度
            l = (max + min) / 2.0;

            if (max == min)
            {
                h = 0;
                s = 0;
            }
            else
            {
                double delta = max - min;
                
                // 计算饱和度
                double denominator = 1 - Math.Abs(2 * l - 1);
                s = denominator > 0.0001 ? delta / denominator : 0;

                // 计算色相
                if (max == red)
                    h = (green - blue) / delta + (green < blue ? 6 : 0);
                else if (max == green)
                    h = (blue - red) / delta + 2;
                else
                    h = (red - green) / delta + 4;

                h /= 6;
            }
        }

        /// <summary>
        /// HSL到RGB转换
        /// </summary>
        private void HslToRgb(double h, double s, double l, out byte r, out byte g, out byte b)
        {
            double red, green, blue;

            if (s == 0)
            {
                red = green = blue = l;
            }
            else
            {
                double q = l < 0.5 ? l * (1 + s) : l + s - l * s;
                double p = 2 * l - q;

                red = HueToRgb(p, q, h + 1.0/3);
                green = HueToRgb(p, q, h);
                blue = HueToRgb(p, q, h - 1.0/3);
            }

            r = (byte)(red * 255);
            g = (byte)(green * 255);
            b = (byte)(blue * 255);
        }

        private double HueToRgb(double p, double q, double t)
        {
            if (t < 0) t += 1;
            if (t > 1) t -= 1;
            if (t < 1.0/6) return p + (q - p) * 6 * t;
            if (t < 1.0/2) return q;
            if (t < 2.0/3) return p + (q - p) * (2.0/3 - t) * 6;
            return p;
        }

        public void Ws2812SetColor(int index, UInt32 rgb)
        { 
           if (index < 0 || index >= WS2812_NUM)
                throw new ArgumentOutOfRangeException(nameof(index), "Index must be between 0 and " + (WS2812_NUM - 1));
            
            // 额外的安全检查，确保数组大小匹配
            if (ws2812_colors == null || ws2812_colors.Length != WS2812_NUM)
            {
                ReinitializeLEDArrays();
            }
            
            // 将RGB值转换为WS2812Color结构
            byte red = (byte)((rgb >> 16) & 0xFF);
            byte green = (byte)((rgb >> 8) & 0xFF);
            byte blue = (byte)(rgb & 0xFF);
            
            // 使用HSL调整亮度，但采用不同的亮度控制策略
            RgbToHsl(red, green, blue, out double h, out double s, out double l);
            
            double temp_brightness = (double)brightness / 100.0;
            l = temp_brightness * l;
            
            // 确保亮度不超过最大值
            if (l > 1) l = 1;
            
            HslToRgb(h, s, l, out red, out green, out blue);
            
            ws2812_colors[index] = new WS2812Color(red, green, blue);
        }

        public void Ws2812SetAllColor(UInt32 rgb)
        {
            for (int i = 0; i < WS2812_NUM; i++)
            {
                Ws2812SetColor(i, rgb);
            }
        }


        public void Ws2812Refresh()
        {
            // 安全检查，确保数组大小匹配
            if (ws2812_colors == null || ws2812_colors.Length != WS2812_NUM)
            {
                ReinitializeLEDArrays();
            }
            
            byte[] uart_data = CreateLedDataPacket(ws2812_colors);

            if (serialPort != null && serialPort.IsOpen)
            {
                serialPort.Write(uart_data, 0, uart_data.Length);
            }
        }
    }
}
