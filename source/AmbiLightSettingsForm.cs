using System;
using System.Windows.Forms;

namespace UART_TO_WS2812
{
    public partial class AmbiLightSettingsForm : Form
    {
        private NumericUpDown numTopLEDs;
        private NumericUpDown numBottomLEDs;
        private NumericUpDown numLeftLEDs;
        private NumericUpDown numRightLEDs;
        private ComboBox comboStartPosition;
        private ComboBox comboDirection;
        private NumericUpDown numSamplePercent;
        private NumericUpDown numRefreshRate;
        
        private Label lblTopLEDs;
        private Label lblBottomLEDs;
        private Label lblLeftLEDs;
        private Label lblRightLEDs;
        private Label lblStartPosition;
        private Label lblDirection;
        private Label lblSamplePercent;
        private Label lblRefreshRate;
        private Label lblTotalLEDs;
        private Label lblTotal;
        
        private Button btnOK;
        private Button btnCancel;

        public AmbiLightSettingsForm()
        {
            InitializeComponent();
            LoadCurrentSettings();
        }

        private void InitializeComponent()
        {
            // 初始化所有控件
            this.numTopLEDs = new System.Windows.Forms.NumericUpDown();
            this.numBottomLEDs = new System.Windows.Forms.NumericUpDown();
            this.numLeftLEDs = new System.Windows.Forms.NumericUpDown();
            this.numRightLEDs = new System.Windows.Forms.NumericUpDown();
            this.comboStartPosition = new System.Windows.Forms.ComboBox();
            this.comboDirection = new System.Windows.Forms.ComboBox();
            this.numSamplePercent = new System.Windows.Forms.NumericUpDown();
            this.numRefreshRate = new System.Windows.Forms.NumericUpDown();
            
            this.lblTopLEDs = new System.Windows.Forms.Label();
            this.lblBottomLEDs = new System.Windows.Forms.Label();
            this.lblLeftLEDs = new System.Windows.Forms.Label();
            this.lblRightLEDs = new System.Windows.Forms.Label();
            this.lblStartPosition = new System.Windows.Forms.Label();
            this.lblDirection = new System.Windows.Forms.Label();
            this.lblSamplePercent = new System.Windows.Forms.Label();
            this.lblRefreshRate = new System.Windows.Forms.Label();
            this.lblTotal = new System.Windows.Forms.Label();
            this.lblTotalLEDs = new System.Windows.Forms.Label();
            
            this.btnOK = new System.Windows.Forms.Button();
            this.btnCancel = new System.Windows.Forms.Button();

            // LED数量设置
            this.numTopLEDs.Location = new System.Drawing.Point(120, 20);
            this.numTopLEDs.Maximum = 100;
            this.numTopLEDs.Name = "numTopLEDs";
            this.numTopLEDs.Size = new System.Drawing.Size(80, 20);
            this.numTopLEDs.ValueChanged += new System.EventHandler(this.UpdateTotalLEDs);

            this.numBottomLEDs.Location = new System.Drawing.Point(120, 50);
            this.numBottomLEDs.Maximum = 100;
            this.numBottomLEDs.Size = new System.Drawing.Size(80, 20);
            this.numBottomLEDs.ValueChanged += new System.EventHandler(this.UpdateTotalLEDs);

            this.numLeftLEDs.Location = new System.Drawing.Point(120, 80);
            this.numLeftLEDs.Maximum = 100;
            this.numLeftLEDs.Size = new System.Drawing.Size(80, 20);
            this.numLeftLEDs.ValueChanged += new System.EventHandler(this.UpdateTotalLEDs);

            this.numRightLEDs.Location = new System.Drawing.Point(120, 110);
            this.numRightLEDs.Maximum = 100;
            this.numRightLEDs.Size = new System.Drawing.Size(80, 20);
            this.numRightLEDs.ValueChanged += new System.EventHandler(this.UpdateTotalLEDs);

            // 起始位置设置
            this.comboStartPosition.Items.AddRange(new object[] { "左上角", "左下角", "右上角", "右下角" });
            this.comboStartPosition.Location = new System.Drawing.Point(120, 170);
            this.comboStartPosition.DropDownStyle = ComboBoxStyle.DropDownList;
            this.comboStartPosition.Size = new System.Drawing.Size(80, 20);
            this.comboStartPosition.SelectedIndex = 0;
            
            // 环绕方向设置
            this.comboDirection.Items.AddRange(new object[] { "顺时针", "逆时针" });
            this.comboDirection.Location = new System.Drawing.Point(120, 200);
            this.comboDirection.DropDownStyle = ComboBoxStyle.DropDownList;
            this.comboDirection.Size = new System.Drawing.Size(80, 20);
            this.comboDirection.SelectedIndex = 0;
            
            // 采样百分比设置
            this.numSamplePercent.Location = new System.Drawing.Point(120, 230);
            this.numSamplePercent.Minimum = 1;
            this.numSamplePercent.Maximum = 20;
            this.numSamplePercent.Value = 2;
            this.numSamplePercent.Size = new System.Drawing.Size(80, 20);
            
            // 刷新频率设置
            this.numRefreshRate.Location = new System.Drawing.Point(120, 260);
            this.numRefreshRate.Minimum = 10;
            this.numRefreshRate.Maximum = 60;
            this.numRefreshRate.Value = 30;
            this.numRefreshRate.Size = new System.Drawing.Size(80, 20);

            // 标签设置
            this.lblTopLEDs.Text = "顶部LED数量:";
            this.lblTopLEDs.Location = new System.Drawing.Point(20, 22);
            this.lblTopLEDs.Size = new System.Drawing.Size(90, 13);

            this.lblBottomLEDs.Text = "底部LED数量:";
            this.lblBottomLEDs.Location = new System.Drawing.Point(20, 52);
            this.lblBottomLEDs.Size = new System.Drawing.Size(90, 13);

            this.lblLeftLEDs.Text = "左侧LED数量:";
            this.lblLeftLEDs.Location = new System.Drawing.Point(20, 82);
            this.lblLeftLEDs.Size = new System.Drawing.Size(90, 13);

            this.lblRightLEDs.Text = "右侧LED数量:";
            this.lblRightLEDs.Location = new System.Drawing.Point(20, 112);
            this.lblRightLEDs.Size = new System.Drawing.Size(90, 13);

            this.lblTotal.Text = "总LED数量:";
            this.lblTotal.Location = new System.Drawing.Point(20, 142);
            this.lblTotal.Size = new System.Drawing.Size(90, 13);

            this.lblTotalLEDs.Text = "0";
            this.lblTotalLEDs.Location = new System.Drawing.Point(120, 142);
            this.lblTotalLEDs.Size = new System.Drawing.Size(80, 13);

            this.lblStartPosition.Text = "起始位置:";
            this.lblStartPosition.Location = new System.Drawing.Point(20, 172);
            this.lblStartPosition.Size = new System.Drawing.Size(90, 13);

            this.lblDirection.Text = "环绕方向:";
            this.lblDirection.Location = new System.Drawing.Point(20, 202);
            this.lblDirection.Size = new System.Drawing.Size(90, 13);

            this.lblSamplePercent.Text = "边缘采样%:";
            this.lblSamplePercent.Location = new System.Drawing.Point(20, 232);
            this.lblSamplePercent.Size = new System.Drawing.Size(90, 13);

            this.lblRefreshRate.Text = "刷新频率Hz:";
            this.lblRefreshRate.Location = new System.Drawing.Point(20, 262);
            this.lblRefreshRate.Size = new System.Drawing.Size(90, 13);

            // 按钮设置
            this.btnOK.DialogResult = System.Windows.Forms.DialogResult.OK;
            this.btnOK.Location = new System.Drawing.Point(40, 300);
            this.btnOK.Size = new System.Drawing.Size(75, 23);
            this.btnOK.Text = "确定";
            this.btnOK.Click += new System.EventHandler(this.btnOK_Click);

            this.btnCancel.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.btnCancel.Location = new System.Drawing.Point(130, 300);
            this.btnCancel.Size = new System.Drawing.Size(75, 23);
            this.btnCancel.Text = "取消";

            // Form设置
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(280, 340);
            this.Controls.Add(this.numTopLEDs);
            this.Controls.Add(this.numBottomLEDs);
            this.Controls.Add(this.numLeftLEDs);
            this.Controls.Add(this.numRightLEDs);
            this.Controls.Add(this.comboStartPosition);
            this.Controls.Add(this.comboDirection);
            this.Controls.Add(this.numSamplePercent);
            this.Controls.Add(this.numRefreshRate);
            this.Controls.Add(this.lblTopLEDs);
            this.Controls.Add(this.lblBottomLEDs);
            this.Controls.Add(this.lblLeftLEDs);
            this.Controls.Add(this.lblRightLEDs);
            this.Controls.Add(this.lblTotal);
            this.Controls.Add(this.lblTotalLEDs);
            this.Controls.Add(this.lblStartPosition);
            this.Controls.Add(this.lblDirection);
            this.Controls.Add(this.lblSamplePercent);
            this.Controls.Add(this.lblRefreshRate);
            this.Controls.Add(this.btnOK);
            this.Controls.Add(this.btnCancel);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "AmbiLightSettingsForm";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "屏光同步设置";
            this.ResumeLayout(false);
        }

