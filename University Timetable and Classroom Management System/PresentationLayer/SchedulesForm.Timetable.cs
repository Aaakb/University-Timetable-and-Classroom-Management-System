using System.Drawing.Drawing2D;
using Guna.UI2.WinForms;

namespace University_Timetable_and_Classroom_Management_System
{
    public partial class SchedulesForm
    {
        private int TimetableRowHeight => TimetablePixels(164);
        private int TimetableCardHeight => TimetablePixels(68);
        private int TimetableStep => TimetablePixels(74);
        private int TimetablePixels(int value) => WindowSizing.Pixels(dgvTimetable, value);
        private readonly Dictionary<(int Row, int Column), int> timetableCellOffsets = new();

        private void ConfigureTimetableEvents()
        {
            dgvTimetable.AutoGenerateColumns = false;
            dgvTimetable.DpiChangedAfterParent += (_, _) =>
            {
                foreach (DataGridViewRow row in dgvTimetable.Rows) row.Height = TimetableRowHeight;
                timetableCellOffsets.Clear();
                dgvTimetable.Invalidate();
            };
            dgvTimetable.CellPainting += PaintTimetableCell;
            dgvTimetable.CellMouseClick += ClickTimetableCell;
            dgvTimetable.CellMouseMove += (_, e) =>
            {
                if (e.Button == MouseButtons.Left) ScrollTimetableTrack(e);
            };
            dgvTimetable.ScrollEntryCell = ScrollTimetableCell;
        }

        private void RenderScheduleTimetable(IReadOnlyList<ScheduleRow> rows, HashSet<int> conflicts)
        {
            dgvTimetable.SuspendLayout();
            try
            {
                dgvTimetable.Rows.Clear();
                timetableCellOffsets.Clear();
                var days = GetTimetableDays(rows);
                foreach (DataGridViewColumn column in dgvTimetable.Columns)
                    if (column != colTimetableTime) column.Visible = days.Contains(column.DataPropertyName, StringComparer.OrdinalIgnoreCase);
                bool includeSemester = rows.Select(row => row.SemesterNumber).Distinct().Take(2).Count() > 1;
                var slots = GetTimetableTimeSlots(rows, includeSemester);
                if (slots.Count == 0) dgvTimetable.Rows[dgvTimetable.Rows.Add()].Cells[colTimetableTime.Index].Value = "-";
                foreach (var slot in slots)
                {
                    int index = dgvTimetable.Rows.Add();
                    var gridRow = dgvTimetable.Rows[index];
                    gridRow.Cells[colTimetableTime.Index].Value = slot.Label;
                    gridRow.Height = TimetableRowHeight;
                    foreach (DataGridViewColumn column in dgvTimetable.Columns)
                    {
                        if (column == colTimetableTime) continue;
                        var entries = rows.Where(row => row.SemesterNumber == slot.SemesterNumber && row.TimeSlotID == slot.TimeSlotId &&
                            string.Equals(row.DayOfWeek, column.DataPropertyName, StringComparison.OrdinalIgnoreCase))
                            .OrderBy(row => row.SectionName).ThenBy(row => row.GroupName).ThenBy(row => row.SubjectName).ToList();
                        gridRow.Cells[column.Index].Tag = new TimetableCell(entries, conflicts);
                        gridRow.Cells[column.Index].Value = string.Join(Environment.NewLine, entries.Select(row => row.SubjectName));
                        gridRow.Cells[column.Index].ToolTipText = string.Join(Environment.NewLine + Environment.NewLine,
                            entries.Select(row => $"{row.SubjectName}{Environment.NewLine}{row.FacultyMemberName}{Environment.NewLine}{row.ClassroomName} | {row.SectionName} | {BuildTimetableTypeLabel(row)}"));
                    }
                }
                dgvTimetable.ClearSelection();
            }
            finally { dgvTimetable.ResumeLayout(); }
        }

