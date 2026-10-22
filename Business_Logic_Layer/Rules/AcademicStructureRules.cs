using University_Timetable_and_Classroom_Management_System.Models;

namespace University_Timetable_and_Classroom_Management_System.BusinessLayer
{
    public static class AcademicStructureRules
    {
        // All fourth-year branches teach practicals to the entire section.
        public static bool AllowsWholeSectionPractical(int studyYearId) => studyYearId == 4;

        public static bool UsesGeneralSections(int studyYearId)
        {
            return studyYearId is 1 or 2;
        }

        public static bool UsesBranches(int studyYearId)
        {
            return studyYearId >= 3;
        }

        public static bool IsGeneralSectionValid(Section section)
        {
            if (UsesGeneralSections(section.StudyYearID))
            {
                // A1/A2/B1/B2 are practical groups, not independent general sections.
                return !section.BranchID.HasValue &&
                    GetAllowedSectionNames(section.StudyYearID)
                        .Contains(section.SectionName.Trim(), StringComparer.OrdinalIgnoreCase);
            }

            return true;
        }

        public static int GetStudyYearOrder(string yearName)
        {
            return yearName.Trim().ToLowerInvariant() switch
            {
                "first year" => 1,
                "second year" => 2,
                "third year" => 3,
                "fourth year" => 4,
                _ => 99
            };
        }

        public static IReadOnlyList<string> GetAllowedSectionNames(int studyYearId)
        {
            return studyYearId switch
            {
                1 or 2 => ["A", "B"],
                3 or 4 => ["A"],
                _ => []
            };
        }

        public static IReadOnlyList<string> GetAllowedPracticalGroupNames(int studyYearId)
        {
            return studyYearId switch
            {
                1 or 2 => ["A1", "A2", "B1", "B2"],
                3 => ["A1", "A2"],
                4 => [],
                _ => []
            };
        }
        public static IReadOnlyList<string> GetAllowedPracticalGroupNames(int studyYearId, string sectionName)
        {
            return AllowsWholeSectionPractical(studyYearId) ? [] : GetAllowedPracticalGroupNames(sectionName);
        }

        public static IReadOnlyList<string> GetAllowedPracticalGroupNames(string sectionName)
        {
            return GetBaseSectionName(sectionName).ToUpperInvariant() switch
            {
                "A" => ["A1", "A2"],
                "B" => ["B1", "B2"],
                _ => []
            };
        }

        public static string GetBaseSectionName(string sectionOrGroupName)
        {
            string value = sectionOrGroupName.Trim();

            return value.ToUpperInvariant() switch
            {
                "A1" or "A2" => "A",
                "B1" or "B2" => "B",
                _ => value
            };
        }

        public static string FormatAllowedSectionNames(int studyYearId)
        {
            var names = GetAllowedSectionNames(studyYearId);
            return names.Count == 0 ? string.Empty : string.Join(", ", names);
        }

        public static bool SectionMatchesSubject(
            int studyYearId,
            int? subjectBranchId,
            int? sectionBranchId,
            string sectionName)
        {
            if (UsesGeneralSections(studyYearId))
            {
                var allowedNames = GetAllowedSectionNames(studyYearId);
                string baseSectionName = GetBaseSectionName(sectionName);

                return !subjectBranchId.HasValue &&
                    !sectionBranchId.HasValue &&
                    allowedNames.Contains(baseSectionName, StringComparer.OrdinalIgnoreCase);
            }

            if (UsesBranches(studyYearId))
            {
                return subjectBranchId.HasValue && sectionBranchId == subjectBranchId;
            }

            return sectionBranchId == subjectBranchId;
        }
    }
}
