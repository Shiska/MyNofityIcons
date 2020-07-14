﻿using System;
using System.IO;
using System.Linq;
using System.Drawing;
using System.Threading;
using System.Diagnostics;
using System.Windows.Forms;
using System.Collections.Generic;
using System.Runtime.InteropServices;

using Microsoft.Win32;

namespace MyNofityIcons
{
    public class MyNofityIcon
    {
        [STAThread]
        static void Main(string[] args)
        {
            if (args.Length != 0)
            {
                switch (args[0])
                {
                    case "--autostart":
                        foreach (var (executable, _) in MyNofityIconSetting.executable)
                        {
                            Start(executable, true);
                        }
                        return;
                    case "--start":
                        Run(args[1], hidden: Boolean.Parse(args[2]));

                        return;
                }
            }
            Application.Run(new MyNofityIconForm());
        }

        private const int SW_HIDE = 0;
        private const int SW_SHOW = 5;
        private const int SW_RESTORE = 9;

        [DllImport("User32")]
        private static extern int ShowWindow(int hwnd, int nCmdShow);
        [DllImport("User32")]
        private static extern int SetForegroundWindow(int hwnd);
        [DllImport("User32")]
        private static extern bool IsIconic(int hwnd);
        [DllImport("User32")]
        private static extern bool EnumWindows(EnumWindowsProc enumProc, IntPtr lParam);
        public delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);
        [DllImport("User32")]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out int ProcessId);

        public static void Run(string executable, string arguments = "", int timeout = 5, bool hidden = false)
        {
            NotifyIcon icon = new NotifyIcon();

            icon.Text = Path.ChangeExtension(executable, null);
            icon.Visible = true;

            if (File.Exists(executable))
            {
                icon.Icon = Icon.ExtractAssociatedIcon(executable);

                ProcessStartInfo startInfo = new ProcessStartInfo(executable, arguments);

                if (hidden)
                {
                    startInfo.WindowStyle = ProcessWindowStyle.Hidden;
                }
                Process process = Process.Start(startInfo);

                if (process != null)
                {
                    int hWnd = 0;

                    icon.MouseDoubleClick += new MouseEventHandler((sender, e) => {
                        if ((hidden = !hidden))
                        {
                            ShowWindow(hWnd, SW_HIDE);
                        }
                        else
                        {
                            ShowWindow(hWnd, IsIconic(hWnd) ? SW_RESTORE : SW_SHOW);
                            SetForegroundWindow(hWnd);
                        }
                    });
                    process.EnableRaisingEvents = true;
                    process.Exited += new EventHandler((sender, e) => { Application.Exit(); });

                    if (hidden)
                    {
                        while ((hWnd = (int)GetHwnd(process.Id)) == 0)
                        {
                            Thread.Yield();
                        }
                    }
                    else
                    {
                        while ((hWnd = (int)process.MainWindowHandle) == 0)
                        {
                            Thread.Yield();
                        }
                    }
                    Application.Run();
                }
                else
                {
                    icon.ShowBalloonTip(timeout, icon.Text, "Failed to start process!", ToolTipIcon.Error);

                    Thread.Sleep(timeout * 1000);
                }
            }
            else
            {
                icon.ShowBalloonTip(timeout, icon.Text, executable + " not found!", ToolTipIcon.Error);

                Thread.Sleep(timeout * 1000);
            }
        }

        public static IntPtr GetHwnd(int processid)
        {
            IntPtr hwnd = IntPtr.Zero;

            EnumWindows(delegate (IntPtr wnd, IntPtr param)
            {
                int lpdwProcessId;

                GetWindowThreadProcessId(wnd, out lpdwProcessId);

                if (lpdwProcessId == processid)
                {
                    hwnd = wnd;
                    return false;
                }
                return true;
            }, IntPtr.Zero);

            return hwnd;
        }

        public static Process Start(string executable, bool hidden)
        {
            return Process.Start(Application.ExecutablePath, "--start \"" + executable + "\" " + hidden.ToString());
        }
    }

    public class MyNofityIconForm : Form
    {
        private TableLayoutPanel layout;
        private Button addButton;

        public MyNofityIconForm()
        {
            this.Text = "MyNofityIcons";
            this.Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
            this.AutoSize = true;
            this.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            this.MaximizeBox = false;

            this.layout = new TableLayoutPanel();
            this.layout.AutoSize = true;
            this.layout.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            this.layout.ColumnCount = 4;
            this.layout.GrowStyle = TableLayoutPanelGrowStyle.AddRows;

            Label pathLabel = new Label();

            pathLabel.Text = "Executable";
            pathLabel.AutoSize = true;
            pathLabel.Anchor = AnchorStyles.Right;

            CheckBox autostartCB = new CheckBox();

            autostartCB.AutoSize = true;
            autostartCB.Text = "Autostart with Microsoft Windows";
            autostartCB.Checked = MyNofityIconSetting.autostart;
            autostartCB.Anchor = AnchorStyles.Left;
            autostartCB.CheckedChanged += new EventHandler((sender, e) => {
                MyNofityIconSetting.autostart = autostartCB.Checked;
            });

            this.addButton = new Button();

            this.addButton.Text = "Add";
            this.addButton.Anchor = AnchorStyles.Left | AnchorStyles.Right;
            this.addButton.Click += new EventHandler(addButton_Click);

            this.layout.SetColumnSpan(autostartCB, 3);
            this.layout.SetColumnSpan(this.addButton, 4);

            this.layout.Controls.Add(pathLabel);
            this.layout.Controls.Add(autostartCB);
            this.layout.Controls.Add(this.addButton);

            this.Controls.Add(this.layout);

            foreach (var (executale, autostart) in MyNofityIconSetting.executable)
            {
                AddRow(executale, autostart);
            }
        }

        void addButton_Click(Object sender, EventArgs e)
        {
            using (OpenFileDialog openFileDialog = new OpenFileDialog())
            {
                openFileDialog.RestoreDirectory = true;

                if (openFileDialog.ShowDialog() == DialogResult.OK)
                {
                    if (MyNofityIconSetting.isInExecutable(openFileDialog.FileName) == false)
                    {
                        AddRow(openFileDialog.FileName, false);

                        storeData();
                    }
                }
            }
        }

        void AddRow(string path, bool autostart)
        {
            TableLayoutControlCollection controls = this.layout.Controls;

            Label label = new Label();

            label.Text = path;
            label.AutoSize = true;
            label.Anchor = AnchorStyles.Right;

            CheckBox cb = new CheckBox();

            cb.AutoSize = true;
            cb.Text = "Autostart";
            cb.Checked = autostart;
            cb.Anchor = AnchorStyles.None;
            cb.CheckedChanged += new EventHandler(storeData);

            Button runButton = new Button();

            runButton.AutoSize = true;
            runButton.Text = "Run";
            runButton.Anchor = AnchorStyles.Left;
            runButton.Click += new EventHandler((sender, e) => {
                MyNofityIcon.Start(path, false);
            });

            Button remButton = new Button();

            remButton.AutoSize = true;
            remButton.Text = "Remove";
            remButton.Anchor = AnchorStyles.Left;
            remButton.Click += new EventHandler((sender, e) => {
                this.SuspendLayout();

                controls.Remove(label);
                controls.Remove(cb);
                controls.Remove(runButton);
                controls.Remove(remButton);

                this.ResumeLayout();
            });
            remButton.Click += new EventHandler(storeData);

            this.SuspendLayout();

            controls.Add(label);
            controls.Add(cb);
            controls.Add(runButton);
            controls.Add(remButton);
            controls.Add(this.addButton);

            this.ResumeLayout();
        }

        void storeData(object sender = null, EventArgs e = null)
        {
            TableLayoutControlCollection controls = this.layout.Controls;

            var executable = new List<(string, bool)>();

            for (int i = controls.Count - 1; (i -= 4) >= 2;)
            {
                executable.Add((
                    ((Label)controls[i]).Text,
                    ((CheckBox)controls[i + 1]).Checked
                ));
            }
            executable.Reverse();

            MyNofityIconSetting.executable = executable;
        }
    }

    public class MyNofityIconSetting
    {
        const string key = "MyNofityIconAutostart";

        public static bool autostart
        {
            get { return Properties.Settings.Default.Autostart; }
            set
            {
                RegistryKey rk = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", true);

                if ((Properties.Settings.Default.Autostart = value))
                {
                    rk.SetValue(key, "\"" + Path.GetFullPath(Application.ExecutablePath) + "\" --autostart");
                }
                else
                {
                    rk.DeleteValue(key, false);
                }
                Properties.Settings.Default.Save();
            }
        }

        public static bool isInExecutable(string executable)
        {
            return Properties.Settings.Default.Executable.Contains(executable);
        }

        public static IEnumerable<(string, bool)> executable
        {
            get
            {
                char[] rowSeparators = { '|' };
                char[] colSeparators = { '(', ',', ')' };

                string[] executable = Properties.Settings.Default.Executable.Split(rowSeparators, StringSplitOptions.RemoveEmptyEntries);

                foreach (string row in executable)
                {
                    string[] column = row.Split(colSeparators, StringSplitOptions.RemoveEmptyEntries);

                    yield return (column[0], Boolean.Parse(column[1]));
                }
            }
            set
            {
                Properties.Settings.Default.Executable = String.Join("|", value.ToArray());
                Properties.Settings.Default.Save();
            }
        }
    }
}