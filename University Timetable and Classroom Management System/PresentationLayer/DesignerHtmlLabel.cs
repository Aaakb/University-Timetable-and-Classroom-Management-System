using System.ComponentModel;

namespace University_Timetable_and_Classroom_Management_System
{
    // Guna clears its HTML renderer during Dispose. The out-of-process designer
    // can still request these browsable properties while a design tab closes.
    public class DesignerHtmlLabel : Guna.UI2.WinForms.Guna2HtmlLabel
    {
        private bool rendererDisposalStarted;
        private bool avoidGeometryAntialias;
        private bool useGdiPlusTextRendering;
        private bool isSelectionEnabled = true;
        private bool isContextMenuEnabled = true;

        private bool UsesArabicText => RightToLeft == RightToLeft.Yes && Text.Any(c => c is >= '\u0600' and <= '\u06ff');

        private string PlainText => System.Net.WebUtility.HtmlDecode(
            System.Text.RegularExpressions.Regex.Replace(Text, @"<br\s*/?>", "\n",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase));

        public override Size GetPreferredSize(Size proposedSize)
        {
            if (UsesArabicText)
                return AutoSize ? MeasureArabicText() : Size;
            return base.GetPreferredSize(proposedSize);
        }

        internal void UseArabicTextLayout()
        {
            if (!AutoSize || !UsesArabicText) return;
            // Disable the HTML renderer's separate autosizing as well as its
            // painting, so a later layout cannot shrink an Arabic sentence.
            AutoSize = false;
            Size = MeasureArabicText();
            TextChanged += (_, _) => Size = MeasureArabicText();
            FontChanged += (_, _) => Size = MeasureArabicText();
        }

        private Size MeasureArabicText()
        {
            Size measured = TextRenderer.MeasureText(PlainText, Font, new Size(int.MaxValue, int.MaxValue),
                TextFormatFlags.RightToLeft | TextFormatFlags.NoPadding);
            return new Size(measured.Width + 8, measured.Height + 2);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            if (UsesArabicText)
            {
                // The HTML renderer lays out Arabic words separately, reversing
                // their order. Native text rendering shapes the whole sentence.
                TextRenderer.DrawText(e.Graphics, PlainText, Font, ClientRectangle, ForeColor,
                    TextFormatFlags.RightToLeft | TextFormatFlags.Right | TextFormatFlags.VerticalCenter |
                    TextFormatFlags.WordBreak | TextFormatFlags.NoPadding | TextFormatFlags.PreserveGraphicsClipping);
                return;
            }
            base.OnPaint(e);
        }

        [Category("Behavior")]
        [DefaultValue(false)]
        [Description("If anti-aliasing should be avoided for geometry like backgrounds and borders")]
        public override bool AvoidGeometryAntialias
        {
            get => rendererDisposalStarted || IsDisposed ? avoidGeometryAntialias : base.AvoidGeometryAntialias;
            set
            {
                if (!rendererDisposalStarted && !IsDisposed) base.AvoidGeometryAntialias = value;
                avoidGeometryAntialias = value;
            }
        }

        [Category("Behavior")]
        [DefaultValue(false)]
        [EditorBrowsable(EditorBrowsableState.Always)]
        [Description("If to use GDI+ text rendering to measure/draw text, false - use GDI")]
        public override bool UseGdiPlusTextRendering
        {
            get => rendererDisposalStarted || IsDisposed ? useGdiPlusTextRendering : base.UseGdiPlusTextRendering;
            set
            {
                if (!rendererDisposalStarted && !IsDisposed) base.UseGdiPlusTextRendering = value;
                useGdiPlusTextRendering = value;
            }
        }

        [Browsable(true)]
        [Category("Behavior")]
        [DefaultValue(true)]
        [EditorBrowsable(EditorBrowsableState.Always)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
        [Description("Is content selection is enabled for the rendered html.")]
        public override bool IsSelectionEnabled
        {
            get => rendererDisposalStarted || IsDisposed ? isSelectionEnabled : base.IsSelectionEnabled;
            set
            {
                if (!rendererDisposalStarted && !IsDisposed) base.IsSelectionEnabled = value;
                isSelectionEnabled = value;
            }
        }

        [Browsable(true)]
        [Category("Behavior")]
        [DefaultValue(true)]
        [EditorBrowsable(EditorBrowsableState.Always)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
        [Description("Is the build-in context menu enabled and will be shown on mouse right click.")]
        public override bool IsContextMenuEnabled
        {
            get => rendererDisposalStarted || IsDisposed ? isContextMenuEnabled : base.IsContextMenuEnabled;
            set
            {
                if (!rendererDisposalStarted && !IsDisposed) base.IsContextMenuEnabled = value;
                isContextMenuEnabled = value;
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (!rendererDisposalStarted && !IsDisposed)
            {
                avoidGeometryAntialias = base.AvoidGeometryAntialias;
                useGdiPlusTextRendering = base.UseGdiPlusTextRendering;
                isSelectionEnabled = base.IsSelectionEnabled;
                isContextMenuEnabled = base.IsContextMenuEnabled;
                rendererDisposalStarted = true;
            }

            base.Dispose(disposing);
        }
    }
}
