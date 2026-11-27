# Courses dorada — plan

> **For agentic workers:** REQUIRED SUB-SKILL: superpowers:executing-plans.

**Goal:** Tri cene, zaštićene lekcije (pristup iz Groups-a), ProblemDetails, `/health`, testovi i CI za Courses.
**Architecture:** Courses ostaje jedan API projekat koji čita Directus tabele; pristup pita Groups preko HTTP-a sa korisnikovim tokenom.
**Tech Stack:** .NET 10, EF Core 10 (Npgsql), xUnit + WebApplicationFactory, GitHub Actions.
**Spec:** `docs/superpowers/specs/2026-11-27-courses-hardening-design.md`

## Global Constraints
- Šema `courses/lessons/sections/tasks` pripada Directus-u: **bez EF migracija**. Testovi koriste `EnsureCreated`.
- Nove cene `decimal?` (`price_individual`, `price_pair`, `price_group`); `price` ostaje.
- Greške: ProblemDetails + `code`; komentari srpski bez dijakritika; poruke engleski.
- Novi async kod prima `CancellationToken`.

## Review Focus
- Groups vrati 401 za istekao token → Courses vraća 401 (ne 503).
- Groups spor/pao → 503 `ACCESS_CHECK_UNAVAILABLE`, bez sadržaja.
- Lekcija na neobjavljenom kursu → 404 čak i za osoblje? Ne: osoblje vidi objavljene lekcije i neobjavljenog kursa (priprema). Učenik 404.
- `order` sa rupama / od 0 / duplikati → pozicija stabilna (sort po `order`, pa po `id`).
- `content` nije validan JSON → vraća se kao string, ne 500.

### Task 1: Paketi, Program, greške, health
`CoursesService.API.csproj` (−AutoMapper, −preview Configuration, +HealthChecks.EntityFrameworkCore 10.0.11), `Errors/ErrorCodes.cs`, `Errors/ApiProblems.cs`, `Program.cs` (`AddProblemDetails`, `/health`, `public partial class Program`).

### Task 2: Cene
`Course` + 3 kolone; `PricesDto { Individual, Pair, Group, Ebook }` u listi i detalju; detalj bez `ebookPdf`; `GET /courses/{id:guid}/pricing` → `{ courseId, currency, individual, pair, group, ebook }` (i neobjavljeni; 404 `COURSE_NOT_FOUND`). `currency` iz `Pricing:Currency` (podrazumevano `RSD`).

### Task 3: Pristup lekcijama
- `Access/IGroupsAccessClient.CheckAsync(string bearer, Guid courseId, CancellationToken) -> Task<CourseAccess>`; `CourseAccess(bool HasAccess, Guid? EnrollmentId, int UnlockedLessons)`;
  `GroupsUnavailableException`, `GroupsUnauthorizedException`. HTTP: `GET {GroupsService:BaseUrl}/enrollments/access?courseId=`, timeout 5 s.
- `Access/LessonPosition.Of(IEnumerable<(Guid Id, int Order)> lessons, Guid lessonId) -> int` (1-based, sort `Order`, pa `Id`).
- `GET /courses/lessons/{id}` [Authorize]: osoblje uvek; učenik → 403 `NO_COURSE_ACCESS` / `LESSON_LOCKED`; 404 `LESSON_NOT_FOUND`; 503 `ACCESS_CHECK_UNAVAILABLE`; 401 ako Groups odbije token.
- Odgovor: `courseId`, sekcije/zadaci po `order`, `content` kao JSON (ili string).

### Task 4: Testovi
`Services/Courses/CoursesService.Tests` (env `COURSES_TEST_CONNECTION`, baza `courses_integration_tests`, `EnsureCreated`, `FakeGroupsAccess`). Unit: `LessonPosition`. Integracija: lista/detalj/cene/pricing, sve grane lekcije iz Review Focus-a, anonimno/nevalidan token, `ebookPdf` nije u odgovoru, `/health`.

### Task 5: CI, compose, sln, dokumenti
`.github/workflows/courses.yml` (test + smoke `/health`), `docker-compose.override.yml` (`GroupsService__BaseUrl`), sln, ROADMAP, `FRONTEND-INTEGRATION.md`.
