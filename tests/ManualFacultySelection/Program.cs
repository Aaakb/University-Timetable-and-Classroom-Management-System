using System.Reflection;
using Guna.UI2.WinForms;
using University_Timetable_and_Classroom_Management_System;
using University_Timetable_and_Classroom_Management_System.Models;

internal static class Program
{
    private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
    private static int checks;

    [STAThread]
    private static void Main()
    {
        ApplicationConfiguration.Initialize();
        using var page = new SchedulesForm { BindingContext = new BindingContext() };
        void Set(string name, object value) => typeof(SchedulesForm).GetField(name, PrivateInstance)!.SetValue(page, value);
        Guna2ComboBox Combo(string name) => (Guna2ComboBox)typeof(SchedulesForm).GetField(name, PrivateInstance)!.GetValue(page)!;
        void Invoke(string name, params object?[] args) => typeof(SchedulesForm).GetMethod(name, PrivateInstance)!.Invoke(page, args);
        int? Selected(Guna2ComboBox combo) => combo.SelectedItem?.GetType().GetProperty("Id")!.GetValue(combo.SelectedItem) as int?;
        void Select(Guna2ComboBox combo, int id)
        {
            combo.SelectedItem = combo.Items.Cast<object>().Single(item => (int?)item.GetType().GetProperty("Id")!.GetValue(item) == id);
        }
        void Check(bool condition, string name)
        {
            if (!condition) throw new Exception(name);
            Console.WriteLine("PASS " + name);
            checks++;
        }

        var first = new FacultyMember { FacultyMemberID = 11, FullName = "Teacher A" };
        var second = new FacultyMember { FacultyMemberID = 12, FullName = "Teacher B" };
        var third = new FacultyMember { FacultyMemberID = 13, FullName = "Teacher C" };
        Set("subjectsLookup", new List<Subject> {
            new() { SubjectID=101, SubjectName="Multiple teachers", StudyYearID=3, BranchID=1, TheoreticalHours=1 },
            new() { SubjectID=102, SubjectName="One teacher", StudyYearID=3, BranchID=1, TheoreticalHours=1 },
            new() { SubjectID=103, SubjectName="Unassigned subject", StudyYearID=3, BranchID=1, TheoreticalHours=1 },
            new() { SubjectID=104, SubjectName="Another branch", StudyYearID=3, BranchID=2, TheoreticalHours=1 }
        });
        Set("sectionsLookup", new List<Section> {
            new() { SectionID=201, SectionName="A", StudyYearID=3, BranchID=1 },
            new() { SectionID=202, SectionName="A", StudyYearID=3, BranchID=2 }
        });
        Set("facultySubjectAssignmentsLookup", new List<FacultyMemberSubject> {
            new() { SubjectID=101, FacultyMemberID=11, FacultyMember=first },
            new() { SubjectID=101, FacultyMemberID=12, FacultyMember=second },
            new() { SubjectID=102, FacultyMemberID=13, FacultyMember=third },
            new() { SubjectID=104, FacultyMemberID=13, FacultyMember=third }
        });
        Invoke("BindSubjectsCombo", null, null, null);
        var subject=Combo("cmbSubject");
        var faculty=Combo("cmbFacultyMember");
        Select(subject,101);
        Check(faculty.Items.Count==2 && Selected(faculty)==11, "Subject selection immediately chooses an assigned teacher when several exist");
        Select(faculty,12);
        Select(Combo("cmbSection"),201);
        Check(Selected(faculty)==12, "Section selection retains the user's valid teacher choice");
        Invoke("BindFacultyMembersForSubject",101,12);
        Check(Selected(faculty)==12, "Editing a saved schedule retains its assigned teacher");
        Select(subject,102);
        Check(faculty.Items.Count==1 && Selected(faculty)==13, "Changing subject replaces the previous teacher with its sole assigned teacher");
        Select(subject,103);
        Check(faculty.Items.Count==0 && Selected(faculty) is null, "An unassigned subject clears the teacher instead of retaining an unrelated teacher");
        Invoke("BindSubjectsCombo",null,null,null);
        Select(subject,101);
        Invoke("BindSectionsCombo",null,null,null);
        Select(Combo("cmbSection"),202);
        Check(Selected(subject) is null && faculty.Items.Count==0, "Changing to an incompatible section clears the subject and its teachers");
        Invoke("BindSubjectsCombo",null,null,null);
        Select(subject,101);
        subject.SelectedIndex=-1;
        Check(faculty.Items.Count==0 && Selected(faculty) is null, "Clearing the subject clears its teacher list");
        Console.WriteLine($"SUMMARY: {checks} checks, 0 failures; no database changes");
    }
}
