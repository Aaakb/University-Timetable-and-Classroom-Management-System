namespace University_Timetable_and_Classroom_Management_System.BusinessLayer
{
    public static class AcademicStructureRules
    {
        public static bool UsesGeneralSections(int studyYearId)
        {
            return studyYearId is 1 or 2;
        }

        public static bool UsesBranches(int studyYearId)
        {
            return studyYearId >= 3;
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
                _ => []
            };
        }

        public static IReadOnlyList<string> GetAllowedPracticalGroupNames(int studyYearId)
        {
            return ["A1", "A2", "B1", "B2"];
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
