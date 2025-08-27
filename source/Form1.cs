using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO.Ports;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.Numerics;
using 串口驱动WS2812;

namespace 串口驱动WS2812
{
    public partial class Form1 : Form
    {
        private static Form1 instance;
        public static Form1 Instance => instance;

        private Timer ws2812Timer;
        public static List<Control> buttonList = new List<Control>();
        
        // 音频分析相关
        private bool isAudioAnalysisRunning = false;
        
        // 显示刷新相关
        private display_refresh displayRefresh;
        
        // 按键状态
        public bool keyUpPressed = false;
        public bool keyDownPressed = false;

        public Form1()
        {
            InitializeComponent();
            instance = this; // 设置静态实例引用
            
            // 启用键盘事件
            this.KeyPreview = true;
            this.KeyDown += Form1_KeyDown;
            this.KeyUp += Form1_KeyUp;
        }

        // ... 其他代码


        public static class gColor
        {
            public static Color gSelectColor;
        }
        private void btnEvent(Object sender, EventArgs e)
        {
            string s = "btn_";
            if (sender is Button)
            {
                Button button = sender as Button;

                for (int i = 0;i<8;i++)
                {
                    for (int j = 0;j<8;j++)
                    {
                        if (button.Name == s + i + "_" + j)
                        {
                            
                            if (button.BackColor == Color.Black)
                            {
                                button.BackColor = gColor.gSelectColor;
                                //这里写鼠标左键点击事件
                            }
                            else
                            {
                                button.BackColor = Color.Black;
                            }
                            
                        }
                    }
                }

            }
        }


        private void Form1_Load(object sender, EventArgs e)
        {
            // ... 你的其他初始化代码 ...

            ws2812Timer = new Timer();
            ws2812Timer.Interval = 2; // 刷新周期，单位毫秒，可根据需要调整
            ws2812Timer.Tick += Ws2812Timer_Tick;

            //ws2812Timer.Start();




            int b_size = 50;
            int b_intervel = 0;
            for (int i = 0; i < 8; i++)
            {
                for (int j = 0; j < 8; j++)
                {
                    Button button = new Button();
                    button.Size = new Size(b_size, b_size);//Button大小
                    button.Location = new Point(i * b_size + b_intervel, (7 - j) * b_size + b_intervel);//位置
                    button.Name = "btn_" + i + "_" + j;
                    
                    button.BackColor = Color.Black; 

                    button.Click += new EventHandler(btnEvent);//注册点击事件
                    this.Controls.Add(button);
                }
            }
                    //把所有按钮添加到List集合

            for (int i = 0; i< 8; i++)
            {
                for (int j = 0; j< 8; j++)
                {
                    Control[] conBtn = this.Controls.Find("btn_" + i.ToString() + "_" + j.ToString(), false);
                    if (conBtn.Count() >= 1)
                    {
                        //判断控件类型是否为按钮
                        if (conBtn[0] is Button)
                        {
                            buttonList.Add(conBtn[0]);
                        }
}
                }
            }


            // 填充串口号
            comboBoxSerialPorts.Items.Clear();
            string[] ports = SerialPort.GetPortNames();
            Array.Sort(ports);
            comboBoxSerialPorts.Items.AddRange(ports);
            if (comboBoxSerialPorts.Items.Count > 0)
            {
                comboBoxSerialPorts.SelectedIndex = 0;
            }

            // 初始化显示刷新系统
            displayRefresh = new display_refresh();

            // 测试HSL转换
            串口驱动WS2812.ws2812.Instance.TestHslConversion();

            // 初始化TrackBar亮度控制
            trackBar1.Value = 20; // 默认60%亮度
            label1.Text = "亮度: 20%";
            ws2812.Instance.SetBrightness(20);



        }

        private void button1_Click(object sender, EventArgs e)
        {
            colorDialog1.ShowDialog();
            Color c = colorDialog1.Color;
            ColorShowButton.BackColor = c;
            gColor.gSelectColor = c;
        }


        private void button2_Click(object sender, EventArgs e)
        {
            //把所有按钮添加到List集合
            List<Control> buttonList = new List<Control>();


            for (int i = 0;i<8;i++)
            {
                for (int j = 0;j<8;j++)
                {
                    Control[] conBtn = this.Controls.Find("btn_" + i.ToString() + "_" + j.ToString(),false);
                    if (conBtn.Count() >= 1)
                    {
                        //判断控件类型是否为按钮
                        if (conBtn[0] is Button)
                        {
                            buttonList.Add(conBtn[0]);
                        }
                    }
                }
            }

            //遍历所有List，设定属性
            for (int i = 0; i < buttonList.Count; i++)
            {
                Button btn = buttonList[i] as Button;
                btn.BackColor = Color.Black;
            }
        }

        private void button4_Click(object sender, EventArgs e)
        {

        }

        private void textBox1_TextChanged(object sender, EventArgs e)
        {

        }

        private void button3_Click_1(object sender, EventArgs e)
        {

        }

