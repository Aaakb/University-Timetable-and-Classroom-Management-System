using University_Timetable_and_Classroom_Management_System.Models;

namespace University_Timetable_and_Classroom_Management_System.BusinessLayer
{
    internal sealed class ScheduleGenerationState
    {
        private readonly IReadOnlyDictionary<ScheduleDayPlanKey, ScheduleDayPlan> dayPlans;
        private readonly IReadOnlyDictionary<(int Faculty, int Semester), FacultyScheduleRequest> preferences;
        private readonly IReadOnlyDictionary<int, TimeSlot> periods;
        private readonly System.Diagnostics.Stopwatch repairClock = System.Diagnostics.Stopwatch.StartNew();
        private readonly ScheduleBuildContext? buildContext;
        private int repairAttempts;
        private readonly HashSet<(int ClassroomID, int SemesterNumber, string DayOfWeek, int TimeSlotID)> classroomBusy = [];
        private readonly HashSet<(int FacultyMemberID, int SemesterNumber, string DayOfWeek, int TimeSlotID)> facultyBusy = [];
        private readonly HashSet<(int? StudyYearID, int? BranchID, int? SectionID, int SemesterNumber, string DayOfWeek, int TimeSlotID)> sectionAnyBusy = [];
        private readonly HashSet<(int? StudyYearID, int? BranchID, int? SectionID, int SemesterNumber, string DayOfWeek, int TimeSlotID)> sectionWholeBusy = [];
        private readonly HashSet<(int? StudyYearID, int? BranchID, int? SectionID, int SemesterNumber, string DayOfWeek, int TimeSlotID, string GroupName)> sectionGroupBusy = [];
        private readonly HashSet<(int SubjectID, int? SectionID, int SemesterNumber, string DayOfWeek)> subjectDayAny = [];
        private readonly HashSet<(int SubjectID, int? SectionID, int SemesterNumber, string DayOfWeek)> subjectDayWhole = [];
        private readonly HashSet<(int SubjectID, int? SectionID, int SemesterNumber, string DayOfWeek, string GroupName)> subjectDayGroup = [];

        private readonly Dictionary<int, int> facultyTotalLoads = [];
        private readonly Dictionary<(int FacultyMemberID, int SemesterNumber, string DayOfWeek), int> facultyDayLoads = [];
        private readonly Dictionary<(int? StudyYearID, int? BranchID, int? SectionID, int SemesterNumber, string DayOfWeek), int> sectionDayLoads = [];
        private readonly Dictionary<(int SemesterNumber, string DayOfWeek, int TimeSlotID), int> slotLoads = [];

        public List<Schedule> Schedules { get; } = [];
        private readonly HashSet<Schedule> fixedSchedules = [];

        public ScheduleGenerationState(IReadOnlyDictionary<ScheduleDayPlanKey, ScheduleDayPlan> dayPlans) : this(dayPlans, null) { }
        public ScheduleGenerationState(IReadOnlyDictionary<ScheduleDayPlanKey, ScheduleDayPlan> dayPlans, ScheduleBuildContext? context)
        {
            this.dayPlans = dayPlans;
            buildContext = context;
            preferences = context?.Preferences ?? new Dictionary<(int, int), FacultyScheduleRequest>();
            periods = context?.TimeSlots.ToDictionary(p => p.TimeSlotID) ?? new Dictionary<int, TimeSlot>();
        }
        public bool TryRepairStep()
        {
            buildContext?.CancellationToken.ThrowIfCancellationRequested();
            return ++repairAttempts <= (buildContext?.RepairAttemptLimit ?? 20000) &&
                repairClock.Elapsed.TotalSeconds < (buildContext?.RepairTimeLimitSeconds ?? 20);
        }
        public bool CanPlaceTogether(IReadOnlyList<Schedule> rows) => rows.All(CanPlace) &&
            (buildContext?.EnforceSectionDayLimit != true || Schedules.Concat(rows).GroupBy(s => (s.SemesterNumber, s.SectionID))
                .All(g => g.Select(s => s.DayOfWeek).Distinct().Count() <= ScheduleDayRules.MaximumSectionTeachingDays)) &&
            !ScheduleConflictChecker.HasAnyPairConflict(rows) && preferences.All(entry =>
                FacultyPreferenceRules.Violation(entry.Value, Schedules.Concat(rows), periods) is null);

