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

    internal class display_8x8_init
    {
        public void showlist_init()
        {
            display_refresh.display_showlist_add(new display_func_draw());
            display_refresh.display_showlist_add(new display_func_music_spectrum());
            display_refresh.display_showlist_add(new display_func_picture());
            display_refresh.display_showlist_add(new display_func_code_rain());
        }
    }
}
            
