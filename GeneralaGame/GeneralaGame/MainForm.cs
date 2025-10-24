using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace GeneralaGame
{
    internal static class Theme
    {
        // Фетр (фон сцени)
        public static readonly Color BackRed = Color.FromArgb(16, 56, 44);   // темний зелений фетр
        public static readonly Color BackRedDark = Color.FromArgb(12, 42, 33);   // ще темніше

        // «Золото» (рамки)
        public static readonly Color Gold = Color.FromArgb(240, 196, 80);
        public static readonly Color GoldDark = Color.FromArgb(180, 140, 60);

        // Заповнення всередині рамок (трохи світліший фетр)
        public static readonly Color BoxFill = Color.FromArgb(22, 74, 58);

        // Права панель — смарагдовий градієнт
        public static readonly Color SideFrom = Color.FromArgb(26, 90, 70);
        public static readonly Color SideTo = Color.FromArgb(16, 56, 44);

        // Верхній банер — золото → бурштин
        public static readonly Color BannerFrom = Color.FromArgb(252, 210, 108);
        public static readonly Color BannerTo = Color.FromArgb(210, 150, 70);

        // «Папір» для рахунків
        public static readonly Color Paper = Color.FromArgb(252, 243, 224); // ivory
        public static readonly Color PaperEdge = Color.FromArgb(150, 110, 70);

        // Текст
        public static readonly Color TextOnDark = Color.FromArgb(255, 250, 240);
        public static readonly Color TextMuted = Color.FromArgb(235, 220, 210);
        public static readonly Color ScoreText = Color.FromArgb(60, 40, 25);    // темно-коричневий

        // Кнопки
        public static readonly Color ButtonPrimary = Color.FromArgb(245, 193, 74); // золотий
        public static readonly Color ButtonPrimaryText = Color.FromArgb(50, 30, 10);

        // Підсвіт «гарних» варіантів
        public static readonly Color HighlightGood = Color.FromArgb(120, 200, 120);

        // Стан кнопок у таблиці
        public static readonly Color BtnPlayerBase = Color.FromArgb(255, 255, 255);
        public static readonly Color BtnPlayerUsed = Color.FromArgb(230, 230, 230);
        public static readonly Color BtnAiBase = Color.FromArgb(245, 245, 245);
        public static readonly Color BtnAiUsed = Color.FromArgb(220, 220, 220);
    }

    internal static class Gfx
    {
        public static GraphicsPath RoundedRect(Rectangle rect, int radius)
        {
            int d = radius * 2;
            var path = new GraphicsPath();
            if (radius <= 0) { path.AddRectangle(rect); path.CloseFigure(); return path; }

            path.AddArc(rect.X, rect.Y, d, d, 180, 90);
            path.AddArc(rect.Right - d, rect.Y, d, d, 270, 90);
            path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90);
            path.AddArc(rect.X, rect.Bottom - d, d, d, 90, 90);
            path.CloseFigure(); return path;
        }
    }

    internal class FramedPanel : Panel
    {
        public Color BorderColor = Theme.Gold;
        public Color BorderShadow = Theme.GoldDark;
        public float BorderWidth = 3f;
        public int Radius = 18;
        public Color FillColor = Theme.BoxFill;

        public FramedPanel()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer, true);
            Padding = new Padding(12);
            BackColor = Color.Transparent;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            var r = ClientRectangle;
            r.Inflate(-2, -2);

            using (var path = Gfx.RoundedRect(r, Radius))
            using (var fill = new SolidBrush(FillColor))
            using (var pen1 = new Pen(BorderColor, BorderWidth))
            using (var pen2 = new Pen(BorderShadow, BorderWidth))
            {
                g.DrawPath(pen2, OffsetPath(path, 1, 1));
                g.FillPath(fill, path);
                g.DrawPath(pen1, path);
            }
        }
        private GraphicsPath OffsetPath(GraphicsPath src, int dx, int dy)
        {
            var m = new Matrix(); m.Translate(dx, dy);
            var dst = (GraphicsPath)src.Clone(); dst.Transform(m); return dst;
        }
    }

    internal class GradientPanel : Panel
    {
        public Color Color1 { get; set; }
        public Color Color2 { get; set; }
        public float Angle { get; set; } = 90f;

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            using (var br = new LinearGradientBrush(ClientRectangle, Color1, Color2, Angle))
                e.Graphics.FillRectangle(br, ClientRectangle);
        }
    }

    public partial class MainForm : Form
    {
        private Label lblRound, lblStatus, lblTotals, lblAiTotals, lblRolls;
        private ComboBox cmbDifficulty;
        private Button btnRoll, btnNew;
        private GradientPanel rightPanel, banner;
        private Panel diceStage;
        private FramedPanel upperFrame, lowerFrame;
        private FlowLayoutPanel dicePanel, holdPanel;
        private TableLayoutPanel sheetHost, scoreGridPlayer, scoreGridAI;

        private readonly Dictionary<ScoreCategory, Button> playerBtns =
            new Dictionary<ScoreCategory, Button>();
        private readonly Dictionary<ScoreCategory, Button> aiBtns =
            new Dictionary<ScoreCategory, Button>();

        private readonly DiceControl[] diceCtrls = new DiceControl[5];
        private int[] dice = new int[5];
        private int rollsLeft = 3;
        private bool playerTurn = true;
        private ScoreCard playerCard = new ScoreCard();
        private ScoreCard aiCard = new ScoreCard();
        private readonly AIPlayer ai = new AIPlayer(AIDifficulty.Medium);

        private const int PLAYER_ROLL_MS = 900;
        private const int AI_ROLL_MS = 5000;
        private const int AI_SHOW_RESULT_MS = 3000;

        private readonly Color highlightColor = Theme.HighlightGood;
        private readonly Color basePlayerBtnColor = Theme.BtnPlayerBase;
        private readonly Color usedPlayerBtnColor = Theme.BtnPlayerUsed;
        private readonly Color aiBaseBtnColor = Theme.BtnAiBase;
        private readonly Color aiUsedBtnColor = Theme.BtnAiUsed;

        private const int DICE_MARGIN = 4;

        public MainForm()
        {
            Text = "Generala — Player vs Computer";
            StartPosition = FormStartPosition.CenterScreen;

            ClientSize = new Size(1360, 840);     // було 1120×720
            FormBorderStyle = FormBorderStyle.Sizable; // можна тягнути
            MaximizeBox = true;
            MinimumSize = new Size(1100, 720);

            BuildUi();
            NewGame();

            this.Resize += (s, e) => UpdateDiceSizes();
        }


        private void BuildUi()
        {
            BackColor = Theme.BackRed;

            var root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 3,
                BackColor = Color.Transparent
            };
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 64));
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 36));
            root.RowStyles.Clear();
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 90)); // банер
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 56));  // зона кидків
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 44));  // таблиці
            Controls.Add(root);

            banner = new GradientPanel { Dock = DockStyle.Fill, Color1 = Theme.BannerFrom, Color2 = Theme.BannerTo, Angle = 0f };
            root.Controls.Add(banner, 0, 0);
            root.SetColumnSpan(banner, 2);

            lblRound = new Label
            {
                Text = "ROUND 1",
                AutoSize = false,
                TextAlign = ContentAlignment.MiddleCenter,
                Dock = DockStyle.Fill,
                Font = new Font("Segoe UI Semibold", 26, FontStyle.Bold),
                ForeColor = Theme.TextOnDark
            };
            banner.Controls.Add(lblRound);

            diceStage = new Panel { Dock = DockStyle.Fill, BackColor = Theme.BackRedDark, Padding = new Padding(18) };
            root.Controls.Add(diceStage, 0, 1);

            var stageLayout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2 };
            stageLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 66));
            stageLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 34));
            diceStage.Controls.Add(stageLayout);

            upperFrame = new FramedPanel { Dock = DockStyle.Fill, Radius = 18, BorderColor = Theme.Gold, BorderShadow = Theme.GoldDark, FillColor = Theme.BoxFill };
            lowerFrame = new FramedPanel { Dock = DockStyle.Fill, Radius = 18, BorderColor = Theme.Gold, BorderShadow = Theme.GoldDark, FillColor = Theme.BoxFill };
            stageLayout.Controls.Add(upperFrame, 0, 0);
            stageLayout.Controls.Add(lowerFrame, 0, 1);

            dicePanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                Padding = new Padding(4),
                BackColor = Color.Transparent,
                Margin = new Padding(0)
            };
            upperFrame.Controls.Add(dicePanel);

            holdPanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                Padding = new Padding(4),
                BackColor = Color.Transparent,
                Margin = new Padding(0)
            };
            lowerFrame.Controls.Add(holdPanel);

            for (int i = 0; i < 5; i++)
            {
                var dc = new DiceControl();
                diceCtrls[i] = dc;
                dc.Click += Dice_Click;
                dicePanel.Controls.Add(dc);
            }

            dicePanel.SizeChanged += delegate { UpdateDiceSizes(); };
            holdPanel.SizeChanged += delegate { UpdateDiceSizes(); };
            upperFrame.SizeChanged += delegate { UpdateDiceSizes(); };
            lowerFrame.SizeChanged += delegate { UpdateDiceSizes(); };
            UpdateDiceSizes();

            rightPanel = new GradientPanel { Dock = DockStyle.Fill, Color1 = Theme.SideFrom, Color2 = Theme.SideTo, Angle = 90f };
            root.Controls.Add(rightPanel, 1, 1);

            var rightFlow = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.TopDown,
                Padding = new Padding(16),
                BackColor = Color.Transparent
            };
            rightPanel.Controls.Add(rightFlow);

            var lblDiffCap = new Label { Text = "Складність", AutoSize = true, ForeColor = Theme.TextOnDark, Font = new Font("Segoe UI", 10, FontStyle.Bold) };
            rightFlow.Controls.Add(lblDiffCap);

            cmbDifficulty = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 200, Font = new Font("Segoe UI", 10) };
            cmbDifficulty.Items.AddRange(new object[] { "Легкий", "Середній", "Складний" });
            cmbDifficulty.SelectedIndex = 1;
            cmbDifficulty.SelectedIndexChanged += delegate
            {
                ai.SetDifficulty(cmbDifficulty.SelectedIndex == 0 ? AIDifficulty.Easy :
                                 cmbDifficulty.SelectedIndex == 1 ? AIDifficulty.Medium : AIDifficulty.Hard);
                lblStatus.Text = "Складність: " + cmbDifficulty.SelectedItem.ToString();
            };
            rightFlow.Controls.Add(cmbDifficulty);

            lblRolls = new Label { AutoSize = true, Text = "ROLLS: 3", ForeColor = Theme.TextOnDark, Font = new Font("Segoe UI Semibold", 11, FontStyle.Bold), Margin = new Padding(0, 12, 0, 0) };
            rightFlow.Controls.Add(lblRolls);

            btnRoll = new Button { Text = "ROLL (3)", Width = 200, Height = 44 };
            StyleButton(btnRoll, true);
            btnRoll.Click += async (s, e) => await BtnRoll_Click();
            rightFlow.Controls.Add(btnRoll);

            btnNew = new Button { Text = "Нова гра", Width = 200, Height = 40 };
            StyleButton(btnNew, false);
            btnNew.Click += (s, e) => NewGame();
            rightFlow.Controls.Add(btnNew);

            lblStatus = new Label { AutoSize = true, Text = "Статус", ForeColor = Theme.TextMuted, Padding = new Padding(0, 10, 0, 0), Font = new Font("Segoe UI", 9.5f) };
            rightFlow.Controls.Add(lblStatus);

            lblTotals = new Label { AutoSize = true, Text = "Гравець: 0", ForeColor = Theme.TextOnDark, Font = new Font("Segoe UI", 10, FontStyle.Bold) };
            lblAiTotals = new Label { AutoSize = true, Text = "Комп'ютер: 0", ForeColor = Theme.TextOnDark, Font = new Font("Segoe UI", 10, FontStyle.Bold) };
            rightFlow.Controls.Add(lblTotals);
            rightFlow.Controls.Add(lblAiTotals);

            sheetHost = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 1,
                BackColor = Color.Transparent,
                Padding = new Padding(18, 12, 18, 18),
                Margin = new Padding(0)
            };
            sheetHost.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            sheetHost.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            root.Controls.Add(sheetHost, 0, 2);
            root.SetColumnSpan(sheetHost, 2);

            var bookPlayer = CreateScoreBook("Player", out scoreGridPlayer);
            var bookAI = CreateScoreBook("Computer", out scoreGridAI);
            sheetHost.Controls.Add(bookPlayer, 0, 0);
            sheetHost.Controls.Add(bookAI, 1, 0);

            BuildScoreButtons();
            dicePanel.SizeChanged += (s, e) => UpdateDiceSizes();
            holdPanel.SizeChanged += (s, e) => UpdateDiceSizes();
            upperFrame.SizeChanged += (s, e) => UpdateDiceSizes();
            lowerFrame.SizeChanged += (s, e) => UpdateDiceSizes();
        }

        private Panel CreateScoreBook(string title, out TableLayoutPanel grid)
        {
            var host = new FramedPanel
            {
                Dock = DockStyle.Fill,
                Radius = 18,
                BorderColor = Theme.PaperEdge,
                BorderShadow = Color.FromArgb(80, Theme.PaperEdge),
                FillColor = Theme.Paper,
                Padding = new Padding(14)
            };

            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 2,
                BackColor = Color.Transparent
            };
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            host.Controls.Add(layout);

            var lbl = new Label
            {
                Text = title,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                Font = new Font("Segoe UI Semibold", 12, FontStyle.Bold),
                ForeColor = Theme.ScoreText,
                BackColor = Color.Transparent
            };
            layout.Controls.Add(lbl, 0, 0);

            grid = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 5,
                BackColor = Color.Transparent,
                Margin = new Padding(0)
            };
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            for (int r = 0; r < 5; r++) grid.RowStyles.Add(new RowStyle(SizeType.Percent, 20));
            layout.Controls.Add(grid, 0, 1);

            return host;
        }

        private void StyleButton(Button b, bool primary)
        {
            b.FlatStyle = FlatStyle.Flat;
            b.FlatAppearance.BorderSize = 0;
            b.Font = new Font("Segoe UI Semibold", primary ? 12f : 10.5f, FontStyle.Bold);
            b.BackColor = primary ? Theme.ButtonPrimary : Color.FromArgb(255, 230, 180);
            b.ForeColor = Theme.ButtonPrimaryText;
            b.FlatAppearance.MouseOverBackColor = primary ? Color.FromArgb(255, 210, 110) : Color.FromArgb(255, 240, 200);
            b.FlatAppearance.MouseDownBackColor = primary ? Color.FromArgb(220, 165, 60) : Color.FromArgb(245, 225, 170);
            b.Cursor = Cursors.Hand;
            b.Margin = new Padding(0, 8, 0, 0);
        }


        // ---------- адаптивний розмір кісток ----------
        private int ComputeDiceSizeForPanel(FlowLayoutPanel p)
        {
            int availW = Math.Max(0, p.ClientSize.Width - p.Padding.Horizontal - DICE_MARGIN * 2 * 5);
            int perW = availW / 5;
            int availH = Math.Max(0, p.ClientSize.Height - p.Padding.Vertical - DICE_MARGIN * 2);
            int size = Math.Min(perW, availH);
            return Math.Max(48, size);
        }
        private void UpdateDiceSizes()
        {
            for (int i = 0; i < diceCtrls.Length; i++)
                diceCtrls[i].Margin = new Padding(DICE_MARGIN);

            int topSize = ComputeDiceSizeForPanel(dicePanel);
            int botSize = ComputeDiceSizeForPanel(holdPanel);
            int target = Math.Min(topSize, botSize);
            for (int i = 0; i < diceCtrls.Length; i++)
            { diceCtrls[i].Width = target; diceCtrls[i].Height = target; }
        }

        // ---------- взаємодія з кістками ----------
        private void Dice_Click(object sender, EventArgs e)
        {
            if (!playerTurn) return;

            if (rollsLeft == 3)
            {
                lblStatus.Text = "Спершу киньте кістки (ROLL), потім можна утримувати.";
                return;
            }
            var dc = (DiceControl)sender;
            if (dc.Held) MoveToUpper(dc);
            else MoveToTray(dc);
            RefreshScoreButtons();
        }
        private void MoveToTray(DiceControl dc)
        {
            if (dc.Parent != null) dc.Parent.Controls.Remove(dc);
            holdPanel.Controls.Add(dc); dc.SetHeld(true); UpdateDiceSizes();
        }
        private void MoveToUpper(DiceControl dc)
        {
            if (dc.Parent != null) dc.Parent.Controls.Remove(dc);
            dicePanel.Controls.Add(dc); dc.SetHeld(false); UpdateDiceSizes();
        }

        private void BuildScoreButtons()
        {
            foreach (var kv in playerBtns) if (kv.Value.Parent != null) kv.Value.Parent.Controls.Remove(kv.Value);
            foreach (var kv in aiBtns) if (kv.Value.Parent != null) kv.Value.Parent.Controls.Remove(kv.Value);
            playerBtns.Clear(); aiBtns.Clear();
            scoreGridPlayer.Controls.Clear(); scoreGridAI.Controls.Clear();

            var order = new[]
            {
                ScoreCategory.Ones, ScoreCategory.Twos, ScoreCategory.Threes, ScoreCategory.Fours, ScoreCategory.Fives,
                ScoreCategory.Sixes, ScoreCategory.Straight, ScoreCategory.FullHouse, ScoreCategory.FourOfAKind, ScoreCategory.Generala
            };

            for (int i = 0; i < order.Length; i++)
            {
                var cat = order[i]; int col = i / 5; int row = i % 5;

                var bp = new Button
                {
                    Tag = cat,
                    Dock = DockStyle.Fill,
                    Margin = new Padding(6),
                    BackColor = basePlayerBtnColor,
                    TextAlign = ContentAlignment.MiddleLeft,
                    Padding = new Padding(10, 0, 10, 0),
                    AutoEllipsis = true,
                    Font = new Font("Segoe UI Semibold", 10, FontStyle.Bold),
                    FlatStyle = FlatStyle.Flat,
                    ForeColor = Theme.ScoreText,
                    Cursor = Cursors.No                                // початково: клік не дає результату
                };
                bp.FlatAppearance.BorderSize = 0;
                bp.Click += new EventHandler(ScoreButton_Click);
                scoreGridPlayer.Controls.Add(bp, col, row);
                playerBtns[cat] = bp;

                var ba = new Button
                {
                    Tag = cat,
                    Dock = DockStyle.Fill,
                    Margin = new Padding(6),
                    BackColor = aiBaseBtnColor,
                    TextAlign = ContentAlignment.MiddleLeft,
                    Padding = new Padding(10, 0, 10, 0),
                    AutoEllipsis = true,
                    Font = new Font("Segoe UI Semibold", 9.5f, FontStyle.Bold),
                    FlatStyle = FlatStyle.Flat,
                    ForeColor = Theme.ScoreText
                };
                ba.FlatAppearance.BorderSize = 0;
                scoreGridAI.Controls.Add(ba, col, row);
                aiBtns[cat] = ba;
            }

            RefreshScoreButtons();
        }

        private string CategoryName(ScoreCategory c)
        {
            switch (c)
            {
                case ScoreCategory.Ones: return "• 1";
                case ScoreCategory.Twos: return "• 2";
                case ScoreCategory.Threes: return "• 3";
                case ScoreCategory.Fours: return "• 4";
                case ScoreCategory.Fives: return "• 5";
                case ScoreCategory.Sixes: return "• 6";
                case ScoreCategory.Straight: return "E (20)";
                case ScoreCategory.FullHouse: return "F (30)";
                case ScoreCategory.FourOfAKind: return "P (40)";
                case ScoreCategory.Generala: return "G (50/60)";
                default: return c.ToString();
            }
        }

        private void RefreshScoreButtons()
        {
            for (int i = 0; i < 5; i++) dice[i] = diceCtrls[i].Value;

            foreach (ScoreCategory c in Enum.GetValues(typeof(ScoreCategory)))
            {
                var b = playerBtns[c];
                bool used = playerCard.IsUsed(c);
                bool canScoreNow = playerTurn && !used && (rollsLeft < 3); // тільки після 1-го кидка
                int potential = ScoreCalculator.Score(c, dice, rollsLeft == 2);

                // кнопки тепер завжди Enabled=true — тож керуємо тільки виглядом/курсорем
                b.Cursor = canScoreNow ? Cursors.Hand : Cursors.No;
                b.Text = used
                    ? string.Format("{0}  ✓  ({1})", CategoryName(c), playerCard.Get(c) ?? 0)
                    : string.Format("{0}  →  {1}", CategoryName(c), potential);
                b.BackColor = used ? usedPlayerBtnColor :
                              (canScoreNow && potential > 0) ? highlightColor : basePlayerBtnColor;

                var ba = aiBtns[c];
                if (aiCard.IsUsed(c))
                {
                    ba.Text = string.Format("{0}  ✓  ({1})", CategoryName(c), aiCard.Get(c) ?? 0);
                    ba.BackColor = aiUsedBtnColor;
                }
                else
                {
                    ba.Text = CategoryName(c);
                    ba.BackColor = aiBaseBtnColor;
                }
            }

            lblTotals.Text = "Гравець: " + playerCard.Total();
            lblAiTotals.Text = "Комп'ютер: " + aiCard.Total();

            int round = playerCard.Used.Count + 1;
            int totalRounds = Enum.GetValues(typeof(ScoreCategory)).Length;
            if (round > totalRounds) round = totalRounds;
            lblRound.Text = string.Format("ROUND {0}/{1}", round, totalRounds);
        }

        private void NewGame()
        {
            playerCard = new ScoreCard(); aiCard = new ScoreCard();
            playerTurn = true; rollsLeft = 3;

            dicePanel.Controls.Clear(); holdPanel.Controls.Clear();
            for (int i = 0; i < 5; i++)
            {
                var dc = diceCtrls[i]; dc.SetValue(1); dc.SetHeld(false);
                dicePanel.Controls.Add(dc); dice[i] = 1;
            }
            lblStatus.Text = "Нова гра. Ваш хід: натисніть ROLL.";
            UpdateRollUi(); RefreshScoreButtons(); btnRoll.Enabled = true; UpdateDiceSizes();
        }

        private void UpdateRollUi()
        {
            lblRolls.Text = string.Format("ROLLS: {0}", rollsLeft);
            btnRoll.Text = string.Format("ROLL ({0})", rollsLeft);
        }
        private void SetRollsLabel(int rolls) { lblRolls.Text = string.Format("ROLLS: {0}", rolls); }

        private async Task BtnRoll_Click()
        {
            if (!playerTurn || rollsLeft <= 0) return;

            var rollMask = new bool[5];
            for (int i = 0; i < 5; i++) rollMask[i] = !diceCtrls[i].Held;

            var final = new int[5];
            for (int i = 0; i < 5; i++) final[i] = rollMask[i] ? DiceUtil.Rng.Next(1, 7) : diceCtrls[i].Value;

            bool disallowAllOnes = (rollsLeft == 3) && DiceUtil.AllOnes(dice);
            await AnimateDiceAsync(PLAYER_ROLL_MS, rollMask, final, disallowAllOnes);

            for (int i = 0; i < 5; i++) dice[i] = diceCtrls[i].Value;

            rollsLeft--;
            lblStatus.Text = "Переміщуйте кубики у лоток або оберіть категорію.";
            UpdateRollUi(); RefreshScoreButtons();

            if (rollsLeft == 2 && dice.All(v => v == dice[0]))
            {
                ApplyPlayerScore(ScoreCategory.Generala, 60);
                EndGame("Ви зробили Generala на першому кидку! Перемога!");
            }
        }

        private async void ScoreButton_Click(object sender, EventArgs e)
        {
            if (!playerTurn || rollsLeft == 3)
            {
                lblStatus.Text = "Спершу зробіть перший кидок (ROLL).";
                return;
            }

            var b = (Button)sender;
            var c = (ScoreCategory)b.Tag;

            bool served = (rollsLeft == 2);
            int score = ScoreCalculator.Score(c, dice, served);
            ApplyPlayerScore(c, score);

            if (!CheckEndGame())
            {
                PlayerLockUi(false);
                await ComputerTurnAsync();
            }
        }

        private void ApplyPlayerScore(ScoreCategory c, int value)
        {
            playerCard.Set(c, value);
            lblStatus.Text = string.Format("Ви обрали «{0}» = {1}. Хід комп’ютера…", CategoryName(c), value);
            rollsLeft = 3;

            dicePanel.Controls.Clear(); holdPanel.Controls.Clear();
            for (int i = 0; i < 5; i++)
            {
                var dc = diceCtrls[i];
                dc.SetValue(1); dc.SetHeld(false);
                dicePanel.Controls.Add(dc); dice[i] = 1;
            }
            UpdateRollUi(); RefreshScoreButtons(); UpdateDiceSizes();
        }

        private async Task ComputerTurnAsync()
        {
            playerTurn = false; btnRoll.Enabled = false;

            dicePanel.Controls.Clear(); holdPanel.Controls.Clear();
            for (int i = 0; i < 5; i++) { diceCtrls[i].SetHeld(false); diceCtrls[i].SetValue(1); dicePanel.Controls.Add(diceCtrls[i]); }
            UpdateDiceSizes();

            var log = ai.PlayTurnWithLog(aiCard, playerCard);

            int aiRollsLeft = 3; SetRollsLabel(aiRollsLeft);

            var firstFinal = log.Steps[0].AfterDice;
            await AnimateDiceAsync(AI_ROLL_MS, new[] { true, true, true, true, true }, firstFinal, true);
            await Task.Delay(AI_SHOW_RESULT_MS);

            if (AllEqual(firstFinal))
            {
                aiCard.Set(ScoreCategory.Generala, 60);    
                await FlashAiPick(ScoreCategory.Generala, 60);
                RefreshScoreButtons();
                EndGame("Комп’ютер зробив Generala на першому кидку!");
                return;                                       
            }

            aiRollsLeft--; SetRollsLabel(aiRollsLeft);



            for (int s = 1; s < log.Steps.Count; s++)
            {
                var step = log.Steps[s];

                for (int i = 0; i < 5; i++)
                {
                    if (step.HoldMask[i]) MoveToTray(diceCtrls[i]);
                    else MoveToUpper(diceCtrls[i]);
                }

                var rollMask = new bool[5];
                for (int i = 0; i < 5; i++) rollMask[i] = !step.HoldMask[i];

                await AnimateDiceAsync(AI_ROLL_MS, rollMask, step.AfterDice, false);
                await Task.Delay(AI_SHOW_RESULT_MS);

                aiRollsLeft--; if (aiRollsLeft < 0) aiRollsLeft = 0;
                SetRollsLabel(aiRollsLeft);
            }

            aiCard.Set(log.Category, log.Score);
            await FlashAiPick(log.Category, log.Score);

            lblStatus.Text = string.Format("Комп’ютер натиснув «{0}» = {1}", CategoryName(log.Category), log.Score);
            RefreshScoreButtons();

            if (!CheckEndGame())
            {
                playerTurn = true; rollsLeft = 3;

                dicePanel.Controls.Clear(); holdPanel.Controls.Clear();
                for (int i = 0; i < 5; i++)
                {
                    var dc = diceCtrls[i]; dc.SetValue(1); dc.SetHeld(false);
                    dicePanel.Controls.Add(dc); dice[i] = 1;
                }
                UpdateRollUi(); lblStatus.Text = "Ваш хід. Натисніть ROLL.";
                PlayerLockUi(true); RefreshScoreButtons(); UpdateDiceSizes();
            }
        }

        private async Task FlashAiPick(ScoreCategory c, int score)
        {
            var btn = aiBtns[c];
            btn.Text = string.Format("{0}  ✓  ({1})", CategoryName(c), score);
            var old = btn.BackColor; btn.BackColor = Theme.ButtonPrimary;
            await Task.Delay(900);
            btn.BackColor = aiUsedBtnColor;
        }

        private async Task AnimateDiceAsync(int durationMs, bool[] rollMask, int[] finalValues, bool disallowAllOnesAtStart)
        {
            if (disallowAllOnesAtStart && DiceUtil.AllOnes(finalValues))
            {
                for (int i = 0; i < 5; i++)
                    finalValues[i] = rollMask[i] ? Math.Max(2, DiceUtil.Rng.Next(1, 7)) : finalValues[i];
                if (DiceUtil.AllOnes(finalValues)) finalValues[0] = 2;
            }

            int elapsed = 0; int frame = 65;
            while (elapsed < durationMs)
            {
                for (int i = 0; i < 5; i++)
                    if (rollMask[i]) diceCtrls[i].SetValue(DiceUtil.Rng.Next(1, 7));
                await Task.Delay(frame); elapsed += frame;
            }
            for (int i = 0; i < 5; i++) diceCtrls[i].SetValue(finalValues[i]);
            for (int i = 0; i < 5; i++) dice[i] = diceCtrls[i].Value;
        }

        private void PlayerLockUi(bool enable)
        {
            btnRoll.Enabled = enable; // кнопки скора тепер не вимикаємо — керуємо лише курсором/підсвітом
        }

        private bool CheckEndGame()
        {
            if (playerCard.Completed() && aiCard.Completed())
            {
                int p = playerCard.Total(); int a = aiCard.Total();
                if (p > a) EndGame(string.Format("Гра закінчена. Ви перемогли {0}:{1}!", p, a));
                else if (a > p) EndGame(string.Format("Гра закінчена. Комп’ютер переміг {0}:{1}.", a, p));
                else EndGame(string.Format("Гра закінчена. Нічия {0}:{1}.", p, a));
                return true;
            }
            return false;
        }
        private void EndGame(string message)
        {
            lblStatus.Text = message;
            btnRoll.Enabled = false;
            foreach (var kv in playerBtns) kv.Value.Cursor = Cursors.No;

            // Підсумки
            int p = playerCard.Total();
            int a = aiCard.Total();

            string title;
            MessageBoxIcon icon;
            if (p > a) { title = "Перемога!"; icon = MessageBoxIcon.Information; }
            else if (a > p) { title = "Поразка"; icon = MessageBoxIcon.Warning; }
            else { title = "Нічия"; icon = MessageBoxIcon.Question; }

            var res = MessageBox.Show(
                $"{message}\n\nРахунок: Ви {p} — Комп’ютер {a}\n\nПочати нову гру?",
                title, MessageBoxButtons.YesNo, icon);

            if (res == DialogResult.Yes)
                NewGame();
        }

        private static bool AllEqual(int[] arr)
        {
            for (int i = 1; i < arr.Length; i++)
                if (arr[i] != arr[0]) return false;
            return true;
        }
    }
}
