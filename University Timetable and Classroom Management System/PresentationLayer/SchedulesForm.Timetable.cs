using System.Drawing;
using System.Windows.Forms;
using Guna.UI2.WinForms;

namespace University_Timetable_and_Classroom_Management_System
{
    public partial class SchedulesForm
    {
        private const int TimetableHeaderHeight = 42;
        private const int TimetableRowHeight = 164;
        private const int TimetableTimeColumnWidth = 142;
        private const int TimetableCardHeight = 68;
        private const int TimetableCardPadding = 8;

        private void ConfigureScheduleTimetableView()
        {
            btnScheduleGridView = CreateScheduleViewButton("Grid", true);
            btnScheduleTimetableView = CreateScheduleViewButton("Timetable", false);

            pnlTimetableHost = new Panel
            {
                Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right,
                AutoScroll = true,
                BackColor = Color.White,
                Location = dgvSchedules.Location,
                Size = dgvSchedules.Size,
                Visible = false
            };

            tblScheduleTimetable = new TableLayoutPanel
            {
                AutoSize = false,
                BackColor = Color.White,
                CellBorderStyle = TableLayoutPanelCellBorderStyle.Single,
                Location = new Point(0, 0),
                Margin = Padding.Empty,
                Padding = Padding.Empty
            };

            pnlTimetableHost.Controls.Add(tblScheduleTimetable);
            pnlSchedulesTable.Controls.Add(pnlTimetableHost);
            pnlSchedulesTable.Controls.Add(btnScheduleGridView);
            pnlSchedulesTable.Controls.Add(btnScheduleTimetableView);

            ConfigureScheduleTableHeader();
            ApplyScheduleViewButtonStyles();
        }

        private static Guna2Button CreateScheduleViewButton(string text, bool selected)
        {
            var button = new Guna2Button
            {
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                BorderColor = Color.FromArgb(203, 213, 225),
                BorderRadius = 8,
                BorderThickness = 1,
                Cursor = Cursors.Hand,
                FillColor = selected ? Color.FromArgb(15, 23, 42) : Color.White,
                Font = new Font("Segoe UI Semibold", 9F, FontStyle.Bold),
                ForeColor = selected ? Color.White : Color.FromArgb(51, 65, 85),
                HoverState = { FillColor = selected ? Color.FromArgb(30, 41, 59) : Color.FromArgb(248, 250, 252) },
                Size = new Size(88, 32),
                Text = text
            };

            return button;
        }

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
        }