        public void SeedFixed(IEnumerable<Schedule> schedules)
        {
            foreach (var schedule in schedules)
            {
                if (!CanPlace(schedule)) throw new ArgumentException("Fixed lectures conflict with one another.");
                fixedSchedules.Add(schedule);
                Add(schedule);
            }
        }

        public bool IsFixed(Schedule schedule) => fixedSchedules.Contains(schedule);

        public IEnumerable<int> GetOrderedFacultyOptions(
            IReadOnlyList<int> facultyMemberIds,
            int semesterNumber,
            string dayOfWeek)
        {
            return ScheduleFacultySelector.GetOrderedFacultyOptions(
                facultyMemberIds,
                semesterNumber,
                dayOfWeek,
                facultyDayLoads,
                facultyTotalLoads);
        }

        public bool CanPlace(Schedule schedule)
        {
            if (buildContext?.EnforceSectionDayLimit == true &&
                !Schedules.Any(s => s.SemesterNumber == schedule.SemesterNumber && s.SectionID == schedule.SectionID && s.DayOfWeek == schedule.DayOfWeek) &&
                Schedules.Where(s => s.SemesterNumber == schedule.SemesterNumber && s.SectionID == schedule.SectionID)
                    .Select(s => s.DayOfWeek).Distinct().Count() >= ScheduleDayRules.MaximumSectionTeachingDays) return false;
            if (preferences.TryGetValue((schedule.FacultyMemberID, schedule.SemesterNumber), out var profile) &&
                FacultyPreferenceRules.Violation(profile, Schedules.Append(schedule), periods) is not null) return false;
            if (classroomBusy.Contains(ClassroomTimeKey(schedule)) ||
                facultyBusy.Contains(FacultyTimeKey(schedule)))
            {
                return false;
            }

            var sectionKey = SectionTimeKey(schedule);

            if (string.IsNullOrWhiteSpace(schedule.GroupName))
            {
                return !sectionAnyBusy.Contains(sectionKey);
            }

            return !sectionWholeBusy.Contains(sectionKey) &&
                !sectionGroupBusy.Contains(SectionGroupTimeKey(schedule));
        }

        public bool HasSameSubjectSectionOnDay(Schedule schedule)
        {
            var subjectKey = SubjectDayKey(schedule);

            if (string.IsNullOrWhiteSpace(schedule.GroupName))
            {
                return subjectDayAny.Contains(subjectKey);
            }

            return subjectDayWhole.Contains(subjectKey) ||
                subjectDayGroup.Contains(SubjectGroupDayKey(schedule));
        }

        public int ScorePlacement(Schedule schedule, SchedulePlacementCandidate candidate)
        {
            int facultyDayLoad = Count(
                facultyDayLoads,
                (schedule.FacultyMemberID, schedule.SemesterNumber, schedule.DayOfWeek));
            int facultyTotalLoad = Count(facultyTotalLoads, schedule.FacultyMemberID);
            int sectionDayLoad = Count(sectionDayLoads, SectionDayKey(schedule));
            int slotLoad = Count(slotLoads, (schedule.SemesterNumber, schedule.DayOfWeek, schedule.TimeSlotID));
            int roomWaste = Math.Max(0, candidate.Classroom.Capacity - candidate.RequiredCapacity);
            int sameSubjectDayPenalty = HasSameSubjectSectionOnDay(schedule) ? 180 : 0;
            int practicalPairingScore = GetPracticalPairingScore(schedule);
            int compactDayScore = GetCompactDayScore(schedule, sectionDayLoad);
            int preferenceScore = 0;
            if (preferences.TryGetValue((schedule.FacultyMemberID, schedule.SemesterNumber), out var profile))
            {
                if (profile.HasScoredPreferences)
                {
                    if (FacultyPreferenceRules.Level(profile, schedule.DayOfWeek, schedule.TimeSlotID) == FacultyPreferenceLevel.Preferred)
                        preferenceScore += 2 * FacultyPreferenceRules.Duration(candidate.TimeSlot) * 100;
                    int attended = facultyDayLoads.Keys.Count(k => k.FacultyMemberID == schedule.FacultyMemberID && k.SemesterNumber == schedule.SemesterNumber);
                    if (profile.UsePreferenceMatrix && facultyDayLoad == 0 && attended < profile.SelectedDays.Count) preferenceScore -= 100000;
                }
                if (!profile.SelectedDays.Contains(schedule.DayOfWeek)) preferenceScore += 10000;
                if (profile.PreferredPeriodIds!.Count > 0 && !profile.PreferredPeriodIds.Contains(schedule.TimeSlotID)) preferenceScore += 150;
                if (profile.TargetTeachingDays.HasValue && facultyDayLoad == 0 &&
                    facultyDayLoads.Keys.Count(k => k.FacultyMemberID == schedule.FacultyMemberID && k.SemesterNumber == schedule.SemesterNumber) >= profile.TargetTeachingDays)
                    preferenceScore += 1500;
            }

            return
                facultyDayLoad * 45 +
                facultyTotalLoad * 6 +
                slotLoad * 3 +
                roomWaste +
                sameSubjectDayPenalty +
                practicalPairingScore +
                compactDayScore +
                preferenceScore +
                ScheduleDayRules.GetDayOrder(schedule.DayOfWeek);
        }

