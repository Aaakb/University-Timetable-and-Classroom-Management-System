# Local PWA implementation

## Baseline and compatibility — 2026-10-17

The existing solution builds successfully (0 warnings/errors). The working tree already
contains extensive desktop/BLL changes; they belong to the user and must be preserved.
Models, DAL and BLL target net8.0; the desktop targets net8.0-windows. EF Core/SQL
Server 8.0.28 and the existing service classes are directly reusable. No port or
database migration is required. The new host targets net8.0 to match these libraries.

The BLL constructs and disposes an AppDbContext per operation, reading
UNIVERSITY_TIMETABLE_CONNECTION_STRING through DatabaseConnection. The web host
uses the same setting. Existing local integrated-security fallback remains available.
No startup maintenance, seeding, migration, or schema creation is called by the web host.

## Verified source inventory

| Desktop source in PresentationLayer | Shared service | Web workflow / route |
|---|---|---|
| LoginForm / RegisterForm | AuthService | Login, registration, logout / api/auth |
| MainShellForm | Existing data services | Navigation, live dashboard |
| BranchesForm | BranchService | CRUD / api/branches |
| StudyYearsForm | StudyYearService | CRUD / api/studyyears |
| SectionsForm | SectionService | CRUD / api/sections |
| SubjectsForm | SubjectService | CRUD and filters / api/subjects |
| FacultyMembersForm | FacultyMemberService | CRUD / api/facultymembers |
| FacultyMemberSubjectsForm | FacultyMemberSubjectService | Composite-key CRUD / api/assignments |
| ClassroomsForm | ClassroomService | CRUD / api/classrooms |
| TimeSlotsForm | TimeSlotService | Lecture-period CRUD / api/timeslots |
| SchedulesForm and partials | ScheduleService | CRUD, filters, timetable / api/schedules |
| SchedulesForm.Actions | ScheduleService.GenerateAsync | Confirmed replacement / api/schedules/generate |
| SchedulesForm.Actions | SchedulePdfExportService | Filtered PDF / api/schedules/export |

Acceptance criteria: each management workflow has real list/create/update/delete,
server-side BLL validation, refreshed lookups, loading/error states, and authenticated
access. Updates/deletes carry a version token and reject a stale record. Schedule
views, filters, and exported rows agree. Installation/offline checks are separate
from build and database checks; execution evidence is recorded at the end of this file.

## Existing rules and limitations

AuthService uses PBKDF2-SHA256 with a random salt and fixed-time verification.
There are no role/permission columns or user administration workflows. Registration
is available on the desktop login screen. All signed-in users have management access.
Web registration retains the shared validation; legacy minimum password length is 4.
This policy must be reviewed before internet deployment.

AcademicStructureRules includes existing year-ID-dependent rules for general years,
branches, sections A/B and practical groups. These are retained, not invented or
replaced by web rules. ClassroomRules and ScheduleTimingRules validate room type,
capacity, non-overlapping lecture periods, and flexible duration. Scheduling checks
faculty assignments, subject/year/section relationships, practical groups, room,
teacher and section clashes, and required lesson completeness. The existing generator
is reused and only persists a complete result, replacing existing timetable records.
Generation is never invoked as a production-data smoke test.

The existing PDF renderer uses SkiaSharp/RichTextKit and Windows native loading;
cross-platform publication needs separate native-library verification.

Web mutations are serialized within one API process. Version preconditions detect
stale edits before invoking the BLL. Existing serializable BLL transactions and
database constraints remain in force. This does NOT provide an atomic version check
against a simultaneous desktop write between the API check and BLL save, nor does a
web-only gate serialize desktop generation. Full cross-process optimistic concurrency
requires shared DAL/BLL changes (and potentially rowversion schema changes). Avoid
simultaneous generation/editing across desktop and web until that is implemented.

## Scope

One additional ASP.NET Core project serves static HTML/CSS/JavaScript and the API.
Arabic RTL is used for the web interface; existing database text and BLL validation
messages retain their original language. No frontend framework or Blazor is used.
No existing desktop, BLL, DAL, model or Designer source is changed by this addition.

## API contract

All management routes require the existing authenticated user cookie. There are no
separate administrator roles in the existing schema. API responses use explicit scalar
DTOs; authentication responses never contain password hashes/salts.

