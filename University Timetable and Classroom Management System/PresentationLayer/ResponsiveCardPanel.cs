namespace University_Timetable_and_Classroom_Management_System
{
    // Measure the layout inside the existing Guna card before its bounds change.
    // This lets an AutoSize table row include wrapped fields in the same pass.
    public class ResponsiveCardPanel : Guna.UI2.WinForms.Guna2Panel
    {
        public override Size GetPreferredSize(Size proposedSize)
        {
            if (!AutoSize || Controls.Count != 1 || Controls[0] is not TableLayoutPanel and not FlowLayoutPanel)
                return base.GetPreferredSize(proposedSize);
            int width = proposedSize.Width > 1 && proposedSize.Width < 65536
                ? proposedSize.Width : Width;
            var content = Controls[0].GetPreferredSize(new Size(Math.Max(1, width - Padding.Horizontal), 0));
            return new Size(content.Width + Padding.Horizontal, content.Height + Padding.Vertical);
        }
    }
}
