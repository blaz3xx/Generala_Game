using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace GeneralaGame
{
    public class DiceControl : Control
    {
        public int Value { get; private set; } = 1;
        public bool Held { get; private set; }

        // --- Casino palette (ruby red) ---
        private readonly Color bodyLight = Color.FromArgb(215, 32, 32);  // світліший червоний
        private readonly Color bodyDark = Color.FromArgb(132, 12, 16);  // темний бордо для країв/тіні
        private readonly Color edgeStroke = Color.FromArgb(95, 10, 12);   // обвідка
        private readonly Color pipFill = Color.FromArgb(252, 252, 252); // білі піпи
        private readonly Color pipShadow = Color.FromArgb(80, 0, 0, 0);   // тінь піпів

        public DiceControl()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw |
                     ControlStyles.SupportsTransparentBackColor, true);

            BackColor = Color.Transparent;
            Width = 96; Height = 96;
            Cursor = Cursors.Hand;
            Margin = new Padding(4);
        }

        // фарбуємо фоном батька (жодних білих квадратів)
        protected override void OnPaintBackground(PaintEventArgs e)
        {
            var col = EffectiveBackColor();
            using (var b = new SolidBrush(col))
                e.Graphics.FillRectangle(b, ClientRectangle);
        }
        private Color EffectiveBackColor()
        {
            Control p = Parent;
            while (p != null && p.BackColor == Color.Transparent) p = p.Parent;
            return p != null ? p.BackColor : SystemColors.Control;
        }

        public void SetValue(int v) { if (v < 1) v = 1; if (v > 6) v = 6; Value = v; Invalidate(); }
        public void SetHeld(bool held) { Held = held; Invalidate(); }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            var r = ClientRectangle; r.Inflate(-3, -3);
            int cornerRadius = Math.Max(6, (int)(Math.Min(r.Width, r.Height) * 0.15f));

            // Форма та глянцевий градієнт (зверху світліше, знизу темніше)
            using (var path = RoundedRect(r, cornerRadius))
            {
                this.Region = new Region(path);

                using (var lg = new LinearGradientBrush(r, bodyLight, bodyDark, 90f))
                    g.FillPath(lg, path);

                using (var pen = new Pen(edgeStroke, 3f))
                    g.DrawPath(pen, path);
            }

            // Блік зверху зліва (м’яка “полірована” пляма)
            var gloss = new Rectangle(r.X + 6, r.Y + 6, r.Width - 12, (int)(r.Height * 0.38));
            using (var gp = RoundedRect(gloss, Math.Max(3, cornerRadius / 2)))
            using (var lg2 = new LinearGradientBrush(gloss, Color.FromArgb(80, Color.White), Color.FromArgb(0, Color.White), 90f))
                g.FillPath(lg2, gp);

            DrawPips(g, r, Value);
        }

        private void DrawPips(Graphics g, Rectangle r, int value)
        {
            // Сітка 3×3 із відступами
            float pad = r.Width * 0.16f;
            float d = r.Width * 0.16f;
            float x1 = r.Left + pad, x2 = r.Left + r.Width / 2f - d / 2f, x3 = r.Right - pad - d;
            float y1 = r.Top + pad, y2 = r.Top + r.Height / 2f - d / 2f, y3 = r.Bottom - pad - d;

            using (var bPip = new SolidBrush(pipFill))
            using (var bShadow = new SolidBrush(pipShadow))
            using (var outline = new Pen(Color.FromArgb(170, 170, 170), 1.2f))
            {
                Action<float, float> P = (x, y) =>
                {
                    // легка тінь вниз-праворуч
                    g.FillEllipse(bShadow, x + 1, y + 1, d, d);
                    // сам піп
                    g.FillEllipse(bPip, x, y, d, d);
                    g.DrawEllipse(outline, x, y, d, d);
                };

                Action Corners = () => { P(x1, y1); P(x3, y3); };
                Action AntiCorners = () => { P(x3, y1); P(x1, y3); };

                switch (value)
                {
                    case 1: P(x2, y2); break;
                    case 2: Corners(); break;
                    case 3: Corners(); P(x2, y2); break;
                    case 4: Corners(); AntiCorners(); break;
                    case 5: Corners(); AntiCorners(); P(x2, y2); break;
                    case 6:
                        P(x1, y1); P(x1, y2); P(x1, y3);
                        P(x3, y1); P(x3, y2); P(x3, y3);
                        break;
                }
            }
        }

        private static GraphicsPath RoundedRect(Rectangle bounds, int radius)
        {
            int d = radius * 2;
            var path = new GraphicsPath();
            if (radius <= 0) { path.AddRectangle(bounds); path.CloseFigure(); return path; }

            path.AddArc(bounds.X, bounds.Y, d, d, 180, 90);
            path.AddArc(bounds.Right - d, bounds.Y, d, d, 270, 90);
            path.AddArc(bounds.Right - d, bounds.Bottom - d, d, d, 0, 90);
            path.AddArc(bounds.X, bounds.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }
    }
}
