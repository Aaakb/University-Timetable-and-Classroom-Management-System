using University_Timetable_and_Classroom_Management_System.Models;

namespace University_Timetable_and_Classroom_Management_System.BusinessLayer
{
    internal static class ScheduleGenerator
    {
        public static SchedulePlanningResult BuildPlan(
            IReadOnlyList<ScheduleGenerationRequest> requests,
            ScheduleBuildContext context)
        {
            context.CancellationToken.ThrowIfCancellationRequested();
            var dayPlans = BuildScheduleDayPlans(requests, context.TimeSlots.Count);
            if (context.Preferences.Count > 0 || context.UseAllTeachingDays && context.FixedSchedules.Count > 0)
            {
                // Pending requests alone do not describe the occupied days of an
                // imported timetable. Broaden candidates only on explicit retry;
                // the original practical exchange/placement algorithms stay intact.
                dayPlans = dayPlans.ToDictionary(entry => entry.Key, entry => entry.Value with
                {
                    Days = ScheduleDayRules.AllDays,
                    TargetDayCount = context.FixedSchedules.Count == 0 ? entry.Value.TargetDayCount : Math.Min(ScheduleDayRules.AllDays.Count,
                        context.FixedSchedules.Where(s => s.SectionID == entry.Key.SectionID &&
                            s.SemesterNumber == entry.Key.SemesterNumber).Select(s => s.DayOfWeek).Distinct().Count() + 1)
                });
            }
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

            var scheduleState = new ScheduleGenerationState(dayPlans, context);
            foreach (var profile in context.Preferences.Values)
            {
                FacultyPreferenceRules.ValidateMatrix(profile, context.TimeSlots);
                if (FacultyPreferenceRules.Violation(profile, context.FixedSchedules, context.TimeSlots.ToDictionary(p => p.TimeSlotID)) is { } error)
                    throw new ArgumentException($"تفضيلات التدريسي #{profile.FacultyId}: {error}. أعد تنظيم الجدول الموجود أولًا.");
            }
            scheduleState.SeedFixed(context.FixedSchedules);
            int conflictCount = 0;
            var pairedRequestIds = new HashSet<ScheduleRequestIdentity>();

            // A partial import may contain only one member of a practical pair.
            // Reuse the normal placement/repair path for remaining group sessions.
            bool completePartialGroups = context.FixedSchedules.Count > 0 && !context.RequirePracticalExchange;
            var exchangeIssues = completePartialGroups ? new List<string>() : PlacePracticalExchangeBlocks(
                requestPlans,
                scheduleState,
                pairedRequestIds,
                context.TimeSlots,
                context.RequirePracticalExchange);
            if (!completePartialGroups) conflictCount += pendingPlans.Count(plan => IsPracticalRequest(plan.Request) &&
                !pairedRequestIds.Contains(ScheduleRequestIdentity.From(plan.Request)));

            var unresolvedPlans = new List<ScheduleRequestPlan>();

            foreach (var plan in pendingPlans
                // Practical requests are placed only as complete exchange blocks.
                .Where(plan => !IsPracticalRequest(plan.Request) || completePartialGroups)
                .OrderBy(plan => plan.Candidates.Count)
                .ThenByDescending(plan => plan.DifficultyScore)
                .ThenBy(plan => plan.Request.Subject.SemesterNumber)
                .ThenByDescending(plan => plan.Request.Subject.StudyYearID)
                .ThenBy(plan => plan.Request.Subject.SubjectName)
                .ThenBy(plan => plan.Request.Section.SectionName))
            {
                context.CancellationToken.ThrowIfCancellationRequested();
                if (TryPlaceFast(plan, scheduleState, avoidSameSubjectDay: true))
                {
                    continue;
                }

                unresolvedPlans.Add(plan);
            }

            foreach (var plan in unresolvedPlans)
            {
                context.CancellationToken.ThrowIfCancellationRequested();
                bool placed = false;
                foreach (int depth in new[] { 1, 2, 3, 4, 6 })
                {
                    if (!scheduleState.TryRepairStep()) break;
                    var snapshot = scheduleState.Schedules.ToList();
                    if (TryPlaceWithRepair(plan, pendingPlans, scheduleState, depth, context.RequirePracticalExchange))
                    { placed = true; break; }
                    scheduleState.ResetTo(snapshot);
                }
                if (placed)
                {
                    continue;
                }

                conflictCount++;
            }

            return new SchedulePlanningResult(
                scheduleState.Schedules,
                noClassroomCount,
                conflictCount) { PracticalExchangeIssues = exchangeIssues };
        }

