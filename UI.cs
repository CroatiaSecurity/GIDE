using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace GIDE
{
    /// <summary>
    /// Centralized theme — creamy black with deep purple accents.
    /// </summary>
    public static class Theme
    {
        // Backgrounds (creamy black, slight purple tint)
        public static readonly Color BgDeep      = Color.FromArgb(14, 10, 20);    // root form
        public static readonly Color BgMid       = Color.FromArgb(26, 19, 34);    // side panels
        public static readonly Color BgPanel     = Color.FromArgb(35, 23, 48);    // cards / input
        public static readonly Color BgPanelHi   = Color.FromArgb(46, 30, 64);    // hover on cards
        public static readonly Color BgInput     = Color.FromArgb(42, 27, 61);    // input field
        public static readonly Color BgStatus    = Color.FromArgb(20, 14, 28);    // status bar

        // Accent (purple)
        public static readonly Color Accent      = Color.FromArgb(168, 85, 247);  // primary purple
        public static readonly Color AccentHi    = Color.FromArgb(192, 132, 252); // hover
        public static readonly Color AccentLo    = Color.FromArgb(126, 34, 206);  // pressed

        // Text
        public static readonly Color Text        = Color.FromArgb(244, 237, 255);
        public static readonly Color TextDim     = Color.FromArgb(184, 168, 216);
        public static readonly Color TextFaint   = Color.FromArgb(120, 105, 150);

        // Borders / dividers
        public static readonly Color Border      = Color.FromArgb(61, 42, 82);
        public static readonly Color BorderSoft  = Color.FromArgb(46, 32, 64);

        // Semantic
        public static readonly Color UserBubble  = Color.FromArgb(192, 132, 252); // light purple
        public static readonly Color AiBubble    = Color.FromArgb(134, 239, 172); // mint green
        public static readonly Color ErrorBubble = Color.FromArgb(248, 113, 113); // soft red

        public static readonly string FontFamily      = "Segoe UI Variable";
        public static readonly string FontFamilyMono  = "Cascadia Mono";
        public static readonly string FontFamilyFallback = "Segoe UI";

        public static Font Font(float size, FontStyle style = FontStyle.Regular)
        {
            try { return new Font(FontFamily, size, style); }
            catch { return new Font(FontFamilyFallback, size, style); }
        }

        public static Font FontMono(float size, FontStyle style = FontStyle.Regular)
        {
            try { return new Font(FontFamilyMono, size, style); }
            catch { return new Font("Consolas", size, style); }
        }
    }

    /// <summary>
    /// A panel that paints a smooth vertical (or diagonal) gradient background.
    /// Used as the root background instead of a flat color for visual depth.
    /// </summary>
    public class GradientPanel : Panel
    {
        public Color GradientTop    { get; set; }
        public Color GradientBottom { get; set; }
        public float Angle          { get; set; }
        public bool  ShowGlow       { get; set; }

        public GradientPanel()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint
                   | ControlStyles.OptimizedDoubleBuffer
                   | ControlStyles.UserPaint
                   | ControlStyles.ResizeRedraw
                   | ControlStyles.SupportsTransparentBackColor, true);
            DoubleBuffered  = true;
            BackColor       = Theme.BgDeep;
            GradientTop     = Theme.BgDeep;
            GradientBottom  = Theme.BgMid;
            Angle           = 90f;
            ShowGlow        = true;
        }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            if (Width <= 0 || Height <= 0) return;
            using (var brush = new LinearGradientBrush(ClientRectangle, GradientTop, GradientBottom, Angle))
            {
                e.Graphics.FillRectangle(brush, ClientRectangle);
            }
            PaintAmbientGlow(e.Graphics);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            // Background-only panel
        }

        // Subtle radial-style glow centered roughly where the input card will sit.
        // Gives the form an atmospheric "spotlight" feel without needing real Mica.
        private void PaintAmbientGlow(Graphics g)
        {
            if (!ShowGlow) return;
            try
            {
                g.SmoothingMode = SmoothingMode.HighQuality;
                int cx = Width / 2;
                int cy = (int)(Height * 0.45f);
                int r  = (int)(Math.Max(Width, Height) * 0.55f);

                using (var path = new GraphicsPath())
                {
                    path.AddEllipse(cx - r, cy - r, r * 2, r * 2);
                    using (var pgb = new PathGradientBrush(path))
                    {
                        pgb.CenterColor = Color.FromArgb(50, 138, 92, 215);
                        pgb.SurroundColors = new[] { Color.FromArgb(0, 14, 10, 20) };
                        pgb.CenterPoint = new PointF(cx, cy);
                        g.FillPath(pgb, path);
                    }
                }
            }
            catch { }
        }
    }

    /// <summary>
    /// Helper to draw rounded rectangles.
    /// </summary>
    public static class RoundedDraw
    {
        public static GraphicsPath Path(Rectangle rect, int radius)
        {
            var path = new GraphicsPath();
            if (radius <= 0)
            {
                path.AddRectangle(rect);
                path.CloseFigure();
                return path;
            }

            int d = radius * 2;
            if (d > rect.Width)  d = rect.Width;
            if (d > rect.Height) d = rect.Height;

            path.AddArc(rect.X, rect.Y, d, d, 180, 90);
            path.AddArc(rect.Right - d, rect.Y, d, d, 270, 90);
            path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90);
            path.AddArc(rect.X, rect.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }
    }

    /// <summary>
    /// A panel with rounded corners and optional subtle border.
    /// </summary>
    public class RoundedPanel : Panel
    {
        private int _radius = 14;
        private Color _borderColor = Color.Empty;
        private int _borderWidth = 0;

        public int Radius
        {
            get { return _radius; }
            set { _radius = value; Invalidate(); }
        }

        public Color BorderColor
        {
            get { return _borderColor; }
            set { _borderColor = value; Invalidate(); }
        }

        public int BorderWidth
        {
            get { return _borderWidth; }
            set { _borderWidth = value; Invalidate(); }
        }

        public RoundedPanel()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint
                   | ControlStyles.OptimizedDoubleBuffer
                   | ControlStyles.UserPaint
                   | ControlStyles.ResizeRedraw
                   | ControlStyles.SupportsTransparentBackColor, true);
            DoubleBuffered = true;
            BackColor = Theme.BgPanel;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;

            // Fill parent background first so corners blend
            if (Parent != null)
            {
                using (var pb = new SolidBrush(Parent.BackColor))
                    g.FillRectangle(pb, ClientRectangle);
            }

            var rect = new Rectangle(0, 0, Width - 1, Height - 1);
            using (var path = RoundedDraw.Path(rect, _radius))
            {
                using (var brush = new SolidBrush(BackColor))
                    g.FillPath(brush, path);

                if (_borderWidth > 0 && _borderColor != Color.Empty)
                {
                    using (var pen = new Pen(_borderColor, _borderWidth))
                    {
                        pen.Alignment = PenAlignment.Inset;
                        g.DrawPath(pen, path);
                    }
                }
            }
        }
    }

    /// <summary>
    /// A flat button with rounded corners, hover/press states, optional border.
    /// </summary>
    public class RoundedButton : Button
    {
        private int _radius = 12;
        private Color _hoverColor = Color.Empty;
        private Color _pressColor = Color.Empty;
        private Color _borderColor = Color.Empty;
        private int _borderWidth = 0;
        private bool _hover = false;
        private bool _press = false;

        public int Radius
        {
            get { return _radius; }
            set { _radius = value; Invalidate(); }
        }

        public Color HoverColor
        {
            get { return _hoverColor; }
            set { _hoverColor = value; Invalidate(); }
        }

        public Color PressColor
        {
            get { return _pressColor; }
            set { _pressColor = value; Invalidate(); }
        }

        public Color BorderColor
        {
            get { return _borderColor; }
            set { _borderColor = value; Invalidate(); }
        }

        public int BorderWidth
        {
            get { return _borderWidth; }
            set { _borderWidth = value; Invalidate(); }
        }

        public RoundedButton()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint
                   | ControlStyles.OptimizedDoubleBuffer
                   | ControlStyles.UserPaint
                   | ControlStyles.ResizeRedraw
                   | ControlStyles.SupportsTransparentBackColor, true);
            DoubleBuffered = true;
            FlatStyle = FlatStyle.Flat;
            FlatAppearance.BorderSize = 0;
            Cursor = Cursors.Hand;
            BackColor = Theme.BgPanel;
            ForeColor = Theme.Text;
            Font = Theme.Font(9.5f);
        }

        protected override void OnMouseEnter(EventArgs e)
        {
            _hover = true; Invalidate(); base.OnMouseEnter(e);
        }
        protected override void OnMouseLeave(EventArgs e)
        {
            _hover = false; _press = false; Invalidate(); base.OnMouseLeave(e);
        }
        protected override void OnMouseDown(MouseEventArgs mevent)
        {
            _press = true; Invalidate(); base.OnMouseDown(mevent);
        }
        protected override void OnMouseUp(MouseEventArgs mevent)
        {
            _press = false; Invalidate(); base.OnMouseUp(mevent);
        }

        protected override void OnPaint(PaintEventArgs pe)
        {
            var g = pe.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;

            // Parent fill so corners blend
            if (Parent != null)
            {
                using (var pb = new SolidBrush(Parent.BackColor))
                    g.FillRectangle(pb, ClientRectangle);
            }

            var rect = new Rectangle(0, 0, Width - 1, Height - 1);
            Color fill = BackColor;
            if (!Enabled)
                fill = Color.FromArgb(180, BackColor);
            else if (_press && _pressColor != Color.Empty)
                fill = _pressColor;
            else if (_hover && _hoverColor != Color.Empty)
                fill = _hoverColor;

            using (var path = RoundedDraw.Path(rect, _radius))
            {
                using (var brush = new SolidBrush(fill))
                    g.FillPath(brush, path);

                if (_borderWidth > 0 && _borderColor != Color.Empty)
                {
                    using (var pen = new Pen(_borderColor, _borderWidth))
                    {
                        pen.Alignment = PenAlignment.Inset;
                        g.DrawPath(pen, path);
                    }
                }
            }

            // Text
            TextRenderer.DrawText(g, Text, Font, ClientRectangle,
                Enabled ? ForeColor : Color.FromArgb(160, ForeColor),
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter
                | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis);
        }
    }

    /// <summary>
    /// Win11 Mica + dark title-bar enabler via DWM API. Falls back gracefully on older Windows.
    /// </summary>
    public static class MicaHelper
    {
        [StructLayout(LayoutKind.Sequential)]
        private struct MARGINS
        {
            public int cxLeftWidth;
            public int cxRightWidth;
            public int cyTopHeight;
            public int cyBottomHeight;
        }

        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

        [DllImport("dwmapi.dll")]
        private static extern int DwmExtendFrameIntoClientArea(IntPtr hWnd, ref MARGINS pMarInset);

        // Win11 22H2+
        private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;
        private const int DWMWA_SYSTEMBACKDROP_TYPE = 38;

        // DWM_SYSTEMBACKDROP_TYPE
        private const int DWMSBT_AUTO = 0;
        private const int DWMSBT_NONE = 1;
        private const int DWMSBT_MAINWINDOW = 2;        // Mica
        private const int DWMSBT_TRANSIENTWINDOW = 3;   // Acrylic
        private const int DWMSBT_TABBEDWINDOW = 4;      // Mica Alt

        public static void Apply(Form form)
        {
            try
            {
                IntPtr h = form.Handle;
                int dark = 1;
                DwmSetWindowAttribute(h, DWMWA_USE_IMMERSIVE_DARK_MODE, ref dark, sizeof(int));

                int backdrop = DWMSBT_MAINWINDOW;
                DwmSetWindowAttribute(h, DWMWA_SYSTEMBACKDROP_TYPE, ref backdrop, sizeof(int));

                // Extend frame so the backdrop shows through
                MARGINS m = new MARGINS { cxLeftWidth = -1, cxRightWidth = -1, cyTopHeight = -1, cyBottomHeight = -1 };
                DwmExtendFrameIntoClientArea(h, ref m);
            }
            catch
            {
                // Older Windows — silently no-op. The dark theme still looks great as a flat color.
            }
        }
    }
}