        private void LoadCurrentSettings()
        {
            var config = DisplayConfig.AmbiLightSettings;
            numTopLEDs.Value = config.TopLEDs;
            numBottomLEDs.Value = config.BottomLEDs;
            numLeftLEDs.Value = config.LeftLEDs;
            numRightLEDs.Value = config.RightLEDs;
            
            // 加载起始位置
            comboStartPosition.SelectedIndex = (int)config.StartPosition;
            
            // 加载环绕方向
            comboDirection.SelectedIndex = (int)config.Direction;
            
            // 加载采样百分比和刷新频率
            numSamplePercent.Value = config.SamplePercent;
            numRefreshRate.Value = config.RefreshRate;
            
            UpdateTotalLEDs(null, null);
        }

        private void UpdateTotalLEDs(object sender, EventArgs e)
        {
            int total = (int)(numTopLEDs.Value + numBottomLEDs.Value + numLeftLEDs.Value + numRightLEDs.Value);
            lblTotalLEDs.Text = total.ToString();
        }

        private void btnOK_Click(object sender, EventArgs e)
        {
            var config = DisplayConfig.AmbiLightSettings;
            
            // 获取选择的起始位置
            AmbiLightStartPosition startPosition = (AmbiLightStartPosition)comboStartPosition.SelectedIndex;
            
            // 获取选择的环绕方向
            AmbiLightDirection direction = (AmbiLightDirection)comboDirection.SelectedIndex;
            
            // 更新配置
            config.UpdateConfig(
                (int)numTopLEDs.Value,
                (int)numBottomLEDs.Value,
                (int)numLeftLEDs.Value,
                (int)numRightLEDs.Value,
                startPosition,
                direction,
                (int)numSamplePercent.Value,
                (int)numRefreshRate.Value
            );
        }
    }
}