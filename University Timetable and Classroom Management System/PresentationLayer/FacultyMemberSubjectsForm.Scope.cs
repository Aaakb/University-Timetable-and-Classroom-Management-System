using Guna.UI2.WinForms;
using University_Timetable_and_Classroom_Management_System.BusinessLayer;
using University_Timetable_and_Classroom_Management_System.Models;

namespace University_Timetable_and_Classroom_Management_System;

public partial class FacultyMemberSubjectsForm
{
    private readonly Guna2ComboBox cmbAssignmentStudyYear = new() { Name = "cmbAssignmentStudyYear", DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly Guna2ComboBox cmbAssignmentSemester = new() { Name = "cmbAssignmentSemester", DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly Guna2ComboBox cmbAssignmentBranch = new() { Name = "cmbAssignmentBranch", DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly Guna2ComboBox cmbAssignmentSection = new() { Name = "cmbAssignmentSection", DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly Guna2ComboBox cmbAssignmentLectureType = new() { Name = "cmbAssignmentLectureType", DropDownStyle = ComboBoxStyle.DropDownList };
    private List<Subject> assignmentSubjects = [];
    private List<Section> assignmentSections = [];
    private List<Branch> assignmentBranches = [];
    private bool bindingAssignmentScope;
    private List<AssignmentRow> assignmentRows = [];

    private void InitializeAssignmentScopeEditor()
    {
        using var layout = new LayoutUpdateScope(this);
        btnClearAssignmentForm.Visible = true;
        AddField("المرحلة", cmbAssignmentStudyYear, 0);
        AddField("التخصص", cmbAssignmentBranch, 1);
        AddField("الشعبة", cmbAssignmentSection, 4);
        AddField("نوع التكليف", cmbAssignmentLectureType, 5);
        AddField("الفصل الدراسي", cmbAssignmentSemester, 0);
        BindCombo(cmbAssignmentSemester, new[] { new ComboOption(null, "جميع الفصول"), new ComboOption(1, "الأول"), new ComboOption(2, "الثاني") });
        cmbAssignmentSemester.SelectedIndex = 0;
        cmbAssignmentLectureType.Items.AddRange(new object[] { "كلاهما", "نظري", "عملي" });
        cmbAssignmentLectureType.SelectedIndex = 0;
        lblPageSubtitle.Text = "حدد الفصل الدراسي والتدريسي والمادة والشعبة ونوع التكليف: نظري أو عملي أو كلاهما.";
        lblEditorSubtitle.Text = "يلتزم التوليد بالشعبة ونوع التكليف المحددين لكل تدريسي.";
        lblTableSubtitle.Text = "التكليفات المحفوظة حسب الفصل الدراسي والمرحلة والتخصص والشعبة والنوع";

        void AddField(string caption, Guna2ComboBox combo, int index)
        {
            combo.Font = cmbFacultyMember.Font;
            combo.Size = new Size(264, 36);
            combo.Dock = DockStyle.Fill;
            combo.Margin = new Padding(0);
            var field = new TableLayoutPanel { Name = "layout" + combo.Name, ColumnCount = 1, RowCount = 2,
                AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, MinimumSize = new Size(264, 0),
                MaximumSize = new Size(264, 0), Margin = new Padding(0, 0, 20, 12), BackColor = Color.White };
            field.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            field.RowStyles.Add(new RowStyle(SizeType.AutoSize)); field.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            field.Controls.Add(new Label { Text = caption, AutoSize = true, Margin = new Padding(0, 0, 0, 6), ForeColor = Color.FromArgb(51, 65, 85) }, 0, 0);
            field.Controls.Add(combo, 0, 1);
            flowEditorFields.Controls.Add(field); flowEditorFields.Controls.SetChildIndex(field, index);
        }
    }

    private void ApplyAssignmentYearSelection()
    {
        if (bindingAssignmentScope || IsDisposed || Disposing) return;
        bindingAssignmentScope = true;
        BindAssignmentBranches(); BindAssignmentSubjects(); BindAssignmentSections();
        bindingAssignmentScope = false;
    }

    private void ApplyAssignmentSemesterSelection()
    {
        if (bindingAssignmentScope || IsDisposed || Disposing) return;
        ClearAssignmentForm();
        ApplyAssignmentRowsFilter();
    }

    private void ApplyAssignmentRowsFilter()
    {
        int? semester = ComboBoxHelper.GetSelectedOptionalId(cmbAssignmentSemester);
        bool wasBinding = bindingAssignmentScope;
        bindingAssignmentScope = true;
        try
        {
            dgvFacultyMemberSubjects.DataSource = assignmentRows
                .Where(row => !semester.HasValue || row.SemesterNumber == semester.Value)
                .OrderBy(row => row.SemesterNumber).ThenBy(row => row.SubjectName)
                .ThenBy(row => row.FacultyMemberName).ToList();
            dgvFacultyMemberSubjects.ClearSelection();
        }
        finally { bindingAssignmentScope = wasBinding; }
    }

    private void ApplyAssignmentBranchSelection()
    {
        if (bindingAssignmentScope || IsDisposed || Disposing) return;
        bindingAssignmentScope = true;
        BindAssignmentSubjects(); BindAssignmentSections();
        bindingAssignmentScope = false;
    }

    private void BindAssignmentBranches(int? selected = null)
    {
        int year = GetSelectedRequiredId(cmbAssignmentStudyYear);
        bool usesBranches = AcademicStructureRules.UsesBranches(year);
        cmbAssignmentBranch.Enabled = usesBranches;
        BindCombo(cmbAssignmentBranch, usesBranches
            ? assignmentBranches.OrderBy(b => b.BranchName).Select(b => new ComboOption(b.BranchID, b.BranchName))
            : year > 0 ? new[] { new ComboOption(null, "عام") } : []);
        if (selected.HasValue) SelectComboValue(cmbAssignmentBranch, selected.Value);
        else if (year > 0 && !usesBranches) cmbAssignmentBranch.SelectedIndex = 0;
    }

    private void BindAssignmentSubjects(int? selected = null)
    {
        int year = GetSelectedRequiredId(cmbAssignmentStudyYear);
        int? branch = ComboBoxHelper.GetSelectedOptionalId(cmbAssignmentBranch);
        int? semester = ComboBoxHelper.GetSelectedOptionalId(cmbAssignmentSemester);
        BindCombo(cmbSubject, assignmentSubjects.Where(s => s.StudyYearID == year && s.BranchID == branch)
            .Where(s => !semester.HasValue || s.SemesterNumber == semester.Value)
            .OrderBy(s => s.SubjectName).ThenBy(s => s.SemesterNumber)
            .Select(s => new ComboOption(s.SubjectID, $"{s.SubjectName} — الفصل {s.SemesterNumber}")));
        if (selected.HasValue) SelectComboValue(cmbSubject, selected.Value);
    }

    private void BindAssignmentSections(int? selected = null, bool preserveAll = false)
    {
        var subject = assignmentSubjects.FirstOrDefault(s => s.SubjectID == GetSelectedRequiredId(cmbSubject));
        var sections = subject is null ? new List<Section>() : assignmentSections
            .Where(s => FacultyAssignmentRules.Matches(subject, s)).OrderBy(s => s.SectionName).ToList();
        var options = new List<ComboOption>();
        if (sections.Count > 0) options.Add(new(null, "جميع الشعب المطابقة"));
        options.AddRange(sections.Select(s => new ComboOption(s.SectionID, s.SectionName)));
        BindCombo(cmbAssignmentSection, options);
        if (selected.HasValue) SelectComboValue(cmbAssignmentSection, selected.Value);
        else if (preserveAll && options.Count > 0) cmbAssignmentSection.SelectedIndex = 0;
        else if (sections.Count == 1) SelectComboValue(cmbAssignmentSection, sections[0].SectionID);
    }
}
