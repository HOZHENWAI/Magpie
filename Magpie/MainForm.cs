﻿using EasyHook;
using Gma.System.MouseKeyHook;
using Magpie.CursorHook;
using Magpie.Properties;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Runtime.Remoting;
using System.Windows.Forms;
using System.Threading;


namespace Magpie {
    partial class MainForm : Form {
        
        private const string AnimeEffectJson = @"[
  {
    ""effect"": ""scale"",
    ""type"": ""Anime4KxDenoise""
  }
]";
        private const string CommonEffectJson = @"[
]";

        IKeyboardMouseEvents keyboardEvents = null;

        Thread thread = null;

        public MainForm() {
            InitializeComponent();

            // 加载设置
            txtHotkey.Text = Settings.Default.Hotkey;
            cbbScaleMode.SelectedIndex = Settings.Default.ScaleMode;
            ckbShowFPS.Checked = Settings.Default.ShowFPS;
            ckbNoVSync.Checked = Settings.Default.NoVSync;
            cbbInjectMode.SelectedIndex = Settings.Default.InjectMode;
            cbbCaptureMode.SelectedIndex = Settings.Default.CaptureMode;
            ckbLowLatencyMode.Checked = Settings.Default.LowLatencyMode;

        }

        protected override void WndProc(ref Message m) {
            if (m.Msg == NativeMethods.MAGPIE_WM_SHOWME) {
                // 收到 WM_SHOWME 激活窗口
                if (WindowState == FormWindowState.Minimized) {
                    Show();
                    WindowState = FormWindowState.Normal;
                }

                _ = NativeMethods.SetForegroundWindow(Handle);
            }
            base.WndProc(ref m);
        }

        private void MainForm_FormClosing(object sender, FormClosingEventArgs e) {
            Settings.Default.Save();
        }

        private void TxtHotkey_TextChanged(object sender, EventArgs e) {
            keyboardEvents?.Dispose();
            keyboardEvents = Hook.GlobalEvents();

            string hotkey = txtHotkey.Text.Trim();

            try {
                keyboardEvents.OnCombination(new Dictionary<Combination, Action> {{
                    Combination.FromString(hotkey), () => {
                        string effectJson = Settings.Default.ScaleMode == 0
                            ? CommonEffectJson : AnimeEffectJson;
                        bool showFPS = Settings.Default.ShowFPS;
                        bool noVSync = Settings.Default.NoVSync;
                        int captureMode = Settings.Default.CaptureMode;
                        bool lowLatencyMode = Settings.Default.LowLatencyMode;

                        if(thread == null) {
                            thread = new Thread(() => {
                                NativeMethods.RunMagWindow(
                                    effectJson,     // 缩放模式
                                    captureMode,    // 抓取模式
                                    showFPS,        // 显示 FPS
                                    lowLatencyMode, // 低延迟模式
                                    noVSync,        // 关闭垂直同步
                                    false           // 用于调试
                                );
                            });
                            thread.SetApartmentState(ApartmentState.MTA);
                            thread.Start();

                            if(cbbInjectMode.SelectedIndex == 1) {
                                HookCursorAtRuntime();
                            }
                        } else {
                            NativeMethods.BroadcastMessage(NativeMethods.MAGPIE_WM_DESTORYMAG);
                            thread.Abort();
                            thread = null;
                        }
                    }
                }});

                txtHotkey.ForeColor = Color.Black;
                Settings.Default.Hotkey = hotkey;

                tsmiHotkey.Text = "热键：" + hotkey;
            } catch (ArgumentException) {
                txtHotkey.ForeColor = Color.Red;
            }
            
        }

        private void HookCursorAtRuntime() {
            IntPtr hwndSrc = NativeMethods.GetForegroundWindow();
            int pid = NativeMethods.GetWindowProcessId(hwndSrc);
            if (pid == 0 || pid == Process.GetCurrentProcess().Id) {
                // 不能 hook 本进程
                return;
            }

#if DEBUG
            string channelName = null;
            // DEBUG 时创建 IPC server
            RemoteHooking.IpcCreateServer<ServerInterface>(ref channelName, WellKnownObjectMode.Singleton);
#endif

            // 获取 CursorHook.dll 的绝对路径
            string injectionLibrary = Path.Combine(
                Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location),
                "CursorHook.dll"
            );

            // 使用 EasyHook 注入
            try {
                EasyHook.RemoteHooking.Inject(
                pid,                // 要注入的进程的 ID
                injectionLibrary,   // 32 位 DLL
                injectionLibrary,   // 64 位 DLL
                // 下面为传递给注入 DLL 的参数
#if DEBUG
                channelName,
#endif
                hwndSrc
                );
            } catch (Exception e) {
                Console.WriteLine("CursorHook 注入失败：" + e.Message);
            }
        }

        private void HookCursorAtStartUp(string exePath) {
#if DEBUG
            string channelName = null;
            // DEBUG 时创建 IPC server
            RemoteHooking.IpcCreateServer<ServerInterface>(ref channelName, WellKnownObjectMode.Singleton);
#endif

            // 获取 CursorHook.dll 的绝对路径
            string injectionLibrary = Path.Combine(
                Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location),
                "CursorHook.dll"
            );

            try {
                EasyHook.RemoteHooking.CreateAndInject(
                    exePath,    // 可执行文件路径
                    "",         // 命令行参数
                    0,          // 传递给 CreateProcess 的标志
                    injectionLibrary,   // 32 位 DLL
                    injectionLibrary,   // 64 位 DLL
                    out int _  // 忽略进程 ID
                               // 下面为传递给注入 DLL 的参数
#if DEBUG
                    , channelName
#endif
                );
            } catch (Exception e) {
                Console.WriteLine("CursorHook 注入失败：" + e.Message);
            }
        }

        private void CbbScaleMode_SelectedIndexChanged(object sender, EventArgs e) {
            Settings.Default.ScaleMode = cbbScaleMode.SelectedIndex;
        }

        private void MainForm_Resize(object sender, EventArgs e) {
            if(WindowState == FormWindowState.Minimized) {
                Hide();
                notifyIcon.Visible = true;
            } else {
                notifyIcon.Visible = false;
            }
        }

        private void TsmiMainForm_Click(object sender, EventArgs e) {
            Show();
            WindowState = FormWindowState.Normal;
        }

        private void TsmiExit_Click(object sender, EventArgs e) {
            Close();
        }

        private void NotifyIcon_MouseClick(object sender, MouseEventArgs e) {
            if (e.Button == MouseButtons.Left) {
                tsmiMainForm.PerformClick();
            }
        }

        private void CkbNoVSync_CheckedChanged(object sender, EventArgs e) {
            Settings.Default.NoVSync = ckbNoVSync.Checked;
        }

        private void CkbShowFPS_CheckedChanged(object sender, EventArgs e) {
            Settings.Default.ShowFPS = ckbShowFPS.Checked;
        }

        private void CbbInjectMode_SelectedIndexChanged(object sender, EventArgs e) {
            if(cbbInjectMode.SelectedIndex == 2) {
                // 启动时注入
                if(openFileDialog.ShowDialog() != DialogResult.OK) {
                    // 未选择文件，恢复原来的选项
                    cbbInjectMode.SelectedIndex = Settings.Default.InjectMode;
                    return;
                }

                HookCursorAtStartUp(openFileDialog.FileName);
            } else {
                // 不保存启动时注入的选项
                Settings.Default.InjectMode = cbbInjectMode.SelectedIndex;
            }
        }

        private void CbbCaptureMode_SelectedIndexChanged(object sender, EventArgs e) {
            Settings.Default.CaptureMode = cbbCaptureMode.SelectedIndex;
        }


        private void CkbLowLatencyMode_CheckedChanged(object sender, EventArgs e) {
            Settings.Default.LowLatencyMode = ckbLowLatencyMode.Checked;
        }
    }
}


