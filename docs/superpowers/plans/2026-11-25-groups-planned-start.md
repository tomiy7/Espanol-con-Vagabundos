# Groups planirani datum prvog časa — plan

> **For agentic workers:** REQUIRED SUB-SKILL: superpowers:executing-plans. Koraci su `- [ ]`.

**Goal:** Profesor/admin postavlja datum prvog časa; Groups objavljuje `GroupConfirmed`, `MemberJoinedConfirmedGroup`, `FirstLessonRescheduled`; Notifications šalje mejl za pomeren datum.

**Architecture:** Domenska logika u `Group`; novi handler + endpoint; mapiranje u outbox; novi Notifications handler i šablon.

**Tech Stack:** .NET 10, EF Core 10 (Npgsql), xUnit, RabbitMQ, MailKit.

**Spec:** `docs/superpowers/specs/2026-11-25-groups-first-lesson-date-design.md`

## Global Constraints
- Komentari srpski bez dijakritika; greške i API odgovori engleski.
- Novi handler prima i prosleđuje `CancellationToken`.
- Migracija samo preko `dotnet ef migrations add` (DB_CONNECTION_STRING za design-time).
- Datum: UTC, strogo posle `now`, najviše `now + 365 dana` (`Group.MaxPlannedStartOffset`).

## Review Focus
- Datum u prošlosti ili tačno `now` → 400 `CANNOT_SCHEDULE`.
- Postavljanje posle `Start`/`Dissolve`/`Complete` → 400.
- Grupa padne u Forming pa se vrati u Ready → `GroupConfirmed` ne izlazi drugi put.
- Isti datum poslat dvaput → nema novog događaja ni mejla.
- Student pozove endpoint → 403; nepostojeća grupa → 404.

---

### Task 1: Domen
**Files:** `GroupsService.Domain/Entities/Group.cs`, `Domain/Events/DomainEvents.cs`, test `GroupsService.Domain.Tests/PlannedStartTests.cs`

**Produces:**
- `Group.PlannedStartAt : DateTime?`, `Group.ConfirmedAt : DateTime?`, `Group.MaxPlannedStartOffset = 365 dana`
- `void SchedulePlannedStart(DateTime at, DateTime now)`
- domenski: `GroupConfirmed(Guid GroupId)`, `MemberJoinedConfirmedGroup(Guid GroupId, Guid EnrollmentId, Guid UserId)`, `FirstLessonRescheduled(Guid GroupId, DateTime OldAt, DateTime NewAt)`, `FirstLessonScheduled(Guid GroupId)` (interni)

Testovi: set u Forming bez potvrde; Ready+datum potvrđuje tačno jednom; datum pa članovi potvrđuje na `AddMember`; kasni član u potvrđenoj grupi daje `MemberJoinedConfirmedGroup` (ne i `GroupConfirmed`); Ready→Forming→Ready ne potvrđuje opet; pomeranje daje `FirstLessonRescheduled` sa starim/novim; isti datum ništa; prošlost/`now`/>365 dana i status Active/Dissolved/Completed bacaju `GroupsDomainException`. Individual i Pair se potvrđuju sa 1/2 člana.

### Task 2: Perzistencija + migracija
`GroupConfiguration`: `planned_start_at`, `confirmed_at`. `dotnet ef migrations add AddGroupPlannedStart`. Build zelen.

### Task 3: Ugovori + outbox
`GroupsEvents.cs`: `GroupConfirmed(GroupId, CourseId, GroupType, FirstLessonAt, Members)`, `MemberJoinedConfirmedGroup(GroupId, CourseId, GroupType, FirstLessonAt, GroupMemberInfo Member)`, `FirstLessonRescheduled(GroupId, CourseId, OldFirstLessonAt, NewFirstLessonAt, Members)`; mapiranje u `IntegrationEventMapper`.

### Task 4: Handler + endpoint
`Commands/SchedulePlannedStart/` (`SchedulePlannedStartCommand(Guid GroupId, DateTime PlannedStartAt)`, handler `Handle(cmd, CancellationToken)`), `PUT /groups/{id}/planned-start` (Staff, `ErrorCodes.CannotSchedule = "CANNOT_SCHEDULE"`, audit `SchedulePlannedStart`). `GroupDetailDto` + `plannedStartAt`, `confirmedAt`. Integracioni testovi `Groups/PlannedStartTests.cs`: 204 + outbox događaji, Review Focus slučajevi, audit zapis.

### Task 5: Notifications
Handler za `FirstLessonRescheduled` (svim članovima), šablon `FirstLessonRescheduled(name, course, newAt)` u `IEmailTemplates` i `SerbianEmailTemplates`; test šablona + integracioni test toka.

### Task 6: Verifikacija
Sve Groups i Notifications test suite-ove pokrenuti; build rešenja; ROADMAP ažurirati.
