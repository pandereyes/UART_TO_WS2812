using System;
using UART_TO_WS2812;

namespace UART_TO_WS2812
{
    public class display_ambilight_init
    {
        private display_func_ambilight ambiLightFunc;

        public void showlist_init()
        {
            // 初始化环境光功能
            ambiLightFunc = new display_func_ambilight();
            display_refresh.display_showlist_add(ambiLightFunc);
        }

        public void Dispose()
        {
            ambiLightFunc?.Dispose();
        }

        public void UpdateConfig()
        {
            // 停止当前功能
            if (ambiLightFunc != null)
            {
                ambiLightFunc.Dispose();
                ambiLightFunc = null;
            }

            // 重新创建环境光功能
            ambiLightFunc = new display_func_ambilight();
            
            // 清空并重新添加到显示列表
            display_refresh.display_showlist_clear();
            display_refresh.display_showlist_add(ambiLightFunc);
        }
    }
}