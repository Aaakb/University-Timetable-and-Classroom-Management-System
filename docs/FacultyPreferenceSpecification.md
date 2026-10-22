# Faculty preference scheduling — revised implementation specification

This document is a proposed implementation specification. Editing or reviewing this document does not authorize changes to application code or the database. Implement it only when the project owner explicitly requests implementation.

You are a senior C# software architect, constraint optimization engineer, and university timetabling specialist.

The project is a University Timetable Management System built with C#, SQL Server, and a layered architecture. Inspect the actual implementation before selecting an optimization approach. Reuse existing scheduling, validation, and optimization components wherever possible.

## 1. Single administrator

- One administrator operates the system and enters, updates, and reviews preferences for every faculty member.
- Do not introduce faculty self-service accounts, faculty login screens, or a preference approval workflow involving faculty accounts.
- The administrator selects the faculty member and academic term before editing preferences.
- Store each faculty member's preferences independently and scope them to the project's academic-term identifiers. Inspect whether academic year and semester are represented separately; do not accidentally reuse one year's preferences in another year.
- Saving preferences does not automatically move lectures or regenerate a published timetable.

## 2. Inspect the existing architecture

Inspect the complete scheduling flow, particularly:

- ScheduleGenerator.cs and ScheduleGenerationState.cs.
- ScheduleService.cs and ScheduleConflictChecker.cs.
- ScheduleFacultySelector.cs and ScheduleRoomSelector.cs.
- Faculty, lecture, section, group, room, and time-slot entities.
- Database relationships, Entity Framework configuration, and schema upgrade mechanisms.
- Theoretical and practical scheduling rules, including exchange blocks where required.
- Existing hard constraints, soft objectives, search limits, and locked assignments.
- Generation, import repair, rescheduling, validation, and persistence entry points.
- Existing Windows Forms and web interfaces, where present.
- Existing preference handling and CP-SAT or other optimization components.

Do not add a second competing scheduler or duplicate constraint implementations. Do not replace a working algorithm without a specific technical justification. Preserve the layered architecture and existing features.

## 3. Exactly three preference levels

Support only these persisted and user-facing preference levels:

| Code | Arabic label | Symbol | Meaning |
| --- | --- | --- | --- |
| StronglyPreferred | مفضّل جداً | ⭐ | Allowed; best preference quality |
| Preferred | مفضّل | ✅ | Allowed; lower preference quality than StronglyPreferred |
| Prohibited | مرفوض | 🚫 | Mandatory exclusion; scheduling is forbidden |

Do not add Neutral, Discouraged, StronglyDiscouraged, ordinary, available, or reserve as additional preference levels.

An absent profile means preferences have not been configured. It is a configuration state, not a fourth preference level. Preserve existing scheduling behavior for faculty without a configured profile and clearly report their preferences as not entered.

For a new profile, the administrator may explicitly initialize permitted attendance days to Preferred and all other days to Prohibited, review the matrix, and then save it. Do not silently activate or backfill preferences for existing faculty. A saved active profile must resolve every relevant day/period cell to one of the three levels; no implicit Neutral fallback is permitted.

## 4. Compact attendance in the same two or three days

This is a central requirement, separate from the degree of preference:

- The administrator selects an attendance plan of two or three specific teaching days for the faculty member.
- The chosen attendance-day set is stored independently from individual preference levels.
- All lectures for that faculty member in the selected term must fit inside this day set.
- Days outside the selected set are unavailable for this profile. They cannot become usable merely because an individual period is marked preferred.
- For an existing timetable, retain the same actual attendance days during preference-only optimization by default. Do not add, remove, or substitute an attendance day merely to improve the preference score.
- Changing a period from StronglyPreferred to Preferred, or the reverse, changes its relative desirability; it does not grant permission to spread lectures across additional days.
- Changing a period to Prohibited removes that placement from the allowed candidates. Search for another valid placement within the same attendance days.
- The administrator must explicitly edit the attendance-day plan before the optimizer may move the faculty member to another day or change from two days to three days.
- For initial generation, use the selected attendance-day plan. If the requested number of attended days cannot be achieved because there are too few lectures or insufficient valid placements, explain that conflict and let the administrator revise the plan. Do not fabricate lectures or silently change the requested attendance count.
- Keep the timetable unchanged if a complete valid solution cannot be found under the fixed attendance-day plan.
- Distinguish proven infeasibility from failure to find a solution within the search limits.

Within the same attendance days, prefer compact lecture blocks and reduce avoidable internal gaps while preserving mandatory breaks, duration, practical rules, and student and room availability. Compactness must not cause overlapping lectures or elimination of required breaks.

Example:

