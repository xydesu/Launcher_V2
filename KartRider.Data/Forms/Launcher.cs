using ExcData;
using KartRider.Common.Data;
using KartRider.Common.Utilities;
using KartRider.IO.Packet;
using Launcher.Properties;
using LoggerLibrary;
using Profile;
using RHOParser;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace KartRider
{
    public partial class Launcher : Form
    {
        public string kartRiderDirectory;
        public static string KartRider;
        public static string pinFile;
        public static string pinFileBak;

        public Launcher()
        {
            RestorePinFile();
            this.InitializeComponent();
        }

        /// <summary>
        /// 恢复备份的 PIN 文件
        /// </summary>
        private void RestorePinFile()
        {
            if (string.IsNullOrEmpty(pinFileBak) || string.IsNullOrEmpty(pinFile))
                return;

            if (File.Exists(pinFileBak))
            {
                try
                {
                    if (File.Exists(pinFile))
                        File.Delete(pinFile);
                    File.Move(pinFileBak, pinFile);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"恢复 PIN 文件失败: {ex.Message}");
                }
            }
        }

        private void OnFormClosing(object sender, FormClosingEventArgs e)
        {
            RestorePinFile();
        }

        private void OnLoad(object sender, EventArgs e)
        {
            if (string.IsNullOrEmpty(pinFile) || !File.Exists(pinFile))
            {
                MessageBox.Show("PIN 文件路径无效或文件不存在！", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            try
            {
                PINFile val = new PINFile(pinFile);
                ProfileService.SettingConfig.ClientVersion = val.Header.MinorVersion;
                ProfileService.SettingConfig.LocaleID = val.Header.LocaleID;
                ProfileService.SettingConfig.nClientLoc = val.Header.Unk2;
                ProfileService.SaveSettings();
                ClientVersion.Text = val.Header.MinorVersion.ToString();
                Console.WriteLine($"ClientVersion: {val.Header.MinorVersion}");
                Console.WriteLine($"程序编译时间: {CompileTime.Time}");
                VersionLabel.Text = CompileTime.Time;
                Console.WriteLine("Process: {0}", KartRider);
                Load_Data();

                try
                {
                    RouterListener.Start();
                }
                catch (System.Net.Sockets.SocketException)
                {
                    LauncherSystem.MessageBoxType2();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"启动路由器监听失败: {ex.Message}");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"加载 PIN 文件失败: {ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void Start_Button_Click(object sender, EventArgs e)
        {
            if (Process.GetProcessesByName("KartRider").Length != 0)
            {
                LauncherSystem.MessageBoxType1();
            }
            else
            {
                var thread = new Thread(() =>
                {
                    try
                    {
                        LaunchGame();
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"启动游戏失败: {ex.Message}");
                    }
                })
                {
                    IsBackground = true,
                    Name = "GameLauncherThread"
                };
                thread.Start();
            }
        }

        /// <summary>
        /// 启动游戏的核心逻辑
        /// </summary>
        private void LaunchGame()
        {
            if (string.IsNullOrEmpty(pinFile) || !File.Exists(pinFile))
            {
                Console.WriteLine("PIN 文件不存在，无法启动游戏");
                return;
            }

            RestorePinFile();

            Console.WriteLine("Backing up old PinFile...");
            Console.WriteLine(pinFile);

            try
            {
                File.Copy(pinFile, pinFileBak, overwrite: true);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"备份 PIN 文件失败: {ex.Message}");
                return;
            }

            PINFile val;
            try
            {
                val = new PINFile(pinFile);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"读取 PIN 文件失败: {ex.Message}");
                return;
            }

            if (val.AuthMethods != null)
            {
                foreach (PINFile.AuthMethod authMethod in val.AuthMethods)
                {
                    Console.WriteLine("Changing IP Addr to local... {0}", authMethod.Name);
                    authMethod.LoginServers?.Clear();
                    authMethod.LoginServers?.Add(new PINFile.IPEndPoint
                    {
                        IP = ProfileService.SettingConfig.ServerIP,
                        Port = ProfileService.SettingConfig.ServerPort
                    });
                }
            }

            if (!ProfileService.SettingConfig.NgsOn && val.BmlObjects != null)
            {
                foreach (BmlObject bml in val.BmlObjects)
                {
                    if (bml.Name == "extra" && bml.SubObjects != null)
                    {
                        for (int i = bml.SubObjects.Count - 1; i >= 0; i--)
                        {
                            Console.WriteLine("Removing {0}", bml.SubObjects[i].Item1);
                            if (bml.SubObjects[i].Item1 == "NgsOn")
                            {
                                bml.SubObjects.RemoveAt(i);
                                break;
                            }
                        }
                    }
                }
            }

            try
            {
                File.WriteAllBytes(pinFile, val.GetEncryptedData());
            }
            catch (Exception ex)
            {
                Console.WriteLine($"写入 PIN 文件失败: {ex.Message}");
                return;
            }

            var modifier = new MemoryModifier();
            modifier.LaunchAndModifyMemory(kartRiderDirectory);
        }

        private void Setting_Button_Click(object sender, EventArgs e)
        {
            Program.SettingDlg = new Setting();
            Program.SettingDlg.ShowDialog();
        }

        public void Load_Data()
        {
            try
            {
                string ModelMax = Resources.ModelMax;
                if (!File.Exists(FileName.ModelMax_LoadFile))
                {
                    string directory = Path.GetDirectoryName(FileName.ModelMax_LoadFile);
                    if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                    {
                        Directory.CreateDirectory(directory);
                    }

                    File.WriteAllText(FileName.ModelMax_LoadFile, ModelMax);
                }

                var updater = new XmlFileUpdater.XmlUpdater();
                updater.UpdateLocalXmlWithResource(FileName.ModelMax_LoadFile, ModelMax);

                SpecialKartConfig.SaveConfigToFile(FileName.SpecialKartConfig);
                SlotData.kartConfig = SpecialKartConfig.LoadConfigFromFile(FileName.SpecialKartConfig);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"加载数据失败: {ex.Message}");
            }
        }

        private void GitHub_Click(object sender, EventArgs e)
        {
            string url = "https://yanygm.github.io/Launcher_V2/";
            try
            {
                Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"错误: {ex.Message}");
            }
        }

        private void KartInfo_Click(object sender, EventArgs e)
        {
            string url = "https://kartinfo.me/thread-9369-1-1.html";
            try
            {
                Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"错误: {ex.Message}");
            }
        }

        /// <summary>
        /// 标签点击事件处理器（同步包装）
        /// </summary>
        private async void label_Client_Click(object sender, EventArgs e)
        {
            try
            {
                await label_Client_ClickAsync(sender, e);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"检查更新时出错: {ex.Message}");
                MessageBox.Show($"检查更新失败: {ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        /// <summary>
        /// 异步执行检查更新逻辑
        /// </summary>
        private async Task label_Client_ClickAsync(object sender, EventArgs e)
        {
            var data = await global::KartRider.Update.GetUpdateAsync();
            if (data == null)
            {
                MessageBox.Show("获取游戏版本失败！", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            // 弹出“是否”确认框
            DialogResult result = MessageBox.Show(
                $"当前版本为：P{ClientVersion.Text}\n最新版本为：{data.version}\n是否需要更新？",
                "确认操作",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question
            );

            // 根据用户选择执行对应逻辑
            if (result == DialogResult.Yes)
            {
                RestorePinFile();
                LauncherSystem.CheckGame(kartRiderDirectory);
            }
        }

        private void button_ToggleTerminal_Click(object sender, EventArgs e)
        {
            Program.isVisible = !Program.isVisible;
            Program.ShowWindow(Program.consoleHandle, Program.isVisible ? Program.SW_SHOW : Program.SW_HIDE);
            ProfileService.SettingConfig.Console = Program.isVisible;
            ProfileService.SaveSettings();
        }

        private void ConsoleLogger_Click(object sender, EventArgs e)
        {
            CachedConsoleWriter.SaveToFile();
            CachedConsoleWriter.cachedWriter.ClearCache();
        }

        /// <summary>
        /// 检查指定名称的进程是否正在运行
        /// </summary>
        /// <param name="processName">进程名（不含.exe后缀）</param>
        /// <returns>true=运行中，false=未运行</returns>
        static bool IsProcessRunning(string processName)
        {
            try
            {
                // 关键方法：根据进程名获取所有正在运行的进程
                // GetProcessesByName 会忽略大小写，且不需要.exe后缀
                Process[] processes = Process.GetProcessesByName(processName);

                // 如果数组长度大于0，说明进程正在运行
                return processes.Length > 0;
            }
            catch (Exception ex)
            {
                // 捕获可能的异常（比如权限不足）
                Console.WriteLine($"检查进程时出错：{ex.Message}");
                return false;
            }
        }
    }
}