| Method | Route | Behavior |
|---|---|---|
| GET | /api/auth/csrf | Issue request token; required as X-CSRF-TOKEN on mutations |
| POST | /api/auth/register | Existing shared registration rules; rate limited |
| POST | /api/auth/login | Validate existing credentials; HttpOnly session cookie |
| GET | /api/auth/me | Current account without credential material |
| POST | /api/auth/logout | Remove authentication cookie |
| GET | /api/{resource} | `{items:[{data,version}],total,page,pageSize}`; q/page/pageSize parameters |
| GET | /api/{resource}/{id} | `{data,version}` plus ETag |
| POST | /api/{resource} | Create through BLL; 201 and Location |
| PUT | /api/{resource}/{id} | Update through BLL; If-Match required |
| DELETE | /api/{resource}/{id} | Delete through BLL; If-Match required; 204 |
| GET | /api/overview | Current counts and unassigned subjects |
| GET | /api/overview/rules | Shared academic structure/section choices |
| GET | /api/schedules/details | Joined, filtered timetable rows |
| GET | /api/schedules/export | Filtered PDF from original renderer |
| GET | /api/schedules/generation-state | Fingerprint of resources and existing timetable |
| POST | /api/schedules/generate | `{confirmReplacement:true,version}`; original generator |

Resources: branches, studyyears, classrooms, facultymembers, sections, subjects,
timeslots, assignments, schedules. Assignment IDs are `facultyMemberID-subjectID`;
other IDs are positive integers. No user-admin route was invented.

Schedule filters: semesterNumber, studyYearId, branchId, sectionId, facultyMemberId,
classroomId, lectureType, dayOfWeek, groupName, q. Group filters also include the base
section's common theory lessons. Generic pagination currently occurs after BLL list
loading, preserving the existing services; very large deployments should add query
pagination to the shared BLL. Current local data sizes were exercised in the browser.

Status codes include 400 validation/CSRF, 401 unauthenticated, 404 missing record,
409 DB relationship/conflict/busy, 412 stale version, 428 missing version, 429 auth
rate limit, and 503 database failure. API responses are no-store. Static assets are
same-origin; CSP disallows inline script and framing. Network API access requires HTTPS;
plain HTTP is accepted only for loopback development.

## Discovered database/UI discrepancy

Read-only inspection of current SQL CHECK constraints showed RequirementType accepts
University, College, Department. Desktop Designer lists Core/Elective and other labels
that do not match this database. The web editor uses the actual valid values and leaves
the desktop untouched. The database also requires StudentCount > 0. An initial test
using a nonconforming requirement value was rejected; the test-owned records were
cleaned and the original data fingerprint matched afterwards.

## Executed verification

- Baseline and final entire solution builds: 0 warnings, 0 errors. A rebuild attempted
  while the owned development host was running encountered an apphost file lock;
  stopping that host and rebuilding resolved it.
- Integration suite with browser: 98 checks passed, including authenticated list,
  create, get, update and delete for all nine resources, CSRF, stale/missing If-Match,
  schedule conflict validation, no credential disclosure, PDF bytes, shared desktop-BLL
  visibility in both directions, logout, and final original-row fingerprint equality.
- Browser: 30 checks passed in headless Edge with a temporary normal profile, including
  all editors, live dashboard, mobile CRUD/navigation, PDF download, weekly view, and
  no viewport-wide overflow at all seven requested sizes. Screenshots were reviewed.
- Chromium's installability report returned no errors in the normal profile. The first
  attempt in an isolated/incognito context correctly reported `in-incognito`; it was
  repeated using a temporary normal profile. No permanent OS installation was performed.
- Service worker cache inspection confirmed no /api data. Offline navigation showed the
  offline fallback and online recovery retained the valid server session. First-install
  update notice behavior was checked; a subsequent release/update transition was not
  end-to-end exercised.
- Original pure generator tested with synthetic capacity cases and with all current
  database inputs: 293 required, 293 planned, no pair conflicts, no practical issues,
  633 ms planner time. No generated rows were persisted. This timing supports retaining
  the existing request-based generator for current local workloads.
- Test-owned temporary rows/account were deleted, and all existing scalar business
  rows, schedule columns, break slots, and users matched the pre-test fingerprint.
  Identity counters can advance during tests; the test does not reset them.
- Existing desktop code was not modified and builds successfully; interactive desktop
  navigation, browser print dialog completion, physical mobile devices, trusted LAN
  HTTPS, and production deployment were not tested.

Evidence: `docs/Verification/PWA/api-results.json`, `browser-results.json`, and
`current-plan.json`. Reproduction: `docs/PWA_STARTUP_AR.md`.

## Feature matrix status

All rows in the inventory are implemented. CRUD/auth/read/filter/PDF workflows were
executed against the existing database using test-owned records. Weekly/responsive/PWA
workflows were browser-verified. Generation is integrated and its planning verified,
but persisted replacement was intentionally not run on the user's timetable. Printing
uses the browser dialog and a print stylesheet; physical printing was not performed.
The shared desktop/web atomic-concurrency limitation above remains explicit.