The administrator fixes Sunday and Wednesday as the faculty member's two attendance days. Sunday morning initially contains StronglyPreferred periods. If the administrator changes these cells to Preferred and marks later periods StronglyPreferred, the optimizer may rearrange lectures within Sunday and Wednesday. It must not silently introduce Monday or Thursday. If a newly prohibited period makes this attendance plan infeasible, report it and propose an explicit plan change for review, without applying it automatically.

## 5. Whole-day and individual-period editing

- Support whole-day preference editing and individual day/period cell editing.
- A whole-day edit provides an explicit bulk operation for the cells in that day, with a clear warning if it replaces individual overrides.
- Individual-period edits may override the inherited whole-day soft preference inside an allowed attendance day.
- A prohibited whole day is a mandatory exclusion. The administrator must explicitly change its day-level prohibition before assigning allowed periods within it.
- The separate attendance-day restriction always applies; a period edit cannot bypass it.
- Show the effective value of every cell and make any exception visible. Do not maintain hidden contradictory rules.
- Use existing configured teaching days and TimeSlot IDs, start times, end times, break definitions, and duration rules. Do not hardcode the example times below or create new periods solely to match a mock-up.

## 6. Arabic administrator interface

Use the project's existing UI technology and visual style. The interface must be right-to-left and display Arabic labels.

Provide:

- Searchable faculty selection and academic-term selection.
- A list showing which faculty profiles have and have not been entered.
- A two-day or three-day attendance selector with explicit day selection.
- A weekly matrix: teaching days as columns and the project's actual periods as rows.
- Exactly three options per editable cell: ⭐ مفضّل جداً, ✅ مفضّل, 🚫 مرفوض.
- Accessible text and color indicators; do not rely only on emoji or color.
- A day-header action to assign the same level to a whole allowed day.
- A visible summary of selected attendance days and preference counts.
- Save preferences, cancel edits, and explicitly reset allowed cells to Preferred. Do not provide a reset-to-Neutral action.
- An unsaved-changes warning when switching faculty or academic term.
- A separate preview-optimization action and an explicit save-timetable action after reviewing changes.

Illustrative layout only, using the actual configured periods in the implementation:

| الفترة | الأحد | الاثنين | الثلاثاء | الأربعاء | الخميس |
| --- | --- | --- | --- | --- | --- |
| الفترة الأولى | ⭐ | 🚫 | 🚫 | ⭐ | 🚫 |
| الفترة الثانية | ⭐ | 🚫 | 🚫 | ✅ | 🚫 |
| الفترة الثالثة | ✅ | 🚫 | 🚫 | ⭐ | 🚫 |

In this example, Sunday and Wednesday are the fixed attendance days. Other days remain excluded. The administrator can explicitly change the attendance plan to three selected days when needed.

## 7. Mandatory constraints

Always reject a placement that violates any mandatory constraint, including:

- Faculty time conflicts.
- Student section and group conflicts.
- Room time conflicts, capacity, and lecture-type compatibility.
- Lecture duration and all existing theoretical and practical rules.
- Required practical exchange structures where applicable.
- Explicitly locked lectures.
- Prohibited faculty days or periods.
- The faculty member's fixed attendance-day plan.
- Any other mandatory constraint already present in the project.

Never change IsLocked automatically. If a locked lecture conflicts with a newly saved preference profile, identify the conflict before timetable changes. Do not silently unlock it, ignore the prohibition, or move it to a different day.

## 8. Preference scoring and objective priorities

Suggested initial soft penalties:

- StronglyPreferred: 0.
- Preferred: 2 per teaching hour, prorated by actual lecture duration.
- Prohibited: invalid placement, never a finite penalty.

These are starting values for evaluation, not hardcoded universal constants. Explain units, bounds, and configurable weights. Avoid overflow and arbitrary weights that accidentally override mandatory constraints.

Use the following documented priorities in preference-optimization mode:

1. Satisfy all mandatory constraints and preserve the approved attendance-day plan.
2. Minimize duration-weighted preference penalties within the permitted movement scope.
3. Among solutions with equal preference quality, reduce the worst individual duration-normalized preference penalty, avoiding systematic favoritism.
4. Among equally good preference outcomes, minimize the number of moved lectures and affected other faculty.
5. Improve compactness and reduce avoidable internal gaps within the same approved days.
6. Use deterministic tie-breaking where practical.

Document whether each priority is optimized exactly or approximately within the available search budget. Do not claim lexicographic guarantees if the implementation uses heuristic weighted sums without valid bounds.

Do not interpret raw penalty points as satisfaction percentages. Report actual teaching minutes and percentages in StronglyPreferred and Preferred periods separately. The percentage in StronglyPreferred periods is a preference-attainment metric, not a measurement of subjective satisfaction. Exclude faculty with no entered preferences or no assigned teaching minutes from aggregate preference percentages, and report their counts separately.

Report both duration-weighted overall attainment and individual faculty results so aggregate improvements do not hide poor outcomes for particular faculty.