        private void PaintTimetableCell(object? sender, DataGridViewCellPaintingEventArgs e)
        {
            if (e.RowIndex < 0 || e.ColumnIndex < 0 || e.ColumnIndex == colTimetableTime.Index || e.Graphics is null) return;
            e.PaintBackground(e.CellBounds, false);
            e.Paint(e.CellBounds, DataGridViewPaintParts.Border);
            var cell = dgvTimetable.Rows[e.RowIndex].Cells[e.ColumnIndex].Tag as TimetableCell;
            if (cell is null || cell.Entries.Count == 0)
            {
                TextRenderer.DrawText(e.Graphics, "-", dgvTimetable.DefaultCellStyle.Font,
                    new Rectangle(e.CellBounds.X, e.CellBounds.Y + 6, e.CellBounds.Width, 30), Color.FromArgb(148, 163, 184),
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.PreserveGraphicsClipping);
                e.Handled = true;
                return;
            }
            GraphicsState state = e.Graphics.Save();
            e.Graphics.SetClip(Rectangle.Inflate(e.CellBounds, -1, -1), CombineMode.Intersect);
            int offset = timetableCellOffsets.GetValueOrDefault((e.RowIndex, e.ColumnIndex));
            int cardWidth = Math.Max(TimetablePixels(150), e.CellBounds.Width - TimetablePixels(32));
            var font = dgvTimetable.DefaultCellStyle.Font ?? Font;
            using var titleFont = new Font(font, FontStyle.Bold);
            for (int i = 0; i < cell.Entries.Count; i++)
            {
                var entry = cell.Entries[i];
                bool conflict = cell.Conflicts.Contains(entry.ScheduleID);
                bool practical = entry.LectureType == "Practical";
                var bounds = new Rectangle(e.CellBounds.X + TimetablePixels(6), e.CellBounds.Y + TimetablePixels(6) + i * TimetableStep - offset, cardWidth, TimetableCardHeight);
                Color fill = conflict ? Color.FromArgb(254, 242, 242) : practical ? Color.FromArgb(240, 253, 244) : Color.FromArgb(239, 246, 255);
                Color border = conflict ? Color.FromArgb(248, 113, 113) : practical ? Color.FromArgb(34, 197, 94) : Color.FromArgb(96, 165, 250);
                using var path = RoundedTimetableCard(bounds);
                using var brush = new SolidBrush(fill);
                using var pen = new Pen(border);
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                e.Graphics.FillPath(brush, path);
                e.Graphics.DrawPath(pen, path);
                DrawTimetableLine(e.Graphics, (entry.IsLocked ? "مثبتة | " : "") + entry.SubjectName, titleFont, bounds, 5, Color.FromArgb(15, 23, 42));
                DrawTimetableLine(e.Graphics, entry.FacultyMemberName, font, bounds, 24, Color.FromArgb(51, 65, 85));
                DrawTimetableLine(e.Graphics, $"{entry.ClassroomName} | {entry.SectionName} | {BuildTimetableTypeLabel(entry)}", font, bounds, 43,
                    conflict ? Color.FromArgb(185, 28, 28) : Color.FromArgb(71, 85, 105));
            }
            int maxOffset = Math.Max(0, cell.Entries.Count * TimetableStep + TimetablePixels(6) - e.CellBounds.Height);
            if (maxOffset > 0)
            {
                var track = new Rectangle(e.CellBounds.Right - TimetablePixels(16), e.CellBounds.Top + TimetablePixels(2), TimetablePixels(12), e.CellBounds.Height - TimetablePixels(4));
                using var trackBrush = new SolidBrush(Color.FromArgb(241, 245, 249));
                using var thumbBrush = new SolidBrush(Color.FromArgb(148, 163, 184));
                e.Graphics.FillRectangle(trackBrush, track);
                int thumbHeight = Math.Max(TimetablePixels(20), track.Height * track.Height / (track.Height + maxOffset));
                e.Graphics.FillRectangle(thumbBrush, new Rectangle(track.X + TimetablePixels(2), track.Y + offset * (track.Height - thumbHeight) / maxOffset, TimetablePixels(8), thumbHeight));
            }
            e.Graphics.Restore(state);
            e.Handled = true;
        }

