using System.Drawing;
using System.Windows.Forms;

#nullable disable

namespace University_Timetable_and_Classroom_Management_System
{
    public partial class MainShellForm
    {
        private System.ComponentModel.IContainer components = null;

        protected override void Dispose(bool disposing)
        {
            if (disposing && components != null) components.Dispose();
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        private void InitializeComponent()
        {
            Guna.UI2.WinForms.Suite.CustomizableEdges customizableEdges19 = new Guna.UI2.WinForms.Suite.CustomizableEdges();
            Guna.UI2.WinForms.Suite.CustomizableEdges customizableEdges20 = new Guna.UI2.WinForms.Suite.CustomizableEdges();
            Guna.UI2.WinForms.Suite.CustomizableEdges customizableEdges1 = new Guna.UI2.WinForms.Suite.CustomizableEdges();
            Guna.UI2.WinForms.Suite.CustomizableEdges customizableEdges2 = new Guna.UI2.WinForms.Suite.CustomizableEdges();
            Guna.UI2.WinForms.Suite.CustomizableEdges customizableEdges3 = new Guna.UI2.WinForms.Suite.CustomizableEdges();
            Guna.UI2.WinForms.Suite.CustomizableEdges customizableEdges4 = new Guna.UI2.WinForms.Suite.CustomizableEdges();
            Guna.UI2.WinForms.Suite.CustomizableEdges customizableEdges5 = new Guna.UI2.WinForms.Suite.CustomizableEdges();
            Guna.UI2.WinForms.Suite.CustomizableEdges customizableEdges6 = new Guna.UI2.WinForms.Suite.CustomizableEdges();
            Guna.UI2.WinForms.Suite.CustomizableEdges customizableEdges7 = new Guna.UI2.WinForms.Suite.CustomizableEdges();
            Guna.UI2.WinForms.Suite.CustomizableEdges customizableEdges8 = new Guna.UI2.WinForms.Suite.CustomizableEdges();
            Guna.UI2.WinForms.Suite.CustomizableEdges customizableEdges9 = new Guna.UI2.WinForms.Suite.CustomizableEdges();
            Guna.UI2.WinForms.Suite.CustomizableEdges customizableEdges10 = new Guna.UI2.WinForms.Suite.CustomizableEdges();
            Guna.UI2.WinForms.Suite.CustomizableEdges customizableEdges11 = new Guna.UI2.WinForms.Suite.CustomizableEdges();
            Guna.UI2.WinForms.Suite.CustomizableEdges customizableEdges12 = new Guna.UI2.WinForms.Suite.CustomizableEdges();
            Guna.UI2.WinForms.Suite.CustomizableEdges customizableEdges13 = new Guna.UI2.WinForms.Suite.CustomizableEdges();
            Guna.UI2.WinForms.Suite.CustomizableEdges customizableEdges14 = new Guna.UI2.WinForms.Suite.CustomizableEdges();
            Guna.UI2.WinForms.Suite.CustomizableEdges customizableEdges15 = new Guna.UI2.WinForms.Suite.CustomizableEdges();
            Guna.UI2.WinForms.Suite.CustomizableEdges customizableEdges16 = new Guna.UI2.WinForms.Suite.CustomizableEdges();
            Guna.UI2.WinForms.Suite.CustomizableEdges customizableEdges17 = new Guna.UI2.WinForms.Suite.CustomizableEdges();
            Guna.UI2.WinForms.Suite.CustomizableEdges customizableEdges18 = new Guna.UI2.WinForms.Suite.CustomizableEdges();
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(MainShellForm));
            Guna.UI2.WinForms.Suite.CustomizableEdges customizableEdges21 = new Guna.UI2.WinForms.Suite.CustomizableEdges();
            Guna.UI2.WinForms.Suite.CustomizableEdges customizableEdges22 = new Guna.UI2.WinForms.Suite.CustomizableEdges();
            pnlSidebar = new Guna.UI2.WinForms.Guna2Panel();
            lblSidebarFooter = new DesignerHtmlLabel();
            btnNavigationBranches = new Guna.UI2.WinForms.Guna2Button();
            btnNavigationStudyYears = new Guna.UI2.WinForms.Guna2Button();
            btnNavigationSections = new Guna.UI2.WinForms.Guna2Button();
            btnNavigationSubjects = new Guna.UI2.WinForms.Guna2Button();
            btnNavigationClassrooms = new Guna.UI2.WinForms.Guna2Button();
            btnNavigationTimeSlots = new Guna.UI2.WinForms.Guna2Button();
            btnNavigationFacultyAssignments = new Guna.UI2.WinForms.Guna2Button();
            btnNavigationFacultyMembers = new Guna.UI2.WinForms.Guna2Button();
            btnNavigationSchedules = new Guna.UI2.WinForms.Guna2Button();
            btnExcelImport = new Guna.UI2.WinForms.Guna2Button();
            btnFacultyScheduleEditor = new Guna.UI2.WinForms.Guna2Button();
            btnTimetableAlternatives = new Guna.UI2.WinForms.Guna2Button();
            separatorSidebar = new Guna.UI2.WinForms.Guna2Separator();
            lblSidebarSubtitle = new DesignerHtmlLabel();
            lblApplicationName = new DesignerHtmlLabel();
            picSidebarLogo = new PictureBox();
            pnlPageHost = new Guna.UI2.WinForms.Guna2Panel();
            layoutNavigation = new TableLayoutPanel();
            layoutNavigation.SuspendLayout();
            layoutSidebarBrand = new TableLayoutPanel();
            layoutSidebarBrand.SuspendLayout();
            pnlSidebar.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)picSidebarLogo).BeginInit();
            SuspendLayout();
            //
            // pnlSidebar
            //
            pnlSidebar.AutoScroll = true;
            pnlSidebar.AutoScrollMinSize = new Size(0, 670);
            pnlSidebar.BackColor = Color.Transparent;
            pnlSidebar.CustomizableEdges = customizableEdges19;
            pnlSidebar.Dock = DockStyle.Left;
            pnlSidebar.FillColor = Color.FromArgb(24, 38, 62);
            pnlSidebar.Location = new Point(0, 0);
            pnlSidebar.Margin = new Padding(3, 4, 3, 4);
            pnlSidebar.Name = "pnlSidebar";
            pnlSidebar.ShadowDecoration.CustomizableEdges = customizableEdges20;
            pnlSidebar.Size = new Size(274, 1061);
            pnlSidebar.TabIndex = 0;
            //
            // lblSidebarFooter
            //
            lblSidebarFooter.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
            lblSidebarFooter.BackColor = Color.Transparent;
            lblSidebarFooter.Font = new Font("Segoe UI", 8.5F);
            lblSidebarFooter.ForeColor = Color.FromArgb(148, 163, 184);
            lblSidebarFooter.Location = new Point(27, 995);
            lblSidebarFooter.Margin = new Padding(3, 4, 3, 4);
            lblSidebarFooter.Name = "lblSidebarFooter";
            lblSidebarFooter.Size = new Size(88, 15);
            lblSidebarFooter.TabIndex = 3;
            lblSidebarFooter.Text = "النظام الأكاديمي";
            lblSidebarFooter.Visible = false;
            //
            // btnNavigationBranches
            //
            btnNavigationBranches.BorderRadius = 6;
            btnNavigationBranches.CheckedState.FillColor = Color.FromArgb(37, 99, 235);
            btnNavigationBranches.CheckedState.ForeColor = Color.White;
            btnNavigationBranches.Cursor = Cursors.Hand;
            btnNavigationBranches.CustomizableEdges = customizableEdges1;
            btnNavigationBranches.DisabledState.BorderColor = Color.FromArgb(37, 99, 235);
            btnNavigationBranches.DisabledState.FillColor = Color.FromArgb(37, 99, 235);
            btnNavigationBranches.DisabledState.ForeColor = Color.White;
            btnNavigationBranches.FillColor = Color.Transparent;
            btnNavigationBranches.Font = new Font("Segoe UI Semibold", 9.5F, FontStyle.Bold);
            btnNavigationBranches.ForeColor = Color.FromArgb(226, 232, 240);
            btnNavigationBranches.HoverState.FillColor = Color.FromArgb(36, 55, 86);
            btnNavigationBranches.Location = new Point(27, 147);
            btnNavigationBranches.Margin = new Padding(0, 0, 0, 16);
            btnNavigationBranches.Name = "btnNavigationBranches";
            btnNavigationBranches.ShadowDecoration.CustomizableEdges = customizableEdges2;
            btnNavigationBranches.Size = new Size(219, 53);
            btnNavigationBranches.TabIndex = 10;
            btnNavigationBranches.Text = "الفروع";
            btnNavigationBranches.TextAlign = HorizontalAlignment.Left;
            btnNavigationBranches.TextOffset = new Point(14, 0);
            //
            // btnNavigationStudyYears
            //
            btnNavigationStudyYears.BorderRadius = 6;
            btnNavigationStudyYears.CheckedState.FillColor = Color.FromArgb(37, 99, 235);
            btnNavigationStudyYears.CheckedState.ForeColor = Color.White;
            btnNavigationStudyYears.Cursor = Cursors.Hand;
            btnNavigationStudyYears.CustomizableEdges = customizableEdges3;
            btnNavigationStudyYears.DisabledState.BorderColor = Color.FromArgb(37, 99, 235);
            btnNavigationStudyYears.DisabledState.FillColor = Color.FromArgb(37, 99, 235);
            btnNavigationStudyYears.DisabledState.ForeColor = Color.White;
            btnNavigationStudyYears.FillColor = Color.Transparent;
            btnNavigationStudyYears.Font = new Font("Segoe UI Semibold", 9.5F, FontStyle.Bold);
            btnNavigationStudyYears.ForeColor = Color.FromArgb(226, 232, 240);
            btnNavigationStudyYears.HoverState.FillColor = Color.FromArgb(36, 55, 86);
            btnNavigationStudyYears.Location = new Point(27, 221);
            btnNavigationStudyYears.Margin = new Padding(0, 0, 0, 16);
            btnNavigationStudyYears.Name = "btnNavigationStudyYears";
            btnNavigationStudyYears.ShadowDecoration.CustomizableEdges = customizableEdges4;
            btnNavigationStudyYears.Size = new Size(219, 53);
            btnNavigationStudyYears.TabIndex = 10;
            btnNavigationStudyYears.Text = "المراحل الدراسية";
            btnNavigationStudyYears.TextAlign = HorizontalAlignment.Left;
            btnNavigationStudyYears.TextOffset = new Point(14, 0);
            //
            // btnNavigationSections
            //
            btnNavigationSections.BorderRadius = 6;
            btnNavigationSections.CheckedState.FillColor = Color.FromArgb(37, 99, 235);
            btnNavigationSections.CheckedState.ForeColor = Color.White;
            btnNavigationSections.Cursor = Cursors.Hand;
            btnNavigationSections.CustomizableEdges = customizableEdges5;
            btnNavigationSections.DisabledState.BorderColor = Color.FromArgb(37, 99, 235);
            btnNavigationSections.DisabledState.FillColor = Color.FromArgb(37, 99, 235);
            btnNavigationSections.DisabledState.ForeColor = Color.White;
            btnNavigationSections.FillColor = Color.Transparent;
            btnNavigationSections.Font = new Font("Segoe UI Semibold", 9.5F, FontStyle.Bold);
            btnNavigationSections.ForeColor = Color.FromArgb(226, 232, 240);
            btnNavigationSections.HoverState.FillColor = Color.FromArgb(36, 55, 86);
            btnNavigationSections.Location = new Point(27, 296);
            btnNavigationSections.Margin = new Padding(0, 0, 0, 16);
            btnNavigationSections.Name = "btnNavigationSections";
            btnNavigationSections.ShadowDecoration.CustomizableEdges = customizableEdges6;
            btnNavigationSections.Size = new Size(219, 53);
            btnNavigationSections.TabIndex = 10;
            btnNavigationSections.Text = "الشعب";
            btnNavigationSections.TextAlign = HorizontalAlignment.Left;
            btnNavigationSections.TextOffset = new Point(14, 0);
            //
            // btnNavigationSubjects
            //
            btnNavigationSubjects.BorderRadius = 6;
            btnNavigationSubjects.CheckedState.FillColor = Color.FromArgb(37, 99, 235);
            btnNavigationSubjects.CheckedState.ForeColor = Color.White;
            btnNavigationSubjects.Cursor = Cursors.Hand;
            btnNavigationSubjects.CustomizableEdges = customizableEdges7;
            btnNavigationSubjects.DisabledState.BorderColor = Color.FromArgb(37, 99, 235);
            btnNavigationSubjects.DisabledState.FillColor = Color.FromArgb(37, 99, 235);
            btnNavigationSubjects.DisabledState.ForeColor = Color.White;
            btnNavigationSubjects.FillColor = Color.Transparent;
            btnNavigationSubjects.Font = new Font("Segoe UI Semibold", 9.5F, FontStyle.Bold);
            btnNavigationSubjects.ForeColor = Color.FromArgb(226, 232, 240);
            btnNavigationSubjects.HoverState.FillColor = Color.FromArgb(36, 55, 86);
            btnNavigationSubjects.Location = new Point(27, 371);
            btnNavigationSubjects.Margin = new Padding(0, 0, 0, 16);
            btnNavigationSubjects.Name = "btnNavigationSubjects";
            btnNavigationSubjects.ShadowDecoration.CustomizableEdges = customizableEdges8;
            btnNavigationSubjects.Size = new Size(219, 53);
            btnNavigationSubjects.TabIndex = 10;
            btnNavigationSubjects.Text = "المواد";
            btnNavigationSubjects.TextAlign = HorizontalAlignment.Left;
            btnNavigationSubjects.TextOffset = new Point(14, 0);
            //
            // btnNavigationClassrooms
            //
            btnNavigationClassrooms.BorderRadius = 6;
            btnNavigationClassrooms.CheckedState.FillColor = Color.FromArgb(37, 99, 235);
            btnNavigationClassrooms.CheckedState.ForeColor = Color.White;
            btnNavigationClassrooms.Cursor = Cursors.Hand;
            btnNavigationClassrooms.CustomizableEdges = customizableEdges9;
            btnNavigationClassrooms.DisabledState.BorderColor = Color.FromArgb(37, 99, 235);
            btnNavigationClassrooms.DisabledState.FillColor = Color.FromArgb(37, 99, 235);
            btnNavigationClassrooms.DisabledState.ForeColor = Color.White;
            btnNavigationClassrooms.FillColor = Color.Transparent;
            btnNavigationClassrooms.Font = new Font("Segoe UI Semibold", 9.5F, FontStyle.Bold);
            btnNavigationClassrooms.ForeColor = Color.FromArgb(226, 232, 240);
            btnNavigationClassrooms.HoverState.FillColor = Color.FromArgb(36, 55, 86);
            btnNavigationClassrooms.Location = new Point(27, 445);
            btnNavigationClassrooms.Margin = new Padding(0, 0, 0, 16);
            btnNavigationClassrooms.Name = "btnNavigationClassrooms";
            btnNavigationClassrooms.ShadowDecoration.CustomizableEdges = customizableEdges10;
            btnNavigationClassrooms.Size = new Size(219, 53);
            btnNavigationClassrooms.TabIndex = 10;
            btnNavigationClassrooms.Text = "القاعات";
            btnNavigationClassrooms.TextAlign = HorizontalAlignment.Left;
            btnNavigationClassrooms.TextOffset = new Point(14, 0);
            //
            // btnNavigationTimeSlots
            //
            btnNavigationTimeSlots.BorderRadius = 6;
            btnNavigationTimeSlots.CheckedState.FillColor = Color.FromArgb(37, 99, 235);
            btnNavigationTimeSlots.CheckedState.ForeColor = Color.White;
            btnNavigationTimeSlots.Cursor = Cursors.Hand;
            btnNavigationTimeSlots.CustomizableEdges = customizableEdges11;
            btnNavigationTimeSlots.DisabledState.BorderColor = Color.FromArgb(37, 99, 235);
            btnNavigationTimeSlots.DisabledState.FillColor = Color.FromArgb(37, 99, 235);
            btnNavigationTimeSlots.DisabledState.ForeColor = Color.White;
            btnNavigationTimeSlots.FillColor = Color.Transparent;
            btnNavigationTimeSlots.Font = new Font("Segoe UI Semibold", 9.5F, FontStyle.Bold);
            btnNavigationTimeSlots.ForeColor = Color.FromArgb(226, 232, 240);
            btnNavigationTimeSlots.HoverState.FillColor = Color.FromArgb(36, 55, 86);
            btnNavigationTimeSlots.Location = new Point(27, 520);
            btnNavigationTimeSlots.Margin = new Padding(0, 0, 0, 16);
            btnNavigationTimeSlots.Name = "btnNavigationTimeSlots";
            btnNavigationTimeSlots.ShadowDecoration.CustomizableEdges = customizableEdges12;
            btnNavigationTimeSlots.Size = new Size(219, 53);
            btnNavigationTimeSlots.TabIndex = 10;
            btnNavigationTimeSlots.Text = "الفترات الدراسية";
            btnNavigationTimeSlots.TextAlign = HorizontalAlignment.Left;
            btnNavigationTimeSlots.TextOffset = new Point(14, 0);
            //
            // btnNavigationFacultyAssignments
            //
            btnNavigationFacultyAssignments.BorderRadius = 6;
            btnNavigationFacultyAssignments.CheckedState.FillColor = Color.FromArgb(37, 99, 235);
            btnNavigationFacultyAssignments.CheckedState.ForeColor = Color.White;
            btnNavigationFacultyAssignments.Cursor = Cursors.Hand;
            btnNavigationFacultyAssignments.CustomizableEdges = customizableEdges13;
            btnNavigationFacultyAssignments.DisabledState.BorderColor = Color.FromArgb(37, 99, 235);
            btnNavigationFacultyAssignments.DisabledState.FillColor = Color.FromArgb(37, 99, 235);
            btnNavigationFacultyAssignments.DisabledState.ForeColor = Color.White;
            btnNavigationFacultyAssignments.FillColor = Color.Transparent;
            btnNavigationFacultyAssignments.Font = new Font("Segoe UI Semibold", 9.5F, FontStyle.Bold);
            btnNavigationFacultyAssignments.ForeColor = Color.FromArgb(226, 232, 240);
            btnNavigationFacultyAssignments.HoverState.FillColor = Color.FromArgb(36, 55, 86);
            btnNavigationFacultyAssignments.Location = new Point(27, 595);
            btnNavigationFacultyAssignments.Margin = new Padding(0, 0, 0, 16);
            btnNavigationFacultyAssignments.Name = "btnNavigationFacultyAssignments";
            btnNavigationFacultyAssignments.ShadowDecoration.CustomizableEdges = customizableEdges14;
            btnNavigationFacultyAssignments.Size = new Size(219, 53);
            btnNavigationFacultyAssignments.TabIndex = 10;
            btnNavigationFacultyAssignments.Text = "التكليفات التدريسية";
            btnNavigationFacultyAssignments.TextAlign = HorizontalAlignment.Left;
            btnNavigationFacultyAssignments.TextOffset = new Point(14, 0);
            //
            // btnNavigationFacultyMembers
            //
            btnNavigationFacultyMembers.BorderRadius = 6;
            btnNavigationFacultyMembers.CheckedState.FillColor = Color.FromArgb(37, 99, 235);
            btnNavigationFacultyMembers.CheckedState.ForeColor = Color.White;
            btnNavigationFacultyMembers.Cursor = Cursors.Hand;
            btnNavigationFacultyMembers.CustomizableEdges = customizableEdges15;
            btnNavigationFacultyMembers.DisabledState.BorderColor = Color.FromArgb(37, 99, 235);
            btnNavigationFacultyMembers.DisabledState.FillColor = Color.FromArgb(37, 99, 235);
            btnNavigationFacultyMembers.DisabledState.ForeColor = Color.White;
            btnNavigationFacultyMembers.FillColor = Color.Transparent;
            btnNavigationFacultyMembers.Font = new Font("Segoe UI Semibold", 9.5F, FontStyle.Bold);
            btnNavigationFacultyMembers.ForeColor = Color.FromArgb(226, 232, 240);
            btnNavigationFacultyMembers.HoverState.FillColor = Color.FromArgb(36, 55, 86);
            btnNavigationFacultyMembers.Location = new Point(27, 669);
            btnNavigationFacultyMembers.Margin = new Padding(0, 0, 0, 16);
            btnNavigationFacultyMembers.Name = "btnNavigationFacultyMembers";
            btnNavigationFacultyMembers.ShadowDecoration.CustomizableEdges = customizableEdges16;
            btnNavigationFacultyMembers.Size = new Size(219, 53);
            btnNavigationFacultyMembers.TabIndex = 10;
            btnNavigationFacultyMembers.Text = "التدريسيون";
            btnNavigationFacultyMembers.TextAlign = HorizontalAlignment.Left;
            btnNavigationFacultyMembers.TextOffset = new Point(14, 0);
            //
            // btnNavigationSchedules
            //
            btnNavigationSchedules.BorderRadius = 6;
            btnNavigationSchedules.CheckedState.FillColor = Color.FromArgb(37, 99, 235);
            btnNavigationSchedules.CheckedState.ForeColor = Color.White;
            btnNavigationSchedules.Cursor = Cursors.Hand;
            btnNavigationSchedules.CustomizableEdges = customizableEdges17;
            btnNavigationSchedules.DisabledState.BorderColor = Color.FromArgb(37, 99, 235);
            btnNavigationSchedules.DisabledState.FillColor = Color.FromArgb(37, 99, 235);
            btnNavigationSchedules.DisabledState.ForeColor = Color.White;
            btnNavigationSchedules.FillColor = Color.Transparent;
            btnNavigationSchedules.Font = new Font("Segoe UI Semibold", 9.5F, FontStyle.Bold);
            btnNavigationSchedules.ForeColor = Color.FromArgb(226, 232, 240);
            btnNavigationSchedules.HoverState.FillColor = Color.FromArgb(36, 55, 86);
            btnNavigationSchedules.Location = new Point(27, 744);
            btnNavigationSchedules.Margin = new Padding(0, 0, 0, 16);
            btnNavigationSchedules.Name = "btnNavigationSchedules";
            btnNavigationSchedules.ShadowDecoration.CustomizableEdges = customizableEdges18;
            btnNavigationSchedules.Size = new Size(219, 53);
            btnNavigationSchedules.TabIndex = 10;
            btnNavigationSchedules.Text = "الجدول الدراسي";
            btnNavigationSchedules.TextAlign = HorizontalAlignment.Left;
            btnNavigationSchedules.TextOffset = new Point(14, 0);
            //
            // separatorSidebar
            //
            separatorSidebar.FillColor = Color.FromArgb(51, 65, 85);
            separatorSidebar.Location = new Point(27, 115);
            separatorSidebar.Margin = new Padding(0, 16, 0, 16);
            separatorSidebar.Name = "separatorSidebar";
            separatorSidebar.Size = new Size(219, 13);
            separatorSidebar.TabIndex = 2;
            //
            // lblSidebarSubtitle
            //
            lblSidebarSubtitle.BackColor = Color.Transparent;
            lblSidebarSubtitle.Font = new Font("Segoe UI", 9F);
            lblSidebarSubtitle.ForeColor = Color.FromArgb(148, 163, 184);
            lblSidebarSubtitle.Location = new Point(30, 69);
            lblSidebarSubtitle.Margin = new Padding(3, 4, 3, 4);
            lblSidebarSubtitle.Name = "lblSidebarSubtitle";
            lblSidebarSubtitle.Size = new Size(133, 17);
            lblSidebarSubtitle.TabIndex = 1;
            lblSidebarSubtitle.Text = "إدارة الجداول الجامعية";
            lblSidebarSubtitle.Visible = false;
            //
            // lblApplicationName
            //
            lblApplicationName.AutoSize = false;
            lblApplicationName.BackColor = Color.Transparent;
            lblApplicationName.Font = new Font("Segoe UI Semibold", 10.5F, FontStyle.Bold);
            lblApplicationName.ForeColor = Color.White;
            lblApplicationName.Location = new Point(89, 32);
            lblApplicationName.Margin = new Padding(3, 4, 3, 4);
            lblApplicationName.Name = "lblApplicationName";
            lblApplicationName.Size = new Size(151, 48);
            lblApplicationName.TabIndex = 0;
            lblApplicationName.Text = "نظام إدارة<br>الجداول الجامعية";
            //
            // picSidebarLogo
            //
            picSidebarLogo.BackColor = Color.Transparent;
            picSidebarLogo.Image = (Image)resources.GetObject("picSidebarLogo.Image");
            picSidebarLogo.Location = new Point(23, 24);
            picSidebarLogo.Margin = new Padding(3, 4, 3, 4);
            picSidebarLogo.Name = "picSidebarLogo";
            picSidebarLogo.Size = new Size(55, 64);
            picSidebarLogo.SizeMode = PictureBoxSizeMode.Zoom;
            picSidebarLogo.TabIndex = 4;
            picSidebarLogo.TabStop = false;
            //
            // pnlPageHost
            //
            pnlPageHost.BackColor = Color.FromArgb(245, 247, 251);
            pnlPageHost.AutoScroll = true;
            pnlPageHost.CustomizableEdges = customizableEdges21;
            pnlPageHost.Dock = DockStyle.Fill;
            pnlPageHost.Location = new Point(274, 0);
            pnlPageHost.Margin = new Padding(3, 4, 3, 4);
            pnlPageHost.Name = "pnlPageHost";
            pnlPageHost.ShadowDecoration.CustomizableEdges = customizableEdges22;
            pnlPageHost.Size = new Size(1189, 1061);
            pnlPageHost.TabIndex = 1;
            //
            // btnExcelImport
            //
            btnExcelImport.Name = "btnExcelImport";
            btnExcelImport.Text = "استيراد Excel / PDF";
            btnExcelImport.Dock = DockStyle.Fill;
            btnExcelImport.Size = new Size(219, 53);
            btnExcelImport.Margin = new Padding(0, 0, 0, 16);
            btnExcelImport.BorderRadius = 6;
            btnExcelImport.FillColor = Color.Transparent;
            btnExcelImport.ForeColor = Color.FromArgb(226, 232, 240);
            btnExcelImport.Font = new Font("Segoe UI Semibold", 9.5F, FontStyle.Bold);
            btnExcelImport.TextAlign = HorizontalAlignment.Left;
            btnExcelImport.TextOffset = new Point(-14, 0);
            btnExcelImport.Cursor = Cursors.Hand;
            //
            // btnFacultyScheduleEditor
            //
            btnFacultyScheduleEditor.Name = "btnFacultyScheduleEditor";
            btnFacultyScheduleEditor.Text = "تفضيلات التدريسيين";
            btnFacultyScheduleEditor.Dock = DockStyle.Fill;
            btnFacultyScheduleEditor.Size = new Size(219, 53);
            btnFacultyScheduleEditor.Margin = new Padding(0, 0, 0, 16);
            btnFacultyScheduleEditor.BorderRadius = 6;
            btnFacultyScheduleEditor.FillColor = Color.Transparent;
            btnFacultyScheduleEditor.ForeColor = Color.FromArgb(226, 232, 240);
            btnFacultyScheduleEditor.Font = new Font("Segoe UI Semibold", 9.5F, FontStyle.Bold);
            btnFacultyScheduleEditor.TextAlign = HorizontalAlignment.Left;
            btnFacultyScheduleEditor.TextOffset = new Point(-14, 0);
            btnFacultyScheduleEditor.Cursor = Cursors.Hand;
            //
            // btnTimetableAlternatives
            //
            btnTimetableAlternatives.Name = "btnTimetableAlternatives";
            btnTimetableAlternatives.Text = "جداول بديلة";
            btnTimetableAlternatives.Dock = DockStyle.Fill;
            btnTimetableAlternatives.Size = new Size(219, 53);
            btnTimetableAlternatives.Margin = new Padding(0, 0, 0, 16);
            btnTimetableAlternatives.BorderRadius = 6;
            btnTimetableAlternatives.FillColor = Color.Transparent;
            btnTimetableAlternatives.ForeColor = Color.FromArgb(226, 232, 240);
            btnTimetableAlternatives.Font = new Font("Segoe UI Semibold", 9.5F, FontStyle.Bold);
            btnTimetableAlternatives.TextAlign = HorizontalAlignment.Left;
            btnTimetableAlternatives.TextOffset = new Point(-14, 0);
            btnTimetableAlternatives.Cursor = Cursors.Hand;
            //
            // MainShellForm
            //
            AccessibleName = "النافذة الرئيسية لنظام إدارة الجداول الجامعية";
            AutoScaleDimensions = new SizeF(96F, 96F);
            AutoScaleMode = AutoScaleMode.Dpi;
            BackColor = Color.FromArgb(245, 247, 251);
            ClientSize = new Size(1463, 1061);
            Controls.Add(pnlPageHost);
            Controls.Add(pnlSidebar);
            Icon = (Icon)resources.GetObject("$this.Icon");
            Margin = new Padding(3, 4, 3, 4);
            MinimumSize = new Size(640, 320);
            Name = "MainShellForm";
            RightToLeft = RightToLeft.Yes;
            StartPosition = FormStartPosition.CenterScreen;
            Text = "نظام إدارة الجداول الجامعية";