## 9. Integration and preference-aware rescheduling

- Apply the same effective preference rules during initial generation, timetable improvement, imported-timetable repair, and final save validation.
- Preference scoring must participate in candidate evaluation and reconsideration of earlier placements. Sorting the day list once is insufficient.
- Reuse the existing optimization machinery where appropriate, including CP-SAT if it is already present.
- Support bounded move or swap chains with configurable time and attempt limits.
- Other faculty's lectures remain fixed by default.
- Let the administrator explicitly set a bounded scope or count of other unlocked lectures that may move. Respect those faculty members' profiles and attendance-day plans as well.
- Search in temporary state. Validate any accepted complete candidate before presenting it as saveable. Incomplete exploratory states must never be persisted.
- Preserve lecture IDs, faculty assignments, subjects, sections, groups, term identity, lecture types, and duration.
- Saving a changed preference profile must invalidate earlier optimization previews based on the previous profile.
- Revalidate the database and preference versions before committing an approved timetable.

Distinguish the existing minimum-change conflict-repair workflow from preference-optimization mode. Conflict repair restores validity with minimal changes; preference optimization prioritizes preference quality inside the explicitly approved movement scope. Both must enforce prohibited periods and attendance-day restrictions.

Do not include teacher-absence simulation, laboratory or classroom closure simulation, or emergency rescheduling features. These are outside the requested scope.

## 10. Persistence and safe upgrades

- Follow the existing Data Access Layer and Entity Framework conventions.
- Use proper foreign keys, valid enum constraints, and unique keys for faculty/academic term/day/time-slot settings.
- Prevent duplicate or contradictory effective preference records.
- Keep attendance-day selection separate from the soft preference degree.
- Use safe, repeatable schema upgrades. Preserve all existing faculty records and schedules.
- Avoid overwriting newer preference changes from another open administrator window; use version checks or optimistic concurrency.
- Persist timetable changes atomically only after complete validation.
- Keep business rules out of UI event handlers.

## 11. Preview, explanation, and reporting

Show before/after values for:

- Hard constraint violations; a saveable result must have zero.
- The exact attendance days for each affected faculty member, confirming they remain unchanged unless an attendance-plan change was explicitly requested.
- Duration-weighted total preference penalty.
- Teaching minutes and percentages in StronglyPreferred and Preferred cells.
- Faculty with teaching in Preferred cells rather than their StronglyPreferred alternatives; explain when no better valid placement was found.
- Avoidable internal gap minutes, with required breaks treated consistently.
- Moved lectures and affected faculty.
- Search duration, configured limits, and search status.

Clearly distinguish a feasible result, the best result found within limits, and proven optimality. Any optimality claim must be limited to the actual modeled objective, constraints, and movement scope. A time limit without a solution is not proof of infeasibility.

When the fixed two-day or three-day plan cannot be satisfied, explain the blocking constraints as specifically as the implementation can establish. Any proposed alternative must be labeled as either tested feasible or an unverified suggestion. Do not claim a mathematically minimal explanation or globally smallest relaxation without proof.

## 12. Meaningful automated verification

Cover:

1. All three preference levels and the absence of unsupported levels.
2. Whole-day and period-level editing and effective values.
3. Prohibited whole days and prohibition outside the attendance-day set.
4. Two-day and three-day initial generation.
5. StronglyPreferred-to-Preferred changes preserving the same attendance days.
6. Preferred-to-StronglyPreferred changes improving placement within the same days.
7. Newly Prohibited cells producing a valid within-day-plan repair or an accurate failure without saving.
8. No silent expansion, reduction, or substitution of attendance days during preference-only optimization.
9. Conflicting preferences between faculty, including bounded moves of other unlocked lectures.
10. Theoretical and practical scheduling, duration, group conflicts, capacity, and required exchange rules.
11. Locked lecture preservation and conflicts between locks and preferences.
12. Compactness without invalid consecutive teaching or removal of required breaks.
13. Duration-normalized individual metrics and fairness tie-breaking.
14. Profiles not yet entered, explicit profile initialization, and database persistence.
15. Concurrent or stale preference changes invalidating save and preview.
16. Search limits, cancellation, deterministic results, infeasibility, and unknown status.
17. Transaction rollback, stable lecture identities, and no writes during exploration.
18. Regression coverage for existing generation and import workflows.

Use isolated test databases and representative datasets. Do not test by altering the administrator's live timetable.

## 13. Implementation report, when implementation is authorized

Provide the inspected architecture, reused functionality, new components, schema changes, scoring and objective definitions, modified files, actual test results, before/after measurements, and known limitations.

Preserve working features. Report measured results rather than assumptions. Do not claim the feature is complete merely because preference fields or a colored grid have been added: generation and optimization must actively enforce the attendance-day plan and use the three preference levels.
