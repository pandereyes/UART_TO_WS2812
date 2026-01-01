using System;
using System.IO;
using System.Collections.Generic;
using System.Globalization;

namespace UART_TO_WS2812
{
    public static class ConfigManager
    {
        private static readonly string ConfigFileName = "config.txt";
        private static readonly string ConfigFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, ConfigFileName);
        
        // 配置项字典
        private static Dictionary<string, string> configItems = new Dictionary<string, string>();

        // 配置项键名常量
        public static class Keys
        {
            public const string BoardType = "BoardType";
            public const string SerialPort = "SerialPort";
            public const string MusicSensitivity = "MusicSensitivity";
            
            // 图片处理参数
            public const string PictureBrightness = "PictureBrightness";
            public const string PictureContrast = "PictureContrast";
            public const string PictureSaturation = "PictureSaturation";
            public const string PictureThreshold = "PictureThreshold";
            public const string PictureForceBinarization = "PictureForceBinarization";
            
            // B站粉丝数UID
            public const string BilibiliUID = "BilibiliUID";
            
            // 环境光配置
            public const string AmbiLightTopLEDs = "AmbiLightTopLEDs";
            public const string AmbiLightBottomLEDs = "AmbiLightBottomLEDs";
            public const string AmbiLightLeftLEDs = "AmbiLightLeftLEDs";
            public const string AmbiLightRightLEDs = "AmbiLightRightLEDs";
            public const string AmbiLightSampleInterval = "AmbiLightSampleInterval";
            public const string AmbiLightSampleWidth = "AmbiLightSampleWidth";
            public const string AmbiLightStartPosition = "AmbiLightStartPosition";
            public const string AmbiLightDirection = "AmbiLightDirection";
            public const string AmbiLightSamplePercent = "AmbiLightSamplePercent";
            public const string AmbiLightRefreshRate = "AmbiLightRefreshRate";
            
            // LED亮度
            public const string LEDBrightness = "LEDBrightness";
        }

        // 默认配置值
        private static readonly Dictionary<string, string> DefaultValues = new Dictionary<string, string>
        {
            { Keys.BoardType, "16x16" },
            { Keys.SerialPort, "COM3" },
            { Keys.MusicSensitivity, "1" },
            { Keys.PictureBrightness, "1.0" },
            { Keys.PictureContrast, "2.5" },
            { Keys.PictureSaturation, "1.3" },
            { Keys.PictureThreshold, "0.5" },
            { Keys.PictureForceBinarization, "False" },
            { Keys.BilibiliUID, "33028512" },
            { Keys.AmbiLightTopLEDs, "30" },
            { Keys.AmbiLightBottomLEDs, "30" },
            { Keys.AmbiLightLeftLEDs, "20" },
            { Keys.AmbiLightRightLEDs, "20" },
            { Keys.AmbiLightSampleInterval, "50" },
            { Keys.AmbiLightSampleWidth, "20" },
            { Keys.AmbiLightStartPosition, "0" },
            { Keys.AmbiLightDirection, "0" },
            { Keys.AmbiLightSamplePercent, "2" },
            { Keys.AmbiLightRefreshRate, "30" },
            { Keys.LEDBrightness, "20" }
        };

        /// <summary>
        /// 初始化配置管理器，加载配置文件
        /// </summary>
        public static void Initialize()
        {
            LoadConfig();
        }

