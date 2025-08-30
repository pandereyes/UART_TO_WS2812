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
using System.Threading;

namespace 串口驱动WS2812
{
    public partial class Form1 : Form
    {
        private static Form1 instance;
        public static Form1 Instance => instance;

        public static List<Control> buttonList = new List<Control>();
        
        // 音频分析相关
        private bool isAudioAnalysisRunning = false;
        
        // 显示刷新相关
        private object displayRefresh;
        
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
            
            // 灯板选择事件
            comboBoxLEDBoardSelect.SelectedIndexChanged += ComboBoxLEDBoardSelect_SelectedIndexChanged;

        }

        // ... 其他代码


        public static class gColor
        {
            public static Color gSelectColor;
        }
        private void btnEvent(Object sender, EventArgs e)
        {
            if (sender is Button button)
            {
                // 解析按钮名称 "btn_x_y"
                string[] parts = button.Name.Split('_');
                if (parts.Length == 3 && int.TryParse(parts[1], out int x) && int.TryParse(parts[2], out int y))
                {
                    // 确保坐标在当前灯板范围内
                    if (x < DisplayConfig.CurrentConfig.Width && y < DisplayConfig.CurrentConfig.Height)
                    {
                        if (button.BackColor == Color.Black)
                        {
                            button.BackColor = gColor.gSelectColor;
                        }
                        else
                        {
                            button.BackColor = Color.Black;
                        }
                    }
                }
            }
        }