        private GraphicsPath RoundedTimetableCard(Rectangle bounds)
        {
            var path = new GraphicsPath();
            int diameter = TimetablePixels(12);
            path.AddArc(bounds.X, bounds.Y, diameter, diameter, 180, 90);
            path.AddArc(bounds.Right - diameter - 1, bounds.Y, diameter, diameter, 270, 90);
            path.AddArc(bounds.Right - diameter - 1, bounds.Bottom - diameter - 1, diameter, diameter, 0, 90);
            path.AddArc(bounds.X, bounds.Bottom - diameter - 1, diameter, diameter, 90, 90);
            path.CloseFigure();
            return path;
        }

        private void DrawTimetableLine(Graphics graphics, string text, Font font, Rectangle card, int top, Color color)
        {
            TextRenderer.DrawText(graphics, text, font, new Rectangle(card.X + TimetablePixels(8), card.Y + TimetablePixels(top), Math.Max(TimetablePixels(80), card.Width - TimetablePixels(16)), Math.Max(TimetablePixels(17), font.Height)), color,
                TextFormatFlags.Right | TextFormatFlags.RightToLeft | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix | TextFormatFlags.PreserveGraphicsClipping);
        }

        private bool ScrollTimetableCell(MouseEventArgs e)
        {
            var hit = dgvTimetable.HitTest(e.X, e.Y);
            if (hit.RowIndex < 0 || hit.ColumnIndex < 0 || hit.ColumnIndex == colTimetableTime.Index || dgvTimetable.Rows[hit.RowIndex].Cells[hit.ColumnIndex].Tag is not TimetableCell cell) return false;
            int height = dgvTimetable.Rows[hit.RowIndex].Height;
            int max = Math.Max(0, cell.Entries.Count * TimetableStep + TimetablePixels(6) - height);
            if (max == 0) return false;
            var key = (hit.RowIndex, hit.ColumnIndex);
            timetableCellOffsets[key] = Math.Clamp(timetableCellOffsets.GetValueOrDefault(key) - Math.Sign(e.Delta) * TimetableStep, 0, max);
            dgvTimetable.InvalidateCell(hit.ColumnIndex, hit.RowIndex);
            return true;
        }

        private void ClickTimetableCell(object? sender, DataGridViewCellMouseEventArgs e)
        {
            if (ScrollTimetableTrack(e)) return;
            if (e.RowIndex < 0 || e.ColumnIndex < 0 || e.ColumnIndex == colTimetableTime.Index || dgvTimetable.Rows[e.RowIndex].Cells[e.ColumnIndex].Tag is not TimetableCell cell) return;
            int y = e.Y - TimetablePixels(6) + timetableCellOffsets.GetValueOrDefault((e.RowIndex, e.ColumnIndex));
            int cardWidth = Math.Max(TimetablePixels(150), dgvTimetable.Columns[e.ColumnIndex].Width - TimetablePixels(32));
            if (y < 0 || y % TimetableStep >= TimetableCardHeight || e.X < TimetablePixels(6) || e.X > cardWidth + TimetablePixels(6)) return;
            int index = y / TimetableStep;
            if (index >= 0 && index < cell.Entries.Count) SelectScheduleRowById(cell.Entries[index].ScheduleID);
        }

        private bool ScrollTimetableTrack(DataGridViewCellMouseEventArgs e)
        {
            if (e.RowIndex < 0 || e.ColumnIndex < 0 || e.ColumnIndex == colTimetableTime.Index || dgvTimetable.Rows[e.RowIndex].Cells[e.ColumnIndex].Tag is not TimetableCell cell) return false;
            int height = dgvTimetable.Rows[e.RowIndex].Height;
            int maximum = Math.Max(0, cell.Entries.Count * TimetableStep + TimetablePixels(6) - height);
            if (maximum == 0 || e.X < dgvTimetable.Columns[e.ColumnIndex].Width - TimetablePixels(16)) return false;
            timetableCellOffsets[(e.RowIndex, e.ColumnIndex)] = Math.Clamp((e.Y - TimetablePixels(2)) * maximum / Math.Max(1, height - TimetablePixels(4)), 0, maximum);
            dgvTimetable.InvalidateCell(e.ColumnIndex, e.RowIndex);
            return true;
        }