        public uint VariationOrder(string day, int period, int room, int faculty = 0) =>
            buildContext?.VariationSeed is > 0
                ? unchecked((uint)HashCode.Combine(buildContext.VariationSeed, day, period, room, faculty))
                : 0;

        public void Add(Schedule schedule)
        {
            Schedules.Add(schedule);
            AddIndexes(schedule);
        }

        public void RemoveRange(IEnumerable<Schedule> schedules)
        {
            var schedulesToRemove = schedules.ToHashSet();
            if (schedulesToRemove.Overlaps(fixedSchedules))
                throw new InvalidOperationException("Locked lectures cannot be removed by generation.");
            Schedules.RemoveAll(schedulesToRemove.Contains);
            RebuildIndexes();
        }

        public void ResetTo(IEnumerable<Schedule> schedules)
        {
            var snapshot = schedules.ToList();
            if (!fixedSchedules.IsSubsetOf(snapshot))
                throw new InvalidOperationException("A generation snapshot cannot discard locked lectures.");
            Schedules.Clear();
            Schedules.AddRange(snapshot);
            RebuildIndexes();
        }

        public IEnumerable<Schedule> GetConflictingSchedules(Schedule schedule)
        {
            return Schedules.Where(existing => ScheduleConflictChecker.HasPairConflict(existing, schedule));
        }

        private void AddIndexes(Schedule schedule)
        {
            classroomBusy.Add(ClassroomTimeKey(schedule));
            facultyBusy.Add(FacultyTimeKey(schedule));

            var sectionTimeKey = SectionTimeKey(schedule);
            sectionAnyBusy.Add(sectionTimeKey);

            if (string.IsNullOrWhiteSpace(schedule.GroupName))
            {
                sectionWholeBusy.Add(sectionTimeKey);
                subjectDayWhole.Add(SubjectDayKey(schedule));
            }
            else
            {
                sectionGroupBusy.Add(SectionGroupTimeKey(schedule));
                subjectDayGroup.Add(SubjectGroupDayKey(schedule));
            }

            subjectDayAny.Add(SubjectDayKey(schedule));

            Increment(facultyTotalLoads, schedule.FacultyMemberID);
            Increment(facultyDayLoads, (schedule.FacultyMemberID, schedule.SemesterNumber, schedule.DayOfWeek));
            Increment(sectionDayLoads, SectionDayKey(schedule));
            Increment(slotLoads, (schedule.SemesterNumber, schedule.DayOfWeek, schedule.TimeSlotID));
        }

        private void RebuildIndexes()
        {
            classroomBusy.Clear();
            facultyBusy.Clear();
            sectionAnyBusy.Clear();
            sectionWholeBusy.Clear();
            sectionGroupBusy.Clear();
            subjectDayAny.Clear();
            subjectDayWhole.Clear();
            subjectDayGroup.Clear();
            facultyTotalLoads.Clear();
            facultyDayLoads.Clear();
            sectionDayLoads.Clear();
            slotLoads.Clear();

            foreach (var schedule in Schedules)
            {
                AddIndexes(schedule);
            }
        }

        private static (int ClassroomID, int SemesterNumber, string DayOfWeek, int TimeSlotID) ClassroomTimeKey(Schedule schedule)
        {
            return (schedule.ClassroomID, schedule.SemesterNumber, schedule.DayOfWeek, schedule.TimeSlotID);
        }

        private static (int FacultyMemberID, int SemesterNumber, string DayOfWeek, int TimeSlotID) FacultyTimeKey(Schedule schedule)
        {
            return (schedule.FacultyMemberID, schedule.SemesterNumber, schedule.DayOfWeek, schedule.TimeSlotID);
        }

