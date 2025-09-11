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
using UART_TO_WS2812;
using System.Threading;
using System.Security.Policy;
using System.Net.Http;
using System.Threading.Tasks;

namespace UART_TO_WS2812
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
        
        // WS2812驱动实例
        public ws2812 ws2812Instance;
        
        // 按键状态
        public bool keyUpPressed = false;
        public bool keyDownPressed = false;
        
        // 串口检测相关
        private System.Threading.Timer serialPortMonitorTimer;
        private string[] lastSerialPorts = new string[0];

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
            string[] ports = SerialPort.GetPortNames();
            Array.Sort(ports);
            UpdateSerialPortsComboBox(ports);

            // 初始化显示刷新系统
            InitializeDisplayRefresh();

            // 创建WS2812实例（非单例）
            ws2812Instance = new ws2812();

            // 设置灯板选择默认值
            comboBoxLEDBoardSelect.SelectedIndex = 0;

            // 测试HSL转换
            ws2812Instance.TestHslConversion();
            
            // 初始化串口检测定时器（每秒检测一次）
            InitializeSerialPortMonitor();

            // 初始化TrackBar亮度控制
            trackBar1.Value = 20; // 默认60%亮度
            label1.Text = "亮度: 20%";
            ws2812Instance.SetBrightness(20);
            
            // 检查并清理可能残留的按钮
            CheckForStrayButtons();

            display_refresh.InitShowList();
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
                if (!string.IsNullOrEmpty(selectedPort) && ws2812Instance != null)
                {
                    if (ws2812Instance.InitSerial(selectedPort, 3000000)) // 初始化串口    
                    {
                        // 修改当前按键的背景色为绿色    
                        if (sender is Button button)
                        {
                            button.BackColor = Color.Green;
                            button.Text = "关闭";
                        }
                        
                        display_refresh.InitShowList();

                        SetDisplayFuncIndex(0); 

                        ws2812Timer.Change(0, 5);
                    }
                    else
                    {
                        MessageBox.Show("串口打开失败！");
                        //ClearSerialPortSelection();
                    }
                }
                else
                {
                    MessageBox.Show("请选择串口号！");
                }
            }
            else
            {
                if (ws2812Instance != null && ws2812Instance.serialPort.IsOpen) // 关闭串口  
                {
                    ws2812Instance.CloseSerial();
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
            if (ws2812Instance != null)
            {
                ws2812Instance.Ws2812Refresh();
            }
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
           
            // 关闭串口并清空combobox选择
            if (ws2812Instance != null)
            {
                ws2812Instance.CloseSerial();
                ClearSerialPortSelection();
            }
            
            base.OnFormClosing(e);
        }

        private void button5_Click(object sender, EventArgs e)
        {
            // 音频分析开关按钮
            if (!isAudioAnalysisRunning)
            {
                isAudioAnalysisRunning = true;

                display_func_music_spectrum.init_para();

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
        
        // 清空串口选择
        private void ClearSerialPortSelection()
        {
            comboBoxSerialPorts.SelectedIndex = -1;
            comboBoxSerialPorts.Text = "";
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
            if (comboBoxLEDBoardSelect.SelectedItem != null && ws2812Instance != null)
            {
                // 首先停止当前显示刷新定时器
                StopCurrentDisplayRefreshTimer();

                string selectedBoard = comboBoxLEDBoardSelect.SelectedItem.ToString();
                DisplayConfig.SetBoardTypeFromString(selectedBoard);

                display_refresh.InitShowList();

                // 重新初始化显示数据
                display_global_define.ReinitializeDisplayData();

                // 重新初始化WS2812驱动数组
                ws2812Instance.ReinitializeLEDArrays();
                
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
        
        // 初始化串口检测定时器
        private void InitializeSerialPortMonitor()
        {
            // 保存当前的串口列表
            lastSerialPorts = SerialPort.GetPortNames();
            Array.Sort(lastSerialPorts);
            
            // 创建定时器，每秒检测一次串口变化
            serialPortMonitorTimer = new System.Threading.Timer(
                SerialPortMonitorCallback,
                null,
                1000,  // 1秒后开始
                1000   // 每秒检测一次
            );
        }
        
        // 串口检测回调函数
        private void SerialPortMonitorCallback(object state)
        {
            // 获取当前串口列表
            string[] currentPorts = SerialPort.GetPortNames();
            Array.Sort(currentPorts);
            
            // 检查串口列表是否发生变化
            if (!SerialPortsEqual(lastSerialPorts, currentPorts))
            {
                // 串口列表发生变化，更新UI
                this.BeginInvoke(new Action(() =>
                {
                    UpdateSerialPortsComboBox(currentPorts);
                }));
                
                // 更新最后一次检测的串口列表
                lastSerialPorts = currentPorts;
            }
        }
        
        // 比较两个串口数组是否相同
        private bool SerialPortsEqual(string[] ports1, string[] ports2)
        {
            if (ports1.Length != ports2.Length)
                return false;
            
            for (int i = 0; i < ports1.Length; i++)
            {
                if (ports1[i] != ports2[i])
                    return false;
            }
            
            return true;
        }
        
        // 更新串口下拉框
        private void UpdateSerialPortsComboBox(string[] ports)
        {
            // 保存当前选中的串口
            string selectedPort = comboBoxSerialPorts.SelectedItem as string;
            
            // 清空并重新填充串口列表
            comboBoxSerialPorts.Items.Clear();
            comboBoxSerialPorts.Items.AddRange(ports);
            
            // 尝试恢复之前选中的串口
            if (!string.IsNullOrEmpty(selectedPort) && ports.Contains(selectedPort))
            {
                comboBoxSerialPorts.SelectedItem = selectedPort;
            }
            else if (ports.Length > 0)
            {
                // 如果之前的选中项不存在，选择第一个可用串口
                comboBoxSerialPorts.SelectedIndex = 0;
            }
            else
            {
                ClearSerialPortSelection();
            }
            
            // 如果串口状态发生变化，更新打开/关闭按钮状态
            UpdateSerialPortButtonState();
        }
        
        // 更新串口按钮状态
        private void UpdateSerialPortButtonState()
        {
            string selectedPort = comboBoxSerialPorts.SelectedItem as string;
            
            // 如果没有可用串口，禁用打开按钮
            if (string.IsNullOrEmpty(selectedPort))
            {
                button3.Enabled = false;
                button3.Text = "打开";
                button3.BackColor = Color.LightGray;
            }
            else
            {
                button3.Enabled = true;
                
                // 如果串口已经打开，保持打开状态
                if (button3.Text == "关闭")
                {
                    button3.BackColor = Color.Green;
                }
                else
                {
                    button3.BackColor = Color.White;
                }
            }
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
            if (ws2812Instance != null)
            {
                ws2812Instance.SetBrightness(brightness);
                display_refresh.UIBrightnessLevel = brightness * 3 + 30;
            }
            
            // 更新标签显示当前亮度值
            label1.Text = $"亮度: {brightness}%";
        }

        private void button6_Click_1(object sender, EventArgs e)
        {
            if (DisplayConfig.CurrentBoardType == LEDBoardType.Board8x8 || DisplayConfig.CurrentBoardType == LEDBoardType.Board16x16)
            {
                SetDisplayFuncIndex(3);
            }
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
            if (DisplayConfig.CurrentBoardType != LEDBoardType.Board16x16) return;

            // B站粉丝数显示功能
            try
            {
                // 弹出对话框让用户输入B站用户ID
                string input = ShowInputDialog(
                    "请输入B站用户ID（UID）：\n\n" +
                    "示例：33028512\n" +
                    "您可以在B站用户主页的URL中找到UID\n" +
                    "格式：https://space.bilibili.com/用户ID",
                    "B站粉丝数显示设置",
                    display_func_bilibili_fans.BilibiliUID.ToString()
                );

                // 如果用户取消输入
                if (string.IsNullOrEmpty(input))
                {
                    return;
                }

                // 验证输入的ID格式
                if (long.TryParse(input.Trim(), out long uid) && uid > 0)
                {
                    // 设置B站用户ID
                    display_func_bilibili_fans.SetBilibiliUID(uid);
                    
                    // 切换到B站粉丝数显示模式（索引4）
                    SetDisplayFuncIndex(4);
                    
                    // 显示确认消息
                    MessageBox.Show($"已设置B站用户ID为：{uid}\n" +
                                  "正在切换到粉丝数显示模式...\n" +
                                  "首次获取数据可能需要几秒钟时间。",
                                  "设置成功", 
                                  MessageBoxButtons.OK, 
                                  MessageBoxIcon.Information);
                }
                else
                {
                    // 输入格式错误
                    MessageBox.Show("输入的ID格式不正确！\n" +
                                  "请输入有效的数字ID。\n\n" +
                                  "提示：B站用户ID通常是一串数字，可以在用户主页URL中找到。",
                                  "输入错误", 
                                  MessageBoxButtons.OK, 
                                  MessageBoxIcon.Error);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"设置B站粉丝数显示时发生错误：\n{ex.Message}",
                              "错误", 
                              MessageBoxButtons.OK, 
                              MessageBoxIcon.Error);
            }
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

        string url = "www.bilibili.com/video/BV1R4hRz3E7H/?spm_id_from=333.1387.homepage.video_card.click";
        private void linkLabel1_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            System.Diagnostics.Process.Start(url);
        }

        // 简单的输入对话框方法
        private string ShowInputDialog(string message, string title, string defaultValue = "")
        {
            Form inputForm = new Form()
            {
                Width = 400,
                Height = 250,
                Text = title,
                StartPosition = FormStartPosition.CenterParent,
                FormBorderStyle = FormBorderStyle.FixedDialog,
                MaximizeBox = false,
                MinimizeBox = false
            };

            Label label = new Label()
            {
                Text = message,
                Top = 20,
                Left = 20,
                Width = 340,
                Height = 80
            };

            TextBox textBox = new TextBox()
            {
                Text = defaultValue,
                Top = 110,
                Left = 20,
                Width = 340
            };

            Button okButton = new Button()
            {
                Text = "确定",
                DialogResult = DialogResult.OK,
                Top = 150,
                Left = 200,
                Width = 80
            };

            Button cancelButton = new Button()
            {
                Text = "取消",
                DialogResult = DialogResult.Cancel,
                Top = 150,
                Left = 290,
                Width = 80
            };

            inputForm.Controls.AddRange(new Control[] { label, textBox, okButton, cancelButton });
            inputForm.AcceptButton = okButton;
            inputForm.CancelButton = cancelButton;

            return inputForm.ShowDialog() == DialogResult.OK ? textBox.Text : null;
        }

        private void button14_Click(object sender, EventArgs e)
        {
            if (DisplayConfig.CurrentBoardType != LEDBoardType.Board16x16) return;
            // B站粉丝数显示功能
            try
            {
                // 弹出对话框让用户输入B站用户ID
                string input = ShowInputDialog(
                    "请输入B站用户ID（UID）：\n\n" +
                    "示例：33028512\n" +
                    "您可以在B站用户主页的URL中找到UID\n" +
                    "格式：https://space.bilibili.com/用户ID",
                    "B站粉丝数显示设置",
                    display_func_bilibili_fans.BilibiliUID.ToString()
                );

                // 如果用户取消输入
                if (string.IsNullOrEmpty(input))
                {
                    return;
                }

                // 验证输入的ID格式
                if (long.TryParse(input.Trim(), out long uid) && uid > 0)
                {
                    // 设置B站用户ID
                    display_func_bilibili_fans.SetBilibiliUID(uid);

                    // 显示确认消息
                    MessageBox.Show($"已设置B站用户ID为：{uid}\n" +
                                  "正在切换到粉丝数显示模式...\n" +
                                  "首次获取数据可能需要几秒钟时间。",
                                  "设置成功",
                                  MessageBoxButtons.OK,
                                  MessageBoxIcon.Information);
                }
                else
                {
                    // 输入格式错误
                    MessageBox.Show("输入的ID格式不正确！\n" +
                                  "请输入有效的数字ID。\n\n" +
                                  "提示：B站用户ID通常是一串数字，可以在用户主页URL中找到。",
                                  "输入错误",
                                  MessageBoxButtons.OK,
                                  MessageBoxIcon.Error);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"设置B站粉丝数显示时发生错误：\n{ex.Message}",
                              "错误",
                              MessageBoxButtons.OK,
                              MessageBoxIcon.Error);
            }
        }
    }
}
