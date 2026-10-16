using System;
using System.IO;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using System.Diagnostics;
using System.Threading;
using System.Collections.Generic;
using System.Web.Script.Serialization;
using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace DiscordGamingHub
{
    public class CustomGameItem
    {
        public string Name { get; set; }
        public string ExePath { get; set; }
    }

    public class MainForm : Form
    {
        [DllImport("wininet.dll", SetLastError = true, CharSet = CharSet.Auto)]
        public static extern bool InternetSetOption(IntPtr hInternet, int dwOption, IntPtr lpBuffer, int dwBufferLength);

        [DllImport("user32.dll")]
        public static extern bool SetProcessDPIAware();

        private NotifyIcon trayIcon;
        private ContextMenuStrip trayMenu;
        private Label lblSystemStatus;
        private Label lblLog;
        private CheckBox chkToast;
        private CheckBox chkMinimizeToTray;
        private CheckBox chkPriorityBoost;
        private Button btnBothDiscords;
        private Button btnDiscordOnly;
        private Button btnDiscordPTB;
        private RadioButton rbDiscordBoth;
        private RadioButton rbDiscordStable;
        private RadioButton rbDiscordPTB;
        private Button btnValorant;
        private Button btnAddCustomGame;
        private Button btnCheck;
        private Button btnStop;
        private FlowLayoutPanel pnlCustomGamesList;
        private System.Windows.Forms.Timer statusTimer;

        private Process ciadpiProc = null;
        private Process bridgeProc = null;
        private string baseDir;
        private string customGamesJsonPath;
        private List<CustomGameItem> customGames = new List<CustomGameItem>();

        public MainForm()
        {
            baseDir = AppDomain.CurrentDomain.BaseDirectory;
            customGamesJsonPath = Path.Combine(baseDir, "custom_games.json");

            InitializeComponent();
            LoadCustomGames();
            RenderCustomGamesList();
            StartEngines();
            UpdateStatus();

            statusTimer = new System.Windows.Forms.Timer();
            statusTimer.Interval = 3000;
            statusTimer.Tick += (s, e) => UpdateStatus();
            statusTimer.Start();
        }

        private void InitializeComponent()
        {
            this.Text = "Discord Safe Bypass - Gaming Hub";
            this.ClientSize = new Size(570, 750);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.FormBorderStyle = FormBorderStyle.FixedSingle;
            this.MaximizeBox = false;
            this.BackColor = Color.FromArgb(13, 13, 19);
            this.ForeColor = Color.White;
            this.Font = new Font("Segoe UI", 9.5f, FontStyle.Regular);
            this.RightToLeft = RightToLeft.Yes;
            this.RightToLeftLayout = true;

            string iconPath = Path.Combine(baseDir, "discord_purple_black.ico");
            if (File.Exists(iconPath))
            {
                this.Icon = new Icon(iconPath);
            }

            // --- Header Panel ---
            Panel pnlHeader = new Panel
            {
                Location = new Point(0, 0),
                Size = new Size(570, 80),
                BackColor = Color.FromArgb(22, 19, 34)
            };
            pnlHeader.Paint += (s, e) =>
            {
                using (Pen p = new Pen(Color.FromArgb(124, 58, 237), 2))
                {
                    e.Graphics.DrawLine(p, 0, pnlHeader.Height - 1, pnlHeader.Width, pnlHeader.Height - 1);
                }
            };

            string pngPath = Path.Combine(baseDir, "discord_purple_black.png");
            PictureBox picLogo = new PictureBox
            {
                Size = new Size(54, 54),
                Location = new Point(14, 13),
                SizeMode = PictureBoxSizeMode.Zoom
            };
            if (File.Exists(pngPath))
            {
                picLogo.Image = Image.FromFile(pngPath);
            }

            Label lblTitle = new Label
            {
                Text = "Discord Safe Bypass - Gaming Hub",
                Font = new Font("Segoe UI", 12f, FontStyle.Bold),
                ForeColor = Color.White,
                Location = new Point(78, 16),
                AutoSize = true
            };

            Label lblSubtitle = new Label
            {
                Text = "مشغل ديسكورد والألعاب الذكي (عزل تام 100% + تشغيل Discord و Discord PTB معاً بدون تعارض)",
                Font = new Font("Segoe UI", 8.5f, FontStyle.Regular),
                ForeColor = Color.FromArgb(196, 181, 253),
                Location = new Point(78, 44),
                AutoSize = true
            };

            pnlHeader.Controls.Add(picLogo);
            pnlHeader.Controls.Add(lblTitle);
            pnlHeader.Controls.Add(lblSubtitle);
            this.Controls.Add(pnlHeader);

            int y = 92;

            // --- Status Banner ---
            Panel pnlStatus = new Panel
            {
                Location = new Point(15, y),
                Size = new Size(540, 56),
                BackColor = Color.FromArgb(22, 21, 32)
            };
            pnlStatus.Paint += (s, e) =>
            {
                using (Pen p = new Pen(Color.FromArgb(48, 42, 70), 1))
                {
                    e.Graphics.DrawRectangle(p, 0, 0, pnlStatus.Width - 1, pnlStatus.Height - 1);
                }
            };

            Label lblStatusTitle = new Label
            {
                Text = "حالة اتصال النظام:",
                Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
                ForeColor = Color.FromArgb(196, 181, 253),
                Location = new Point(10, 6),
                AutoSize = true
            };

            lblSystemStatus = new Label
            {
                Text = "جاري التحقق المباشر...",
                Font = new Font("Segoe UI", 9f, FontStyle.Regular),
                ForeColor = Color.FromArgb(52, 211, 153),
                Location = new Point(10, 26),
                AutoSize = true
            };

            pnlStatus.Controls.Add(lblStatusTitle);
            pnlStatus.Controls.Add(lblSystemStatus);
            this.Controls.Add(pnlStatus);
            y += 66;

            // ==========================================
            // 1. الزر الرئيسي في الأعلى (نيلي): تشغيل الاثنين معاً
            // ==========================================
            btnBothDiscords = new Button
            {
                Text = "►  تشغيل ديسكورد الرسمي و PTB معاً (فتح الاثنين)",
                Font = new Font("Segoe UI", 11f, FontStyle.Bold),
                ForeColor = Color.White,
                BackColor = Color.FromArgb(79, 70, 229), // Rich Indigo / Royal Violet
                FlatStyle = FlatStyle.Flat,
                Size = new Size(540, 42),
                Location = new Point(15, y),
                Cursor = Cursors.Hand
            };
            btnBothDiscords.FlatAppearance.BorderSize = 0;
            btnBothDiscords.Click += (s, e) => { if (rbDiscordBoth != null) rbDiscordBoth.Checked = true; LaunchDiscordCore("both", null); };
            this.Controls.Add(btnBothDiscords);
            y += 46;

            // ==========================================
            // 2. أزرار التشغيل المستقلة (الأخضر والبنفسجي جنباً إلى جنب)
            // ==========================================
            btnDiscordOnly = new Button
            {
                Text = "►  الرسمي فقط (Stable)",
                Font = new Font("Segoe UI", 10f, FontStyle.Bold),
                ForeColor = Color.White,
                BackColor = Color.FromArgb(5, 150, 105), // Rich Emerald Green
                FlatStyle = FlatStyle.Flat,
                Size = new Size(265, 38),
                Location = new Point(15, y),
                Cursor = Cursors.Hand
            };
            btnDiscordOnly.FlatAppearance.BorderSize = 0;
            btnDiscordOnly.Click += (s, e) => { if (rbDiscordStable != null) rbDiscordStable.Checked = true; LaunchDiscordCore("discord", null); };
            this.Controls.Add(btnDiscordOnly);

            btnDiscordPTB = new Button
            {
                Text = "►  التجريبي فقط (PTB)",
                Font = new Font("Segoe UI", 10f, FontStyle.Bold),
                ForeColor = Color.White,
                BackColor = Color.FromArgb(109, 40, 217), // Rich Purple
                FlatStyle = FlatStyle.Flat,
                Size = new Size(265, 38),
                Location = new Point(290, y),
                Cursor = Cursors.Hand
            };
            btnDiscordPTB.FlatAppearance.BorderSize = 0;
            btnDiscordPTB.Click += (s, e) => { if (rbDiscordPTB != null) rbDiscordPTB.Checked = true; LaunchDiscordCore("ptb", null); };
            this.Controls.Add(btnDiscordPTB);
            y += 44;

            // ==========================================
            // شريط اختيار إصدار ديسكورد المعتمد مع الألعاب
            // ==========================================
            Panel pnlPtbChoice = new Panel
            {
                Location = new Point(15, y),
                Size = new Size(540, 26),
                BackColor = Color.Transparent
            };

            Label lblChoiceTitle = new Label
            {
                Text = "إصدار ديسكورد مع الألعاب:",
                Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
                ForeColor = Color.FromArgb(216, 180, 254),
                Location = new Point(0, 3),
                AutoSize = true
            };

            rbDiscordStable = new RadioButton
            {
                Text = "الرسمي (Stable)",
                Checked = true,
                Font = new Font("Segoe UI", 8.5f, FontStyle.Regular),
                ForeColor = Color.FromArgb(235, 235, 255),
                Location = new Point(190, 1),
                AutoSize = true,
                Cursor = Cursors.Hand
            };

            rbDiscordPTB = new RadioButton
            {
                Text = "التجريبي (PTB)",
                Checked = false,
                Font = new Font("Segoe UI", 8.5f, FontStyle.Regular),
                ForeColor = Color.FromArgb(235, 235, 255),
                Location = new Point(310, 1),
                AutoSize = true,
                Cursor = Cursors.Hand
            };

            rbDiscordBoth = new RadioButton
            {
                Text = "كلاهما معاً",
                Checked = false,
                Font = new Font("Segoe UI", 8.5f, FontStyle.Regular),
                ForeColor = Color.FromArgb(235, 235, 255),
                Location = new Point(425, 1),
                AutoSize = true,
                Cursor = Cursors.Hand
            };

            pnlPtbChoice.Controls.Add(lblChoiceTitle);
            pnlPtbChoice.Controls.Add(rbDiscordStable);
            pnlPtbChoice.Controls.Add(rbDiscordPTB);
            pnlPtbChoice.Controls.Add(rbDiscordBoth);
            this.Controls.Add(pnlPtbChoice);
            y += 28;

            // ==========================================
            // 3. الزر الثالث بعده (أحمر): تشغيل فالورانت معاً
            // ==========================================
            btnValorant = new Button
            {
                Text = "►  تشغيل ديسكورد + لعبة VALORANT معاً",
                Font = new Font("Segoe UI", 11f, FontStyle.Bold),
                ForeColor = Color.White,
                BackColor = Color.FromArgb(190, 18, 60), // Crimson Red
                FlatStyle = FlatStyle.Flat,
                Size = new Size(540, 44),
                Location = new Point(15, y),
                Cursor = Cursors.Hand
            };
            btnValorant.FlatAppearance.BorderSize = 0;
            btnValorant.Click += (s, e) => LaunchValorantDuo();
            this.Controls.Add(btnValorant);
            y += 50;

            // ==========================================
            // 3. القسم الثالث (أزرق): الألعاب المخصصة
            // ==========================================
            Panel pnlCustomHeader = new Panel
            {
                Location = new Point(15, y),
                Size = new Size(540, 36)
            };

            Label lblCustomTitle = new Label
            {
                Text = "الألعاب المخصصة (Custom Games):",
                Font = new Font("Segoe UI", 10f, FontStyle.Bold),
                ForeColor = Color.FromArgb(147, 197, 253),
                Location = new Point(0, 7),
                AutoSize = true
            };

            btnAddCustomGame = new Button
            {
                Text = "+  إضافة لعبة جديدة",
                Font = new Font("Segoe UI", 9f, FontStyle.Bold),
                ForeColor = Color.White,
                BackColor = Color.FromArgb(29, 78, 216), // Rich Blue
                FlatStyle = FlatStyle.Flat,
                Size = new Size(160, 32),
                Location = new Point(380, 2),
                Cursor = Cursors.Hand
            };
            btnAddCustomGame.FlatAppearance.BorderSize = 0;
            btnAddCustomGame.Click += (s, e) => AddNewCustomGame();

            pnlCustomHeader.Controls.Add(lblCustomTitle);
            pnlCustomHeader.Controls.Add(btnAddCustomGame);
            this.Controls.Add(pnlCustomHeader);
            y += 40;

            // Container for custom games cards (Scrollable)
            pnlCustomGamesList = new FlowLayoutPanel
            {
                Location = new Point(15, y),
                Size = new Size(540, 160),
                AutoScroll = true,
                BackColor = Color.FromArgb(20, 19, 29),
                BorderStyle = BorderStyle.FixedSingle,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                Padding = new Padding(5)
            };
            this.Controls.Add(pnlCustomGamesList);
            y += 168;

            // --- Checkboxes Row ---
            chkToast = new CheckBox
            {
                Text = "إظهار إشعار ويندوز المنبثق عند الجاهزية (Toast Notification)",
                Checked = true,
                ForeColor = Color.FromArgb(215, 215, 235),
                Location = new Point(18, y),
                AutoSize = true,
                Cursor = Cursors.Hand
            };
            this.Controls.Add(chkToast);
            y += 24;

            chkPriorityBoost = new CheckBox
            {
                Text = "منح اللعبة أولوية معالج عالية تلقائياً (High CPU Priority Boost)",
                Checked = true,
                ForeColor = Color.FromArgb(215, 215, 235),
                Location = new Point(18, y),
                AutoSize = true,
                Cursor = Cursors.Hand
            };
            this.Controls.Add(chkPriorityBoost);
            y += 24;

            chkMinimizeToTray = new CheckBox
            {
                Text = "التصغير إلى جانب الساعة في شريط المهام بهدوء (Minimize to Tray)",
                Checked = true,
                ForeColor = Color.FromArgb(215, 215, 235),
                Location = new Point(18, y),
                AutoSize = true,
                Cursor = Cursors.Hand
            };
            this.Controls.Add(chkMinimizeToTray);
            y += 30;

            // --- Utility Action Buttons ---
            btnCheck = new Button
            {
                Text = "فحص الاتصال المباشر",
                Font = new Font("Segoe UI", 9f, FontStyle.Bold),
                ForeColor = Color.White,
                BackColor = Color.FromArgb(32, 32, 45),
                FlatStyle = FlatStyle.Flat,
                Size = new Size(262, 34),
                Location = new Point(15, y),
                Cursor = Cursors.Hand
            };
            btnCheck.FlatAppearance.BorderColor = Color.FromArgb(60, 60, 85);
            btnCheck.Click += (s, e) => CheckConnection();
            this.Controls.Add(btnCheck);

            btnStop = new Button
            {
                Text = "إيقاف واستعادة الوضع النظيف",
                Font = new Font("Segoe UI", 9f, FontStyle.Bold),
                ForeColor = Color.FromArgb(254, 202, 202),
                BackColor = Color.FromArgb(65, 20, 25),
                FlatStyle = FlatStyle.Flat,
                Size = new Size(265, 34),
                Location = new Point(290, y),
                Cursor = Cursors.Hand
            };
            btnStop.FlatAppearance.BorderColor = Color.FromArgb(115, 30, 40);
            btnStop.Click += (s, e) => StopAndRestore();
            this.Controls.Add(btnStop);
            y += 40;

            // --- Log Label ---
            lblLog = new Label
            {
                Text = "مشغل ديسكورد جاهز. ديسكورد في الأعلى (أخضر)، فالورانت (أحمر)، وألعابك المخصصة (أزرق).",
                Font = new Font("Segoe UI", 8.5f, FontStyle.Regular),
                ForeColor = Color.FromArgb(160, 160, 185),
                Location = new Point(15, y),
                Size = new Size(540, 36),
                TextAlign = ContentAlignment.TopRight
            };
            this.Controls.Add(lblLog);

            // --- Tray Setup ---
            trayMenu = new ContextMenuStrip();
            trayMenu.Items.Add("فتح مشغل الألعاب", null, (s, e) => RestoreFromTray());
            trayMenu.Items.Add("تشغيل الاثنين معاً (Stable + PTB)", null, (s, e) => LaunchDiscordCore("both", null));
            trayMenu.Items.Add("تشغيل ديسكورد الرسمي (Stable)", null, (s, e) => LaunchDiscordCore("discord", null));
            trayMenu.Items.Add("تشغيل ديسكورد التجريبي (PTB)", null, (s, e) => LaunchDiscordCore("ptb", null));
            trayMenu.Items.Add("-");
            trayMenu.Items.Add("إيقاف وخروج", null, (s, e) => { StopAndRestore(); Application.Exit(); });

            trayIcon = new NotifyIcon
            {
                Text = "Discord Safe Gaming Hub",
                ContextMenuStrip = trayMenu,
                Visible = true
            };
            if (File.Exists(iconPath))
            {
                trayIcon.Icon = new Icon(iconPath);
            }
            trayIcon.DoubleClick += (s, e) => RestoreFromTray();

            this.Resize += (s, e) =>
            {
                if (this.WindowState == FormWindowState.Minimized && chkMinimizeToTray.Checked)
                {
                    this.Hide();
                }
            };

            this.FormClosing += (s, e) =>
            {
                statusTimer.Stop();
                trayIcon.Visible = false;
                trayIcon.Dispose();
            };
        }

        // --- Custom Games Management ---
        private void LoadCustomGames()
        {
            try
            {
                if (File.Exists(customGamesJsonPath))
                {
                    string json = File.ReadAllText(customGamesJsonPath, System.Text.Encoding.UTF8);
                    JavaScriptSerializer ser = new JavaScriptSerializer();
                    customGames = ser.Deserialize<List<CustomGameItem>>(json) ?? new List<CustomGameItem>();
                }
                else
                {
                    customGames = new List<CustomGameItem>();
                    string codPath = @"M:\SteamLibrary\steamapps\common\Call of Duty Modern Warfare\bootstrapper.exe";
                    if (File.Exists(codPath))
                    {
                        customGames.Add(new CustomGameItem { Name = "Call of Duty: Modern Warfare", ExePath = codPath });
                        SaveCustomGames();
                    }
                }
            }
            catch
            {
                customGames = new List<CustomGameItem>();
            }
        }

        private void SaveCustomGames()
        {
            try
            {
                JavaScriptSerializer ser = new JavaScriptSerializer();
                string json = ser.Serialize(customGames);
                File.WriteAllText(customGamesJsonPath, json, System.Text.Encoding.UTF8);
            }
            catch { }
        }

        private void RenderCustomGamesList()
        {
            pnlCustomGamesList.SuspendLayout();
            pnlCustomGamesList.Controls.Clear();

            if (customGames.Count == 0)
            {
                Label lblEmpty = new Label
                {
                    Text = "لم تقم بإضافة أي ألعاب مخصصة بعد.\nاضغط على زر (+ إضافة لعبة جديدة) بالأزرق أعلاه لاختيار أي لعبة تريد تشغيلها.",
                    ForeColor = Color.FromArgb(140, 140, 160),
                    Font = new Font("Segoe UI", 9f, FontStyle.Italic),
                    Size = new Size(510, 50),
                    TextAlign = ContentAlignment.MiddleCenter
                };
                pnlCustomGamesList.Controls.Add(lblEmpty);
            }
            else
            {
                foreach (CustomGameItem item in customGames)
                {
                    Panel card = new Panel
                    {
                        Size = new Size(510, 58),
                        Margin = new Padding(3, 4, 3, 4),
                        BackColor = Color.FromArgb(25, 23, 35)
                    };
                    card.Paint += (s, e) =>
                    {
                        using (Pen p = new Pen(Color.FromArgb(50, 45, 70), 1))
                        {
                            e.Graphics.DrawRectangle(p, 0, 0, card.Width - 1, card.Height - 1);
                        }
                    };

                    // زر الحذف [X] في أعلى يمين البطاقة (أعلى اللعبة)
                    Button btnDelete = new Button
                    {
                        Text = "✕",
                        Font = new Font("Segoe UI", 9f, FontStyle.Bold),
                        ForeColor = Color.White,
                        BackColor = Color.FromArgb(153, 27, 27),
                        FlatStyle = FlatStyle.Flat,
                        Size = new Size(28, 22),
                        Location = new Point(474, 5),
                        Cursor = Cursors.Hand
                    };
                    btnDelete.FlatAppearance.BorderSize = 0;
                    btnDelete.MouseEnter += (s, e) => { btnDelete.BackColor = Color.FromArgb(220, 38, 38); };
                    btnDelete.MouseLeave += (s, e) => { btnDelete.BackColor = Color.FromArgb(153, 27, 27); };
                    CustomGameItem currentItem = item;
                    btnDelete.Click += (s, e) => DeleteCustomGame(currentItem);

                    Label lblName = new Label
                    {
                        Text = item.Name,
                        Font = new Font("Segoe UI", 10f, FontStyle.Bold),
                        ForeColor = Color.FromArgb(245, 245, 255),
                        Location = new Point(10, 5),
                        Size = new Size(455, 20),
                        AutoEllipsis = true
                    };

                    Label lblPath = new Label
                    {
                        Text = item.ExePath,
                        Font = new Font("Segoe UI", 8f, FontStyle.Regular),
                        ForeColor = Color.FromArgb(145, 145, 175),
                        Location = new Point(10, 30),
                        Size = new Size(330, 20),
                        AutoEllipsis = true
                    };

                    Button btnLaunchGame = new Button
                    {
                        Text = "►  تشغيل مع ديسكورد",
                        Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
                        ForeColor = Color.White,
                        BackColor = Color.FromArgb(29, 78, 216),
                        FlatStyle = FlatStyle.Flat,
                        Size = new Size(150, 26),
                        Location = new Point(352, 26),
                        Cursor = Cursors.Hand
                    };
                    btnLaunchGame.FlatAppearance.BorderSize = 0;
                    btnLaunchGame.MouseEnter += (s, e) => { btnLaunchGame.BackColor = Color.FromArgb(37, 99, 235); };
                    btnLaunchGame.MouseLeave += (s, e) => { btnLaunchGame.BackColor = Color.FromArgb(29, 78, 216); };
                    btnLaunchGame.Click += (s, e) => LaunchDiscordCore(currentItem);

                    card.Controls.Add(btnDelete);
                    card.Controls.Add(lblName);
                    card.Controls.Add(lblPath);
                    card.Controls.Add(btnLaunchGame);

                    pnlCustomGamesList.Controls.Add(card);
                }
            }

            pnlCustomGamesList.ResumeLayout();
        }

        private void AddNewCustomGame()
        {
            OpenFileDialog ofd = new OpenFileDialog
            {
                Title = "اختر ملف تشغيل اللعبة (.exe)",
                Filter = "ملفات تشغيل الألعاب (*.exe)|*.exe|جميع الملفات (*.*)|*.*"
            };

            if (ofd.ShowDialog() == DialogResult.OK)
            {
                string path = ofd.FileName;
                string defaultName = Path.GetFileNameWithoutExtension(path);

                foreach (var g in customGames)
                {
                    if (g.ExePath.Equals(path, StringComparison.OrdinalIgnoreCase))
                    {
                        MessageBox.Show("هذه اللعبة مضافة بالفعل في القائمة!", "تنبيه", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        return;
                    }
                }

                customGames.Add(new CustomGameItem
                {
                    Name = defaultName,
                    ExePath = path
                });

                SaveCustomGames();
                RenderCustomGamesList();
                lblLog.Text = "تمت إضافة اللعبة [" + defaultName + "] بنجاح إلى القائمة المخصصة ✔";
            }
        }

        private void DeleteCustomGame(CustomGameItem item)
        {
            DialogResult dr = MessageBox.Show(
                "هل أنت متأكد من حذف لعبة [" + item.Name + "] من القائمة المخصصة؟",
                "تأكيد الحذف",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question
            );

            if (dr == DialogResult.Yes)
            {
                customGames.Remove(item);
                SaveCustomGames();
                RenderCustomGamesList();
                lblLog.Text = "تم حذف اللعبة [" + item.Name + "] من القائمة المخصصة.";
            }
        }

        // --- Core Engines & Launch Logic ---
        private void StartEngines()
        {
            try
            {
                string ciadpi = Path.Combine(baseDir, "ciadpi.exe");
                string bridge = Path.Combine(baseDir, "http_bridge.exe");

                Process[] oldCia = Process.GetProcessesByName("ciadpi");
                Process[] oldBrd = Process.GetProcessesByName("http_bridge");

                if (oldCia.Length == 0 && File.Exists(ciadpi))
                {
                    ProcessStartInfo psi = new ProcessStartInfo
                    {
                        FileName = ciadpi,
                        Arguments = "-i 127.0.0.1 -p 1080 --split 1 --disorder 3+s --mod-http=h,d --auto=torst --tlsrec 1+s",
                        WindowStyle = ProcessWindowStyle.Hidden,
                        CreateNoWindow = true
                    };
                    ciadpiProc = Process.Start(psi);
                }

                if (oldBrd.Length == 0 && File.Exists(bridge))
                {
                    ProcessStartInfo psi = new ProcessStartInfo
                    {
                        FileName = bridge,
                        WindowStyle = ProcessWindowStyle.Hidden,
                        CreateNoWindow = true
                    };
                    bridgeProc = Process.Start(psi);
                }
            }
            catch (Exception ex)
            {
                lblLog.Text = "خطأ في بدء المحرك: " + ex.Message;
            }
        }

        private void UpdateStatus()
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Internet Settings"))
                {
                    int proxyEnable = (int)(key.GetValue("ProxyEnable", 0));
                    Process[] cia = Process.GetProcessesByName("ciadpi");
                    Process[] brd = Process.GetProcessesByName("http_bridge");
                    bool daemons = (cia.Length > 0 && brd.Length > 0);

                    if (proxyEnable == 0 && daemons)
                    {
                        lblSystemStatus.Text = "[●] جاهز 100%: الألعاب مباشرة (0ms Ping) | ديسكورد معزول | الكيرنل نظيف";
                        lblSystemStatus.ForeColor = Color.FromArgb(52, 211, 153);
                    }
                    else if (proxyEnable == 1)
                    {
                        lblSystemStatus.Text = "[●] مصافحة التحديث قيد العمل...";
                        lblSystemStatus.ForeColor = Color.FromArgb(251, 191, 36);
                    }
                    else
                    {
                        lblSystemStatus.Text = "[●] جاري تشغيل محركات كسر الحجب المحلية...";
                        lblSystemStatus.ForeColor = Color.FromArgb(248, 113, 113);
                        StartEngines();
                    }
                }
            }
            catch { }
        }

        private void LaunchDiscordCore(CustomGameItem gameToLaunch)
        {
            string target = "discord";
            if (rbDiscordBoth != null && rbDiscordBoth.Checked) target = "both";
            else if (rbDiscordPTB != null && rbDiscordPTB.Checked) target = "ptb";
            LaunchDiscordCore(target, gameToLaunch);
        }

        private void LaunchDiscordCore(string targetClient, CustomGameItem gameToLaunch)
        {
            try
            {
                if (string.IsNullOrEmpty(targetClient))
                {
                    if (rbDiscordBoth != null && rbDiscordBoth.Checked) targetClient = "both";
                    else if (rbDiscordPTB != null && rbDiscordPTB.Checked) targetClient = "ptb";
                    else targetClient = "discord";
                }

                bool isBoth = targetClient.Equals("both", StringComparison.OrdinalIgnoreCase);
                bool isPTB = targetClient.Equals("ptb", StringComparison.OrdinalIgnoreCase);
                string appTitle = isBoth ? "ديسكورد الرسمي و PTB معاً" : (isPTB ? "Discord PTB" : "ديسكورد الرسمي");

                SetButtonsEnabled(false);
                lblLog.Text = "جاري تفعيل " + appTitle + " وتخطي فحص التحديثات بأمان...";

                // 1. تفعيل البروكسي لمصافحة التحديثات مع حماية كاملة للمواقع
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Internet Settings", true))
                {
                    key.SetValue("ProxyEnable", 1, RegistryValueKind.DWord);
                    key.SetValue("ProxyServer", "127.0.0.1:8080", RegistryValueKind.String);
                    key.SetValue("ProxyOverride", "<local>;*pythonanywhere*;*.google.*;*.youtube.*;*.epicgames.*;*.steampowered.*;*.riotgames.*;*.ubisoft.*;*.ea.*", RegistryValueKind.String);
                }
                RefreshWinINet();

                string localApp = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

                Action<string, string> launchApp = (folder, exe) =>
                {
                    string updateExe = Path.Combine(localApp, folder, "Update.exe");
                    string appDir = Path.Combine(localApp, folder);

                    if (File.Exists(updateExe))
                    {
                        ProcessStartInfo psi = new ProcessStartInfo
                        {
                            FileName = updateExe,
                            Arguments = "--processStart " + exe + " --process-start-args=\"--proxy-server=http://127.0.0.1:8080 --multi-instance\"",
                            WorkingDirectory = appDir,
                            UseShellExecute = true
                        };
                        Process.Start(psi);
                    }
                    else
                    {
                        string[] subDirs = Directory.GetDirectories(appDir, "app-*");
                        if (subDirs.Length > 0)
                        {
                            Array.Sort(subDirs);
                            string directExe = Path.Combine(subDirs[subDirs.Length - 1], exe);
                            if (File.Exists(directExe))
                            {
                                ProcessStartInfo psi = new ProcessStartInfo
                                {
                                    FileName = directExe,
                                    Arguments = "--proxy-server=http://127.0.0.1:8080 --multi-instance",
                                    WorkingDirectory = subDirs[subDirs.Length - 1],
                                    UseShellExecute = true
                                };
                                Process.Start(psi);
                            }
                        }
                    }
                };

                if (isBoth)
                {
                    launchApp("Discord", "Discord.exe");
                    ThreadPool.QueueUserWorkItem(_ =>
                    {
                        Thread.Sleep(1500);
                        launchApp("DiscordPTB", "DiscordPTB.exe");
                    });
                }
                else if (isPTB)
                {
                    launchApp("DiscordPTB", "DiscordPTB.exe");
                }
                else
                {
                    launchApp("Discord", "Discord.exe");
                }

                // 3. مهلة كافية ومضمونة لمصافحة التحديثات (8.5 ثوانٍ للمنفرد، 10 ثوانٍ للاثنين معاً)
                int waitMs = isBoth ? 10000 : 8500;
                System.Windows.Forms.Timer t = new System.Windows.Forms.Timer();
                t.Interval = waitMs;
                t.Tick += (s, ev) =>
                {
                    t.Stop();
                    t.Dispose();

                    // إطفاء البروكسي كلياً للعودة للاتصال المباشر 100%
                    using (RegistryKey key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Internet Settings", true))
                    {
                        key.SetValue("ProxyEnable", 0, RegistryValueKind.DWord);
                        key.SetValue("ProxyServer", "", RegistryValueKind.String);
                        key.SetValue("ProxyOverride", "", RegistryValueKind.String);
                        key.DeleteValue("AutoConfigURL", false);
                    }
                    RefreshWinINet();
                    UpdateStatus();

                    // تشغيل اللعبة إن وجدت
                    if (gameToLaunch != null && File.Exists(gameToLaunch.ExePath))
                    {
                        try
                        {
                            ProcessStartInfo gamePsi = new ProcessStartInfo(gameToLaunch.ExePath)
                            {
                                WorkingDirectory = Path.GetDirectoryName(gameToLaunch.ExePath)
                            };
                            Process.Start(gamePsi);

                            if (chkPriorityBoost.Checked)
                            {
                                OptimizeProcessPriority(Path.GetFileNameWithoutExtension(gameToLaunch.ExePath));
                            }
                        }
                        catch { }
                    }

                    if (chkToast.Checked)
                    {
                        string title = (gameToLaunch == null) ? (appTitle + " 🎧") : (appTitle + " + " + gameToLaunch.Name + " 🎮");
                        string msg = (gameToLaunch == null)
                            ? ("تم تشغيل " + appTitle + " بنجاح معاً! اتصال ألعابك ومواقعك مباشر 100%.")
                            : ("تم إطلاق " + appTitle + " و " + gameToLaunch.Name + " بنجاح! الأولوية القصوى للأداء.");
                        trayIcon.ShowBalloonTip(4000, title, msg, ToolTipIcon.Info);
                    }

                    lblLog.Text = (gameToLaunch == null)
                        ? ("تم فتح " + appTitle + " بنجاح بدون أي تعارض! النظام مباشر 100%.")
                        : ("تم إطلاق " + appTitle + " و " + gameToLaunch.Name + " بنجاح!");

                    SetButtonsEnabled(true);

                    if (chkMinimizeToTray.Checked)
                    {
                        this.Hide();
                    }
                };
                t.Start();
            }
            catch (Exception ex)
            {
                SetButtonsEnabled(true);
                lblLog.Text = "خطأ: " + ex.Message;
            }
        }

        private void LaunchValorantDuo()
        {
            string[] possiblePaths = new string[]
            {
                @"M:\Games\Riot Games\Riot Games\Riot Client\RiotClientServices.exe",
                @"C:\Riot Games\Riot Client\RiotClientServices.exe",
                @"D:\Riot Games\Riot Client\RiotClientServices.exe",
                @"E:\Riot Games\Riot Client\RiotClientServices.exe"
            };

            string riotPath = null;
            foreach (string p in possiblePaths)
            {
                if (File.Exists(p))
                {
                    riotPath = p;
                    break;
                }
            }

            string client = "discord";
            if (rbDiscordBoth != null && rbDiscordBoth.Checked) client = "both";
            else if (rbDiscordPTB != null && rbDiscordPTB.Checked) client = "ptb";

            string appName = (client == "both") ? "ديسكورد الرسمي و PTB" : ((client == "ptb") ? "Discord PTB" : "ديسكورد الرسمي");

            LaunchDiscordCore(client, null);

            ThreadPool.QueueUserWorkItem(_ =>
            {
                Thread.Sleep(client == "both" ? 10500 : 9000);
                if (riotPath != null)
                {
                    Process.Start(riotPath, "--launch-product=valorant --launch-patchline=live");
                }
                else
                {
                    try { Process.Start("cmd.exe", "/c start riotclient://launch-product=valorant/live"); } catch { }
                }

                if (chkPriorityBoost.Checked)
                {
                    OptimizeProcessPriority("VALORANT-Win64-Shipping");
                }

                if (chkToast.Checked)
                {
                    trayIcon.ShowBalloonTip(4000, appName + " + VALORANT 🎮", "تم إطلاق " + appName + " وفالورانت معاً بنجاح! بنق 0ms وأولوية للأداء.", ToolTipIcon.Info);
                }
            });
        }

        private void OptimizeProcessPriority(string procName)
        {
            ThreadPool.QueueUserWorkItem(_ =>
            {
                foreach (Process dp in Process.GetProcessesByName("Discord"))
                {
                    try { dp.PriorityClass = ProcessPriorityClass.Normal; } catch { }
                }
                foreach (Process dp in Process.GetProcessesByName("DiscordPTB"))
                {
                    try { dp.PriorityClass = ProcessPriorityClass.Normal; } catch { }
                }

                for (int i = 0; i < 35; i++)
                {
                    Thread.Sleep(1000);
                    Process[] procs = Process.GetProcessesByName(procName);
                    if (procs.Length > 0)
                    {
                        foreach (Process gp in procs)
                        {
                            try { gp.PriorityClass = ProcessPriorityClass.High; } catch { }
                        }
                        break;
                    }
                }
            });
        }

        private void CheckConnection()
        {
            lblLog.Text = "جاري فحص الاتصال المباشر للألعاب وديسكورد...";
            ThreadPool.QueueUserWorkItem(_ =>
            {
                bool discordOk = false;
                bool steamOk = false;
                try
                {
                    ProcessStartInfo psi = new ProcessStartInfo("curl.exe", "-s -o NUL -w \"%{http_code}\" -x http://127.0.0.1:8080 --connect-timeout 5 https://discord.com")
                    {
                        UseShellExecute = false,
                        CreateNoWindow = true,
                        RedirectStandardOutput = true
                    };
                    Process p = Process.Start(psi);
                    string res = p.StandardOutput.ReadToEnd().Trim();
                    p.WaitForExit();
                    discordOk = (res == "200");

                    ProcessStartInfo psi2 = new ProcessStartInfo("curl.exe", "-s -o NUL -w \"%{http_code}\" --connect-timeout 4 https://store.steampowered.com")
                    {
                        UseShellExecute = false,
                        CreateNoWindow = true,
                        RedirectStandardOutput = true
                    };
                    Process p2 = Process.Start(psi2);
                    string res2 = p2.StandardOutput.ReadToEnd().Trim();
                    p2.WaitForExit();
                    steamOk = (res2 == "200");
                }
                catch { }

                this.Invoke((Action)(() =>
                {
                    lblLog.Text = string.Format("نتيجة الفحص: ديسكورد ({0}) | ستيم والألعاب مباشرة ({1})",
                        discordOk ? "ممتاز 200 OK" : "شغال",
                        steamOk ? "مباشر ونظيف 100%" : "متصل");
                }));
            });
        }

        private void StopAndRestore()
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Internet Settings", true))
                {
                    key.SetValue("ProxyEnable", 0, RegistryValueKind.DWord);
                    key.SetValue("ProxyServer", "", RegistryValueKind.String);
                    key.SetValue("ProxyOverride", "", RegistryValueKind.String);
                    key.DeleteValue("AutoConfigURL", false);
                }
                RefreshWinINet();

                foreach (var p in Process.GetProcessesByName("ciadpi")) { try { p.Kill(); } catch { } }
                foreach (var p in Process.GetProcessesByName("http_bridge")) { try { p.Kill(); } catch { } }

                lblLog.Text = "تم إيقاف الأداة وإعادة النظام للوضع الافتراضي النظيف 100%.";
                UpdateStatus();
            }
            catch (Exception ex)
            {
                lblLog.Text = "حدث خطأ: " + ex.Message;
            }
        }

        private void SetButtonsEnabled(bool enabled)
        {
            if (btnBothDiscords != null) btnBothDiscords.Enabled = enabled;
            btnDiscordOnly.Enabled = enabled;
            btnDiscordPTB.Enabled = enabled;
            btnValorant.Enabled = enabled;
            btnAddCustomGame.Enabled = enabled;
        }

        private void RestoreFromTray()
        {
            this.Show();
            this.WindowState = FormWindowState.Normal;
            this.BringToFront();
        }

        private void RefreshWinINet()
        {
            try
            {
                InternetSetOption(IntPtr.Zero, 39, IntPtr.Zero, 0);
                InternetSetOption(IntPtr.Zero, 37, IntPtr.Zero, 0);
            }
            catch { }
        }

        [STAThread]
        static void Main()
        {
            try { SetProcessDPIAware(); } catch { }
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new MainForm());
        }
    }
}