        private static (int? StudyYearID, int? BranchID, int? SectionID, int SemesterNumber, string DayOfWeek, int TimeSlotID) SectionTimeKey(Schedule schedule)
        {
            return (schedule.StudyYearID, schedule.BranchID, schedule.SectionID, schedule.SemesterNumber, schedule.DayOfWeek, schedule.TimeSlotID);
        }

        private static (int? StudyYearID, int? BranchID, int? SectionID, int SemesterNumber, string DayOfWeek, int TimeSlotID, string GroupName) SectionGroupTimeKey(Schedule schedule)
        {
            return (schedule.StudyYearID, schedule.BranchID, schedule.SectionID, schedule.SemesterNumber, schedule.DayOfWeek, schedule.TimeSlotID, schedule.GroupName ?? string.Empty);
        }

        private static (int? StudyYearID, int? BranchID, int? SectionID, int SemesterNumber, string DayOfWeek) SectionDayKey(Schedule schedule)
        {
            return (schedule.StudyYearID, schedule.BranchID, schedule.SectionID, schedule.SemesterNumber, schedule.DayOfWeek);
        }

        private static (int SubjectID, int? SectionID, int SemesterNumber, string DayOfWeek) SubjectDayKey(Schedule schedule)
        {
            return (schedule.SubjectID, schedule.SectionID, schedule.SemesterNumber, schedule.DayOfWeek);
        }

        private static (int SubjectID, int? SectionID, int SemesterNumber, string DayOfWeek, string GroupName) SubjectGroupDayKey(Schedule schedule)
        {
            return (schedule.SubjectID, schedule.SectionID, schedule.SemesterNumber, schedule.DayOfWeek, schedule.GroupName ?? string.Empty);
        }

        private int GetPracticalPairingScore(Schedule schedule)
        {
            if (string.IsNullOrWhiteSpace(schedule.GroupName) ||
                !string.Equals(schedule.LectureType, "Practical", StringComparison.OrdinalIgnoreCase))
            {
                return 0;
            }

            var pairedPractical = Schedules
                .Where(existing =>
                    existing.SemesterNumber == schedule.SemesterNumber &&
                    existing.StudyYearID == schedule.StudyYearID &&
                    existing.BranchID == schedule.BranchID &&
                    existing.SectionID == schedule.SectionID &&
                    existing.DayOfWeek == schedule.DayOfWeek &&
                    existing.TimeSlotID == schedule.TimeSlotID &&
                    string.Equals(existing.LectureType, "Practical", StringComparison.OrdinalIgnoreCase) &&
                    !string.IsNullOrWhiteSpace(existing.GroupName) &&
                    !string.Equals(existing.GroupName, schedule.GroupName, StringComparison.OrdinalIgnoreCase));

            if (!pairedPractical.Any())
            {
                return 18;
            }

            return pairedPractical.Any(existing => existing.SubjectID != schedule.SubjectID)
                ? -220
                : 140;
        }

        private int GetCompactDayScore(Schedule schedule, int sectionDayLoad)
        {
            int usedDayCount = sectionDayLoads.Keys.Count(key =>
                key.StudyYearID == schedule.StudyYearID &&
                key.BranchID == schedule.BranchID &&
                key.SectionID == schedule.SectionID &&
                key.SemesterNumber == schedule.SemesterNumber);
            int targetDayCount = GetTargetDayCount(schedule);

            if (usedDayCount < targetDayCount)
            {
                return sectionDayLoad == 0 ? -180 : 110;
            }

            return sectionDayLoad switch
            {
                0 => usedDayCount == 0 ? 0 : 140,
                1 => -95,
                2 => -35,
                _ => 55
            };
        }

        private int GetTargetDayCount(Schedule schedule)
        {
            if (!schedule.SectionID.HasValue)
            {
                return 4;
            }

            var key = new ScheduleDayPlanKey(
                schedule.StudyYearID ?? 0,
                schedule.SectionID.Value,
                schedule.SemesterNumber);

            return dayPlans.TryGetValue(key, out var dayPlan)
                ? dayPlan.TargetDayCount
                : 4;
        }

        private static int Count<TKey>(Dictionary<TKey, int> values, TKey key)
            where TKey : notnull
        {
            return values.TryGetValue(key, out int count) ? count : 0;
        }

        private static void Increment<TKey>(Dictionary<TKey, int> values, TKey key)
            where TKey : notnull
        {
            values[key] = Count(values, key) + 1;
        }
    }
}