        private static List<string> PlacePracticalExchangeBlocks(
            IReadOnlyList<ScheduleRequestPlan> pendingPlans,
            ScheduleGenerationState scheduleState,
            HashSet<ScheduleRequestIdentity> pairedRequestIds,
            IReadOnlyCollection<TimeSlot> timeSlots,
            bool requirePracticalExchange)
        {
            var issues = new List<string>();
            var practicalPlans = pendingPlans
                .Where(plan => IsPracticalRequest(plan.Request))
                .OrderBy(plan => plan.Request.Subject.SemesterNumber)
                .ThenByDescending(plan => plan.Request.Subject.StudyYearID)
                .ThenBy(plan => plan.Request.Section.SectionID)
                .ThenBy(plan => plan.Request.Subject.SubjectName)
                .ThenBy(plan => plan.Request.GroupName)
                .ToList();

            foreach (var scope in practicalPlans.GroupBy(plan => new
                {
                    plan.Request.Section.SectionID,
                    plan.Request.Subject.SemesterNumber,
                    plan.Request.LessonNumber
                }))
            {
                var plans = scope.OrderBy(plan => plan.Candidates.Count)
                    .ThenBy(plan => plan.Request.FacultyMemberIds.Count)
                    .ThenBy(plan => plan.Request.Subject.SubjectName).ToList();
                var section = plans[0].Request.Section;
                var groups = AcademicStructureRules.GetAllowedPracticalGroupNames(section.SectionName);
                string label = $"Section {section.SectionName} (ID {section.SectionID}), semester {scope.Key.SemesterNumber}";
                if (groups.Count != 2 || plans.Any(plan =>
                    !groups.Contains(plan.Request.GroupName ?? string.Empty, StringComparer.OrdinalIgnoreCase)) ||
                    plans.GroupBy(plan => plan.Request.Subject.SubjectID).Any(subject =>
                        subject.Count() != 2 || subject.Select(p => p.Request.GroupName)
                            .Distinct(StringComparer.OrdinalIgnoreCase).Count() != 2))
                {
                    issues.Add($"{label}: each practical subject needs both groups of the section.");
                    continue;
                }

                foreach (var firstPlan in plans.Where(plan => string.Equals(
                    plan.Request.GroupName, groups[0], StringComparison.OrdinalIgnoreCase)))
                {
                    var firstIdentity = ScheduleRequestIdentity.From(firstPlan.Request);
                    if (pairedRequestIds.Contains(firstIdentity)) continue;
                    foreach (var secondPlan in plans.Where(candidate =>
                        !pairedRequestIds.Contains(ScheduleRequestIdentity.From(candidate.Request)) &&
                        CanExchangePracticals(firstPlan.Request, candidate.Request)))
                    {
                        var thirdPlan = FindExchangeCounterpart(plans, firstPlan.Request,
                            secondPlan.Request.GroupName, pairedRequestIds);
                        var fourthPlan = FindExchangeCounterpart(plans, secondPlan.Request,
                            firstPlan.Request.GroupName, pairedRequestIds);
                        if (thirdPlan is null || fourthPlan is null ||
                            !TryPlacePracticalExchangeBlock(firstPlan, secondPlan, thirdPlan, fourthPlan,
                                scheduleState, timeSlots)) continue;

                        foreach (var placed in new[] { firstPlan, secondPlan, thirdPlan, fourthPlan })
                            pairedRequestIds.Add(ScheduleRequestIdentity.From(placed.Request));
                        break;
                    }
                }

                var remaining = plans.Where(plan => !pairedRequestIds.Contains(ScheduleRequestIdentity.From(plan.Request))).ToList();
                if (plans.Select(plan => plan.Request.Subject.SubjectID).Distinct().Count() % 2 == 1 &&
                    remaining.Count == 2 && remaining.Select(plan => plan.Request.Subject.SubjectID).Distinct().Count() == 1)
                {
                    var firstGroup = remaining.Single(plan => string.Equals(plan.Request.GroupName, groups[0], StringComparison.OrdinalIgnoreCase));
                    var secondGroup = remaining.Single(plan => string.Equals(plan.Request.GroupName, groups[1], StringComparison.OrdinalIgnoreCase));
                    if (TryPlaceSinglePracticalSubject(firstGroup, secondGroup, scheduleState))
                        foreach (var placed in remaining)
                            pairedRequestIds.Add(ScheduleRequestIdentity.From(placed.Request));
                }
                if (!requirePracticalExchange)
                {
                    // Prefer complete exchanges, but both groups may use other free periods.
                    foreach (var subject in plans
                        .Where(plan => !pairedRequestIds.Contains(ScheduleRequestIdentity.From(plan.Request)))
                        .GroupBy(plan => plan.Request.Subject.SubjectID))
                    {
                        var pair = subject.ToList();
                        if (pair.Count == 2 && TryPlaceSinglePracticalSubject(pair[0], pair[1], scheduleState))
                            foreach (var placed in pair)
                                pairedRequestIds.Add(ScheduleRequestIdentity.From(placed.Request));
                    }
                }
                var unplaced = plans.Where(plan => !pairedRequestIds.Contains(ScheduleRequestIdentity.From(plan.Request)))
                    .Select(plan => plan.Request.Subject.SubjectName).Distinct().ToArray();
                if (unplaced.Length > 0)
                    issues.Add(requirePracticalExchange
                        ? $"{label}: no complete exchange for {string.Join(", ", unplaced)}. " +
                          "Paired subjects need two suitable labs, two different teachers and consecutive periods on one day. An odd remaining subject needs two separate available group sessions."
                        : $"{label}: no available sessions for both groups of {string.Join(", ", unplaced)}.");
            }
            return issues;
        }

