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

    // BվAPI����ģ����
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
        public int follower { get; set; }  // ��˿��
    }

    // BվAPI������
    public static class BilibiliApiService
    {
        private static readonly HttpClient httpClient = new HttpClient();

        static BilibiliApiService()
        {
            // ����User-Agent���ⱻ����
            httpClient.DefaultRequestHeaders.Add("User-Agent",
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/91.0.4472.124 Safari/537.36");
        }

        /// <summary>
        /// ��ȡBվ�û���˿����
        /// </summary>
        /// <param name="uid">Bվ�û�ID</param>
        /// <returns>��˿������ȡʧ�ܷ���-1</returns>
        public static async Task<int> GetFollowerCountAsync(long uid)
        {
            try
            {
                string url = $"https://api.bilibili.com/x/relation/stat?vmid={uid}";
                string jsonResponse = await httpClient.GetStringAsync(url);

                // ʹ���������ʽ�򵥽���JSON�е�follower�ֶ�
                // JSON��ʽ: {"code":0,"message":"0","ttl":1,"data":{"mid":33028512,"following":362,"whisper":0,"black":2,"follower":250}}
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
                // ��¼���󵫲��׳��쳣
                System.Diagnostics.Debug.WriteLine($"��ȡBվ��˿��ʧ��: {ex.Message}");
                return -1;
            }
        }

        /// <summary>
        /// ͬ���汾�Ļ�ȡ��˿������
        /// </summary>
        /// <param name="uid">Bվ�û�ID</param>
        /// <returns>��˿������ȡʧ�ܷ���-1</returns>
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

    // Bվ��˿����ʾ������
    internal class display_func_bilibili_fans : IShowable
    {
        // Bվ�û�ID����Ҫ����
        private static long bilibiliUID = 33028512;
        
        public static long BilibiliUID 
        { 
            get => bilibiliUID; 
            set 
            { 
                bilibiliUID = value;
                ConfigManager.SetLong(ConfigManager.Keys.BilibiliUID, value);
                ConfigManager.SaveConfig();
            } 
        }

        // 从配置加载UID
        public static void LoadUIDFromConfig()
        {
            bilibiliUID = ConfigManager.GetLong(ConfigManager.Keys.BilibiliUID, 33028512);
        }

        // ��˿����
        private static int currentFollowers = -1;
        private static int previousFollowers = -1;
        private static DateTime lastUpdateTime = DateTime.MinValue;
        private static readonly TimeSpan updateInterval = TimeSpan.FromSeconds(5); // 30�����һ��

        // ��˸Ч�����
        private static int blink_cnt = 0;
        private static bool isShowingBlink = false;
        private static DateTime lastBlinkTime = DateTime.MinValue;
        private static readonly TimeSpan blinkInterval = TimeSpan.FromMilliseconds(500);

        // bվͼ��
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

            // ��ָ��3x5������ʾ����
            for (int i = 0; i < 8; i++)
            {
                for (int j = 0; j < 8; j++)
                {
                    if (bilibiliIcon[0, 7 - i, j] == 1)
                    {
                        // ȷ����������ʾ��Χ��
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

            // ���ڸ��·�˿����
            if (now - lastUpdateTime > updateInterval)
            {
                UpdateFollowersData();
                lastUpdateTime = now;
            }

            // �����ʾ����
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

        // ���·�˿����
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

                        // ����˿������
                        if (previousFollowers > 0 && newFollowers > previousFollowers)
                        {
                            isShowingBlink = true;
                            lastBlinkTime = DateTime.Now;
                        }
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"���·�˿����ʧ��: {ex.Message}");
                }
            });
        }

        // ��ʾ��ͨ�ķ�˿��
        private void ShowFollowersCount()
        {
            if (currentFollowers <= 0)
            {
                // ��ʾ"��ȡ��..."����ʾ
                ShowLoadingMessage();
                return;
            }

            // ����˿��ת��Ϊ�ַ�������ʾ
            string fansStr = currentFollowers.ToString();
            DisplayNumberString(fansStr, 0x00FF00); // ��ɫ��ʾ
        }

        // ��ʾ��������Ϣ
        private void ShowLoadingMessage()
        {
            // �򵥵ĵ��󶯻���ʾ������
            int animFrame = (int)((DateTime.Now.Ticks / TimeSpan.TicksPerSecond) % 4);

            // ��������ʾһ����ת�ĵ�
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
                    display_global_define.g_display_data[index] = 0x0080FF; // ��ɫ����ָʾ
                }
            }
        }

        // ��ʾ�����ַ���
        private void DisplayNumberString(string numberStr, uint color, int yOffset = 0)
        {
            if (string.IsNullOrEmpty(numberStr)) return;

            // ������ʼλ���Ծ�����ʾ
            int totalWidth = numberStr.Length * 4 - 1; // ÿ������3���� + 1���
            int startX = Math.Max(0, (DisplayConfig.CurrentConfig.Width - totalWidth) / 2) + 1;
            int startY = Math.Max(0, (DisplayConfig.CurrentConfig.Height - 5 + yOffset) / 2) - 4;

            for (int i = 0; i < numberStr.Length; i++)
            {
                char c = numberStr[i];

                if (c >= '0' && c <= '9')
                {
                    // ��ʾ����
                    byte digit = (byte)(c - '0');
                    int x = startX + i * 4;

                    if (x + 3 <= DisplayConfig.CurrentConfig.Width && startY + 5 <= DisplayConfig.CurrentConfig.Height)
                    {
                        display_number.show((byte)x, (byte)startY, digit, color);
                    }
                }
            }
        }

        // ����Bվ�û�ID
        public static void SetBilibiliUID(long uid)
        {
            BilibiliUID = uid;
            // �������ݣ�ǿ�����»�ȡ
            currentFollowers = -1;
            previousFollowers = -1;
            lastUpdateTime = DateTime.MinValue;
        }

        // ��ȡ��ǰ��˿��
        public static int GetCurrentFollowers()
        {
            return currentFollowers;
        }
    }
}