        private static System.Threading.Timer ws2812Timer;
        private void Form1_Load(object sender, EventArgs e)
        {
            // ... 你的其他初始化代码 ...

            

            ws2812Timer = new System.Threading.Timer(Ws2812Timer_Tick,null, Timeout.Infinite, Timeout.Infinite); //间隔5ms

            //ws2812Timer.Start();

            // 初始化创建LED按钮
            RecreateLEDButtons();


            // 填充串口号
            comboBoxSerialPorts.Items.Clear();
            string[] ports = SerialPort.GetPortNames();
            Array.Sort(ports);
            comboBoxSerialPorts.Items.AddRange(ports);
            if (comboBoxSerialPorts.Items.Count > 0)
            {
                comboBoxSerialPorts.SelectedIndex = 0;
            }

            // 设置灯板选择默认值
            comboBoxLEDBoardSelect.SelectedIndex = 0; // 默认选择8x8

            // 初始化显示刷新系统
            InitializeDisplayRefresh();

            // 测试HSL转换
            串口驱动WS2812.ws2812.Instance.TestHslConversion();

            // 初始化TrackBar亮度控制
            trackBar1.Value = 20; // 默认60%亮度
            label1.Text = "亮度: 20%";
            ws2812.Instance.SetBrightness(20);
            
            // 检查并清理可能残留的按钮
            CheckForStrayButtons();
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
            SetDisplayFuncIndex(0);
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
                        
                        display_refresh.display_showlist_clear();
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
                        else
                        {
                            // 初始化8x8显示功能列表
                            display_8x8_init displayInit = new display_8x8_init();
                            displayInit.showlist_init();
                        }

                        SetDisplayFuncIndex(0); 

                        ws2812Timer.Change(0, 5);
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
                        ws2812Timer.Change(Timeout.Infinite, Timeout.Infinite);                        
                    }
                }
            }
        }

        private void Ws2812Timer_Tick(object state)
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
                ws2812Timer.Change(Timeout.Infinite, Timeout.Infinite);
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

                if (DisplayConfig.CurrentBoardType == LEDBoardType.Board1x30)
                {
                    SetDisplayFuncIndex(0);
                }
                else
                {
                    SetDisplayFuncIndex(1);
                }            

                if (DisplayConfig.CurrentBoardType == LEDBoardType.Board8x8)
                {
                    display_func_music_spectrum.set_frequency_bands(display_global_define.frequence_bands_8x8);
                }
                else if (DisplayConfig.CurrentBoardType == LEDBoardType.Board16x16)
                {
                    display_func_music_spectrum.set_frequency_bands(display_global_define.frequence_bands_16x16);
                }


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
                bool imageLoaded = false;
                
                if (DisplayConfig.CurrentBoardType == LEDBoardType.Board8x8 || DisplayConfig.CurrentBoardType == LEDBoardType.Board16x16)
                {
                    imageLoaded = display_func_picture.SetImagePath(openFileDialog.FileName, true);
                    if (imageLoaded)
                    {
                        SetDisplayFuncIndex(2); // 切换到图片显示模式
                    }
                }               
            }
        }

        // 灯板选择切换事件
        private void ComboBoxLEDBoardSelect_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (comboBoxLEDBoardSelect.SelectedItem != null)
            {
                // 首先停止当前显示刷新定时器
                StopCurrentDisplayRefreshTimer();
                
                string selectedBoard = comboBoxLEDBoardSelect.SelectedItem.ToString();
                DisplayConfig.SetBoardTypeFromString(selectedBoard);

                // 重新初始化显示数据
                display_global_define.ReinitializeDisplayData();

                // 重新初始化WS2812驱动数组
                ws2812.Instance.ReinitializeLEDArrays();
                
                // 重新创建UI按钮
                RecreateLEDButtons();
                
                // 检查并清理可能残留的按钮
                CheckForStrayButtons();
                
                // 重新初始化显示刷新系统
                InitializeDisplayRefresh();
                
                // 启动新的显示刷新定时器
                StartCurrentDisplayRefreshTimer();
                
                // 重置显示模式
                SetDisplayFuncIndex(0);
            }
        }
        
        // 停止当前显示刷新定时器
        private void StopCurrentDisplayRefreshTimer()
        {
            display_refresh.StopRefreshTimer();
        }
        
        // 启动当前显示刷新定时器
        private void StartCurrentDisplayRefreshTimer()
        {
            display_refresh.StartRefreshTimer();
        }
        
        // 初始化显示刷新系统
        private void InitializeDisplayRefresh()
        {
            displayRefresh = new display_refresh();
        }
        
        // 设置显示模式索引
        private void SetDisplayFuncIndex(int index)
        {
            display_global_define.g_display_func_index = index;
        }
        
        // 获取显示模式索引
        private int GetDisplayFuncIndex()
        {
            return display_global_define.g_display_func_index;
        }
        
        // 重新创建LED按钮
        private void RecreateLEDButtons()
        {
            // 清除现有按钮 - 使用ToList避免修改集合时的枚举错误
            List<Control> buttonsToRemove = new List<Control>();
            foreach (Control control in this.Controls)
            {
                if (control is Button && control.Name.StartsWith("btn_"))
                {
                    buttonsToRemove.Add(control);
                }
            }
            
            foreach (Control button in buttonsToRemove)
            {
                this.Controls.Remove(button);
                button.Dispose();
            }
            buttonList.Clear();
            
            // 创建新的按钮
            int b_size = 30; // 按钮大小
            int b_interval = 2; // 按钮间距
            int startX = 20; // 起始X坐标
            int startY = 20; // 起始Y坐标
            
            BoardConfig config = DisplayConfig.CurrentConfig;
            
            for (int x = 0; x < config.Width; x++)
            {
                for (int y = 0; y < config.Height; y++)
                {
                    Button button = new Button();
                    button.Size = new Size(b_size, b_size);
                    button.Location = new Point(startX + x * b_size + b_interval, 
                                              startY + ((config.Height - 1) - y) * b_size + b_interval);
                    button.Name = $"btn_{x}_{y}";
                    button.BackColor = Color.Black;
                    button.Click += new EventHandler(btnEvent);
                    
                    this.Controls.Add(button);
                    buttonList.Add(button);
                }
            }
        }

        // 测试方法：检查是否有残留的16x16按钮
        private void CheckForStrayButtons()
        {
            int strayButtonCount = 0;
            foreach (Control control in this.Controls)
            {
                if (control is Button button && button.Name.StartsWith("btn_"))
                {
                    // 解析按钮坐标
                    string[] parts = button.Name.Split('_');
                    if (parts.Length == 3 && 
                        int.TryParse(parts[1], out int x) && 
                        int.TryParse(parts[2], out int y))
                    {
                        // 检查是否超出当前配置范围
                        if (x >= DisplayConfig.CurrentConfig.Width || 
                            y >= DisplayConfig.CurrentConfig.Height)
                        {
                            strayButtonCount++;
                            // 移除超出范围的按钮
                            this.Controls.Remove(button);
                            button.Dispose();
                        }
                    }
                }
            }
            
            if (strayButtonCount > 0)
            {
                Console.WriteLine($"发现并移除了 {strayButtonCount} 个残留按钮");
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

        private void comboBoxLEDBoardSelect_SelectedIndexChanged_1(object sender, EventArgs e)
        {

        }

        private void button10_Click(object sender, EventArgs e)
        {

        }

        private void button9_Click(object sender, EventArgs e)
        {

        }

        private void button8_Click(object sender, EventArgs e)
        {

        }

        private void label2_Click(object sender, EventArgs e)
        {

        }

        private void ColorShowButton_Click(object sender, EventArgs e)
        {

        }

        private void button12_Click(object sender, EventArgs e)
        {
            if (DisplayConfig.CurrentBoardType != LEDBoardType.Board1x30)
            {
                display_func_music_spectrum.change_sensitivity();
            }
            else
            {
                display_func_1x30_music_spectrum.change_sensitivity();
            }
        }

        private void button13_Click(object sender, EventArgs e)
        {
            // 清除所有按钮颜色
            foreach (Control control in buttonList)
            {
                if (control is Button button)
                {
                    button.BackColor = Color.Black;
                }
            }
        }
    }
}
