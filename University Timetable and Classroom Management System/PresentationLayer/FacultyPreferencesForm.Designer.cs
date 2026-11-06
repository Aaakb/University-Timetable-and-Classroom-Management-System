#nullable disable

namespace University_Timetable_and_Classroom_Management_System
{
    partial class FacultyPreferencesForm
    {
        private System.ComponentModel.IContainer components = null;

        protected override void Dispose(bool disposing)
        {
            if (disposing) components?.Dispose();
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        private void InitializeComponent()
        {
            DataGridViewCellStyle dataGridViewCellStyle1 = new DataGridViewCellStyle();
            DataGridViewCellStyle dataGridViewCellStyle2 = new DataGridViewCellStyle();
            DataGridViewCellStyle dataGridViewCellStyle3 = new DataGridViewCellStyle();
            DataGridViewCellStyle dataGridViewCellStyle4 = new DataGridViewCellStyle();
            DataGridViewCellStyle dataGridViewCellStyle5 = new DataGridViewCellStyle();
            DataGridViewCellStyle dataGridViewCellStyle6 = new DataGridViewCellStyle();
            dayCardSunday = new TableLayoutPanel();
            dayLabelSunday = new Label();
            dayPreferenceSunday = new ComboBox();
            dayCardMonday = new TableLayoutPanel();
            dayLabelMonday = new Label();
            dayPreferenceMonday = new ComboBox();
            dayCardTuesday = new TableLayoutPanel();
            dayLabelTuesday = new Label();
            dayPreferenceTuesday = new ComboBox();
            dayCardWednesday = new TableLayoutPanel();
            dayLabelWednesday = new Label();
            dayPreferenceWednesday = new ComboBox();
            dayCardThursday = new TableLayoutPanel();
            dayLabelThursday = new Label();
            dayPreferenceThursday = new ComboBox();
            root = new TableLayoutPanel();
            heading = new TableLayoutPanel();
            lblHeading = new Label();
            lblDescription = new Label();
            filters = new FlowLayoutPanel();
            flow = new FlowLayoutPanel();
            lblCaption = new Label();
            faculty = new ComboBox();
            flow2 = new FlowLayoutPanel();
            lblCaption2 = new Label();
            semester = new ComboBox();
            flow3 = new FlowLayoutPanel();
            lblCaption3 = new Label();
            seconds = new NumericUpDown();
            layout = new TableLayoutPanel();
            enabled = new CheckBox();
            dayCards = new TableLayoutPanel();
            lblCaption4 = new Label();
            profileStatus = new Label();
            tabs = new TabControl();
            tabCurrent = new TabPage();
            current = new DataGridView();
            tabChanges = new TabPage();
            changes = new DataGridView();
            status = new Label();
            footer = new TableLayoutPanel();
            save = new Button();
            reload = new Button();
            cancel = new Button();
            close = new Button();
            dayCardSunday.SuspendLayout();
            dayCardMonday.SuspendLayout();
            dayCardTuesday.SuspendLayout();
            dayCardWednesday.SuspendLayout();
            dayCardThursday.SuspendLayout();
            root.SuspendLayout();
            heading.SuspendLayout();
            filters.SuspendLayout();
            flow.SuspendLayout();
            flow2.SuspendLayout();
            flow3.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)seconds).BeginInit();
            layout.SuspendLayout();
            dayCards.SuspendLayout();
            tabs.SuspendLayout();
            tabCurrent.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)current).BeginInit();
            tabChanges.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)changes).BeginInit();
            footer.SuspendLayout();
            SuspendLayout();
            // 
            // dayCardSunday
            // 
            dayCardSunday.BackColor = DesktopPalette.Canvas;
            dayCardSunday.ColumnCount = 1;
            dayCardSunday.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            dayCardSunday.Controls.Add(dayLabelSunday, 0, 0);
            dayCardSunday.Controls.Add(dayPreferenceSunday, 0, 1);
            dayCardSunday.Dock = DockStyle.Fill;
            dayCardSunday.Location = new Point(893, 3);
            dayCardSunday.Name = "dayCardSunday";
            dayCardSunday.Padding = new Padding(5);
            dayCardSunday.RowCount = 2;
            dayCardSunday.RowStyles.Add(new RowStyle(SizeType.Absolute, 27F));
            dayCardSunday.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            dayCardSunday.Size = new Size(216, 60);
            dayCardSunday.TabIndex = 0;
            // 
            // dayLabelSunday
            // 
            dayLabelSunday.Dock = DockStyle.Fill;
            dayLabelSunday.Font = new Font("Segoe UI", 11F, FontStyle.Bold);
            dayLabelSunday.Location = new Point(8, 5);
            dayLabelSunday.Name = "dayLabelSunday";
            dayLabelSunday.Size = new Size(200, 27);
            dayLabelSunday.TabIndex = 0;
            dayLabelSunday.Text = "الأحد";
            dayLabelSunday.TextAlign = ContentAlignment.MiddleCenter;
            // 
            // dayPreferenceSunday
            // 
            dayPreferenceSunday.Dock = DockStyle.Top;
            dayPreferenceSunday.DropDownHeight = 240;
            dayPreferenceSunday.DropDownStyle = ComboBoxStyle.DropDownList;
            dayPreferenceSunday.IntegralHeight = false;
            dayPreferenceSunday.Items.AddRange(new object[] { "مفضّل", "مسموح", "غير متاح" });
            dayPreferenceSunday.Location = new Point(8, 35);
            dayPreferenceSunday.Name = "dayPreferenceSunday";
            dayPreferenceSunday.Size = new Size(200, 25);
            dayPreferenceSunday.TabIndex = 1;
            // 
            // dayCardMonday
            // 
            dayCardMonday.BackColor = DesktopPalette.Canvas;
            dayCardMonday.ColumnCount = 1;
            dayCardMonday.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            dayCardMonday.Controls.Add(dayLabelMonday, 0, 0);
            dayCardMonday.Controls.Add(dayPreferenceMonday, 0, 1);
            dayCardMonday.Dock = DockStyle.Fill;
            dayCardMonday.Location = new Point(671, 3);
            dayCardMonday.Name = "dayCardMonday";
            dayCardMonday.Padding = new Padding(5);
            dayCardMonday.RowCount = 2;
            dayCardMonday.RowStyles.Add(new RowStyle(SizeType.Absolute, 27F));
            dayCardMonday.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            dayCardMonday.Size = new Size(216, 60);
            dayCardMonday.TabIndex = 1;
            // 
            // dayLabelMonday
            // 
            dayLabelMonday.Dock = DockStyle.Fill;
            dayLabelMonday.Font = new Font("Segoe UI", 11F, FontStyle.Bold);
            dayLabelMonday.Location = new Point(8, 5);
            dayLabelMonday.Name = "dayLabelMonday";
            dayLabelMonday.Size = new Size(200, 27);
            dayLabelMonday.TabIndex = 0;
            dayLabelMonday.Text = "الاثنين";
            dayLabelMonday.TextAlign = ContentAlignment.MiddleCenter;
            // 
            // dayPreferenceMonday
            // 
            dayPreferenceMonday.Dock = DockStyle.Top;
            dayPreferenceMonday.DropDownHeight = 240;
            dayPreferenceMonday.DropDownStyle = ComboBoxStyle.DropDownList;
            dayPreferenceMonday.IntegralHeight = false;
            dayPreferenceMonday.Items.AddRange(new object[] { "مفضّل", "مسموح", "غير متاح" });
            dayPreferenceMonday.Location = new Point(8, 35);
            dayPreferenceMonday.Name = "dayPreferenceMonday";
            dayPreferenceMonday.Size = new Size(200, 25);
            dayPreferenceMonday.TabIndex = 1;
            // 
            // dayCardTuesday
            // 
            dayCardTuesday.BackColor = DesktopPalette.Canvas;
            dayCardTuesday.ColumnCount = 1;
            dayCardTuesday.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            dayCardTuesday.Controls.Add(dayLabelTuesday, 0, 0);
            dayCardTuesday.Controls.Add(dayPreferenceTuesday, 0, 1);
            dayCardTuesday.Dock = DockStyle.Fill;
            dayCardTuesday.Location = new Point(449, 3);
            dayCardTuesday.Name = "dayCardTuesday";
            dayCardTuesday.Padding = new Padding(5);
            dayCardTuesday.RowCount = 2;
            dayCardTuesday.RowStyles.Add(new RowStyle(SizeType.Absolute, 27F));
            dayCardTuesday.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            dayCardTuesday.Size = new Size(216, 60);
            dayCardTuesday.TabIndex = 2;
            // 
            // dayLabelTuesday
            // 
            dayLabelTuesday.Dock = DockStyle.Fill;
            dayLabelTuesday.Font = new Font("Segoe UI", 11F, FontStyle.Bold);
            dayLabelTuesday.Location = new Point(8, 5);
            dayLabelTuesday.Name = "dayLabelTuesday";
            dayLabelTuesday.Size = new Size(200, 27);
            dayLabelTuesday.TabIndex = 0;
            dayLabelTuesday.Text = "الثلاثاء";
            dayLabelTuesday.TextAlign = ContentAlignment.MiddleCenter;
            // 
            // dayPreferenceTuesday
            // 
            dayPreferenceTuesday.Dock = DockStyle.Top;
            dayPreferenceTuesday.DropDownHeight = 240;
            dayPreferenceTuesday.DropDownStyle = ComboBoxStyle.DropDownList;
            dayPreferenceTuesday.IntegralHeight = false;
            dayPreferenceTuesday.Items.AddRange(new object[] { "مفضّل", "مسموح", "غير متاح" });
            dayPreferenceTuesday.Location = new Point(8, 35);
            dayPreferenceTuesday.Name = "dayPreferenceTuesday";
            dayPreferenceTuesday.Size = new Size(200, 25);
            dayPreferenceTuesday.TabIndex = 1;
            // 
            // dayCardWednesday
            // 
            dayCardWednesday.BackColor = DesktopPalette.Canvas;
            dayCardWednesday.ColumnCount = 1;
            dayCardWednesday.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            dayCardWednesday.Controls.Add(dayLabelWednesday, 0, 0);
            dayCardWednesday.Controls.Add(dayPreferenceWednesday, 0, 1);
            dayCardWednesday.Dock = DockStyle.Fill;
            dayCardWednesday.Location = new Point(227, 3);
            dayCardWednesday.Name = "dayCardWednesday";
            dayCardWednesday.Padding = new Padding(5);
            dayCardWednesday.RowCount = 2;
            dayCardWednesday.RowStyles.Add(new RowStyle(SizeType.Absolute, 27F));
            dayCardWednesday.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            dayCardWednesday.Size = new Size(216, 60);
            dayCardWednesday.TabIndex = 3;
            // 
            // dayLabelWednesday
            // 
            dayLabelWednesday.Dock = DockStyle.Fill;
            dayLabelWednesday.Font = new Font("Segoe UI", 11F, FontStyle.Bold);
            dayLabelWednesday.Location = new Point(8, 5);
            dayLabelWednesday.Name = "dayLabelWednesday";
            dayLabelWednesday.Size = new Size(200, 27);
            dayLabelWednesday.TabIndex = 0;
            dayLabelWednesday.Text = "الأربعاء";
            dayLabelWednesday.TextAlign = ContentAlignment.MiddleCenter;
            // 
            // dayPreferenceWednesday
            // 
            dayPreferenceWednesday.Dock = DockStyle.Top;
            dayPreferenceWednesday.DropDownHeight = 240;
            dayPreferenceWednesday.DropDownStyle = ComboBoxStyle.DropDownList;
            dayPreferenceWednesday.IntegralHeight = false;
            dayPreferenceWednesday.Items.AddRange(new object[] { "مفضّل", "مسموح", "غير متاح" });
            dayPreferenceWednesday.Location = new Point(8, 35);
            dayPreferenceWednesday.Name = "dayPreferenceWednesday";
            dayPreferenceWednesday.Size = new Size(200, 25);
            dayPreferenceWednesday.TabIndex = 1;
            // 
            // dayCardThursday
            // 
            dayCardThursday.BackColor = DesktopPalette.Canvas;
            dayCardThursday.ColumnCount = 1;
            dayCardThursday.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            dayCardThursday.Controls.Add(dayLabelThursday, 0, 0);
            dayCardThursday.Controls.Add(dayPreferenceThursday, 0, 1);
            dayCardThursday.Dock = DockStyle.Fill;
            dayCardThursday.Location = new Point(3, 3);
            dayCardThursday.Name = "dayCardThursday";
            dayCardThursday.Padding = new Padding(5);
            dayCardThursday.RowCount = 2;
            dayCardThursday.RowStyles.Add(new RowStyle(SizeType.Absolute, 27F));
            dayCardThursday.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            dayCardThursday.Size = new Size(218, 60);
            dayCardThursday.TabIndex = 4;
            // 
            // dayLabelThursday
            // 
            dayLabelThursday.Dock = DockStyle.Fill;
            dayLabelThursday.Font = new Font("Segoe UI", 11F, FontStyle.Bold);
            dayLabelThursday.Location = new Point(8, 5);
            dayLabelThursday.Name = "dayLabelThursday";
            dayLabelThursday.Size = new Size(202, 27);
            dayLabelThursday.TabIndex = 0;
            dayLabelThursday.Text = "الخميس";
            dayLabelThursday.TextAlign = ContentAlignment.MiddleCenter;
            // 
            // dayPreferenceThursday
            // 
            dayPreferenceThursday.Dock = DockStyle.Top;
            dayPreferenceThursday.DropDownHeight = 240;
            dayPreferenceThursday.DropDownStyle = ComboBoxStyle.DropDownList;
            dayPreferenceThursday.IntegralHeight = false;
            dayPreferenceThursday.Items.AddRange(new object[] { "مفضّل", "مسموح", "غير متاح" });
            dayPreferenceThursday.Location = new Point(8, 35);
            dayPreferenceThursday.Name = "dayPreferenceThursday";
            dayPreferenceThursday.Size = new Size(202, 25);
            dayPreferenceThursday.TabIndex = 1;
            // 
            // root
            // 
            root.ColumnCount = 1;
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            root.Controls.Add(heading, 0, 0);
            root.Controls.Add(filters, 0, 1);
            root.Controls.Add(layout, 0, 2);
            root.Controls.Add(tabs, 0, 3);
            root.Controls.Add(status, 0, 4);
            root.Controls.Add(footer, 0, 5);
            root.Dock = DockStyle.Fill;
            root.Location = new Point(0, 0);
            root.Name = "root";
            root.Padding = new Padding(16);
            root.RowCount = 6;
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 56F));
            root.RowStyles.Add(new RowStyle());
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 190F));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 48F));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 52F));
            root.Size = new Size(1164, 781);
            root.TabIndex = 0;
            // 
            // heading
            // 
            heading.ColumnCount = 1;
            heading.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 20F));
            heading.Controls.Add(lblHeading);
            heading.Controls.Add(lblDescription);
            heading.Dock = DockStyle.Fill;
            heading.Location = new Point(16, 16);
            heading.Margin = new Padding(0);
            heading.Name = "heading";
            heading.RowCount = 2;
            heading.RowStyles.Add(new RowStyle(SizeType.Absolute, 32F));
            heading.RowStyles.Add(new RowStyle(SizeType.Absolute, 20F));
            heading.Size = new Size(1132, 56);
            heading.TabIndex = 0;
            // 
            // lblHeading
            // 
            lblHeading.AutoSize = true;
            lblHeading.Font = new Font("Segoe UI", 19F, FontStyle.Bold);
            lblHeading.ForeColor = DesktopPalette.Ink;
            lblHeading.Location = new Point(848, 0);
            lblHeading.Name = "lblHeading";
            lblHeading.Size = new Size(281, 32);
            lblHeading.TabIndex = 0;
            lblHeading.Text = "تفضيلات أيام التدريسيين";
            // 
            // lblDescription
            // 
            lblDescription.AutoSize = true;
            lblDescription.ForeColor = DesktopPalette.Primary;
            lblDescription.Location = new Point(739, 32);
            lblDescription.Name = "lblDescription";
            lblDescription.Size = new Size(390, 19);
            lblDescription.TabIndex = 1;
            lblDescription.Text = "اختر الأيام، وسنُعيد توزيع الجدول الحالي وفق التفضيلات المتوفرة.";
            // 
            // filters
            // 
            filters.AutoSize = true;
            filters.Controls.Add(flow);
            filters.Controls.Add(flow2);
            filters.Controls.Add(flow3);
            filters.Dock = DockStyle.Fill;
            filters.Location = new Point(16, 72);
            filters.Margin = new Padding(0);
            filters.Name = "filters";
            filters.Size = new Size(1132, 63);
            filters.TabIndex = 1;
            // 
            // flow
            // 
            flow.AutoSize = true;
            flow.Controls.Add(lblCaption);
            flow.Controls.Add(faculty);
            flow.FlowDirection = FlowDirection.TopDown;
            flow.Location = new Point(820, 0);
            flow.Margin = new Padding(6, 0, 6, 5);
            flow.MinimumSize = new Size(0, 58);
            flow.Name = "flow";
            flow.Size = new Size(306, 58);
            flow.TabIndex = 0;
            flow.WrapContents = false;
            // 
            // lblCaption
            // 
            lblCaption.AutoSize = true;
            lblCaption.ForeColor = DesktopPalette.Primary;
            lblCaption.Location = new Point(241, 0);
            lblCaption.Margin = new Padding(3, 0, 3, 5);
            lblCaption.Name = "lblCaption";
            lblCaption.Size = new Size(62, 19);
            lblCaption.TabIndex = 0;
            lblCaption.Text = "التدريسي";
            // 
            // faculty
            // 
            faculty.AutoCompleteMode = AutoCompleteMode.SuggestAppend;
            faculty.AutoCompleteSource = AutoCompleteSource.ListItems;
            faculty.DisplayMember = "Text";
            faculty.DropDownHeight = 240;
            faculty.DropDownWidth = 300;
            faculty.IntegralHeight = false;
            faculty.Location = new Point(3, 27);
            faculty.Name = "faculty";
            faculty.Size = new Size(300, 25);
            faculty.TabIndex = 1;
            // 
            // flow2
            // 
            flow2.AutoSize = true;
            flow2.Controls.Add(lblCaption2);
            flow2.Controls.Add(semester);
            flow2.FlowDirection = FlowDirection.TopDown;
            flow2.Location = new Point(662, 0);
            flow2.Margin = new Padding(6, 0, 6, 5);
            flow2.MinimumSize = new Size(0, 58);
            flow2.Name = "flow2";
            flow2.Size = new Size(146, 58);
            flow2.TabIndex = 1;
            flow2.WrapContents = false;
            // 
            // lblCaption2
            // 
            lblCaption2.AutoSize = true;
            lblCaption2.ForeColor = DesktopPalette.Primary;
            lblCaption2.Location = new Point(46, 0);
            lblCaption2.Margin = new Padding(3, 0, 3, 5);
            lblCaption2.Name = "lblCaption2";
            lblCaption2.Size = new Size(97, 19);
            lblCaption2.TabIndex = 0;
            lblCaption2.Text = "الفصل الدراسي";
            // 
            // semester
            // 
            semester.DropDownHeight = 240;
            semester.DropDownStyle = ComboBoxStyle.DropDownList;
            semester.DropDownWidth = 140;
            semester.IntegralHeight = false;
            semester.Items.AddRange(new object[] { "الفصل الأول", "الفصل الثاني" });
            semester.Location = new Point(3, 27);
            semester.Name = "semester";
            semester.Size = new Size(140, 25);
            semester.TabIndex = 1;
            // 
            // flow3
            // 
            flow3.AutoSize = true;
            flow3.Controls.Add(lblCaption3);
            flow3.Controls.Add(seconds);
            flow3.FlowDirection = FlowDirection.TopDown;
            flow3.Location = new Point(517, 0);
            flow3.Margin = new Padding(6, 0, 6, 5);
            flow3.Name = "flow3";
            flow3.Size = new Size(133, 55);
            flow3.TabIndex = 2;
            flow3.WrapContents = false;
            // 
            // lblCaption3
            // 
            lblCaption3.AutoSize = true;
            lblCaption3.ForeColor = DesktopPalette.Primary;
            lblCaption3.Location = new Point(3, 0);
            lblCaption3.Margin = new Padding(3, 0, 3, 5);
            lblCaption3.Name = "lblCaption3";
            lblCaption3.Size = new Size(127, 19);
            lblCaption3.TabIndex = 0;
            lblCaption3.Text = "مهلة التحسين / ثانية";
            // 
            // seconds
            // 
            seconds.Location = new Point(58, 27);
            seconds.Maximum = new decimal(new int[] { 120, 0, 0, 0 });
            seconds.Minimum = new decimal(new int[] { 1, 0, 0, 0 });
            seconds.Name = "seconds";
            seconds.Size = new Size(72, 25);
            seconds.TabIndex = 1;
            seconds.Value = new decimal(new int[] { 20, 0, 0, 0 });
            // 
            // layout
            // 
            layout.BackColor = Color.White;
            layout.ColumnCount = 1;
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            layout.Controls.Add(enabled, 0, 0);
            layout.Controls.Add(dayCards, 0, 1);
            layout.Controls.Add(lblCaption4, 0, 2);
            layout.Controls.Add(profileStatus, 0, 3);
            layout.Dock = DockStyle.Fill;
            layout.Location = new Point(16, 143);
            layout.Margin = new Padding(0, 8, 0, 8);
            layout.Name = "layout";
            layout.Padding = new Padding(10);
            layout.RowCount = 4;
            layout.RowStyles.Add(new RowStyle());
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 80F));
            layout.RowStyles.Add(new RowStyle());
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            layout.Size = new Size(1132, 174);
            layout.TabIndex = 2;
            // 
            // enabled
            // 
            enabled.AutoSize = true;
            enabled.Location = new Point(892, 14);
            enabled.Margin = new Padding(4);
            enabled.Name = "enabled";
            enabled.Size = new Size(226, 23);
            enabled.TabIndex = 0;
            enabled.Text = "تحديد تفضيلات أيام لهذا التدريسي";
            enabled.UseVisualStyleBackColor = false;
            // 
            // dayCards
            // 
            dayCards.ColumnCount = 5;
            dayCards.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 20F));
            dayCards.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 20F));
            dayCards.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 20F));
            dayCards.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 20F));
            dayCards.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 20F));
            dayCards.Controls.Add(dayCardSunday, 0, 0);
            dayCards.Controls.Add(dayCardMonday, 1, 0);
            dayCards.Controls.Add(dayCardTuesday, 2, 0);
            dayCards.Controls.Add(dayCardWednesday, 3, 0);
            dayCards.Controls.Add(dayCardThursday, 4, 0);
            dayCards.Dock = DockStyle.Fill;
            dayCards.Enabled = false;
            dayCards.Location = new Point(10, 47);
            dayCards.Margin = new Padding(0, 6, 0, 8);
            dayCards.Name = "dayCards";
            dayCards.RowCount = 1;
            dayCards.RowStyles.Add(new RowStyle(SizeType.Absolute, 20F));
            dayCards.Size = new Size(1112, 66);
            dayCards.TabIndex = 1;
            // 
            // lblCaption4
            // 
            lblCaption4.AutoSize = true;
            lblCaption4.Font = new Font("Segoe UI", 9F);
            lblCaption4.ForeColor = DesktopPalette.Primary;
            lblCaption4.Location = new Point(633, 123);
            lblCaption4.Margin = new Padding(4, 2, 4, 4);
            lblCaption4.Name = "lblCaption4";
            lblCaption4.Size = new Size(485, 15);
            lblCaption4.TabIndex = 2;
            lblCaption4.Text = "مفضّل: أولوية في التحسين    •    مسموح: متاح عند الحاجة    •    غير متاح: يُمنع وضع المحاضرات فيه";
            // 
            // profileStatus
            // 
            profileStatus.AutoSize = true;
            profileStatus.Font = new Font("Segoe UI", 9F);
            profileStatus.ForeColor = DesktopPalette.Primary;
            profileStatus.Location = new Point(1118, 142);
            profileStatus.Margin = new Padding(4, 0, 4, 0);
            profileStatus.MaximumSize = new Size(1000, 0);
            profileStatus.Name = "profileStatus";
            profileStatus.Size = new Size(0, 15);
            profileStatus.TabIndex = 3;
            // 
            // tabs
            // 
            tabs.Controls.Add(tabCurrent);
            tabs.Controls.Add(tabChanges);
            tabs.Dock = DockStyle.Fill;
            tabs.Location = new Point(19, 328);
            tabs.Name = "tabs";
            tabs.Padding = new Point(18, 7);
            tabs.SelectedIndex = 0;
            tabs.Size = new Size(1126, 334);
            tabs.TabIndex = 3;
            // 
            // tabCurrent
            // 
            tabCurrent.BackColor = Color.White;
            tabCurrent.Controls.Add(current);
            tabCurrent.Location = new Point(4, 34);
            tabCurrent.Name = "tabCurrent";
            tabCurrent.Padding = new Padding(6);
            tabCurrent.Size = new Size(1118, 296);
            tabCurrent.TabIndex = 0;
            tabCurrent.Text = "الجدول الحالي للتدريسي";
            tabCurrent.Visible = false;
            // 
            // current
            // 
            current.AllowUserToAddRows = false;
            current.AllowUserToDeleteRows = false;
            dataGridViewCellStyle1.BackColor = DesktopPalette.Canvas;
            current.AlternatingRowsDefaultCellStyle = dataGridViewCellStyle1;
            current.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.DisplayedCells;
            current.BackgroundColor = Color.White;
            current.BorderStyle = BorderStyle.None;
            dataGridViewCellStyle2.BackColor = DesktopPalette.SoftAccent;
            dataGridViewCellStyle2.ForeColor = DesktopPalette.Header;
            dataGridViewCellStyle2.Padding = new Padding(6);
            current.ColumnHeadersDefaultCellStyle = dataGridViewCellStyle2;
            current.ColumnHeadersHeight = 42;
            dataGridViewCellStyle3.Alignment = DataGridViewContentAlignment.MiddleLeft;
            dataGridViewCellStyle3.BackColor = SystemColors.Window;
            dataGridViewCellStyle3.Font = new Font("Segoe UI", 10F);
            dataGridViewCellStyle3.ForeColor = SystemColors.ControlText;
            dataGridViewCellStyle3.Padding = new Padding(4);
            dataGridViewCellStyle3.SelectionBackColor = DesktopPalette.Selection;
            dataGridViewCellStyle3.SelectionForeColor = DesktopPalette.Ink;
            dataGridViewCellStyle3.WrapMode = DataGridViewTriState.False;
            current.DefaultCellStyle = dataGridViewCellStyle3;
            current.Dock = DockStyle.Fill;
            current.EnableHeadersVisualStyles = false;
            current.Location = new Point(6, 6);
            current.Name = "current";
            current.ReadOnly = true;
            current.RowHeadersVisible = false;
            current.RowTemplate.Height = 34;
            current.Size = new Size(1106, 284);
            current.TabIndex = 0;
            // 
            // tabChanges
            // 
            tabChanges.BackColor = Color.White;
            tabChanges.Controls.Add(changes);
            tabChanges.Location = new Point(4, 34);
            tabChanges.Name = "tabChanges";
            tabChanges.Padding = new Padding(6);
            tabChanges.Size = new Size(1118, 295);
            tabChanges.TabIndex = 1;
            tabChanges.Text = "التغييرات المحفوظة في الفصل";
            tabChanges.Visible = false;
            // 
            // changes
            // 
            changes.AllowUserToAddRows = false;
            changes.AllowUserToDeleteRows = false;
            dataGridViewCellStyle4.BackColor = DesktopPalette.Canvas;
            changes.AlternatingRowsDefaultCellStyle = dataGridViewCellStyle4;
            changes.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.DisplayedCells;
            changes.BackgroundColor = Color.White;
            changes.BorderStyle = BorderStyle.None;
            dataGridViewCellStyle5.BackColor = DesktopPalette.SoftAccent;
            dataGridViewCellStyle5.ForeColor = DesktopPalette.Header;
            dataGridViewCellStyle5.Padding = new Padding(6);
            changes.ColumnHeadersDefaultCellStyle = dataGridViewCellStyle5;
            changes.ColumnHeadersHeight = 42;
            dataGridViewCellStyle6.Alignment = DataGridViewContentAlignment.MiddleLeft;
            dataGridViewCellStyle6.BackColor = SystemColors.Window;
            dataGridViewCellStyle6.Font = new Font("Segoe UI", 10F);
            dataGridViewCellStyle6.ForeColor = SystemColors.ControlText;
            dataGridViewCellStyle6.Padding = new Padding(4);
            dataGridViewCellStyle6.SelectionBackColor = DesktopPalette.Selection;
            dataGridViewCellStyle6.SelectionForeColor = DesktopPalette.Ink;
            dataGridViewCellStyle6.WrapMode = DataGridViewTriState.False;
            changes.DefaultCellStyle = dataGridViewCellStyle6;
            changes.Dock = DockStyle.Fill;
            changes.EnableHeadersVisualStyles = false;
            changes.Location = new Point(6, 6);
            changes.Name = "changes";
            changes.ReadOnly = true;
            changes.RowHeadersVisible = false;
            changes.RowTemplate.Height = 34;
            changes.Size = new Size(1106, 283);
            changes.TabIndex = 0;
            // 
            // status
            // 
            status.BackColor = Color.FromArgb(231, 241, 238);
            status.Dock = DockStyle.Fill;
            status.ForeColor = Color.FromArgb(32, 92, 76);
            status.Location = new Point(19, 665);
            status.Name = "status";
            status.Padding = new Padding(12, 4, 12, 4);
            status.Size = new Size(1126, 48);
            status.TabIndex = 4;
            status.TextAlign = ContentAlignment.MiddleRight;
            // 
            // footer
            // 
            footer.ColumnCount = 4;
            footer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 43F));
            footer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 23F));
            footer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 19F));
            footer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 15F));
            footer.Controls.Add(save, 0, 0);
            footer.Controls.Add(reload, 1, 0);
            footer.Controls.Add(cancel, 2, 0);
            footer.Controls.Add(close, 3, 0);
            footer.Dock = DockStyle.Fill;
            footer.Location = new Point(16, 721);
            footer.Margin = new Padding(0, 8, 0, 0);
            footer.Name = "footer";
            footer.RowCount = 1;
            footer.RowStyles.Add(new RowStyle(SizeType.Absolute, 20F));
            footer.Size = new Size(1132, 44);
            footer.TabIndex = 5;
            // 
            // save
            // 
            save.BackColor = DesktopPalette.Primary;
            save.Dock = DockStyle.Fill;
            save.Enabled = false;
            save.FlatStyle = FlatStyle.Flat;
            save.ForeColor = Color.White;
            save.Location = new Point(650, 4);
            save.Margin = new Padding(4);
            save.Name = "save";
            save.Size = new Size(478, 36);
            save.TabIndex = 0;
            save.Text = "حفظ بدون تفضيلات";
            save.UseVisualStyleBackColor = false;
            // 
            // reload
            // 
            reload.BackColor = Color.White;
            reload.Dock = DockStyle.Fill;
            reload.Enabled = false;
            reload.FlatStyle = FlatStyle.Flat;
            reload.Location = new Point(390, 4);
            reload.Margin = new Padding(4);
            reload.Name = "reload";
            reload.Size = new Size(252, 36);
            reload.TabIndex = 1;
            reload.Text = "تحديث البيانات";
            reload.UseVisualStyleBackColor = false;
            // 
            // cancel
            // 
            cancel.BackColor = Color.White;
            cancel.Dock = DockStyle.Fill;
            cancel.Enabled = false;
            cancel.FlatStyle = FlatStyle.Flat;
            cancel.Location = new Point(175, 4);
            cancel.Margin = new Padding(4);
            cancel.Name = "cancel";
            cancel.Size = new Size(207, 36);
            cancel.TabIndex = 2;
            cancel.Text = "إلغاء البحث";
            cancel.UseVisualStyleBackColor = false;
            // 
            // close
            // 
            close.BackColor = Color.White;
            close.Dock = DockStyle.Fill;
            close.FlatStyle = FlatStyle.Flat;
            close.Location = new Point(4, 4);
            close.Margin = new Padding(4);
            close.Name = "close";
            close.Size = new Size(163, 36);
            close.TabIndex = 3;
            close.Text = "إغلاق";
            close.UseVisualStyleBackColor = false;
            // 
            // FacultyPreferencesForm
            // 
            AutoScaleDimensions = new SizeF(96F, 96F);
            AutoScaleMode = AutoScaleMode.Dpi;
            BackColor = DesktopPalette.Canvas;
            ClientSize = new Size(1164, 781);
            Controls.Add(root);
            Font = new Font("Segoe UI", 10F);
            MinimumSize = new Size(800, 680);
            Name = "FacultyPreferencesForm";
            RightToLeft = RightToLeft.Yes;
            RightToLeftLayout = true;
            StartPosition = FormStartPosition.CenterParent;
            Text = "تفضيلات أيام التدريسيين";
            dayCardSunday.ResumeLayout(false);
            dayCardMonday.ResumeLayout(false);
            dayCardTuesday.ResumeLayout(false);
            dayCardWednesday.ResumeLayout(false);
            dayCardThursday.ResumeLayout(false);
            root.ResumeLayout(false);
            root.PerformLayout();
            heading.ResumeLayout(false);
            heading.PerformLayout();
            filters.ResumeLayout(false);
            filters.PerformLayout();
            flow.ResumeLayout(false);
            flow.PerformLayout();
            flow2.ResumeLayout(false);
            flow2.PerformLayout();
            flow3.ResumeLayout(false);
            flow3.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)seconds).EndInit();
            layout.ResumeLayout(false);
            layout.PerformLayout();
            dayCards.ResumeLayout(false);
            tabs.ResumeLayout(false);
            tabCurrent.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)current).EndInit();
            tabChanges.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)changes).EndInit();
            footer.ResumeLayout(false);
            ResumeLayout(false);
        }

        #endregion

        private System.Windows.Forms.TableLayoutPanel root;
        private System.Windows.Forms.TableLayoutPanel heading;
        private System.Windows.Forms.Label lblHeading;
        private System.Windows.Forms.Label lblDescription;
        private System.Windows.Forms.FlowLayoutPanel filters;
        private System.Windows.Forms.FlowLayoutPanel flow;
        private System.Windows.Forms.Label lblCaption;
        private System.Windows.Forms.ComboBox faculty;
        private System.Windows.Forms.FlowLayoutPanel flow2;
        private System.Windows.Forms.Label lblCaption2;
        private System.Windows.Forms.ComboBox semester;
        private System.Windows.Forms.FlowLayoutPanel flow3;
        private System.Windows.Forms.Label lblCaption3;
        private System.Windows.Forms.NumericUpDown seconds;
        private System.Windows.Forms.TableLayoutPanel layout;
        private System.Windows.Forms.CheckBox enabled;
        private System.Windows.Forms.TableLayoutPanel dayCards;
        private System.Windows.Forms.Label lblCaption4;
        private System.Windows.Forms.Label profileStatus;
        private System.Windows.Forms.TabControl tabs;
        private System.Windows.Forms.TabPage tabCurrent;
        private System.Windows.Forms.DataGridView current;
        private System.Windows.Forms.TabPage tabChanges;
        private System.Windows.Forms.DataGridView changes;
        private System.Windows.Forms.Label status;
        private System.Windows.Forms.TableLayoutPanel footer;
        private System.Windows.Forms.Button save;
        private System.Windows.Forms.Button reload;
        private System.Windows.Forms.Button cancel;
        private System.Windows.Forms.Button close;
        private System.Windows.Forms.TableLayoutPanel dayCardSunday;
        private System.Windows.Forms.Label dayLabelSunday;
        private System.Windows.Forms.ComboBox dayPreferenceSunday;
        private System.Windows.Forms.TableLayoutPanel dayCardMonday;
        private System.Windows.Forms.Label dayLabelMonday;
        private System.Windows.Forms.ComboBox dayPreferenceMonday;
        private System.Windows.Forms.TableLayoutPanel dayCardTuesday;
        private System.Windows.Forms.Label dayLabelTuesday;
        private System.Windows.Forms.ComboBox dayPreferenceTuesday;
        private System.Windows.Forms.TableLayoutPanel dayCardWednesday;
        private System.Windows.Forms.Label dayLabelWednesday;
        private System.Windows.Forms.ComboBox dayPreferenceWednesday;
        private System.Windows.Forms.TableLayoutPanel dayCardThursday;
        private System.Windows.Forms.Label dayLabelThursday;
        private System.Windows.Forms.ComboBox dayPreferenceThursday;
    }
}