        private void RenderScheduleTimetable(
            IReadOnlyList<ScheduleRow> rows,
            HashSet<int> conflictingScheduleIds)
        {
            if (tblScheduleTimetable is null)
            {
                return;
            }

            tblScheduleTimetable.SuspendLayout();
            tblScheduleTimetable.Controls.Clear();
            tblScheduleTimetable.ColumnStyles.Clear();
            tblScheduleTimetable.RowStyles.Clear();

            var days = GetTimetableDays(rows);
            bool showSemesterInTimeSlot = rows.Select(row => row.SemesterNumber).Distinct().Take(2).Count() > 1;
            var timeSlots = GetTimetableTimeSlots(rows, showSemesterInTimeSlot);
            tblScheduleTimetable.ColumnCount = days.Count + 1;
            tblScheduleTimetable.RowCount = timeSlots.Count + 1;
            tblScheduleTimetable.Width = Math.Max(pnlTimetableHost.ClientSize.Width - 2, 920);
            tblScheduleTimetable.Height = TimetableHeaderHeight + Math.Max(1, timeSlots.Count) * TimetableRowHeight;

            tblScheduleTimetable.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, TimetableTimeColumnWidth));
            float dayWidth = 100F / Math.Max(1, days.Count);

            for (int i = 0; i < days.Count; i++)
            {
                tblScheduleTimetable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, dayWidth));
            }

            tblScheduleTimetable.RowStyles.Add(new RowStyle(SizeType.Absolute, TimetableHeaderHeight));
            tblScheduleTimetable.Controls.Add(CreateTimetableHeaderCell("Time"), 0, 0);

            for (int dayIndex = 0; dayIndex < days.Count; dayIndex++)
            {
                tblScheduleTimetable.Controls.Add(CreateTimetableHeaderCell(days[dayIndex]), dayIndex + 1, 0);
            }

            if (timeSlots.Count == 0)
            {
                AddEmptyTimetableRow(days.Count);
                tblScheduleTimetable.ResumeLayout();
                return;
            }

            for (int slotIndex = 0; slotIndex < timeSlots.Count; slotIndex++)
            {
                var slot = timeSlots[slotIndex];
                int tableRow = slotIndex + 1;
                tblScheduleTimetable.RowStyles.Add(new RowStyle(SizeType.Absolute, TimetableRowHeight));
                tblScheduleTimetable.Controls.Add(CreateTimetableTimeCell(slot.Label), 0, tableRow);

                for (int dayIndex = 0; dayIndex < days.Count; dayIndex++)
                {
                    var entries = rows
                        .Where(row => row.SemesterNumber == slot.SemesterNumber &&
                            row.TimeSlotID == slot.TimeSlotId &&
                            string.Equals(row.DayOfWeek, days[dayIndex], StringComparison.OrdinalIgnoreCase))
                        .OrderBy(row => row.SectionName)
                        .ThenBy(row => row.GroupName)
                        .ThenBy(row => row.SubjectName)
                        .ToList();

                    tblScheduleTimetable.Controls.Add(CreateTimetableEntryCell(entries, conflictingScheduleIds), dayIndex + 1, tableRow);
                }
            }

            tblScheduleTimetable.ResumeLayout();
        }

        private void AddEmptyTimetableRow(int dayCount)
        {
            tblScheduleTimetable.RowStyles.Add(new RowStyle(SizeType.Absolute, TimetableRowHeight));
            tblScheduleTimetable.Controls.Add(CreateTimetableTimeCell("-"), 0, 1);

            for (int column = 1; column <= dayCount; column++)
            {
                tblScheduleTimetable.Controls.Add(CreateEmptyTimetableCell(), column, 1);
            }
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
                        ? $"Semester {group.Key.SemesterNumber}{Environment.NewLine}{group.First().TimeSlotName}"
                        : group.First().TimeSlotName))
                .OrderBy(slot => slot.SemesterNumber)
                .ThenBy(slot => slot.StartTime)
                .ThenBy(slot => slot.Label)
                .ToList();
        }

        private static Control CreateTimetableHeaderCell(string text)
        {
            return new Label
            {
                BackColor = Color.FromArgb(15, 23, 42),
                Dock = DockStyle.Fill,
                Font = new Font("Segoe UI Semibold", 10F, FontStyle.Bold),
                ForeColor = Color.White,
                Margin = Padding.Empty,
                Text = text,
                TextAlign = ContentAlignment.MiddleCenter
            };
        }

        private static Control CreateTimetableTimeCell(string text)
        {
            return new Label
            {
                BackColor = Color.FromArgb(248, 250, 252),
                Dock = DockStyle.Fill,
                Font = new Font("Segoe UI Semibold", 9.5F, FontStyle.Bold),
                ForeColor = Color.FromArgb(37, 99, 235),
                Margin = Padding.Empty,
                Padding = new Padding(8, 0, 8, 0),
                Text = text,
                TextAlign = ContentAlignment.MiddleCenter
            };
        }

        private Control CreateTimetableEntryCell(IReadOnlyList<ScheduleRow> entries, HashSet<int> conflicts)
        {
            var panel = new FlowLayoutPanel
            {
                AutoScroll = true,
                AutoScrollMargin = new Size(0, 8),
                BackColor = Color.White,
                BorderStyle = entries.Count > 1 ? BorderStyle.FixedSingle : BorderStyle.None,
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.TopDown,
                Margin = Padding.Empty,
                Padding = new Padding(6),
                WrapContents = false
            };

            panel.Resize += (_, _) => ResizeTimetableCards(panel);

            if (entries.Count == 0)
            {
                panel.Controls.Add(CreateEmptyTimetableLabel());
                return panel;
            }

            foreach (var entry in entries)
            {
                panel.Controls.Add(CreateTimetableCard(entry, conflicts.Contains(entry.ScheduleID)));
            }

            ResizeTimetableCards(panel);
            return panel;
        }

        private static void ResizeTimetableCards(FlowLayoutPanel panel)
        {
            int cardWidth = Math.Max(
                150,
                panel.ClientSize.Width -
                    panel.Padding.Horizontal -
                    SystemInformation.VerticalScrollBarWidth -
                    4);

            foreach (Guna2Panel card in panel.Controls.OfType<Guna2Panel>())
            {
                card.Width = cardWidth;

                foreach (Label label in card.Controls.OfType<Label>())
                {
                    label.Width = Math.Max(80, card.ClientSize.Width - TimetableCardPadding * 2);
                }
            }
        }

        private static Control CreateEmptyTimetableCell()
        {
            var panel = new Panel
            {
                BackColor = Color.White,
                Dock = DockStyle.Fill,
                Margin = Padding.Empty
            };

            panel.Controls.Add(CreateEmptyTimetableLabel());
            return panel;
        }

        private static Label CreateEmptyTimetableLabel()
        {
            return new Label
            {
                AutoSize = false,
                Dock = DockStyle.Top,
                Font = new Font("Segoe UI", 9F),
                ForeColor = Color.FromArgb(148, 163, 184),
                Height = 30,
                Text = "-",
                TextAlign = ContentAlignment.MiddleCenter
            };
        }

        private Guna2Panel CreateTimetableCard(ScheduleRow row, bool hasConflict)
        {
            bool isPractical = string.Equals(row.LectureType, "Practical", StringComparison.OrdinalIgnoreCase);
            var card = new Guna2Panel
            {
                BorderColor = hasConflict
                    ? Color.FromArgb(248, 113, 113)
                    : isPractical
                        ? Color.FromArgb(34, 197, 94)
                        : Color.FromArgb(96, 165, 250),
                BorderRadius = 6,
                BorderThickness = 1,
                Cursor = Cursors.Hand,
                FillColor = hasConflict
                    ? Color.FromArgb(254, 242, 242)
                    : isPractical
                        ? Color.FromArgb(240, 253, 244)
                        : Color.FromArgb(239, 246, 255),
                Height = TimetableCardHeight,
                Margin = new Padding(0, 0, 0, 6),
                Padding = new Padding(8),
                Tag = row.ScheduleID,
                Width = 220
            };

            var title = CreateTimetableCardLabel(row.SubjectName, TimetableCardPadding, 5, FontStyle.Bold, Color.FromArgb(15, 23, 42));
            var teacher = CreateTimetableCardLabel(row.FacultyMemberName, TimetableCardPadding, 24, FontStyle.Regular, Color.FromArgb(51, 65, 85));
            var details = CreateTimetableCardLabel(
                $"{row.ClassroomName} | {row.SectionName} | {BuildTimetableTypeLabel(row)}",
                TimetableCardPadding,
                43,
                FontStyle.Regular,
                hasConflict ? Color.FromArgb(185, 28, 28) : Color.FromArgb(71, 85, 105));

            card.Controls.Add(details);
            card.Controls.Add(teacher);
            card.Controls.Add(title);

            string toolTip = $"{row.SubjectName}{Environment.NewLine}{row.FacultyMemberName}{Environment.NewLine}{row.ClassroomName} | {row.SectionName} | {row.LectureType} | {row.GroupName}";
            scheduleToolTip.SetToolTip(card, toolTip);
            scheduleToolTip.SetToolTip(title, toolTip);
            scheduleToolTip.SetToolTip(teacher, toolTip);
            scheduleToolTip.SetToolTip(details, toolTip);

            AttachTimetableCardSelection(card, row.ScheduleID);
            return card;
        }

        private static Label CreateTimetableCardLabel(
            string text,
            int x,
            int y,
            FontStyle fontStyle,
            Color foreColor)
        {
            return new Label
            {
                AutoEllipsis = true,
                BackColor = Color.Transparent,
                Font = new Font("Segoe UI", 8.5F, fontStyle),
                ForeColor = foreColor,
                Location = new Point(x, y),
                Size = new Size(204, 17),
                Text = text,
                TextAlign = ContentAlignment.MiddleLeft
            };
        }

        private static string BuildTimetableTypeLabel(ScheduleRow row)
        {
            return string.Equals(row.GroupName, "All", StringComparison.OrdinalIgnoreCase)
                ? row.LectureType
                : $"{row.LectureType} {row.GroupName}";
        }

        private void AttachTimetableCardSelection(Control control, int scheduleId)
        {
            control.Click += (_, _) => SelectScheduleRowById(scheduleId);

            foreach (Control child in control.Controls)
            {
                AttachTimetableCardSelection(child, scheduleId);
            }
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