        /// <summary>
        /// 加载配置文件
        /// </summary>
        public static void LoadConfig()
        {
            try
            {
                if (File.Exists(ConfigFilePath))
                {
                    // 读取现有配置文件
                    string[] lines = File.ReadAllLines(ConfigFilePath);
                    configItems.Clear();

                    foreach (string line in lines)
                    {
                        if (string.IsNullOrWhiteSpace(line) || line.TrimStart().StartsWith("#"))
                            continue; // 跳过空行和注释行

                        int equalIndex = line.IndexOf('=');
                        if (equalIndex > 0)
                        {
                            string key = line.Substring(0, equalIndex).Trim();
                            string value = line.Substring(equalIndex + 1).Trim();
                            configItems[key] = value;
                        }
                    }

                    // 检查是否有缺失的配置项，如果有则补充默认值
                    bool needsSave = false;
                    foreach (var defaultItem in DefaultValues)
                    {
                        if (!configItems.ContainsKey(defaultItem.Key))
                        {
                            configItems[defaultItem.Key] = defaultItem.Value;
                            needsSave = true;
                        }
                    }

                    if (needsSave)
                    {
                        SaveConfig();
                    }
                }
                else
                {
                    // 配置文件不存在，创建默认配置
                    CreateDefaultConfig();
                }
            }
            catch (Exception ex)
            {
                // 加载失败时使用默认配置
                CreateDefaultConfig();
            }
        }

        /// <summary>
        /// 创建默认配置文件
        /// </summary>
        private static void CreateDefaultConfig()
        {
            configItems.Clear();
            foreach (var item in DefaultValues)
            {
                configItems[item.Key] = item.Value;
            }
            SaveConfig();
        }

