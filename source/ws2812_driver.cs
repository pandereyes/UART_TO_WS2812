using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;


namespace 串口驱动WS2812
{

    internal class ws2812
    {


        private static ws2812 _instance;
        public static ws2812 Instance
        {
            get
            {
                if (_instance == null)
                    _instance = new ws2812();
                return _instance;
            }
        }

        // 串口对象  
        public SerialPort serialPort;

        /// <summary>  
        /// 初始化串口  
        /// </summary>  
        /// <param name="portName">串口名称，如"COM3"</param>  
        /// <param name="baudRate">波特率，常用115200</param>  
        public bool InitSerial(string portName, int baudRate = 2000000)
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


        public const int WS2812_NUM = 64;

        // LED数据缓存  
        private UInt32[] leds_rgb = new UInt32[WS2812_NUM];

        // LED显示缓存 
        private byte[] ws2812_display_buffer = new byte[8 * WS2812_NUM];


        // RGB颜色结构
        public struct WS2812Color
        {
            public byte Green;  // WS2812是GRB顺序
            public byte Red;
            public byte Blue;

            public WS2812Color(byte red, byte green, byte blue)
            {
                Red = red;
                Green = green;
                Blue = blue;
            }
        }

        private WS2812Color[] ws2812_colors = new WS2812Color[WS2812_NUM];

        /// <summary>
        /// 位打包函数
        /// 串口必须是8N1协议，起始位固定为0，停止位固定为1，无校验位，一个字节10bit。
        /// 将串口的TX用MOS管进行电平翻转
        /// 将D0D1D2置为1,加上起始位就是0111，翻转后可以作为0码，将D3D4D5D6置为0001，反转后就是1码。
        /// 将D0D1D2置为001，加上起始位就是0001，翻转后可以作为1码，将D3D4D5D6置为0111，反转后就是0码。
        /// 将D7置为1，加上停止位加上字节间隙作为空闲位，空闲位小于50us。
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
            return new byte[resetBytes]; // 全零数组
        }

        /// <summary>
        /// 编码完整的LED数据包（包含复位信号）
        /// </summary>
        /// <param name="colors">LED颜色数组</param>
        /// <returns>完整的数据包</returns>
        private static byte[] CreateLedDataPacket(WS2812Color[] colors)
        {
            byte[] colorData = WS2812EncodeColors(colors);
            byte[] resetData = CreateResetSignal();

            byte[] packet = new byte[colorData.Length + resetData.Length];
            Array.Copy(colorData, 0, packet, 0, colorData.Length);
            Array.Copy(resetData, 0, packet, colorData.Length, resetData.Length);

            return packet;
        }


        public void Ws2812SetColor(int index, UInt32 rgb)
        { 
           if (index < 0 || index >= WS2812_NUM)
                throw new ArgumentOutOfRangeException(nameof(index), "Index must be between 0 and " + (WS2812_NUM - 1));
            // 将RGB值转换为WS2812Color结构
            byte red = (byte)((rgb >> 16) & 0xFF);
            byte green = (byte)((rgb >> 8) & 0xFF);
            byte blue = (byte)(rgb & 0xFF);
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
            byte[] uart_data = CreateLedDataPacket(ws2812_colors);

            if (serialPort == null || !serialPort.IsOpen)
            {
                throw new InvalidOperationException("Serial port is not initialized or not open.");
            }
            else
            {
                serialPort.Write(uart_data, 0, uart_data.Length);
            }
        }
    }
}