            // Flexible layout containers
            layoutNavigation.BackColor = Color.FromArgb(24, 38, 62);
            layoutNavigation.Name = "layoutNavigation";
            layoutNavigation.Margin = new Padding(0);
            layoutNavigation.ColumnCount = 1;
            layoutNavigation.RowCount = 15;
            layoutNavigation.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            layoutNavigation.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layoutNavigation.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layoutNavigation.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layoutNavigation.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layoutNavigation.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layoutNavigation.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layoutNavigation.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layoutNavigation.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layoutNavigation.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layoutNavigation.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layoutNavigation.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layoutNavigation.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layoutNavigation.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layoutNavigation.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layoutNavigation.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layoutNavigation.AutoSize = true;
            layoutNavigation.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            layoutNavigation.Dock = DockStyle.Top;
            pnlSidebar.Controls.Add(layoutNavigation);
            pnlSidebar.Padding = new Padding(24, 24, 24, 24);
            layoutSidebarBrand.BackColor = Color.FromArgb(24, 38, 62);
            layoutSidebarBrand.Name = "layoutSidebarBrand";
            layoutSidebarBrand.Margin = new Padding(0);
            layoutSidebarBrand.Dock = DockStyle.Fill;
            layoutSidebarBrand.AutoSize = true;
            layoutSidebarBrand.ColumnCount = 2;
            layoutSidebarBrand.RowCount = 2;
            layoutSidebarBrand.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            layoutSidebarBrand.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            layoutSidebarBrand.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layoutSidebarBrand.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layoutNavigation.Controls.Add(layoutSidebarBrand, 0, 0);
            layoutSidebarBrand.Controls.Add(picSidebarLogo, 0, 0);
            layoutSidebarBrand.Controls.Add(lblApplicationName, 1, 0);
            layoutSidebarBrand.Controls.Add(lblSidebarSubtitle, 1, 1);
            lblApplicationName.Anchor = AnchorStyles.Left;
            layoutNavigation.Controls.Add(separatorSidebar, 0, 1);
            separatorSidebar.Dock = DockStyle.Fill;
            layoutNavigation.Controls.Add(btnNavigationBranches, 0, 2);
            btnNavigationBranches.Dock = DockStyle.Fill;
            layoutNavigation.Controls.Add(btnNavigationStudyYears, 0, 3);
            btnNavigationStudyYears.Dock = DockStyle.Fill;
            layoutNavigation.Controls.Add(btnNavigationSections, 0, 4);
            btnNavigationSections.Dock = DockStyle.Fill;
            layoutNavigation.Controls.Add(btnNavigationSubjects, 0, 5);
            btnNavigationSubjects.Dock = DockStyle.Fill;
            layoutNavigation.Controls.Add(btnNavigationClassrooms, 0, 6);
            btnNavigationClassrooms.Dock = DockStyle.Fill;
            layoutNavigation.Controls.Add(btnNavigationTimeSlots, 0, 7);
            btnNavigationTimeSlots.Dock = DockStyle.Fill;
            layoutNavigation.Controls.Add(btnNavigationFacultyAssignments, 0, 8);
            btnNavigationFacultyAssignments.Dock = DockStyle.Fill;
            layoutNavigation.Controls.Add(btnNavigationFacultyMembers, 0, 9);
            btnNavigationFacultyMembers.Dock = DockStyle.Fill;
            layoutNavigation.Controls.Add(btnNavigationSchedules, 0, 10);
            btnNavigationSchedules.Dock = DockStyle.Fill;
            layoutNavigation.Controls.Add(btnExcelImport, 0, 11);
            layoutNavigation.Controls.Add(btnFacultyScheduleEditor, 0, 12);
            layoutNavigation.Controls.Add(btnTimetableAlternatives, 0, 13);
            layoutNavigation.Controls.Add(lblSidebarFooter, 0, 14);
            layoutSidebarBrand.ResumeLayout(false);
            layoutSidebarBrand.PerformLayout();
            layoutNavigation.ResumeLayout(false);
            layoutNavigation.PerformLayout();
            pnlSidebar.ResumeLayout(false);
            pnlSidebar.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)picSidebarLogo).EndInit();
            ResumeLayout(false);
        }
        #endregion
        private TableLayoutPanel layoutNavigation;
        private TableLayoutPanel layoutSidebarBrand;


        private Guna.UI2.WinForms.Guna2Panel pnlSidebar;
        private Guna.UI2.WinForms.Guna2Panel pnlPageHost;
        private DesignerHtmlLabel lblSidebarFooter;
        private Guna.UI2.WinForms.Guna2Separator separatorSidebar;
        private DesignerHtmlLabel lblSidebarSubtitle;
        private DesignerHtmlLabel lblApplicationName;
        private PictureBox picSidebarLogo;
        private Guna.UI2.WinForms.Guna2Button btnNavigationBranches;
        private Guna.UI2.WinForms.Guna2Button btnNavigationStudyYears;
        private Guna.UI2.WinForms.Guna2Button btnNavigationSections;
        private Guna.UI2.WinForms.Guna2Button btnNavigationSubjects;
        private Guna.UI2.WinForms.Guna2Button btnNavigationClassrooms;
        private Guna.UI2.WinForms.Guna2Button btnNavigationTimeSlots;
        private Guna.UI2.WinForms.Guna2Button btnNavigationFacultyAssignments;
        private Guna.UI2.WinForms.Guna2Button btnNavigationFacultyMembers;
        private Guna.UI2.WinForms.Guna2Button btnNavigationSchedules;
        private Guna.UI2.WinForms.Guna2Button btnExcelImport;
        private Guna.UI2.WinForms.Guna2Button btnFacultyScheduleEditor;
        private Guna.UI2.WinForms.Guna2Button btnTimetableAlternatives;

    }
}
