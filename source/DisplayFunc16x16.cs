using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using UART_TO_WS2812;

namespace UART_TO_WS2812
{



    public class display_16x16_init
    {
        public void showlist_init()
        {
            display_refresh.display_showlist_add(new display_func_draw());
            display_refresh.display_showlist_add(new display_func_music_spectrum());
            display_refresh.display_showlist_add(new display_func_picture());
            display_refresh.display_showlist_add(new display_func_code_rain());
            display_refresh.display_showlist_add(new display_func_bilibili_fans());
        }
    }

    // B站API数据模型类
    public class BilibiliApiResponse
    {
        public int code { get; set; }
        public string message { get; set; }
        public int ttl { get; set; }
        public BilibiliData data { get; set; }
    }

    public class BilibiliData
    {
        public long mid { get; set; }
        public int following { get; set; }
        public int whisper { get; set; }
        public int black { get; set; }
        public int follower { get; set; }  // 粉丝数
    }

    // B站API服务类
    public static class BilibiliApiService
    {
        private static readonly HttpClient httpClient = new HttpClient();

        static BilibiliApiService()
        {
            // 设置User-Agent避免被反爬
            httpClient.DefaultRequestHeaders.Add("User-Agent",
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/91.0.4472.124 Safari/537.36");
        }

        /// <summary>
        /// 获取B站用户粉丝数据
        /// </summary>
        /// <param name="uid">B站用户ID</param>
        /// <returns>粉丝数，获取失败返回-1</returns>
        public static async Task<int> GetFollowerCountAsync(long uid)
        {
            try
            {
                string url = $"https://api.bilibili.com/x/relation/stat?vmid={uid}";
                string jsonResponse = await httpClient.GetStringAsync(url);

                // 使用正则表达式简单解析JSON中的follower字段
                // JSON格式: {"code":0,"message":"0","ttl":1,"data":{"mid":33028512,"following":362,"whisper":0,"black":2,"follower":250}}
                var followerMatch = Regex.Match(jsonResponse, @"""follower"":(\d+)");
                var codeMatch = Regex.Match(jsonResponse, @"""code"":(\d+)");

                if (codeMatch.Success && codeMatch.Groups[1].Value == "0" && followerMatch.Success)
                {
                    if (int.TryParse(followerMatch.Groups[1].Value, out int followerCount))
                    {
                        return followerCount;
                    }
                }

                return -1;
            }
            catch (Exception ex)
            {
                // 记录错误但不抛出异常
                System.Diagnostics.Debug.WriteLine($"获取B站粉丝数失败: {ex.Message}");
                return -1;
            }
        }

        /// <summary>
        /// 同步版本的获取粉丝数方法
        /// </summary>
        /// <param name="uid">B站用户ID</param>
        /// <returns>粉丝数，获取失败返回-1</returns>
        public static int GetFollowerCount(long uid)
        {
            try
            {
                return GetFollowerCountAsync(uid).Result;
            }
            catch
            {
                return -1;
            }
        }
    }

    // B站粉丝数显示功能类
    internal class display_func_bilibili_fans : IShowable
    {
        // B站用户ID，需要设置
        public static long BilibiliUID { get; set; } = 33028512; // 默认ID，可通过UI设置

        // 粉丝数据
        private static int currentFollowers = -1;
        private static int previousFollowers = -1;
        private static DateTime lastUpdateTime = DateTime.MinValue;
        private static readonly TimeSpan updateInterval = TimeSpan.FromSeconds(5); // 30秒更新一次

        // 闪烁效果相关
        private static int blink_cnt = 0;
        private static bool isShowingBlink = false;
        private static DateTime lastBlinkTime = DateTime.MinValue;
        private static readonly TimeSpan blinkInterval = TimeSpan.FromMilliseconds(500);

        // b站图标
        private static UInt32 bilibili_color = 0x0000FF;

        private static readonly byte[,,] bilibiliIcon = new byte[1, 8, 8]
        {
            { // 0
                { 0,1,0,0,0,0,1,0 },
                { 0,0,1,0,0,1,0,0 },
                { 0,1,1,1,1,1,1,0 },
                { 1,0,0,0,0,0,0,1 },
                { 1,0,1,0,0,1,0,1 },
                { 1,0,0,1,1,0,0,1 },
                { 1,0,0,0,0,0,0,1 },
                { 0,1,1,1,1,1,1,0 }
            }
        };

        public static void showBilibiliIcon(byte x, byte y, uint color)
        {

            // 在指定3x5区域显示数字
            for (int i = 0; i < 8; i++)
            {
                for (int j = 0; j < 8; j++)
                {
                    if (bilibiliIcon[0, 7 - i, j] == 1)
                    {
                        // 确保坐标在显示范围内
                        if ((x + j) < DisplayConfig.CurrentConfig.Width && (y + i) < DisplayConfig.CurrentConfig.Height)
                        {
                            display_global_define.g_display_data[(x + j) * DisplayConfig.CurrentConfig.Width + (y + i)] = color;
                        }
                    }
                }
            }
        }


        public void show()
        {
            DateTime now = DateTime.Now;

            // 定期更新粉丝数据
            if (now - lastUpdateTime > updateInterval)
            {
                UpdateFollowersData();
                lastUpdateTime = now;
            }

            // 清空显示数据
            Array.Clear(display_global_define.g_display_data, 0, display_global_define.g_display_data.Length);

            if (isShowingBlink)
            {
                if (now - lastBlinkTime > blinkInterval)
                {
                    if (blink_cnt % 2 == 0)
                    {
                        bilibili_color = 0xFF0080;
                    }
                    else
                    {
                        bilibili_color = 0x0000FF;
                    }
                    lastBlinkTime = now;
                    blink_cnt++;
                    if (blink_cnt > 5)
                    {
                        blink_cnt = 0;
                        isShowingBlink = false;
                    }
                }
            }
            showBilibiliIcon(0, 8, bilibili_color);
            ShowFollowersCount();
        }

        // 更新粉丝数据
        private void UpdateFollowersData()
        {
            Task.Run(async () =>
            {
                try
                {
                    int newFollowers = await BilibiliApiService.GetFollowerCountAsync(BilibiliUID);

                    if (newFollowers > 0)
                    {
                        previousFollowers = currentFollowers;
                        currentFollowers = newFollowers;

                        // 检测粉丝数增加
                        if (previousFollowers > 0 && newFollowers > previousFollowers)
                        {
                            isShowingBlink = true;
                            lastBlinkTime = DateTime.Now;
                        }
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"更新粉丝数据失败: {ex.Message}");
                }
            });
        }

        // 显示普通的粉丝数
        private void ShowFollowersCount()
        {
            if (currentFollowers <= 0)
            {
                // 显示"获取中..."的提示
                ShowLoadingMessage();
                return;
            }

            // 将粉丝数转换为字符串并显示
            string fansStr = currentFollowers.ToString();
            DisplayNumberString(fansStr, 0x00FF00); // 绿色显示
        }

        // 显示加载中消息
        private void ShowLoadingMessage()
        {
            // 简单的点阵动画表示加载中
            int animFrame = (int)((DateTime.Now.Ticks / TimeSpan.TicksPerSecond) % 4);

            // 在中央显示一个旋转的点
            int centerX = DisplayConfig.CurrentConfig.Width / 2;
            int centerY = DisplayConfig.CurrentConfig.Height / 2 - 4;

            int[] offsetX = { 0, 1, 0, -1 };
            int[] offsetY = { -1, 0, 1, 0 };

            int x = centerX + offsetX[animFrame];
            int y = centerY + offsetY[animFrame];

            if (x >= 0 && x < DisplayConfig.CurrentConfig.Width && y >= 0 && y < DisplayConfig.CurrentConfig.Height)
            {
                int index = x * DisplayConfig.CurrentConfig.Height + y;
                if (index < display_global_define.g_display_data.Length)
                {
                    display_global_define.g_display_data[index] = 0x0080FF; // 蓝色加载指示
                }
            }
        }

        // 显示数字字符串
        private void DisplayNumberString(string numberStr, uint color, int yOffset = 0)
        {
            if (string.IsNullOrEmpty(numberStr)) return;

            // 计算起始位置以居中显示
            int totalWidth = numberStr.Length * 4 - 1; // 每个数字3宽度 + 1间隔
            int startX = Math.Max(0, (DisplayConfig.CurrentConfig.Width - totalWidth) / 2) + 1;
            int startY = Math.Max(0, (DisplayConfig.CurrentConfig.Height - 5 + yOffset) / 2) - 4;

            for (int i = 0; i < numberStr.Length; i++)
            {
                char c = numberStr[i];

                if (c >= '0' && c <= '9')
                {
                    // 显示数字
                    byte digit = (byte)(c - '0');
                    int x = startX + i * 4;

                    if (x + 3 <= DisplayConfig.CurrentConfig.Width && startY + 5 <= DisplayConfig.CurrentConfig.Height)
                    {
                        display_number.show((byte)x, (byte)startY, digit, color);
                    }
                }
            }
        }

        // 设置B站用户ID
        public static void SetBilibiliUID(long uid)
        {
            BilibiliUID = uid;
            // 重置数据，强制重新获取
            currentFollowers = -1;
            previousFollowers = -1;
            lastUpdateTime = DateTime.MinValue;
        }

        // 获取当前粉丝数
        public static int GetCurrentFollowers()
        {
            return currentFollowers;
        }
    }
}