        private void button4_Click_1(object sender, EventArgs e)
        {
            display_globle_define.g_display_func_index = 0;
        }

        private void button3_Click_2(object sender, EventArgs e)
        {
            if ((sender as Button).Text == "打开")
            {
                string selectedPort = comboBoxSerialPorts.SelectedItem as string;
                if (!string.IsNullOrEmpty(selectedPort))
                {
                    if (串口驱动WS2812.ws2812.Instance.InitSerial(selectedPort, 3000000)) // 初始化串口    
                    {
                        // 修改当前按键的背景色为绿色    
                        if (sender is Button button)
                        {
                            button.BackColor = Color.Green;
                            button.Text = "关闭";
                        }
                        ws2812Timer.Start();
                    }
                    else
                    {
                        MessageBox.Show("串口打开失败！");
                    }
                }
                else
                {
                    MessageBox.Show("请选择串口号！");
                }
            }
            else
            {
                if (串口驱动WS2812.ws2812.Instance.serialPort.IsOpen) // 关闭串口  
                {
                    串口驱动WS2812.ws2812.Instance.CloseSerial();
                    if (sender is Button button)
                    {
                        button.BackColor = Color.White;
                        button.Text = "打开";
                    }
                    if (ws2812Timer != null)
                    {
                        ws2812Timer.Stop();
                    }
                }
            }
        }
        private void Ws2812Timer_Tick(object sender, EventArgs e)
        {
            串口驱动WS2812.ws2812.Instance.Ws2812Refresh();
        }
        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            // 停止音频分析
            if (isAudioAnalysisRunning)
            {
                AudioFFTAnalysis.Instance.StopCapture();
            }
            
            
            if (ws2812Timer != null)
            {
                ws2812Timer.Stop();
                ws2812Timer.Dispose();
            }
           
            
            base.OnFormClosing(e);
        }

        private void button5_Click(object sender, EventArgs e)
        {
            // 音频分析开关按钮
            if (!isAudioAnalysisRunning)
            {
                isAudioAnalysisRunning = true;

                display_globle_define.g_display_func_index = 1;                

                AudioFFTAnalysis.Instance.StartCapture();
                if (sender is Button button)
                {
                    button.Text = "停止";
                    button.BackColor = Color.Red;
                }
            }
            else
            {
                isAudioAnalysisRunning = false;
                AudioFFTAnalysis.Instance.StopCapture();
                if (sender is Button button)
                {
                    button.Text = "8x8音乐频谱";
                    button.BackColor = Color.White;
                }
            }
        }
        
        
        private void ClearLEDMatrix()
        {
            for (int i = 0; i < 8; i++)
            {
                for (int j = 0; j < 8; j++)
                {
                    string buttonName = $"btn_{i}_{j}";
                    Control[] controls = this.Controls.Find(buttonName, false);
                    
                    if (controls.Length > 0 && controls[0] is Button button)
                    {
                        button.BackColor = Color.Black;
                    }
                }
            }
        }

        private void comboBoxSerialPorts_SelectedIndexChanged(object sender, EventArgs e)
        {

        }

        // 键盘按下事件
        private void Form1_KeyDown(object sender, KeyEventArgs e)
        {
            switch (e.KeyCode)
            {
                case Keys.Up:
                    keyUpPressed = true;
                    break;
                case Keys.Down:
                    keyDownPressed = true;
                    break;
            }
        }

        // 键盘释放事件
        private void Form1_KeyUp(object sender, KeyEventArgs e)
        {
            switch (e.KeyCode)
            {
                case Keys.Up:
                    keyUpPressed = false;
                    break;
                case Keys.Down:
                    keyDownPressed = false;
                    break;
            }
        }

        private void button7_Click(object sender, EventArgs e)
        {
            // 弹窗选择图片
            OpenFileDialog openFileDialog = new OpenFileDialog();
            openFileDialog.Filter = "Image Files|*.bmp;*.jpg;*.jpeg;*.png;*.gif|All Files|*.*";
            if (openFileDialog.ShowDialog() == DialogResult.OK)
            {
                // 显示参数设置窗口并处理图片，只有在图片成功加载时才切换到图片显示模式
                if (display_func_picture.SetImagePath(openFileDialog.FileName, false))
                {
                    display_globle_define.g_display_func_index = 2; // 切换到图片显示模式
                }
            }
        }

        private void label1_Click(object sender, EventArgs e)
        {

        }

        private void trackBar1_Scroll(object sender, EventArgs e)
        {
            // 获取TrackBar的当前值并设置亮度
            int brightness = trackBar1.Value;
            ws2812.Instance.SetBrightness(brightness);
            
            // 更新标签显示当前亮度值
            label1.Text = $"亮度: {brightness}%";
        }

        private void button6_Click_1(object sender, EventArgs e)
        {

        }

        private void button11_Click(object sender, EventArgs e)
        {
            display_func_picture.ShowImageSettingsDialog();
        }
    }
}