        private static bool TryPlaceSinglePracticalSubject(
            ScheduleRequestPlan firstGroup,
            ScheduleRequestPlan secondGroup,
            ScheduleGenerationState scheduleState)
        {
            foreach (var first in CreateOrderedPlacements(firstGroup, scheduleState, avoidSameSubjectDay: true))
            {
                if (!scheduleState.CanPlace(first.Schedule)) continue;
                foreach (var second in CreateOrderedPlacements(secondGroup, scheduleState, avoidSameSubjectDay: true))
                {
                    if (!scheduleState.CanPlaceTogether([first.Schedule, second.Schedule])) continue;
                    // Both groups must fit, but may use different days, periods, labs or assigned teachers.
                    scheduleState.Add(first.Schedule);
                    scheduleState.Add(second.Schedule);
                    return true;
                }
            }
            return false;
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
                plan.Request.Subject.SemesterNumber == sourceRequest.Subject.SemesterNumber &&
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
            ScheduleGenerationState scheduleState,
            IReadOnlyCollection<TimeSlot> timeSlots)
        {
            var firstCandidates = firstPlan.Candidates
                .Where(candidate => ScheduleRoomSelector.MatchesLectureType(candidate.Classroom, firstPlan.Request.LectureType))
                .OrderBy(candidate => scheduleState.VariationOrder(candidate.DayOfWeek, candidate.TimeSlot.TimeSlotID, candidate.Classroom.ClassroomID))
                .ThenBy(candidate => ScheduleDayRules.GetDayOrder(candidate.DayOfWeek))
                .ThenBy(candidate => candidate.TimeSlot.StartTime)
                .ThenBy(candidate => candidate.Classroom.Capacity)
                .ThenBy(candidate => candidate.Classroom.ClassroomNumber)
                .ToList();

            foreach (var firstCandidate in firstCandidates)
            {
                foreach (var secondCandidate in GetSameSlotExchangeCandidates(firstCandidate, secondPlan.Candidates))
                {
                    if (firstCandidate.Classroom.ClassroomID == secondCandidate.Classroom.ClassroomID)
                    {
                        continue;
                    }

                    foreach (var thirdCandidate in GetNextSlotExchangeCandidates(firstCandidate, thirdPlan.Candidates, timeSlots)
                        .Where(candidate => candidate.Classroom.ClassroomID == firstCandidate.Classroom.ClassroomID))
                    {
                        foreach (var fourthCandidate in GetSameSlotExchangeCandidates(thirdCandidate, fourthPlan.Candidates)
                            .Where(candidate => candidate.Classroom.ClassroomID == secondCandidate.Classroom.ClassroomID))
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
                .ThenBy(candidate => candidate.Classroom.ClassroomNumber);
        }

        private static IEnumerable<SchedulePlacementCandidate> GetNextSlotExchangeCandidates(
            SchedulePlacementCandidate firstCandidate,
            IEnumerable<SchedulePlacementCandidate> nextCandidates,
            IReadOnlyCollection<TimeSlot> timeSlots)
        {
            var nextPeriod = timeSlots.Where(slot => !slot.IsBreak && slot.StartTime > firstCandidate.TimeSlot.StartTime)
                .OrderBy(slot => slot.StartTime).FirstOrDefault();
            if (nextPeriod is null)
            {
                return [];
            }
            return nextCandidates
                .Where(candidate => candidate.DayOfWeek == firstCandidate.DayOfWeek &&
                    candidate.TimeSlot.TimeSlotID == nextPeriod.TimeSlotID)
                .OrderBy(candidate => candidate.Classroom.Capacity)
                .ThenBy(candidate => candidate.Classroom.ClassroomNumber);
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
                if (!thirdPlan.Request.FacultyMemberIds.Contains(firstFacultyId)) continue;
                foreach (int secondFacultyId in scheduleState.GetOrderedFacultyOptions(
                    secondPlan.Request.FacultyMemberIds,
                    secondPlan.Request.Subject.SemesterNumber,
                    secondCandidate.DayOfWeek))
                {
                    if (firstFacultyId == secondFacultyId || !fourthPlan.Request.FacultyMemberIds.Contains(secondFacultyId)) continue;
                    var proposedSchedules = new List<Schedule>
                    {
                        BuildSchedule(firstPlan.Request, firstCandidate, firstFacultyId),
                        BuildSchedule(secondPlan.Request, secondCandidate, secondFacultyId),
                        BuildSchedule(thirdPlan.Request, thirdCandidate, firstFacultyId),
                        BuildSchedule(fourthPlan.Request, fourthCandidate, secondFacultyId)
                    };

                    if (!scheduleState.CanPlaceTogether(proposedSchedules))
                    {
                        continue;
                    }

                    schedules = proposedSchedules;
                    return true;
                }
            }

            return false;
        }

        private static bool IsGroupedPracticalRequest(ScheduleGenerationRequest request)
        {
            return IsPracticalRequest(request) &&
                !string.IsNullOrWhiteSpace(request.GroupName);
        }

        private static bool IsPracticalRequest(ScheduleGenerationRequest request) =>
            string.Equals(request.LectureType, "Practical", StringComparison.OrdinalIgnoreCase);

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
            int remainingDepth,
            bool preservePracticalExchange)
        {
            foreach (var placement in CreateOrderedPlacements(plan, scheduleState, avoidSameSubjectDay: true))
            {
                if (!scheduleState.TryRepairStep()) return false;
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

                if (conflictingSchedules.Any(scheduleState.IsFixed)) continue;

                if (conflictingSchedules.Count == 0 ||
                    scheduleState.GetConflictingSchedules(placement.Schedule).Skip(2).Any())
                {
                    continue;
                }

                // Strict mode protects exchange blocks; flexible mode can move either group.
                if (preservePracticalExchange && conflictingSchedules.Any(schedule => string.Equals(schedule.LectureType,
                    "Practical", StringComparison.OrdinalIgnoreCase))) continue;

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
                        TryPlaceWithRepair(conflictingPlan, allPlans, scheduleState, remainingDepth - 1, preservePracticalExchange))
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
                .ThenBy(placement => scheduleState.VariationOrder(placement.Schedule.DayOfWeek, placement.Schedule.TimeSlotID,
                    placement.Schedule.ClassroomID, placement.Schedule.FacultyMemberID))
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
            SubjectLectureRules.Validate(subject);
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
            return subject.EffectiveTheoreticalLectureCount;
        }

        private static int CalculatePracticalLessonCount(Subject subject)
        {
            return subject.EffectivePracticalLectureCount;
        }

        private static IReadOnlyList<string> GetPracticalGroupsForSection(Section section)
        {
            return AcademicStructureRules.GetAllowedPracticalGroupNames(section.SectionName);
        }

    }
}
