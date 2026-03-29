using ExcData;
using KartRider.Common.Data;
using KartRider.Common.Utilities;
using KartRider.IO.Packet;
using Launcher.Properties;
using LoggerLibrary;
using Profile;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
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
            if (File.Exists(pinFileBak))
            {
                File.Delete(pinFile);
                File.Move(pinFileBak, pinFile);
            }
            this.InitializeComponent();
        }

        private void OnFormClosing(object sender, FormClosingEventArgs e)
        {
            if (File.Exists(pinFileBak))
            {
                File.Delete(pinFile);
                File.Move(pinFileBak, pinFile);
            }
        }

        private void OnLoad(object sender, EventArgs e)
        {
            PINFile val = new PINFile(pinFile);
            ProfileService.SettingConfig.ClientVersion = val.Header.MinorVersion;
            ProfileService.SettingConfig.LocaleID = val.Header.LocaleID;
            ProfileService.SettingConfig.nClientLoc = val.Header.Unk2;
            ProfileService.SettingConfig.ServerList = val.AuthMethods[0].LoginServers;
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
            catch (Exception ex)
            {
                if (ex is System.Net.Sockets.SocketException)
                {
                    LauncherSystem.MessageBoxType2();
                }
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
                (new Thread(() =>
                {
                    if (File.Exists(pinFileBak))
                    {
                        File.Delete(pinFile);
                        File.Move(pinFileBak, pinFile);
                    }
                    Console.WriteLine("Backing up old PinFile...");
                    Console.WriteLine(pinFile);
                    File.Copy(pinFile, pinFileBak);
                    PINFile val = new PINFile(pinFile);
                    foreach (PINFile.AuthMethod authMethod in val.AuthMethods)
                    {
                        Console.WriteLine("Changing IP Addr to local... {0}", authMethod.Name);
                        authMethod.LoginServers.Clear();
                        authMethod.LoginServers.Add(new PINFile.IPEndPoint
                        {
                            IP = ProfileService.SettingConfig.ServerIP,
                            Port = ProfileService.SettingConfig.ServerPort
                        });
                    }
                    if (!ProfileService.SettingConfig.NgsOn)
                    {
                        foreach (BmlObject bml in val.BmlObjects)
                        {
                            if (bml.Name == "extra")
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
                    File.WriteAllBytes(pinFile, val.GetEncryptedData());
                    var modifier = new MemoryModifier();
                    modifier.LaunchAndModifyMemory(kartRiderDirectory);
                })).Start();
            }
        }

        private void Setting_Button_Click(object sender, EventArgs e)
        {
            Program.SettingDlg = new Setting();
            Program.SettingDlg.ShowDialog();
        }

        public void Load_Data()
        {
            string ModelMax = Resources.ModelMax;
            if (!File.Exists(FileName.ModelMax_LoadFile))
            {
                using (StreamWriter streamWriter = new StreamWriter(FileName.ModelMax_LoadFile, false))
                {
                    streamWriter.Write(ModelMax);
                }
            }
            XmlFileUpdater.XmlUpdater updater = new XmlFileUpdater.XmlUpdater();
            updater.UpdateLocalXmlWithResource(FileName.ModelMax_LoadFile, ModelMax);

            SpecialKartConfig.SaveConfigToFile(FileName.SpecialKartConfig);
            SlotData.kartConfig = SpecialKartConfig.LoadConfigFromFile(FileName.SpecialKartConfig);
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

        private void label_Client_Click(object sender, EventArgs e)
        {
            GameVersion version = LauncherSystem.GetGameVersion();
            if (version == null)
            {
                MessageBox.Show("获取游戏版本失败！", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            // 弹出“是否”确认框
            DialogResult result = MessageBox.Show(
                $"当前版本为：P{ClientVersion.Text}\n最新版本为：P{version.Version}\n是否需要更新？",
                "确认操作",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question
            );

            // 根据用户选择执行对应逻辑
            if (result == DialogResult.Yes)
            {
                if (File.Exists(pinFileBak))
                {
                    File.Delete(pinFile);
                    File.Move(pinFileBak, pinFile);
                }

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