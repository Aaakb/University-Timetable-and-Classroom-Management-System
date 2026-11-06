using System.Collections;
using System.ComponentModel;
using System.Reflection;
using Guna.UI2.WinForms;
using University_Timetable_and_Classroom_Management_System;
using University_Timetable_and_Classroom_Management_System.Models;

internal static class Program
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private static int checks;
    private static readonly List<StudyYear> Years = [new(){StudyYearID=1,YearName="الأولى"},new(){StudyYearID=3,YearName="الثالثة"},new(){StudyYearID=4,YearName="الرابعة"}];
    private static readonly List<Branch> Branches = [new(){BranchID=1,BranchName="علوم الحاسوب"},new(){BranchID=2,BranchName="تكنولوجيا المعلومات"}];

    [STAThread]
    private static int Main()
    {
        ApplicationConfiguration.Initialize();
        var old=LicenseManager.CurrentContext;
        LicenseManager.CurrentContext=new DesignContext();
        try {CheckSubjects();CheckAssignments();CheckSharedSelection();Console.WriteLine($"PASS: {checks} lower-filter UI checks; no database access.");return 0;}
        catch(Exception e){Console.Error.WriteLine(e);return 1;}
        finally {LicenseManager.CurrentContext=old;}
    }

    private static void CheckSubjects()
    {
        using var page=new SubjectsForm();
        var semester=Find<Guna2ComboBox>(page,"cmbSemesterFilter");
        Check("Subject page has no top semester selector",page.Controls.Find("semesterSelector",true).Length==0);
        Invoke(page,"ConfigureSubjectsGrid");Invoke(page,"ConfigureSubjectsEvents");
        Set(page,"studyYearsLookup",Years);Set(page,"branchesLookup",Branches);
        var rows=Rows(page,"SubjectRow","subjectRows",
            new(){["SubjectID"]=101,["SubjectName"]="مادة الفصل الأول",["StudyYearID"]=1,["SemesterNumber"]=1},
            new(){["SubjectID"]=205,["SubjectName"]="مادة الحاسوب",["StudyYearID"]=3,["BranchID"]=1,["SemesterNumber"]=2},
            new(){["SubjectID"]=206,["SubjectName"]="مادة تكنولوجيا المعلومات",["StudyYearID"]=3,["BranchID"]=2,["SemesterNumber"]=2},
            new(){["SubjectID"]=309,["SubjectName"]="اسم مستورد",["StudyYearID"]=0,["SemesterNumber"]=1},
            new(){["SubjectID"]=410,["SubjectName"]="مادة جديدة",["StudyYearID"]=3,["SemesterNumber"]=0});
        var grid=Find<DataGridView>(page,"dgvSubjects");grid.BindingContext=new BindingContext();
        Invoke(page,"BindSubjectFilterLookups");Invoke(page,"ApplySubjectFilters");
        Check("Subject semester filter sits beside the stage and department filters",semester.Parent!.Parent==Find<Guna2ComboBox>(page,"cmbStudyYearFilter").Parent!.Parent &&
            semester.Parent.Parent==Find<Guna2ComboBox>(page,"cmbBranchFilter").Parent!.Parent);
        Check("Subjects default to the shared first semester",Ids(grid).SequenceEqual(new[]{101}));
        Select(semester,2);
        Check("Subject second-semester filter excludes other and unclassified rows",Ids(grid).SequenceEqual(new[]{205,206}));
        Check("New material defaults to the lower selected semester",Find<Guna2ComboBox>(page,"cmbSemester").Text=="2");
        Select(Find<Guna2ComboBox>(page,"cmbStudyYearFilter"),3);
        Select(Find<Guna2ComboBox>(page,"cmbBranchFilter"),1);
        Check("Subject semester, stage and department filters intersect",Ids(grid).SequenceEqual(new[]{205}));
        Select(semester,0);
        Check("Unclassified subjects remain reachable from the lower filter",Ids(grid).SequenceEqual(new[]{309}));
        Check("Unclassified filter clears an incompatible stage filter",Selected(Find<Guna2ComboBox>(page,"cmbStudyYearFilter")) is null);
        Check("Unclassified material can still be classified",Find<Guna2ComboBox>(page,"cmbSemester").Enabled);
        Select(semester,-1);
        Check("The recent-import filter shows pending rows independently of both semesters",Ids(grid).SequenceEqual(new[]{410}));
        Check("The subject page shows the Excel action and leaves the pending semester blank",
            Find<Guna2Button>(page,"btnImportSubjects").Parent==semester.Parent!.Parent &&
            Find<Guna2ComboBox>(page,"cmbSemester").SelectedIndex==-1);
        Select(semester,null);
        Check("All-semesters subject filter includes pending, unclassified and both semesters",Ids(grid).SequenceEqual(new[]{101,205,206,309,410}));
        Select(semester,2);
        Invoke(page,"SetSubjectActionsEnabled",false);
        Check("Subject filters are disabled while saving",!semester.Enabled&&!Find<Guna2ComboBox>(page,"cmbStudyYearFilter").Enabled&&!Find<Guna2ComboBox>(page,"cmbBranchFilter").Enabled);
        Invoke(page,"SetSubjectActionsEnabled",true);
        Check("Subject filter changes never rewrite IDs",rows.Cast<object>().Select(x=>(int)x.GetType().GetProperty("SubjectID")!.GetValue(x)!).SequenceEqual(new[]{101,205,206,309,410}));
        Capture(page,"subjects-lower-filters");
    }

    private static void CheckAssignments()
    {
        using var page=new FacultyMemberSubjectsForm();
        Check("Assignment page has no top semester selector",page.Controls.Find("semesterSelector",true).Length==0);
        Invoke(page,"ConfigureAssignmentsGrid");Invoke(page,"ConfigureAssignmentEvents");
        Set(page,"assignmentStudyYears",Years);Set(page,"assignmentBranches",Branches);
        var materials=new List<Subject>{new(){SubjectID=101,SubjectName="عام",StudyYearID=1,SemesterNumber=1},
            new(){SubjectID=205,SubjectName="مادة الحاسوب",StudyYearID=3,BranchID=1,SemesterNumber=2},
            new(){SubjectID=206,SubjectName="مادة تكنولوجيا المعلومات",StudyYearID=3,BranchID=2,SemesterNumber=2},
            new(){SubjectID=309,SubjectName="مادة محذوفة",StudyYearID=3,BranchID=1,SemesterNumber=2,IsDeleted=true},
            new(){SubjectID=401,SubjectName="مادة الرابعة",StudyYearID=4,BranchID=1,SemesterNumber=1}};
        Set(page,"assignmentSubjects",materials);
        Rows(page,"AssignmentRow","assignmentRows",materials.Select(s=>new Dictionary<string,object>{
            ["FacultyMemberID"]=42,["SubjectID"]=s.SubjectID,["SubjectName"]=s.SubjectName,["StudyYearID"]=s.StudyYearID,
            ["SemesterNumber"]=s.SemesterNumber,["IsSubjectDeleted"]=s.IsDeleted,["BranchName"]=s.BranchID==1?"علوم الحاسوب":s.BranchID==2?"تكنولوجيا المعلومات":"عام"
        }.Concat(s.BranchID.HasValue?new[]{new KeyValuePair<string,object>("BranchID",s.BranchID.Value)}:Array.Empty<KeyValuePair<string,object>>()).ToDictionary(x=>x.Key,x=>x.Value)).ToArray());
        Bind(Find<Guna2ComboBox>(page,"cmbAssignmentStudyYear"),Years.Select(y=>((int?)y.StudyYearID,y.YearName)));
        Invoke(page,"BindAssignmentFilterLookups");
        var grid=Find<DataGridView>(page,"dgvFacultyMemberSubjects");grid.BindingContext=new BindingContext();
        Invoke(page,"ApplyAssignmentRowsFilter");
        var semester=Find<Guna2ComboBox>(page,"cmbAssignmentSemesterFilter");
        var year=Find<Guna2ComboBox>(page,"cmbAssignmentStudyYearFilter");
        var branch=Find<Guna2ComboBox>(page,"cmbAssignmentBranchFilter");
        Check("Assignment filters share the lower list header",semester.Parent!.Parent==year.Parent!.Parent&&year.Parent.Parent==branch.Parent!.Parent);
        Check("Assignments default to first semester",Ids(grid).SequenceEqual(new[]{101,401}));
        Select(semester,null);
        Check("All-semesters assignment filter includes every row",Ids(grid).Count()==5);
        Set(page,"selectedSubjectId",101);Set(page,"selectedFacultyMemberId",42);
        Select(semester,2);
        Check("Assignment semester change clears old edit keys",Get(page,"selectedSubjectId") is null&&Get(page,"selectedFacultyMemberId") is null);
        Select(year,3);Select(branch,1);
        Check("All three assignment filters intersect and retain archived assignments",Ids(grid).SequenceEqual(new[]{205,309}));
        Invoke(page,"BindAssignmentFilterLookups");Invoke(page,"ApplyAssignmentRowsFilter");
        Check("Reloading lookups preserves the selected stage and department",Selected(year)==3&&Selected(branch)==1&&Ids(grid).SequenceEqual(new[]{205,309}));
        Select(branch,2);
        Check("Department filter selects only matching assignments",Ids(grid).SequenceEqual(new[]{206}));
        Select(year,1);
        Check("General stages reset and disable department filter",Selected(branch) is null&&!branch.Enabled);
        Check("Incompatible filters display an empty list",Ids(grid).Count()==0);
        Select(semester,null);
        Check("All semesters still respects stage",Ids(grid).SequenceEqual(new[]{101}));
        Select(year,3);Select(branch,1);Select(semester,2);
        // The data-entry fields are still present; only duplicate top navigation is removed.
        Select(Find<Guna2ComboBox>(page,"cmbAssignmentStudyYear"),3);
        Invoke(page,"BindAssignmentBranches",1);Invoke(page,"BindAssignmentSubjects",(object?)null);
        Check("Assignment subject picker follows semester, editor stage and department",Find<Guna2ComboBox>(page,"cmbSubject").Items.Cast<object>().Select(OptionId).SequenceEqual(new int?[]{205}));
        Invoke(page,"SetAssignmentActionsEnabled",false);
        Check("All lower assignment filters are disabled while saving",!semester.Enabled&&!year.Enabled&&!branch.Enabled);
        Invoke(page,"SetAssignmentActionsEnabled",true);
        Select(semester,null);Select(year,null);Select(branch,null);
        Capture(page,"assignments-lower-filters");
        var target=grid.Rows.Cast<DataGridViewRow>().Single(r=>(int)r.DataBoundItem.GetType().GetProperty("SubjectID")!.GetValue(r.DataBoundItem)! == 101);
        grid.CurrentCell=target.Cells.Cast<DataGridViewCell>().First(c=>c.Visible);target.Selected=true;
        Invoke(page,"PopulateAssignmentEditorFromSelection");
        Check("Selecting a row from the full list populates its subject",Selected(Find<Guna2ComboBox>(page,"cmbSubject"))==101);
        Check("Selecting a row does not change lower filters or hide rows",Selected(semester) is null&&Selected(year) is null&&Selected(branch) is null&&Ids(grid).Count()==5);
    }

    private static void CheckSharedSelection()
    {
        var a=typeof(SubjectsForm).Assembly;var state=Activator.CreateInstance(a.GetType(typeof(SubjectsForm).Namespace+".AcademicSemesterSelection")!,true)!;
        var navigation=a.GetType(typeof(SubjectsForm).Namespace+".FormNavigation")!;var pageType=a.GetType(typeof(SubjectsForm).Namespace+".NavigationPage")!;
        UserControl Create(string name)=>(UserControl)navigation.GetMethod("CreatePage")!.Invoke(null,new[]{Enum.Parse(pageType,name),state})!;
        using(var page=Create("Subjects")){Invoke(page,"ConfigureSubjectsEvents");Select(Find<Guna2ComboBox>(page,"cmbSemesterFilter"),2);}
        using(var page=Create("FacultyAssignments"))
        {Invoke(page,"ConfigureAssignmentEvents");Check("Assignments inherit selected semester",Selected(Find<Guna2ComboBox>(page,"cmbAssignmentSemesterFilter"))==2);Select(Find<Guna2ComboBox>(page,"cmbAssignmentSemesterFilter"),1);}
        using(var page=Create("Subjects"))Check("Subjects inherit selected semester on return",Selected(Find<Guna2ComboBox>(page,"cmbSemesterFilter"))==1);
        using var independent=new SubjectsForm();Check("Independent session starts in first semester",Selected(Find<Guna2ComboBox>(independent,"cmbSemesterFilter"))==1);
    }

    private static int? OptionId(object option)=>(int?)option.GetType().GetProperty("Id")!.GetValue(option);
    private static int? Selected(Guna2ComboBox combo)=>combo.SelectedItem is null?null:OptionId(combo.SelectedItem);
    private static void Select(Guna2ComboBox combo,int? value){combo.SelectedItem=combo.Items.Cast<object>().Single(x=>OptionId(x)==value);}
    private static void Bind(Guna2ComboBox combo,IEnumerable<(int? Id,string Text)> values)
    {var type=typeof(SubjectsForm).Assembly.GetType(typeof(SubjectsForm).Namespace+".ComboOption")!;var rows=(IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(type))!;foreach(var value in values)rows.Add(Activator.CreateInstance(type,new object?[]{value.Id,value.Text}));combo.DisplayMember="Text";combo.ValueMember="Id";combo.DataSource=rows;combo.SelectedIndex=-1;}
    private static IList Rows(object page,string nested,string field,params Dictionary<string,object>[] data)
    {var type=page.GetType().GetNestedType(nested,BindingFlags.NonPublic)!;var rows=(IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(type))!;foreach(var values in data){var row=Activator.CreateInstance(type)!;foreach(var pair in values)type.GetProperty(pair.Key)!.SetValue(row,pair.Value);rows.Add(row);}Set(page,field,rows);return rows;}
    private static IEnumerable<int> Ids(DataGridView grid)=>((IEnumerable)grid.DataSource!).Cast<object>().Select(x=>(int)x.GetType().GetProperty("SubjectID")!.GetValue(x)!).Order();
    private static T Find<T>(Control page,string name)where T:Control=>(T)page.Controls.Find(name,true).Single();
    private static object? Get(object x,string name)=>x.GetType().GetField(name,Private)!.GetValue(x);
    private static void Set(object x,string name,object? value)=>x.GetType().GetField(name,Private)!.SetValue(x,value);
    private static object? Invoke(object x,string name,params object?[]? args)=>x.GetType().GetMethod(name,Private)!.Invoke(x,args);
    private static void Check(string name,bool pass){checks++;if(!pass)throw new InvalidOperationException("FAIL: "+name);Console.WriteLine("PASS: "+name);}
    private static void Capture(Control page,string name)
    {typeof(SubjectsForm).Assembly.GetType(typeof(SubjectsForm).Namespace+".ArabicUi")!.GetMethod("Apply",BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic)!.Invoke(null,new[]{page});page.Size=new Size(1280,960);page.CreateControl();page.PerformLayout();Directory.CreateDirectory("output/lower-filters");using var bitmap=new Bitmap(page.Width,page.Height);page.DrawToBitmap(bitmap,page.ClientRectangle);bitmap.Save("output/lower-filters/"+name+".png");}
    private sealed class DesignContext:LicenseContext{public override LicenseUsageMode UsageMode=>LicenseUsageMode.Designtime;}
}
