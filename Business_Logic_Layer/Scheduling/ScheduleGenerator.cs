using University_Timetable_and_Classroom_Management_System.Models;

namespace University_Timetable_and_Classroom_Management_System.BusinessLayer
{
    internal static class ScheduleGenerator
    {
        private const int MaxExchangeAnchorCandidates = 12;
        private const int MaxExchangePairCandidates = 2;

        public static SchedulePlanningResult BuildPlan(
            IReadOnlyList<ScheduleGenerationRequest> requests,
            ScheduleBuildContext context)
        {
            var dayPlans = BuildScheduleDayPlans(requests, context.TimeSlots.Count);
            var requestPlans = requests
                .Select(request => new ScheduleRequestPlan(
                    request,
                    CreatePlacementCandidates(request, context.Classrooms, context.TimeSlots, dayPlans).ToList(),
                    CalculateRequestDifficulty(request)))
                .ToList();

            int noClassroomCount = requestPlans.Count(plan => plan.Candidates.Count == 0);
            var pendingPlans = requestPlans
                .Where(plan => plan.Candidates.Count > 0)
                .ToList();

            var scheduleState = new ScheduleGenerationState(dayPlans);
            int conflictCount = 0;
            var pairedRequestIds = new HashSet<ScheduleRequestIdentity>();

            PlacePracticalExchangeBlocks(
                pendingPlans,
                scheduleState,
                pairedRequestIds);

            var unresolvedPlans = new List<ScheduleRequestPlan>();

            foreach (var plan in pendingPlans
                .Where(plan => !pairedRequestIds.Contains(ScheduleRequestIdentity.From(plan.Request)))
                .OrderBy(plan => plan.Candidates.Count)
                .ThenByDescending(plan => plan.DifficultyScore)
                .ThenBy(plan => plan.Request.Subject.SemesterNumber)
                .ThenByDescending(plan => plan.Request.Subject.StudyYearID)
                .ThenBy(plan => plan.Request.Subject.SubjectName)
                .ThenBy(plan => plan.Request.Section.SectionName))
            {
                if (TryPlaceFast(plan, scheduleState, avoidSameSubjectDay: true))
                {
                    continue;
                }

                unresolvedPlans.Add(plan);
            }

            foreach (var plan in unresolvedPlans)
            {
                if (TryPlaceWithRepair(plan, pendingPlans, scheduleState, remainingDepth: 2))
                {
                    continue;
                }

                conflictCount++;
            }

            return new SchedulePlanningResult(
                scheduleState.Schedules,
                noClassroomCount,
                conflictCount);
        }

        private static void PlacePracticalExchangeBlocks(
            IReadOnlyList<ScheduleRequestPlan> pendingPlans,
            ScheduleGenerationState scheduleState,
            HashSet<ScheduleRequestIdentity> pairedRequestIds)
        {
            var practicalPlans = pendingPlans
                .Where(plan => IsGroupedPracticalRequest(plan.Request))
                .OrderBy(plan => plan.Request.Subject.SemesterNumber)
                .ThenByDescending(plan => plan.Request.Subject.StudyYearID)
                .ThenBy(plan => plan.Request.Section.SectionID)
                .ThenBy(plan => plan.Request.Subject.SubjectName)
                .ThenBy(plan => plan.Request.GroupName)
                .ToList();

            foreach (var firstPlan in practicalPlans)
            {
                var firstIdentity = ScheduleRequestIdentity.From(firstPlan.Request);

                if (pairedRequestIds.Contains(firstIdentity))
                {
                    continue;
                }

                var secondPlan = practicalPlans.FirstOrDefault(candidate =>
                    !pairedRequestIds.Contains(ScheduleRequestIdentity.From(candidate.Request)) &&
                    CanExchangePracticals(firstPlan.Request, candidate.Request));

                if (secondPlan is null)
                {
                    continue;
                }

                var thirdPlan = FindExchangeCounterpart(
                    practicalPlans,
                    firstPlan.Request,
                    secondPlan.Request.GroupName,
                    pairedRequestIds);
                var fourthPlan = FindExchangeCounterpart(
                    practicalPlans,
                    secondPlan.Request,
                    firstPlan.Request.GroupName,
                    pairedRequestIds);

                if (thirdPlan is null || fourthPlan is null)
                {
                    continue;
                }

                if (!TryPlacePracticalExchangeBlock(
                    firstPlan,
                    secondPlan,
                    thirdPlan,
                    fourthPlan,
                    scheduleState))
                {
                    continue;
                }

                pairedRequestIds.Add(firstIdentity);
                pairedRequestIds.Add(ScheduleRequestIdentity.From(secondPlan.Request));
                pairedRequestIds.Add(ScheduleRequestIdentity.From(thirdPlan.Request));
                pairedRequestIds.Add(ScheduleRequestIdentity.From(fourthPlan.Request));
            }
        }