        /// <summary>
        /// 保存配置文件
        /// </summary>
        public static void SaveConfig()
        {
            try
            {
                using (StreamWriter writer = new StreamWriter(ConfigFilePath, false, System.Text.Encoding.UTF8))
                {
                    writer.WriteLine("# WS2812 LED控制程序配置文件");
                    writer.WriteLine("# 修改此文件后重启程序生效");
                    writer.WriteLine("# 配置格式: 键名=值");
                    writer.WriteLine("");

                    writer.WriteLine("# 灯板类型 (8x8, 16x16, 1x30, ambilight)");
                    writer.WriteLine($"{Keys.BoardType}={GetString(Keys.BoardType)}");
                    writer.WriteLine("");

                    writer.WriteLine("# 串口设置");
                    writer.WriteLine($"{Keys.SerialPort}={GetString(Keys.SerialPort)}");
                    writer.WriteLine($"{Keys.LEDBrightness}={GetString(Keys.LEDBrightness)}");
                    writer.WriteLine("");

                    writer.WriteLine("# 音乐频谱设置");
                    writer.WriteLine($"{Keys.MusicSensitivity}={GetString(Keys.MusicSensitivity)}");
                    writer.WriteLine("");

                    writer.WriteLine("# 图片处理参数");
                    writer.WriteLine($"{Keys.PictureBrightness}={GetString(Keys.PictureBrightness)}");
                    writer.WriteLine($"{Keys.PictureContrast}={GetString(Keys.PictureContrast)}");
                    writer.WriteLine($"{Keys.PictureSaturation}={GetString(Keys.PictureSaturation)}");
                    writer.WriteLine($"{Keys.PictureThreshold}={GetString(Keys.PictureThreshold)}");
                    writer.WriteLine($"{Keys.PictureForceBinarization}={GetString(Keys.PictureForceBinarization)}");
                    writer.WriteLine("");

                    writer.WriteLine("# B站粉丝数功能");
                    writer.WriteLine($"{Keys.BilibiliUID}={GetString(Keys.BilibiliUID)}");
                    writer.WriteLine("");

                    writer.WriteLine("# 环境光设置");
                    writer.WriteLine($"{Keys.AmbiLightTopLEDs}={GetString(Keys.AmbiLightTopLEDs)}");
                    writer.WriteLine($"{Keys.AmbiLightBottomLEDs}={GetString(Keys.AmbiLightBottomLEDs)}");
                    writer.WriteLine($"{Keys.AmbiLightLeftLEDs}={GetString(Keys.AmbiLightLeftLEDs)}");
                    writer.WriteLine($"{Keys.AmbiLightRightLEDs}={GetString(Keys.AmbiLightRightLEDs)}");
                    writer.WriteLine($"{Keys.AmbiLightSampleInterval}={GetString(Keys.AmbiLightSampleInterval)}");
                    writer.WriteLine($"{Keys.AmbiLightSampleWidth}={GetString(Keys.AmbiLightSampleWidth)}");
                    writer.WriteLine($"{Keys.AmbiLightStartPosition}={GetString(Keys.AmbiLightStartPosition)}");
                    writer.WriteLine($"{Keys.AmbiLightDirection}={GetString(Keys.AmbiLightDirection)}");
                    writer.WriteLine($"{Keys.AmbiLightSamplePercent}={GetString(Keys.AmbiLightSamplePercent)}");
                    writer.WriteLine($"{Keys.AmbiLightRefreshRate}={GetString(Keys.AmbiLightRefreshRate)}");
                }
            }
            catch (Exception ex)
            {
                // 保存失败时忽略错误，避免程序崩溃
                System.Diagnostics.Debug.WriteLine($"保存配置文件失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 获取字符串配置值
        /// </summary>
        public static string GetString(string key, string defaultValue = "")
        {
            if (configItems.ContainsKey(key))
                return configItems[key];
            return DefaultValues.ContainsKey(key) ? DefaultValues[key] : defaultValue;
        }

        /// <summary>
        /// 设置字符串配置值
        /// </summary>
        public static void SetString(string key, string value)
        {
            configItems[key] = value;
        }

        /// <summary>
        /// 获取整数配置值
        /// </summary>
        public static int GetInt(string key, int defaultValue = 0)
        {
            string strValue = GetString(key);
            if (int.TryParse(strValue, out int result))
                return result;
            return defaultValue;
        }

        /// <summary>
        /// 设置整数配置值
        /// </summary>
        public static void SetInt(string key, int value)
        {
            configItems[key] = value.ToString();
        }

        /// <summary>
        /// 获取长整数配置值
        /// </summary>
        public static long GetLong(string key, long defaultValue = 0)
        {
            string strValue = GetString(key);
            if (long.TryParse(strValue, out long result))
                return result;
            return defaultValue;
        }

        /// <summary>
        /// 设置长整数配置值
        /// </summary>
        public static void SetLong(string key, long value)
        {
            configItems[key] = value.ToString();
        }

        /// <summary>
        /// 获取浮点数配置值
        /// </summary>
        public static float GetFloat(string key, float defaultValue = 0.0f)
        {
            string strValue = GetString(key);
            if (float.TryParse(strValue, NumberStyles.Float, CultureInfo.InvariantCulture, out float result))
                return result;
            return defaultValue;
        }

        /// <summary>
        /// 设置浮点数配置值
        /// </summary>
        public static void SetFloat(string key, float value)
        {
            configItems[key] = value.ToString(CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// 获取布尔配置值
        /// </summary>
        public static bool GetBool(string key, bool defaultValue = false)
        {
            string strValue = GetString(key);
            if (bool.TryParse(strValue, out bool result))
                return result;
            return defaultValue;
        }

        /// <summary>
        /// 设置布尔配置值
        /// </summary>
        public static void SetBool(string key, bool value)
        {
            configItems[key] = value.ToString();
        }

        /// <summary>
        /// 应用加载的配置到各个功能模块
        /// </summary>
        public static void ApplyConfig()
        {
            try
            {
                // 应用灯板类型
                string boardType = GetString(Keys.BoardType);
                DisplayConfig.SetBoardTypeFromString(boardType);

                // 应用环境光配置
                var ambiConfig = DisplayConfig.AmbiLightSettings;
                ambiConfig.UpdateConfig(
                    GetInt(Keys.AmbiLightTopLEDs, 30),
                    GetInt(Keys.AmbiLightBottomLEDs, 30),
                    GetInt(Keys.AmbiLightLeftLEDs, 20),
                    GetInt(Keys.AmbiLightRightLEDs, 20),
                    (AmbiLightStartPosition)GetInt(Keys.AmbiLightStartPosition, 0),
                    (AmbiLightDirection)GetInt(Keys.AmbiLightDirection, 0),
                    GetInt(Keys.AmbiLightSamplePercent, 2),
                    GetInt(Keys.AmbiLightRefreshRate, 30)
                );
                ambiConfig.SampleInterval = GetInt(Keys.AmbiLightSampleInterval, 50);
                ambiConfig.SampleWidth = GetInt(Keys.AmbiLightSampleWidth, 20);

                // 重新初始化显示数据数组
                display_global_define.ReinitializeDisplayData();

                // 应用音乐频谱灵敏度配置
                display_func_music_spectrum.LoadSensitivityFromConfig();

                // 应用图片处理参数配置
                display_func_picture_optimized.LoadImageParamsFromConfig();

                // 应用B站UID配置
                display_func_bilibili_fans.LoadUIDFromConfig();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"应用配置失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 从各个功能模块收集当前配置并保存
        /// </summary>
        public static void CollectAndSaveCurrentConfig()
        {
            try
            {
                // 收集当前配置
                CollectCurrentConfig();
                // 保存到文件
                SaveConfig();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"收集和保存配置失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 从各个功能模块收集当前配置
        /// </summary>
        public static void CollectCurrentConfig()
        {
            try
            {
                // 灯板类型
                string boardTypeString;
                switch (DisplayConfig.CurrentBoardType)
                {
                    case LEDBoardType.Board8x8:
                        boardTypeString = "8x8";
                        break;
                    case LEDBoardType.Board16x16:
                        boardTypeString = "16x16";
                        break;
                    case LEDBoardType.Board1x30:
                        boardTypeString = "1x30";
                        break;
                    case LEDBoardType.MonitorAmbiLight:
                        boardTypeString = "ambilight";
                        break;
                    default:
                        boardTypeString = "16x16";
                        break;
                }
                SetString(Keys.BoardType, boardTypeString);

                // 环境光配置
                var ambiConfig = DisplayConfig.AmbiLightSettings;
                SetInt(Keys.AmbiLightTopLEDs, ambiConfig.TopLEDs);
                SetInt(Keys.AmbiLightBottomLEDs, ambiConfig.BottomLEDs);
                SetInt(Keys.AmbiLightLeftLEDs, ambiConfig.LeftLEDs);
                SetInt(Keys.AmbiLightRightLEDs, ambiConfig.RightLEDs);
                SetInt(Keys.AmbiLightSampleInterval, ambiConfig.SampleInterval);
                SetInt(Keys.AmbiLightSampleWidth, ambiConfig.SampleWidth);
                SetInt(Keys.AmbiLightStartPosition, (int)ambiConfig.StartPosition);
                SetInt(Keys.AmbiLightDirection, (int)ambiConfig.Direction);
                SetInt(Keys.AmbiLightSamplePercent, ambiConfig.SamplePercent);
                SetInt(Keys.AmbiLightRefreshRate, ambiConfig.RefreshRate);

                // 音乐频谱灵敏度
                SetInt(Keys.MusicSensitivity, display_func_music_spectrum.GetSensitivity());

                // 图片处理参数
                SetFloat(Keys.PictureBrightness, display_func_picture_optimized.Brightness);
                SetFloat(Keys.PictureContrast, display_func_picture_optimized.Contrast);
                SetFloat(Keys.PictureSaturation, display_func_picture_optimized.Saturation);
                SetFloat(Keys.PictureThreshold, display_func_picture_optimized.Threshold);
                SetBool(Keys.PictureForceBinarization, display_func_picture_optimized.ForceBinarization);

                // B站UID
                SetLong(Keys.BilibiliUID, display_func_bilibili_fans.BilibiliUID);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"收集配置失败: {ex.Message}");
            }
        }
    }
}