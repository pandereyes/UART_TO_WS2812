using System;
using System.Drawing;
using System.Windows.Forms;

namespace 串口驱动WS2812
{
    public partial class ImageSettingsForm : Form
    {
        // 参数属性
        public float Brightness { get; set; } = 1.0f;
        public float Contrast { get; set; } = 2.5f;
        public float Saturation { get; set; } = 1.3f;
        public float Threshold { get; set; } = 0.5f;
        public bool ForceBinarization { get; set; } = false;
        
        // 预览回调
        public event Action ParametersChanged;
        
        private TrackBar brightnessTrackBar;
        private TrackBar contrastTrackBar;
        private TrackBar saturationTrackBar;
        private TrackBar thresholdTrackBar;
        private CheckBox binarizationCheckBox;
        private Label brightnessLabel;
        private Label contrastLabel;
        private Label saturationLabel;
        private Label thresholdLabel;
        private Button applyButton;
        private Button cancelButton;
        
        public ImageSettingsForm()
        {
            SetupControls();
        }
        
        // 设置初始参数值
        public void SetInitialValues(float brightness, float contrast, float saturation, float threshold, bool forceBinarization)
        {
            Brightness = brightness;
            Contrast = contrast;
            Saturation = saturation;
            Threshold = threshold;
            ForceBinarization = forceBinarization;
            
            // 更新控件值
            if (brightnessTrackBar != null)
            {
                brightnessTrackBar.Value = (int)(Brightness * 100);
                contrastTrackBar.Value = (int)(Contrast * 10);
                saturationTrackBar.Value = (int)(Saturation * 100);
                thresholdTrackBar.Value = (int)(Threshold * 100);
                binarizationCheckBox.Checked = ForceBinarization;
                
                
                UpdateLabels();
            }
        }
        
        private void SetupControls()
        {
            this.Text = "图片处理参数设置";
            this.Size = new Size(400, 350);
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.StartPosition = FormStartPosition.CenterParent;
            
            // 处理窗体关闭事件
            this.FormClosing += ImageSettingsForm_FormClosing;
            
            // 亮度调节
            brightnessLabel = new Label()
            {
                Text = "亮度:",
                Location = new Point(20, 20),
                Size = new Size(80, 20)
            };
            
            brightnessTrackBar = new TrackBar()
            {
                Minimum = 0,
                Maximum = 200,
                Value = 100,
                Location = new Point(100, 20),
                Size = new Size(250, 45),
                TickFrequency = 20
            };
            brightnessTrackBar.Scroll += OnParameterChanged;
            
            // 对比度调节
            contrastLabel = new Label()
            {
                Text = "对比度:",
                Location = new Point(20, 70),
                Size = new Size(80, 20)
            };
            
            contrastTrackBar = new TrackBar()
            {
                Minimum = 10,
                Maximum = 100,
                Value = 25,
                Location = new Point(100, 70),
                Size = new Size(250, 45),
                TickFrequency = 10
            };
            contrastTrackBar.Scroll += OnParameterChanged;
            
            // 饱和度调节
            saturationLabel = new Label()
            {
                Text = "饱和度:",
                Location = new Point(20, 120),
                Size = new Size(80, 20)
            };
            
            saturationTrackBar = new TrackBar()
            {
                Minimum = 0,
                Maximum = 200,
                Value = 130,
                Location = new Point(100, 120),
                Size = new Size(250, 45),
                TickFrequency = 20
            };
            saturationTrackBar.Scroll += OnParameterChanged;
            
            // 阈值调节
            thresholdLabel = new Label()
            {
                Text = "黑白阈值:",
                Location = new Point(20, 170),
                Size = new Size(100, 20) // 增加宽度以显示完整数值
            };
            
            thresholdTrackBar = new TrackBar()
            {
                Minimum = 10,
                Maximum = 90,
                Value = 50,
                Location = new Point(100, 170),
                Size = new Size(250, 45),
                TickFrequency = 10
            };
            thresholdTrackBar.Scroll += OnParameterChanged;
            
            // 二值化选项
            binarizationCheckBox = new CheckBox()
            {
                Text = "强制二值化（黑白模式）",
                Location = new Point(20, 220),
                Size = new Size(200, 20),
                Checked = false
            };
            binarizationCheckBox.CheckedChanged += OnParameterChanged;
            
            // 应用按钮
            applyButton = new Button()
            {
                Text = "应用",
                Location = new Point(200, 250),
                Size = new Size(80, 30)
            };
            applyButton.Click += ApplyButton_Click;
            
            // 取消按钮
            cancelButton = new Button()
            {
                Text = "重置",
                Location = new Point(290, 250),
                Size = new Size(80, 30)
            };
            cancelButton.Click += CancelButton_Click;
            
            // 添加控件到窗体
            this.Controls.AddRange(new Control[] {
                brightnessLabel, brightnessTrackBar,
                contrastLabel, contrastTrackBar,
                saturationLabel, saturationTrackBar,
                thresholdLabel, thresholdTrackBar,
                binarizationCheckBox,
                applyButton, cancelButton
            });
            
            UpdateLabels();
        }
        
        private void OnParameterChanged(object sender, EventArgs e)
        {
            // 更新参数值
            Brightness = brightnessTrackBar.Value / 100.0f;
            Contrast = contrastTrackBar.Value / 10.0f;
            Saturation = saturationTrackBar.Value / 100.0f;
            Threshold = thresholdTrackBar.Value / 100.0f;
            ForceBinarization = binarizationCheckBox.Checked;
            
            
            UpdateLabels();
            
            // 触发预览更新
            ParametersChanged?.Invoke();
        }
        
        private void UpdateLabels()
        {
            brightnessLabel.Text = $"亮度: {Brightness:F1}";
            contrastLabel.Text = $"对比度: {Contrast:F1}";
            saturationLabel.Text = $"饱和度: {Saturation:F1}";
            thresholdLabel.Text = $"黑白阈值: {Threshold:F2}";
            
        }
        
        private void ApplyButton_Click(object sender, EventArgs e)
        {
            // 更新最终参数值
            Brightness = brightnessTrackBar.Value / 100.0f;
            Contrast = contrastTrackBar.Value / 10.0f;
            Saturation = saturationTrackBar.Value / 100.0f;
            Threshold = thresholdTrackBar.Value / 100.0f;
            ForceBinarization = binarizationCheckBox.Checked;
            
            this.DialogResult = DialogResult.OK;
            this.Close();
        }
        
        private void CancelButton_Click(object sender, EventArgs e)
        {
            this.DialogResult = DialogResult.Cancel;
            this.Close();
        }
        
        private void ImageSettingsForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            // 如果用户点击标题栏关闭按钮，设置DialogResult为Cancel
            if (this.DialogResult == DialogResult.None)
            {
                this.DialogResult = DialogResult.Cancel;
            }
        }
    }
}