using Data_Access_Layer;
using Microsoft.EntityFrameworkCore;
using University_Timetable_and_Classroom_Management_System.Models;

namespace University_Timetable_and_Classroom_Management_System.BusinessLayer;

internal static class AcademicExcelImport
{
    public static void Prepare(IReadOnlyList<ExcelSheetData> sheets, ImportCatalog catalog, ExcelImportPreview preview, CancellationToken token)
    {
        sheets = NamedAssignmentImport.Expand(sheets, catalog, preview, token);
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var schema in ExcelImportSchema.Academic.Where(s => s.Name is "FacultyMembers" or "Subjects"))
        foreach (var sheet in sheets.Where(s => s.Name.Equals(schema.Name, StringComparison.OrdinalIgnoreCase)))
        {
            if (!ExcelImportService.CheckColumns(sheet, schema, preview)) continue;
            for (int i = 0; i < sheet.Table.Rows.Count; i++)
            {
                token.ThrowIfCancellationRequested();
                int sourceRow = sheet.SourceRows[i];
                try
                {
                    var value = new ImportValues(sheet.Table.Rows[i]);
                    object entity = Parse(schema.Name, value);
                    string key = Key(entity);
                    if (!seen.Add(schema.Name + "/" + key))
                        throw new ArgumentException("Duplicate key in this workbook.");
                    object? old = catalog.Tables[entity.GetType()].SingleOrDefault(x => Key(x) == key);
                    if (old is not null)
                    {
                        foreach (string optional in schema.Optional.Where(column => !sheet.Table.Columns.Cast<System.Data.DataColumn>()
                            .Any(c => c.ColumnName.Equals(column, StringComparison.OrdinalIgnoreCase))))
                        {
                            var property = entity.GetType().GetProperty(optional)!;
                            property.SetValue(entity, property.GetValue(old));
                        }
                    }
                    if (entity is Subject subject && old is Subject saved)
                    {
                        if (saved.IsDeleted) throw new ArgumentException("المادة محذوفة من قائمة المواد. استخدم معرفاً جديداً لإضافتها دون تغيير تكليفاتها السابقة.");
                        subject.RequirementType = saved.RequirementType;
                    }
                    NormalizeAndCheck(entity, catalog);
                    CheckUnique(entity, catalog, old);
                    string action = old is null ? "Add" : ImportCatalog.Same(entity, old) ? "Unchanged" : "Update";
                    if (action == "Update" && !preview.UpdateExisting)
                        throw new ArgumentException("Existing record differs. Enable updates explicitly to change it.");
                    if (action == "Update") CheckLinkedSchedules(entity, old!, catalog);
                    catalog.Put(entity, old);
                    preview.AcademicRows.Add(new(sheet.SourceName, sourceRow, entity, action, key));
                    preview.Changes.Add(new(sheet.SourceName, sourceRow, action, key));
                }
                catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
                {
                    preview.Issues.Add(new(sheet.SourceName, sourceRow, "", ex.Message));
                }
            }
        }
        foreach (var unknown in sheets.Where(s => !ExcelImportSchema.AcademicInputs.Any(a => a.Name.Equals(s.Name, StringComparison.OrdinalIgnoreCase))))
            preview.ReadingNotes.Add(new(unknown.SourceName, 1, "", "تم تجاهل الورقة؛ الاستيراد الأكاديمي يستقبل التدريسيين والمواد فقط ولا ينشئ تكليفات."));
    }

    private static object Parse(string sheet, ImportValues v) => sheet switch
    {
        "StudyYears" => new StudyYear { StudyYearID = v.Int("StudyYearID"), YearName = v.Text("YearName") },
        "Branches" => new Branch { BranchID = v.Int("BranchID"), BranchName = v.Text("BranchName") },
        "FacultyMembers" => new FacultyMember { FacultyMemberID = v.Int("FacultyMemberID"), FullName = v.Text("FullName"), AcademicTitle = Empty(v.Text("AcademicTitle", false)) },
        "Classrooms" => new Classroom { ClassroomID = v.Int("ClassroomID"), ClassroomNumber = v.Text("ClassroomNumber"), Capacity = v.Int("Capacity"), RoomType = v.Text("RoomType") },
        "TimeSlots" => new TimeSlot { TimeSlotID = v.Int("TimeSlotID"), StartTime = v.Time("StartTime"), EndTime = v.Time("EndTime") },
        "Sections" => new Section { SectionID = v.Int("SectionID"), SectionName = v.Text("SectionName"), StudentCount = v.Int("StudentCount"), StudyYearID = v.Int("StudyYearID"), BranchID = v.OptionalInt("BranchID") },
        "Subjects" => new Subject { SubjectID = v.Int("SubjectID"), SubjectName = v.Text("SubjectName"), StudyYearID = v.Int("StudyYearID"), BranchID = v.OptionalInt("BranchID"), SemesterNumber = v.Int("SemesterNumber"), TheoreticalHours = v.Number("TheoreticalHours"), PracticalHours = v.Number("PracticalHours"), CreditUnits = v.Text("CreditUnits", false).Length == 0 ? 0 : v.Number("CreditUnits"), TheoreticalLectureCount = v.OptionalInt("TheoreticalLectureCount"), PracticalLectureCount = v.OptionalInt("PracticalLectureCount") },
        "FacultyMemberSubjects" => new FacultyMemberSubject { FacultyMemberID = v.Int("FacultyMemberID"), SubjectID = v.Int("SubjectID"), SectionID = v.OptionalInt("SectionID"), LectureType = v.Text("LectureType", false) is { Length: > 0 } t ? t : "Both" },
        _ => throw new ArgumentException("Unsupported worksheet.")
    };
    private static string? Empty(string v) => v.Length == 0 ? null : v;
    internal static string Key(object e) => e is FacultyMemberSubject f
        ? $"{f.FacultyMemberID}/{f.SubjectID}"
        : e.GetType().GetProperty(e.GetType().Name + "ID")!.GetValue(e)!.ToString()!;

    private static void NormalizeAndCheck(object entity, ImportCatalog c)
    {
        if (entity is not FacultyMemberSubject && int.Parse(Key(entity)) <= 0)
            throw new ArgumentException("IDs must be positive integers. Keep IDs stable for re-import.");
        switch (entity)
        {
            case StudyYear x: InputValidation.TextLength(x.YearName, 50, "YearName"); break;
            case Branch x: InputValidation.TextLength(x.BranchName, 100, "BranchName"); break;
            case FacultyMember x:
                InputValidation.TextLength(x.FullName, 100, "FullName");
                InputValidation.TextLength(x.AcademicTitle, 50, "AcademicTitle"); break;
            case Classroom x:
                InputValidation.TextLength(x.ClassroomNumber, 20, "ClassroomNumber");
                if (x.Capacity <= 0) throw new ArgumentException("Capacity must be greater than zero.");
                if (!new[] { "Lecture", "Lab", ClassroomRules.SharedLabType }.Contains(x.RoomType, StringComparer.OrdinalIgnoreCase))
                    throw new ArgumentException("RoomType must be Lecture, Lab or Shared Lab.");
                x.RoomType = ClassroomRules.NormalizeRoomType(x.RoomType, x.ClassroomNumber); break;
            case TimeSlot x:
                TimeSlotService.ValidateLectureRange(x.StartTime, x.EndTime);
                if (c.Rows<TimeSlot>().Any(s => s.TimeSlotID != x.TimeSlotID && !s.IsBreak &&
                    x.StartTime < s.EndTime + ScheduleTimingRules.LectureGap && x.EndTime + ScheduleTimingRules.LectureGap > s.StartTime))
                    throw new ArgumentException("Lecture periods overlap or have less than a 10-minute gap.");
                break;
            case Section x:
                Structure(x.StudyYearID, x.BranchID, c);
                if (x.StudentCount < 0) throw new ArgumentException("StudentCount cannot be negative.");
                InputValidation.TextLength(x.SectionName, 50, "SectionName");
                var allowed = AcademicStructureRules.GetAllowedSectionNames(x.StudyYearID);
                if (allowed.Count > 0 && !allowed.Contains(x.SectionName, StringComparer.OrdinalIgnoreCase))
                    throw new ArgumentException("Allowed section names: " + string.Join(", ", allowed));
                if (c.Rows<FacultyMemberSubject>().Where(a => a.SectionID == x.SectionID).Any(a =>
                    !FacultyAssignmentRules.Matches(c.Rows<Subject>().Single(s => s.SubjectID == a.SubjectID), x)))
                    throw new ArgumentException("تغيير الشعبة لا يطابق مرحلة وتخصص تكليفاتها الحالية.");
                break;
            case Subject x:
                SubjectLectureRules.Validate(x);
                if (!x.IsUnclassified) Structure(x.StudyYearID, x.BranchID, c);
                if (x.IsUnclassified && (x.BranchID.HasValue || x.HasWeeklyLectures || x.TheoreticalLectureCount != 0 || x.PracticalLectureCount != 0))
                    throw new ArgumentException("المادة المستوردة بالاسم تبقى دون محاضرات لحين استكمال بياناتها.");
                if (x.SemesterNumber is not (1 or 2)) throw new ArgumentException("SemesterNumber must be 1 or 2.");
                InputValidation.TextLength(x.SubjectName, 100, "SubjectName");
                if (c.Rows<FacultyMemberSubject>().Where(a => a.SubjectID == x.SubjectID && a.SectionID.HasValue).Any(a =>
                    !FacultyAssignmentRules.Matches(x, c.Rows<Section>().Single(s => s.SectionID == a.SectionID))))
                    throw new ArgumentException("تغيير المادة لا يطابق مرحلة وتخصص الشعب المحددة في تكليفاتها.");
                break;
            case FacultyMemberSubject x:
                x.LectureType = FacultyAssignmentRules.NormalizeLectureType(x.LectureType);
                if (!c.Rows<FacultyMember>().Any(f => f.FacultyMemberID == x.FacultyMemberID) ||
                    !c.Rows<Subject>().Any(s => s.SubjectID == x.SubjectID && !s.IsDeleted))
                    throw new ArgumentException("FacultyMemberID or SubjectID does not exist in the database or preceding sheets.");
                if (x.SectionID.HasValue && !c.Rows<Section>().Any(section => section.SectionID == x.SectionID &&
                    FacultyAssignmentRules.Matches(c.Rows<Subject>().Single(s => s.SubjectID == x.SubjectID), section)))
                    throw new ArgumentException("شعبة التكليف لا تطابق مرحلة المادة وتخصصها.");
                break;
        }
    }
    private static void Structure(int year, int? branch, ImportCatalog c)
    {
        if (!c.Rows<StudyYear>().Any(y => y.StudyYearID == year)) throw new ArgumentException("StudyYearID does not exist.");
        if (branch.HasValue && !c.Rows<Branch>().Any(b => b.BranchID == branch)) throw new ArgumentException("BranchID does not exist.");
        if (AcademicStructureRules.UsesGeneralSections(year) && branch.HasValue) throw new ArgumentException("This study year requires general subjects and sections without a branch.");
        if (AcademicStructureRules.UsesBranches(year) && !branch.HasValue) throw new ArgumentException("This study year requires a BranchID.");
    }
    private static void CheckUnique(object entity, ImportCatalog c, object? old)
    {
        string Natural(object x) => x switch
        {
            StudyYear y => y.YearName,
            Branch b => b.BranchName,
            FacultyMember f => f.FullName,
            Classroom r => r.ClassroomNumber,
            TimeSlot t => $"{t.StartTime}/{t.EndTime}/{t.IsBreak}",
            Section s => $"{s.StudyYearID}/{s.BranchID}/{s.SectionName}",
            Subject s => $"{s.StudyYearID}/{s.BranchID}/{s.SemesterNumber}/{s.SubjectName}",
            _ => Key(x)
        };
        if (c.Tables[entity.GetType()].Any(x => !ReferenceEquals(x, old) && x is not Subject { IsDeleted: true } &&
            string.Equals(Natural(x), Natural(entity), StringComparison.OrdinalIgnoreCase)))
            throw new ArgumentException("A record with the same name and scope already exists under a different ID. Use its existing ID.");
    }
    private static void CheckLinkedSchedules(object e, object old, ImportCatalog c)
    {
        if (e is FacultyMemberSubject assignment &&
            c.Rows<Schedule>().Any(s => s.FacultyMemberID == assignment.FacultyMemberID &&
                s.SubjectID == assignment.SubjectID && !FacultyAssignmentRules.Covers(assignment, s.SubjectID, s.SectionID, s.LectureType)))
            throw new ArgumentException("نطاق التكليف يستبعد شعبًا لها محاضرات محفوظة للتدريسي. انقل المحاضرات أولًا.");
        foreach (var s in c.Rows<Schedule>())
        {
            if (e is Section section && s.SectionID == section.SectionID)
            {
                var previous = (Section)old;
                if (section.StudyYearID != previous.StudyYearID || section.BranchID != previous.BranchID ||
                    !section.SectionName.Equals(previous.SectionName, StringComparison.OrdinalIgnoreCase))
                    throw new ArgumentException("A scheduled section cannot change its name, year or branch.");
                var room = c.Rows<Classroom>().Single(r => r.ClassroomID == s.ClassroomID);
                if (!ScheduleRoomSelector.HasCapacity(room, section, s.GroupName)) throw new ArgumentException("New student count exceeds a booked room capacity.");
            }
            if (e is Classroom roomUpdate && s.ClassroomID == roomUpdate.ClassroomID)
            {
                var sectionData = c.Rows<Section>().Single(x => x.SectionID == s.SectionID);
                if (!ScheduleRoomSelector.MatchesLectureType(roomUpdate, s.LectureType) || !ScheduleRoomSelector.HasCapacity(roomUpdate, sectionData, s.GroupName))
                    throw new ArgumentException("This room change would invalidate an existing lecture.");
            }
            if (e is Subject subject && s.SubjectID == subject.SubjectID)
            {
                var previous = (Subject)old;
                if (subject.StudyYearID != previous.StudyYearID || subject.BranchID != previous.BranchID || subject.SemesterNumber != previous.SemesterNumber)
                    throw new ArgumentException("Import cannot change the year, branch or semester of a scheduled subject. Use the subject editor.");
                SubjectLectureRules.ValidateSavedLectures(subject, c.Rows<Schedule>());
                if (s.LectureType == "Practical" && subject.EffectivePracticalLectureCount <= 0 || s.LectureType == "Theory" && subject.EffectiveTheoreticalLectureCount <= 0)
                    throw new ArgumentException("This change removes a lecture component already used in the timetable.");
            }
        }
    }

    public static async Task SaveAsync(AppDbContext db, AcademicImportRow row)
    {
        bool update = row.Action == "Update";
        switch (row.Entity)
        {
            case StudyYear x: if (update) await StudyYearService.UpdateInContextAsync(db, x); else await StudyYearService.AddInContextAsync(db, x); break;
            case Branch x: if (update) await BranchService.UpdateInContextAsync(db, x); else await BranchService.AddInContextAsync(db, x); break;
            case FacultyMember x: if (update) await FacultyMemberService.UpdateInContextAsync(db, x); else await FacultyMemberService.AddInContextAsync(db, x); break;
            case Classroom x: if (update) await ClassroomService.UpdateInContextAsync(db, x); else await ClassroomService.AddInContextAsync(db, x); break;
            case TimeSlot x: if (update) await TimeSlotService.UpdateInContextAsync(db, x); else await TimeSlotService.AddInContextAsync(db, x); break;
            case Section x: if (update) await SectionService.UpdateInContextAsync(db, x); else await SectionService.AddInContextAsync(db, x); break;
            case Subject x: if (update) await SubjectService.UpdateInContextAsync(db, x); else if (x.IsUnclassified) await SubjectService.AddNameOnlyInContextAsync(db, x); else await SubjectService.AddInContextAsync(db, x); break;
            case FacultyMemberSubject x:
                x.LectureType = FacultyAssignmentRules.NormalizeLectureType(x.LectureType);
                if (update && await db.Schedules.AnyAsync(s => s.FacultyMemberID == x.FacultyMemberID &&
                    s.SubjectID == x.SubjectID && x.LectureType != "Both" && s.LectureType != x.LectureType))
                    throw new ArgumentException("نوع التكليف يستبعد محاضرات محفوظة للتدريسي.");
                if (!await db.FacultyMembers.AnyAsync(f => f.FacultyMemberID == x.FacultyMemberID) ||
                    !await db.Subjects.AnyAsync(s => s.SubjectID == x.SubjectID && !s.IsDeleted)) throw new ArgumentException("Teaching assignment reference no longer exists.");
                if (x.SectionID.HasValue)
                {
                    var subject = await db.Subjects.AsNoTracking().SingleAsync(s => s.SubjectID == x.SubjectID);
                    var section = await db.Sections.AsNoTracking().SingleOrDefaultAsync(s => s.SectionID == x.SectionID);
                    if (section is null || !FacultyAssignmentRules.Matches(subject, section))
                        throw new ArgumentException("شعبة التكليف لا تطابق مرحلة المادة وتخصصها.");
                    if (update && await db.Schedules.AnyAsync(s => s.FacultyMemberID == x.FacultyMemberID &&
                        s.SubjectID == x.SubjectID && s.SectionID != x.SectionID))
                        throw new ArgumentException("نطاق التكليف يستبعد محاضرات محفوظة للتدريسي.");
                }
                if (update)
                {
                    var current = await db.FacultyMemberSubjects.FindAsync(x.FacultyMemberID, x.SubjectID)
                        ?? throw new KeyNotFoundException("Teaching assignment no longer exists.");
                    current.SectionID = x.SectionID;
                    current.LectureType = x.LectureType;
                }
                else db.FacultyMemberSubjects.Add(x);
                await db.SaveChangesAsync(); break;
        }
    }
}