        private static ScheduleRequestPlan? FindExchangeCounterpart(
            IEnumerable<ScheduleRequestPlan> practicalPlans,
            ScheduleGenerationRequest sourceRequest,
            string? targetGroupName,
            HashSet<ScheduleRequestIdentity> pairedRequestIds)
        {
            return practicalPlans.FirstOrDefault(plan =>
                !pairedRequestIds.Contains(ScheduleRequestIdentity.From(plan.Request)) &&
                plan.Request.Subject.SubjectID == sourceRequest.Subject.SubjectID &&
                plan.Request.Section.SectionID == sourceRequest.Section.SectionID &&
                plan.Request.LessonNumber == sourceRequest.LessonNumber &&
                string.Equals(plan.Request.LectureType, sourceRequest.LectureType, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(plan.Request.GroupName, targetGroupName, StringComparison.OrdinalIgnoreCase));
        }

        private static bool TryPlacePracticalExchangeBlock(
            ScheduleRequestPlan firstPlan,
            ScheduleRequestPlan secondPlan,
            ScheduleRequestPlan thirdPlan,
            ScheduleRequestPlan fourthPlan,
            ScheduleGenerationState scheduleState)
        {
            var firstCandidates = firstPlan.Candidates
                .Where(candidate => ScheduleRoomSelector.MatchesLectureType(candidate.Classroom, firstPlan.Request.LectureType))
                .OrderBy(candidate => ScheduleDayRules.GetDayOrder(candidate.DayOfWeek))
                .ThenBy(candidate => candidate.TimeSlot.StartTime)
                .ThenBy(candidate => candidate.Classroom.Capacity)
                .ThenBy(candidate => candidate.Classroom.ClassroomNumber)
                .Take(MaxExchangeAnchorCandidates)
                .ToList();

            foreach (var firstCandidate in firstCandidates)
            {
                foreach (var secondCandidate in GetSameSlotExchangeCandidates(firstCandidate, secondPlan.Candidates))
                {
                    if (firstCandidate.Classroom.ClassroomID == secondCandidate.Classroom.ClassroomID)
                    {
                        continue;
                    }

                    foreach (var thirdCandidate in GetNextSlotExchangeCandidates(firstCandidate, thirdPlan.Candidates))
                    {
                        foreach (var fourthCandidate in GetSameSlotExchangeCandidates(thirdCandidate, fourthPlan.Candidates))
                        {
                            if (thirdCandidate.Classroom.ClassroomID == fourthCandidate.Classroom.ClassroomID)
                            {
                                continue;
                            }

                            if (!TryBuildExchangeSchedules(
                                firstPlan,
                                secondPlan,
                                thirdPlan,
                                fourthPlan,
                                firstCandidate,
                                secondCandidate,
                                thirdCandidate,
                                fourthCandidate,
                                scheduleState,
                                out var schedules))
                            {
                                continue;
                            }

                            foreach (var schedule in schedules)
                            {
                                scheduleState.Add(schedule);
                            }

                            return true;
                        }
                    }
                }
            }

            return false;
        }

        private static IEnumerable<SchedulePlacementCandidate> GetSameSlotExchangeCandidates(
            SchedulePlacementCandidate firstCandidate,
            IEnumerable<SchedulePlacementCandidate> secondCandidates)
        {
            return secondCandidates
                .Where(candidate =>
                    candidate.DayOfWeek == firstCandidate.DayOfWeek &&
                    candidate.TimeSlot.StartTime == firstCandidate.TimeSlot.StartTime &&
                    candidate.TimeSlot.TimeSlotID == firstCandidate.TimeSlot.TimeSlotID)
                .OrderBy(candidate => candidate.Classroom.Capacity)
                .ThenBy(candidate => candidate.Classroom.ClassroomNumber)
                .Take(MaxExchangePairCandidates);
        }

        private static IEnumerable<SchedulePlacementCandidate> GetNextSlotExchangeCandidates(
            SchedulePlacementCandidate firstCandidate,
            IEnumerable<SchedulePlacementCandidate> nextCandidates)
        {
            var laterCandidates = nextCandidates
                .Where(candidate =>
                    candidate.DayOfWeek == firstCandidate.DayOfWeek &&
                    candidate.TimeSlot.StartTime > firstCandidate.TimeSlot.StartTime)
                .ToList();

            if (laterCandidates.Count == 0)
            {
                return [];
            }

            var nextStart = laterCandidates.Min(candidate => candidate.TimeSlot.StartTime);

            return laterCandidates
                .Where(candidate => candidate.TimeSlot.StartTime == nextStart)
                .OrderBy(candidate => candidate.Classroom.Capacity)
                .ThenBy(candidate => candidate.Classroom.ClassroomNumber)
                .Take(MaxExchangePairCandidates);
        }

        private static bool TryBuildExchangeSchedules(
            ScheduleRequestPlan firstPlan,
            ScheduleRequestPlan secondPlan,
            ScheduleRequestPlan thirdPlan,
            ScheduleRequestPlan fourthPlan,
            SchedulePlacementCandidate firstCandidate,
            SchedulePlacementCandidate secondCandidate,
            SchedulePlacementCandidate thirdCandidate,
            SchedulePlacementCandidate fourthCandidate,
            ScheduleGenerationState scheduleState,
            out List<Schedule> schedules)
        {
            schedules = [];

            foreach (int firstFacultyId in scheduleState.GetOrderedFacultyOptions(
                firstPlan.Request.FacultyMemberIds,
                firstPlan.Request.Subject.SemesterNumber,
                firstCandidate.DayOfWeek))
            {
                foreach (int secondFacultyId in scheduleState.GetOrderedFacultyOptions(
                    secondPlan.Request.FacultyMemberIds,
                    secondPlan.Request.Subject.SemesterNumber,
                    secondCandidate.DayOfWeek))
                {
                    foreach (int thirdFacultyId in scheduleState.GetOrderedFacultyOptions(
                        thirdPlan.Request.FacultyMemberIds,
                        thirdPlan.Request.Subject.SemesterNumber,
                        thirdCandidate.DayOfWeek))
                    {
                        foreach (int fourthFacultyId in scheduleState.GetOrderedFacultyOptions(
                            fourthPlan.Request.FacultyMemberIds,
                            fourthPlan.Request.Subject.SemesterNumber,
                            fourthCandidate.DayOfWeek))
                        {
                            var proposedSchedules = new List<Schedule>
                            {
                                BuildSchedule(firstPlan.Request, firstCandidate, firstFacultyId),
                                BuildSchedule(secondPlan.Request, secondCandidate, secondFacultyId),
                                BuildSchedule(thirdPlan.Request, thirdCandidate, thirdFacultyId),
                                BuildSchedule(fourthPlan.Request, fourthCandidate, fourthFacultyId)
                            };

                            if (proposedSchedules.Any(schedule => !scheduleState.CanPlace(schedule)) ||
                                ScheduleConflictChecker.HasAnyPairConflict(proposedSchedules))
                            {
                                continue;
                            }

                            schedules = proposedSchedules;
                            return true;
                        }
                    }
                }
            }

            return false;
        }

        private static bool IsGroupedPracticalRequest(ScheduleGenerationRequest request)
        {
            return string.Equals(request.LectureType, "Practical", StringComparison.OrdinalIgnoreCase) &&
                !string.IsNullOrWhiteSpace(request.GroupName);
        }

        private static bool CanExchangePracticals(
            ScheduleGenerationRequest first,
            ScheduleGenerationRequest second)
        {
            return first.Subject.SubjectID != second.Subject.SubjectID &&
                first.Subject.SemesterNumber == second.Subject.SemesterNumber &&
                first.Section.SectionID == second.Section.SectionID &&
                string.Equals(first.LectureType, second.LectureType, StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(first.GroupName, second.GroupName, StringComparison.OrdinalIgnoreCase);
        }

        private static bool TryPlaceFast(
            ScheduleRequestPlan plan,
            ScheduleGenerationState scheduleState,
            bool avoidSameSubjectDay)
        {
            foreach (var placement in CreateOrderedPlacements(plan, scheduleState, avoidSameSubjectDay))
            {
                if (!scheduleState.CanPlace(placement.Schedule))
                {
                    continue;
                }

                scheduleState.Add(placement.Schedule);
                return true;
            }

            return false;
        }

        private static bool TryPlaceWithRepair(
            ScheduleRequestPlan plan,
            IReadOnlyList<ScheduleRequestPlan> allPlans,
            ScheduleGenerationState scheduleState,
            int remainingDepth)
        {
            foreach (var placement in CreateOrderedPlacements(plan, scheduleState, avoidSameSubjectDay: true))
            {
                if (scheduleState.CanPlace(placement.Schedule))
                {
                    scheduleState.Add(placement.Schedule);
                    return true;
                }

                if (remainingDepth <= 0)
                {
                    continue;
                }

                var conflictingSchedules = scheduleState.GetConflictingSchedules(placement.Schedule)
                    .Take(2)
                    .ToList();

                if (conflictingSchedules.Count == 0 ||
                    scheduleState.GetConflictingSchedules(placement.Schedule).Skip(2).Any())
                {
                    continue;
                }

                var conflictingPlans = conflictingSchedules
                    .Select(schedule => FindPlanForSchedule(allPlans, schedule))
                    .ToList();

                if (conflictingPlans.Any(conflictingPlan => conflictingPlan is null))
                {
                    continue;
                }

                var snapshot = scheduleState.Schedules.ToList();

                scheduleState.RemoveRange(conflictingSchedules);

                if (!scheduleState.CanPlace(placement.Schedule))
                {
                    scheduleState.ResetTo(snapshot);
                    continue;
                }

                scheduleState.Add(placement.Schedule);

                bool replacedConflicts = true;
                foreach (var conflictingPlan in conflictingPlans
                    .OfType<ScheduleRequestPlan>()
                    .OrderBy(item => item.Candidates.Count)
                    .ThenByDescending(item => item.DifficultyScore))
                {
                    if (TryPlaceFast(conflictingPlan, scheduleState, avoidSameSubjectDay: true) ||
                        TryPlaceWithRepair(conflictingPlan, allPlans, scheduleState, remainingDepth - 1))
                    {
                        continue;
                    }

                    replacedConflicts = false;
                    break;
                }

                if (replacedConflicts)
                {
                    return true;
                }

                scheduleState.ResetTo(snapshot);
            }

            return false;
        }

        private static IEnumerable<SchedulePlacementOption> CreateOrderedPlacements(
            ScheduleRequestPlan plan,
            ScheduleGenerationState scheduleState,
            bool avoidSameSubjectDay)
        {
            return plan.Candidates
                .SelectMany(candidate => scheduleState.GetOrderedFacultyOptions(
                        plan.Request.FacultyMemberIds,
                        plan.Request.Subject.SemesterNumber,
                        candidate.DayOfWeek)
                    .Select(facultyMemberId => new
                    {
                        Candidate = candidate,
                        Schedule = BuildSchedule(plan.Request, candidate, facultyMemberId)
                    }))
                .Where(placement => !avoidSameSubjectDay ||
                    !scheduleState.HasSameSubjectSectionOnDay(placement.Schedule))
                .Select(placement => new SchedulePlacementOption(
                    placement.Schedule,
                    scheduleState.ScorePlacement(placement.Schedule, placement.Candidate)))
                .OrderBy(placement => placement.Score)
                .ThenBy(placement => ScheduleDayRules.GetDayOrder(placement.Schedule.DayOfWeek))
                .ThenBy(placement => placement.Schedule.TimeSlotID)
                .ThenBy(placement => placement.Schedule.ClassroomID)
                .ThenBy(placement => placement.Schedule.FacultyMemberID);
        }

        private static Dictionary<ScheduleDayPlanKey, ScheduleDayPlan> BuildScheduleDayPlans(
            IReadOnlyList<ScheduleGenerationRequest> requests,
            int timeSlotCount)
        {
            int slotsPerDay = Math.Max(1, timeSlotCount);
            int fourDayCapacity = slotsPerDay * 4;

            return requests
                .GroupBy(CreateScheduleDayPlanKey)
                .ToDictionary(
                    group => group.Key,
                    group =>
                    {
                        int requiredBlocks = CountRequiredScheduleBlocks(group);
                        int dayCount = requiredBlocks <= fourDayCapacity ? 4 : 5;
                        var days = ScheduleDayRules.GetSchedulingDays(group.Key.StudyYearID, dayCount);

                        return new ScheduleDayPlan(days, Math.Min(dayCount, requiredBlocks));
                    });
        }

        private static int CountRequiredScheduleBlocks(IEnumerable<ScheduleGenerationRequest> requests)
        {
            var requestList = requests.ToList();
            int regularBlocks = requestList.Count(request => !IsGroupedPracticalRequest(request));
            int groupedPracticalBlocks = requestList
                .Where(IsGroupedPracticalRequest)
                .GroupBy(request => new
                {
                    request.Subject.SubjectID,
                    request.Subject.SemesterNumber,
                    request.Section.SectionID,
                    request.LessonNumber
                })
                .Sum(group => (int)Math.Ceiling(
                    group.Select(request => request.GroupName)
                        .Where(groupName => !string.IsNullOrWhiteSpace(groupName))
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .Count() / 2.0));

            return regularBlocks + groupedPracticalBlocks;
        }

        private static ScheduleDayPlanKey CreateScheduleDayPlanKey(ScheduleGenerationRequest request)
        {
            return new ScheduleDayPlanKey(
                request.Subject.StudyYearID,
                request.Section.SectionID,
                request.Subject.SemesterNumber);
        }

        private static ScheduleRequestPlan? FindPlanForSchedule(
            IEnumerable<ScheduleRequestPlan> plans,
            Schedule schedule)
        {
            return plans.FirstOrDefault(plan =>
                plan.Request.Subject.SubjectID == schedule.SubjectID &&
                plan.Request.Section.SectionID == schedule.SectionID &&
                plan.Request.Subject.SemesterNumber == schedule.SemesterNumber &&
                string.Equals(plan.Request.LectureType, schedule.LectureType, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(plan.Request.GroupName ?? string.Empty, schedule.GroupName ?? string.Empty, StringComparison.OrdinalIgnoreCase));
        }

        private static IEnumerable<SchedulePlacementCandidate> CreatePlacementCandidates(
            ScheduleGenerationRequest request,
            IReadOnlyCollection<Classroom> classrooms,
            IReadOnlyCollection<TimeSlot> timeSlots,
            IReadOnlyDictionary<ScheduleDayPlanKey, ScheduleDayPlan> dayPlans)
        {
            int requiredCapacity = ScheduleRoomSelector.GetRequiredCapacity(request.Section, request.GroupName);
            var targetClassrooms = ScheduleRoomSelector.GetTargetClassrooms(
                classrooms,
                requiredCapacity,
                request.LectureType);
            var days = dayPlans.TryGetValue(CreateScheduleDayPlanKey(request), out var plannedDays)
                ? plannedDays.Days
                : ScheduleDayRules.GetSchedulingDays(request.Subject.StudyYearID);

            foreach (string day in days)
            {
                foreach (var timeSlot in timeSlots)
                {
                    foreach (var classroom in targetClassrooms)
                    {
                        yield return new SchedulePlacementCandidate(
                            day,
                            timeSlot,
                            classroom,
                            requiredCapacity);
                    }
                }
            }
        }

        private static Schedule BuildSchedule(
            ScheduleGenerationRequest request,
            SchedulePlacementCandidate candidate,
            int facultyMemberId)
        {
            var subject = request.Subject;
            var section = request.Section;

            return new Schedule
            {
                SubjectID = subject.SubjectID,
                FacultyMemberID = facultyMemberId,
                ClassroomID = candidate.Classroom.ClassroomID,
                TimeSlotID = candidate.TimeSlot.TimeSlotID,
                SemesterNumber = subject.SemesterNumber,
                LectureType = request.LectureType,
                GroupName = request.GroupName,
                DayOfWeek = candidate.DayOfWeek,
                StudyYearID = subject.StudyYearID,
                BranchID = subject.BranchID ?? section.BranchID,
                SectionID = section.SectionID
            };
        }

        private static int CalculateRequestDifficulty(ScheduleGenerationRequest request)
        {
            int difficulty = ScheduleRoomSelector.GetRequiredCapacity(request.Section, request.GroupName);

            if (string.Equals(request.LectureType, "Practical", StringComparison.OrdinalIgnoreCase))
            {
                difficulty += 40;
            }

            if (!string.IsNullOrWhiteSpace(request.GroupName))
            {
                difficulty += 20;
            }

            difficulty += request.Subject.StudyYearID * 10;
            difficulty += Math.Max(0, 6 - request.FacultyMemberIds.Count) * 8;

            return difficulty;
        }

        public static IEnumerable<ScheduleGenerationRequest> CreateScheduleRequests(
            SubjectTeachingAssignment teachingAssignment,
            Section section)
        {
            var subject = teachingAssignment.Subject;
            int theoryLessons = CalculateTheoryLessonCount(subject);

            foreach (int lessonNumber in Enumerable.Range(1, theoryLessons))
            {
                yield return new ScheduleGenerationRequest(
                    subject,
                    teachingAssignment.FacultyMemberIds,
                    section,
                    lessonNumber,
                    theoryLessons,
                    "Theory",
                    null);
            }

            int practicalLessons = CalculatePracticalLessonCount(subject);

            if (practicalLessons <= 0)
            {
                yield break;
            }

            var practicalGroups = GetPracticalGroupsForSection(section);

            if (practicalGroups.Count == 0)
            {
                foreach (int lessonNumber in Enumerable.Range(1, practicalLessons))
                {
                    yield return new ScheduleGenerationRequest(
                        subject,
                        teachingAssignment.FacultyMemberIds,
                        section,
                        lessonNumber,
                        practicalLessons,
                        "Practical",
                        null);
                }

                yield break;
            }

            foreach (string groupName in practicalGroups)
            {
                foreach (int lessonNumber in Enumerable.Range(1, practicalLessons))
                {
                    yield return new ScheduleGenerationRequest(
                        subject,
                        teachingAssignment.FacultyMemberIds,
                        section,
                        lessonNumber,
                        practicalLessons,
                        "Practical",
                        groupName);
                }
            }
        }

        private static int CalculateTheoryLessonCount(Subject subject)
        {
            if (subject.TheoreticalHours > 0)
            {
                return 1;
            }

            if (subject.PracticalHours > 0)
            {
                return 0;
            }

            var fallbackHours = subject.CreditUnits > 0 ? subject.CreditUnits : 1;
            return 1;
        }

        private static int CalculatePracticalLessonCount(Subject subject)
        {
            return subject.PracticalHours > 0 ? 1 : 0;
        }

        private static IReadOnlyList<string> GetPracticalGroupsForSection(Section section)
        {
            return AcademicStructureRules.GetAllowedPracticalGroupNames(section.SectionName);
        }

    }
}
