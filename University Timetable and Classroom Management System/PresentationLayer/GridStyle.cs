using Guna.UI2.WinForms;

namespace University_Timetable_and_Classroom_Management_System
{
    internal static class GridStyle
    {
        private static readonly Color HeaderBackColor = Color.FromArgb(15, 23, 42);
        private static readonly Color TextColor = Color.FromArgb(15, 23, 42);
        private static readonly Color MutedRowColor = Color.FromArgb(241, 245, 249);
        private static readonly Color GridLineColor = Color.FromArgb(226, 232, 240);
        private static readonly Color SelectionBackColor = Color.FromArgb(37, 99, 235);

        public static void Apply(DataGridView grid)
        {
            grid.EnableHeadersVisualStyles = false;
            grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            grid.RowHeadersVisible = false;
            grid.MultiSelect = false;
            grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            grid.BackgroundColor = Color.White;
            grid.BorderStyle = BorderStyle.None;
            grid.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;
            grid.GridColor = GridLineColor;
            grid.RowTemplate.Height = 48;
            grid.ColumnHeadersHeight = 48;
            grid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;

            grid.ColumnHeadersDefaultCellStyle = new DataGridViewCellStyle
            {
                Alignment = DataGridViewContentAlignment.MiddleLeft,
                BackColor = HeaderBackColor,
                ForeColor = Color.White,
                Font = new Font("Segoe UI Semibold", 11F, FontStyle.Bold),
                SelectionBackColor = HeaderBackColor,
                SelectionForeColor = Color.White,
                WrapMode = DataGridViewTriState.True
            };

            grid.DefaultCellStyle = new DataGridViewCellStyle
            {
                Alignment = DataGridViewContentAlignment.MiddleLeft,
                BackColor = Color.White,
                ForeColor = TextColor,
                Font = new Font("Segoe UI", 11F),
                SelectionBackColor = SelectionBackColor,
                SelectionForeColor = Color.White,
                WrapMode = DataGridViewTriState.False
            };

            grid.AlternatingRowsDefaultCellStyle = new DataGridViewCellStyle
            {
                BackColor = MutedRowColor,
                ForeColor = TextColor,
                SelectionBackColor = SelectionBackColor,
                SelectionForeColor = Color.White
            };

            if (grid is Guna2DataGridView gunaGrid)
            {
                gunaGrid.ThemeStyle.GridColor = GridLineColor;
                gunaGrid.ThemeStyle.HeaderStyle.BackColor = HeaderBackColor;
                gunaGrid.ThemeStyle.HeaderStyle.ForeColor = Color.White;
                gunaGrid.ThemeStyle.HeaderStyle.Font = new Font("Segoe UI Semibold", 11F, FontStyle.Bold);
                gunaGrid.ThemeStyle.HeaderStyle.Height = 48;
                gunaGrid.ThemeStyle.RowsStyle.Font = new Font("Segoe UI", 11F);
                gunaGrid.ThemeStyle.RowsStyle.ForeColor = TextColor;
                gunaGrid.ThemeStyle.RowsStyle.Height = 48;
                gunaGrid.ThemeStyle.RowsStyle.SelectionBackColor = SelectionBackColor;
                gunaGrid.ThemeStyle.RowsStyle.SelectionForeColor = Color.White;
                gunaGrid.ThemeStyle.AlternatingRowsStyle.BackColor = MutedRowColor;
                gunaGrid.ThemeStyle.AlternatingRowsStyle.ForeColor = TextColor;
                gunaGrid.ThemeStyle.AlternatingRowsStyle.SelectionBackColor = SelectionBackColor;
                gunaGrid.ThemeStyle.AlternatingRowsStyle.SelectionForeColor = Color.White;
            }
        }
    }
}