        private sealed record TimetableCell(IReadOnlyList<ScheduleRow> Entries, HashSet<int> Conflicts);

        private void ShowScheduleGridView()
        {
            SetScheduleView(showTimetable: false);
        }

        private void ShowScheduleTimetableView()
        {
            SetScheduleView(showTimetable: true);
        }

        private void SetScheduleView(bool showTimetable)
        {
            isTimetableView = showTimetable;
            dgvSchedules.Visible = !showTimetable;
            pnlTimetableHost.Visible = showTimetable;
            ApplyScheduleViewButtonStyles();
        }

        private void ApplyScheduleViewButtonStyles()
        {
            StyleScheduleViewButton(btnScheduleGridView, !isTimetableView);
            StyleScheduleViewButton(btnScheduleTimetableView, isTimetableView);
        }

        private static void StyleScheduleViewButton(Guna2Button button, bool selected)
        {
            button.FillColor = selected ? Color.FromArgb(15, 23, 42) : Color.White;
            button.ForeColor = selected ? Color.White : Color.FromArgb(51, 65, 85);
            button.HoverState.FillColor = selected ? Color.FromArgb(30, 41, 59) : Color.FromArgb(248, 250, 252);
            button.HoverState.ForeColor = selected ? Color.White : Color.FromArgb(51, 65, 85);
            button.DisabledState.BorderColor = Color.FromArgb(203, 213, 225);
            button.DisabledState.FillColor = selected ? Color.FromArgb(15, 23, 42) : Color.White;
            button.DisabledState.ForeColor = selected ? Color.White : Color.FromArgb(51, 65, 85);
        }

        private static List<string> GetTimetableDays(IReadOnlyCollection<ScheduleRow> rows)
        {
            var rowDays = rows
                .Select(row => row.DayOfWeek)
                .Where(day => !string.IsNullOrWhiteSpace(day))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(DayOrder)
                .ToList();

            return rowDays.Count == 0
                ? ["Sunday", "Monday", "Tuesday", "Wednesday", "Thursday"]
                : rowDays;
        }

        private static List<TimetableSlot> GetTimetableTimeSlots(
            IEnumerable<ScheduleRow> rows,
            bool includeSemesterLabel)
        {
            return rows
                .GroupBy(row => new { row.SemesterNumber, row.TimeSlotID })
                .Select(group => new TimetableSlot(
                    group.Key.SemesterNumber,
                    group.Key.TimeSlotID,
                    group.Min(row => row.StartTime),
                    includeSemesterLabel
                        ? $"الفصل {group.Key.SemesterNumber}{Environment.NewLine}{group.First().TimeSlotName}"
                        : group.First().TimeSlotName))
                .OrderBy(slot => slot.SemesterNumber)
                .ThenBy(slot => slot.StartTime)
                .ThenBy(slot => slot.Label)
                .ToList();
        }

        private static string BuildTimetableTypeLabel(ScheduleRow row)
        {
            return string.Equals(row.GroupName, "All", StringComparison.OrdinalIgnoreCase)
                ? ArabicUi.Display(row.LectureType)
                : $"{ArabicUi.Display(row.LectureType)} {row.GroupName}";
        }

        private void SelectScheduleRowById(int scheduleId)
        {
            foreach (DataGridViewRow gridRow in dgvSchedules.Rows)
            {
                if (gridRow.DataBoundItem is not ScheduleRow row || row.ScheduleID != scheduleId)
                {
                    continue;
                }

                dgvSchedules.ClearSelection();
                gridRow.Selected = true;
                dgvSchedules.CurrentCell = gridRow.Cells.Cast<DataGridViewCell>().First(cell => cell.Visible);
                break;
            }
        }

        private sealed record TimetableSlot(int SemesterNumber, int TimeSlotId, TimeSpan StartTime, string Label);
    }
}
