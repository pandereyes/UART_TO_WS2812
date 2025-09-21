using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;

namespace UART_TO_WS2812
{
    static class Program
    {
        /// <summary>
        /// 应用程序的主入口点。
        /// </summary>
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            
            // 初始化配置管理器
            ConfigManager.Initialize();
            ConfigManager.ApplyConfig();
            
            Application.Run(new Form1());
        }
    }
}
