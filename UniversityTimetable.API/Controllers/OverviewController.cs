using Data_Access_Layer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using University_Timetable_and_Classroom_Management_System.BusinessLayer;

namespace UniversityTimetable.API.Controllers;

[ApiController, Authorize, Route("api/overview")]
public sealed class OverviewController : ControllerBase
{
    [HttpGet]
    public async Task<object> Get()
    {
        await using var db = new AppDbContext();
        int unassignedSubjects = (await db.Subjects.AsNoTracking()
            .Where(subject => !subject.IsDeleted && !subject.FacultyMemberSubjects.Any()).ToListAsync()).Count(subject => subject.HasWeeklyLectures);
        return new {
            classrooms = await db.Classrooms.CountAsync(), subjects = await db.Subjects.CountAsync(s => !s.IsDeleted),
            facultyMembers = await db.FacultyMembers.CountAsync(), sections = await db.Sections.CountAsync(),
            schedules = await db.Schedules.CountAsync(), assignments = await db.FacultyMemberSubjects.CountAsync(),
            unassignedSubjects,
            updatedAt = DateTimeOffset.UtcNow
        };
    }

    [HttpGet("rules")]
    public async Task<object> Rules()
    {
        var years = await new StudyYearService().GetAllAsync();
        var sections = await new SectionService().GetAllAsync();
        var subjects = await new SubjectService().GetAllAsync();
        return new {
            years = years.Select(x => new { id = x.StudyYearID,
                usesBranches = AcademicStructureRules.UsesBranches(x.StudyYearID),
                usesGeneralSections = AcademicStructureRules.UsesGeneralSections(x.StudyYearID),
                sectionNames = AcademicStructureRules.GetAllowedSectionNames(x.StudyYearID) }),
            sections = sections.Select(x => new { id = x.SectionID,
                allowsWholeSectionPractical = AcademicStructureRules.AllowsWholeSectionPractical(x.StudyYearID),
                groups = AcademicStructureRules.GetAllowedPracticalGroupNames(x.StudyYearID, x.SectionName) }),
            subjectSections = subjects.Select(x => new { id = x.SubjectID, sectionIds = sections.Where(s =>
                s.StudyYearID == x.StudyYearID && AcademicStructureRules.SectionMatchesSubject(x.StudyYearID,
                    x.BranchID, s.BranchID, s.SectionName)).Select(s => s.SectionID) })
        };
    }
}
