# Español con Vagabundos — Architecture Reference

> Živi dokument. Menjati ovde kada se donose arhitekturalne odluke.

---

## 1. Project Overview

**Šta gradimo:** Moderna platforma za učenje španskog jezika. Korisnici kupuju i prate kurseve sa strukturisanim
lekcijama, kvizovima, praćenjem napretka i sertifikatima.

**Ko su korisnici:**

- Studenti — prate kurseve, rade kvizove, dobijaju sertifikate
- Instruktori — kreiraju i objavljuju kurseve
- Admini — upravljaju platformom

**Dugoročna vizija:** AI-asistovano učenje (tutor, izgovor, flashcard generisanje), live sesije, mobilna aplikacija.

---

## 2. Technology Decisions

| Odluka           | Izbor                            | Zašto                                                                                     |
|------------------|----------------------------------|-------------------------------------------------------------------------------------------|
| Backend          | .NET 10 / ASP.NET Core           | Zreo ekosistem, odlične performance, Native AOT spreman, C# je odličan za domain modeling |
| ORM              | Entity Framework Core            | Code-first migrations, LINQ, integracija sa .NET DI                                       |
| Frontend         | React + TypeScript + TailwindCSS | React Query za server state, TypeScript za type-safety, Tailwind za brz UI                |
| Baza podataka    | PostgreSQL                       | Robustna, UUID podrška, JSON kolone za budućnost, odlična EF Core podrška                 |
| Object storage   | Backblaze B2 (S3-compatible)     | Jeftino, pouzdano, kompatibilno sa AWS SDK                                                |
| Auth             | JWT + Refresh tokens             | Stateless — CourseService ne mora da poziva AuthService za svaki zahtev                   |
| Kontejnerizacija | Docker + Docker Compose          | Konzistentno lokalno i produkcijsko okruženje                                             |
| Reverse proxy    | Nginx                            | Routing, SSL terminacija, serviranje statičnih fajlova                                    |
| Logging          | Serilog → Seq                    | Strukturovani logovi, pretraga u Seq UI                                                   |
| Monitoring       | Prometheus + Grafana             | Industry standard, bogati dashboardi                                                      |
| Cache / Pub-Sub  | Redis                            | Refresh token blacklist, OTP kodovi, rate limiting, course catalog cache                  |
| CI/CD            | GitHub Actions                   | Direktna integracija sa repom                                                             |

### Zašto mikroservisi, ne monolith?

Mikroservisi su **kompleksniji za početak** ali su ovde opravdani zbog:

- Jasnog razdvajanja domenskih granica (Auth ≠ Course ≠ Quiz ≠ Payment)
- Učenja arquitekturalnih paterna u realnom kontekstu
- Svaki servis može biti skaliran nezavisno
- Phase 1 je samo AuthService + infrastruktura — ne startujemo sve odjednom

**Pravilo:** Ne gradimo sva 4 servisa odjednom. Jedan po jedan, po redosledu faza.

---

## 3. Architecture Overview

```
                    ┌──────────────┐
                    │   Frontend   │
                    │ React / TS   │
                    └──────┬───────┘
                           │ HTTPS
                    ┌──────▼───────┐
                    │    Nginx     │  ← Reverse proxy, SSL
                    └──────┬───────┘
            ┌──────────────┼──────────────┬──────────────┐
            │              │              │              │
     ┌──────▼──────┐ ┌─────▼──────┐ ┌────▼──────┐ ┌────▼──────┐
     │ AuthService │ │CourseService│ │QuizService│ │ Payment   │
     │  :5001      │ │  :5002      │ │  :5003    │ │ Service   │
     └──────┬──────┘ └─────┬──────┘ └────┬──────┘ │  :5004    │
            │              │              │        └────┬──────┘
            └──────────────┴──────────────┴────────────┘
                           │  (svaki servis ima sopstvenu bazu)
              ┌────────────▼────────────┐    ┌──────────┐
              │  PostgreSQL (shared)    │    │  Redis   │
              │  authdb / coursesdb /   │    │  :6379   │
              │  quizdb / paymentsdb    │    └──────────┘
              └─────────────────────────┘
```

### Database-per-service pravilo

> Nijedan servis **ne sme direktno pristupiti bazi drugog servisa**. Nikad.

Jedini načini za cross-service podatke:

1. **Synchronous REST call** — CourseService poziva AuthService `/api/v1/users/{id}` za info o korisniku
2. **Asynchronous events** (Phase 2+) — MassTransit + RabbitMQ za event-driven komunikaciju

Primer: `Courses.InstructorId` je UUID koji se čuva kao plain kolona — nema foreign key prema Users tabeli u authdb.

### JWT Validation Pattern

AuthService izdaje JWT potpisan sa `JWT_SECRET_KEY`. **Svi ostali servisi imaju isti secret key** i validiraju token
lokalno — bez pozivanja AuthService-a.

```
Frontend → CourseService (Authorization: Bearer <token>)
CourseService validira JWT lokalno (proverava potpis + expiry)
CourseService ne zove AuthService za svaki request
```

Konfiguracija u svakom servisu:

```json
"JwtSettings": {
  "SecretKey": "${JWT_SECRET_KEY}",
  "Issuer": "espanol-con-vagabundos",
  "Audience": "espanol-con-vagabundos",
  "ExpiryMinutes": 60
}
```

---

## 4. Repository Structure

```
espanol-con-vagabundos/
├── services/
│   ├── AuthService/
│   │   ├── AuthService.API/
│   │   ├── AuthService.Application/
│   │   ├── AuthService.Domain/
│   │   ├── AuthService.Infrastructure/
│   │   └── AuthService.sln
│   ├── CourseService/
│   │   ├── CourseService.API/
│   │   ├── CourseService.Application/
│   │   ├── CourseService.Domain/
│   │   ├── CourseService.Infrastructure/
│   │   └── CourseService.sln
│   ├── QuizService/         ← Phase 6
│   └── PaymentService/      ← Phase 7
├── frontend/                ← React app (Phase 2+)
├── infrastructure/
│   ├── docker-compose.yml         ← Produkcija
│   ├── docker-compose.dev.yml     ← Lokalni development
│   └── nginx/
│       └── nginx.conf
├── docs/                    ← Dijagrami, ADR-ovi
├── .github/
│   └── workflows/
│       ├── auth-service.yml
│       └── course-service.yml
├── .gitignore
└── ARCHITECTURE.md          ← Ovaj fajl
```

### Clean Architecture po servisu

```
ServiceName.API/
  Controllers/
  Middleware/
  Program.cs

ServiceName.Application/
  Features/          ← CQRS handlers (MediatR)
    Courses/
      Commands/
      Queries/
  DTOs/
  Interfaces/
  Validators/        ← FluentValidation

ServiceName.Domain/
  Entities/
  Enums/
  Exceptions/
  ValueObjects/

ServiceName.Infrastructure/
  Persistence/
    DbContext.cs
    Migrations/
    Repositories/
  Services/          ← External services (B2, Stripe)
```

---

## 5. Service Responsibilities

### AuthService

- Registracija i login korisnika
- JWT generisanje i refresh token rotacija
- Role-based access (Student, Instructor, Admin)
- Password hashing (BCrypt)

### CourseService

- CRUD kurseva, sekcija, lekcija
- Enrollment (upis na kurs)
- Access control (je li korisnik upisao kurs?)
- Video URL signing (Backblaze B2 presigned URLs)
- Progress tracking (koji lekciji je završen)
- Reviews i ratings
- Wishlist
- Certificate generisanje i PDF download

### QuizService

- CRUD kvizova i pitanja
- Pokretanje i submisija pokušaja
- Računanje skorova
- Tipovi pitanja: SingleChoice, MultipleChoice, TrueFalse, Text

### PaymentService

- Narudžbine (Orders) i stavke (OrderItems)
- Stripe integracija (Checkout Sessions, Webhooks)
- Post-payment enrollment trigger (via event ili direktan REST call)
- Refund handling

---

## 6. Database Design

### authdb

**Users**
| Kolona | Tip | Constraint |
|--------|-----|------------|
| Id | UUID | PK |
| Email | VARCHAR(255) | UNIQUE NOT NULL |
| PasswordHash | TEXT | NOT NULL |
| FirstName | VARCHAR(100) | NOT NULL |
| LastName | VARCHAR(100) | NOT NULL |
| Role | VARCHAR(50) | NOT NULL (Student/Instructor/Admin) |
| CreatedAt | TIMESTAMP | NOT NULL |
| UpdatedAt | TIMESTAMP | NOT NULL |

Index: Email (unique)

**RefreshTokens**
| Kolona | Tip | Constraint |
|--------|-----|------------|
| Id | UUID | PK |
| UserId | UUID | NOT NULL (ne FK na Users) |
| Token | TEXT | NOT NULL |
| ExpiresAt | TIMESTAMP | NOT NULL |
| RevokedAt | TIMESTAMP | NULL |

---

### coursesdb

**Courses**
| Kolona | Tip | Constraint |
|--------|-----|------------|
| Id | UUID | PK |
| Title | VARCHAR(255) | NOT NULL |
| Description | TEXT | |
| Price | DECIMAL(10,2) | NOT NULL |
| InstructorId | UUID | NOT NULL (UUID reference only, no FK) |
| ThumbnailUrl | TEXT | |
| Level | VARCHAR(50) | Beginner/Intermediate/Advanced |
| Status | VARCHAR(50) | Draft/Published/Archived |
| CreatedAt | TIMESTAMP | NOT NULL |
| UpdatedAt | TIMESTAMP | NOT NULL |

Indexes: InstructorId, Status

**CourseSections**
| Kolona | Tip |
|--------|-----|
| Id | UUID PK |
| CourseId | UUID FK → Courses |
| Title | VARCHAR(255) |
| SortOrder | INTEGER |

**Lessons**
| Kolona | Tip |
|--------|-----|
| Id | UUID PK |
| SectionId | UUID FK → CourseSections |
| Title | VARCHAR(255) |
| Description | TEXT |
| VideoUrl | TEXT |
| DurationSeconds | INTEGER |
| SortOrder | INTEGER |
| IsPreview | BOOLEAN |

**Enrollments**
| Kolona | Tip |
|--------|-----|
| Id | UUID PK |
| UserId | UUID NOT NULL |
| CourseId | UUID FK → Courses |
| EnrolledAt | TIMESTAMP |

Unique index: (UserId, CourseId)

**Progress**
| Kolona | Tip |
|--------|-----|
| Id | UUID PK |
| UserId | UUID NOT NULL |
| LessonId | UUID FK → Lessons |
| Completed | BOOLEAN |
| CompletedAt | TIMESTAMP |
| QuizScore | DECIMAL(5,2) |

Indexes: UserId, LessonId

**Reviews**
| Kolona | Tip | Constraint |
|--------|-----|------------|
| Id | UUID PK | |
| UserId | UUID | NOT NULL |
| CourseId | UUID FK | → Courses |
| Rating | INTEGER | 1-5 |
| ReviewText | TEXT | |
| CreatedAt | TIMESTAMP | |

Index: CourseId

**Wishlist**
| Kolona | Tip |
|--------|-----|
| Id | UUID PK |
| UserId | UUID NOT NULL |
| CourseId | UUID FK → Courses |
| AddedAt | TIMESTAMP |

**Certificates**
| Kolona | Tip |
|--------|-----|
| Id | UUID PK |
| UserId | UUID NOT NULL |
| CourseId | UUID FK → Courses |
| CertificateUrl | TEXT |
| IssuedAt | TIMESTAMP |

---

### quizdb

**Quizzes**
| Kolona | Tip |
|--------|-----|
| Id | UUID PK |
| CourseId | UUID NOT NULL (UUID reference only) |
| Title | VARCHAR(255) |
| PassingScore | INTEGER |
| CreatedAt | TIMESTAMP |

**Questions**
| Kolona | Tip |
|--------|-----|
| Id | UUID PK |
| QuizId | UUID FK → Quizzes |
| QuestionText | TEXT |
| QuestionType | VARCHAR(50) |
| SortOrder | INTEGER |

Tipovi: SingleChoice, MultipleChoice, TrueFalse, Text

**Answers**
| Kolona | Tip |
|--------|-----|
| Id | UUID PK |
| QuestionId | UUID FK → Questions |
| AnswerText | TEXT |
| IsCorrect | BOOLEAN |

**QuizAttempts**
| Kolona | Tip |
|--------|-----|
| Id | UUID PK |
| UserId | UUID NOT NULL |
| QuizId | UUID FK → Quizzes |
| Score | DECIMAL(5,2) |
| StartedAt | TIMESTAMP |
| FinishedAt | TIMESTAMP |

---

### paymentsdb

**Orders**
| Kolona | Tip | Constraint |
|--------|-----|------------|
| Id | UUID PK | |
| UserId | UUID | NOT NULL |
| TotalPrice | DECIMAL(10,2) | |
| Status | VARCHAR(50) | Pending/Paid/Cancelled/Refunded |
| CreatedAt | TIMESTAMP | |

Indexes: UserId, Status

**OrderItems**
| Kolona | Tip |
|--------|-----|
| Id | UUID PK |
| OrderId | UUID FK → Orders |
| CourseId | UUID NOT NULL |
| Price | DECIMAL(10,2) |

**Payments**
| Kolona | Tip | Constraint |
|--------|-----|------------|
| Id | UUID PK | |
| OrderId | UUID FK | → Orders |
| UserId | UUID | NOT NULL |
| Provider | VARCHAR(50) | Stripe/PayPal |
| ProviderTransactionId | TEXT | |
| Amount | DECIMAL(10,2) | |
| Status | VARCHAR(50) | Pending/Succeeded/Failed/Refunded |
| PaidAt | TIMESTAMP | |

Index: ProviderTransactionId (unique)

---

## 7. API Standards

### Error Response — ProblemDetails (RFC 7807)

Svi servisi vraćaju greške u ovom formatu:

```json
{
  "type": "https://tools.ietf.org/html/rfc7807",
  "title": "Validation Error",
  "status": 400,
  "detail": "Email is already taken.",
  "instance": "/api/v1/auth/register",
  "errors": {
    "Email": ["Email is already taken."]
  }
}
```

Implementirati globalni exception handling middleware u svakom servisu.

### URL konvencije

```
GET    /api/v1/courses          ← lista
POST   /api/v1/courses          ← kreiranje
GET    /api/v1/courses/{id}     ← detalj
PUT    /api/v1/courses/{id}     ← update
DELETE /api/v1/courses/{id}     ← brisanje
```

### Paginacija

```
GET /api/v1/courses?page=1&pageSize=20
```

Response:

```json
{
  "data": [...],
  "page": 1,
  "pageSize": 20,
  "totalCount": 150,
  "totalPages": 8
}
```

### Autentikacija

```
Authorization: Bearer <jwt_token>
```

Neautentifikovane rute: `GET /api/v1/courses` (lista/detalj javnih kurseva), `POST /api/v1/auth/register`,
`POST /api/v1/auth/login`.

---

## 8. Local Development Setup

### Opcija A — Native PostgreSQL + pgAdmin (jednostavniji start)

Preporučeno za prve faze razvoja. Nema potrebe za Dockerom.

**Preduslovi:**

- [PostgreSQL 16](https://www.postgresql.org/download/windows/) (Windows installer — dolazi sa pgAdmin)
- .NET 10 SDK
- IDE: Rider ili VS Code

**Setup baze u pgAdmin:**

1. Otvori pgAdmin 4
2. Desni klik na **Servers** → **Register → Server...**
    - General: Name = `Local Dev`
    - Connection: Host = `localhost`, Port = `5432`, Username = `postgres`, Password = (iz instalacije)
3. Klikni na server → bazu `postgres` → **Tools → Query Tool**
4. Pokreni `infrastructure/postgres/init.sql` (Ctrl+A, F5) — kreira sve 4 baze i usere
5. Refresh panela (F5) — trebaš da vidiš `authdb`, `coursesdb`, `quizdb`, `paymentsdb`

**Connection string za servise** (`appsettings.Development.json`):

```json
{
  "ConnectionStrings": {
    "AuthDb": "Host=localhost;Port=5432;Database=authdb;Username=authuser;Password=authpass"
  }
}
```

**Ograničenje:** Redis i Seq nisu dostupni bez Dockera. Za Phase 1 možeš:

- Redis: preskočiti (koristi DB za refresh token revocation privremeno)
- Seq: logovi idu samo u konzolu

---

### Opcija B — Docker Compose (full stack)

Potreban Docker Desktop. Pokreće PostgreSQL + Redis + Seq sve odjednom.

**Preduslovi:**

- Docker Desktop
- .NET 10 SDK

### docker-compose.dev.yml

```yaml
services:
  postgres:
    image: postgres:16
    environment:
      POSTGRES_PASSWORD: devpass
    volumes:
      - postgres-dev-data:/var/lib/postgresql/data
      - ./infrastructure/postgres/init.sql:/docker-entrypoint-initdb.d/init.sql
    ports:
      - "5432:5432"

  redis:
    image: redis:7-alpine
    command: redis-server --requirepass devredis
    ports:
      - "6379:6379"

  seq:
    image: datalust/seq
    environment:
      ACCEPT_EULA: Y
    ports:
      - "5341:5341"
      - "8080:80"

volumes:
  postgres-dev-data:
```

### .env fajlovi (ne commitovati u git!)

Svaki servis ima `appsettings.Development.json` ili `.env` sa:

```
JWT_SECRET_KEY=dev-secret-key-min-32-chars-long
AUTH_DB_CONNECTION=Host=localhost;Port=5432;Database=authdb;Username=authuser;Password=authpass
COURSE_DB_CONNECTION=Host=localhost;Port=5432;Database=coursesdb;Username=courseuser;Password=coursepass
REDIS_CONNECTION=localhost:6379,password=devredis
SEQ_URL=http://localhost:5341
```

**VAŽNO:** `.env` ide u `.gitignore`. Commit `.env.example` sa placeholder vrednostima.

### Pokretanje

```bash
# Podignuti infra (baze + seq)
docker-compose -f infrastructure/docker-compose.dev.yml up -d

# AuthService
cd services/AuthService
dotnet run --project AuthService.API

# CourseService (u zasebnom terminalu)
cd services/CourseService
dotnet run --project CourseService.API
```

### Database setup — init.sql

Baze i useri se kreiraju automatski pri prvom `docker-compose up` iz `infrastructure/postgres/init.sql`. **Ovaj script
se izvršava samo jednom** — dok je data volume prazan.

Ako trebaš da resetuješ (npr. dodaš novu bazu):

```bash
docker-compose -f infrastructure/docker-compose.dev.yml down -v
docker-compose -f infrastructure/docker-compose.dev.yml up -d
```

### Database migracije — EF Core na startup

Svaki servis pokreće migracije automatski pri startu. U `Program.cs`:

```csharp
// Odmah pre app.Run()
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AuthDbContext>();
    await db.Database.MigrateAsync();
}
```

EF Core prati koje su migracije već primenjene u `__EFMigrationsHistory` tabeli — siguran je za višestruko pokretanje.

**Dodavanje nove migracije:**

```bash
cd services/AuthService/AuthService.Infrastructure
dotnet ef migrations add AddRefreshTokens --startup-project ../AuthService.API
# Commituj — migracija se primenjuje automatski na sledećem pokretanju servisa
```

**Ručni update (opciono):**

```bash
dotnet ef database update --startup-project ../AuthService.API
```

---

## 9. Branching Strategy

```
main          ← Produkcija. Samo tagged releases. Merge iz dev sa PR-om.
dev           ← Integracija. Uvek stable. Feature branch-evi se merguju ovde.
feature/*     ← Svaka nova funkcionalnost.
```

### Naming konvencija za feature branch-eve

```
feature/auth-register
feature/auth-jwt
feature/course-crud
feature/course-enrollment
feature/quiz-system
```

### Flow

```
feature/auth-register → PR → dev → (kad phase završena) PR → main (tag v0.1.0)
```

---

## 10. Implementation Phases

### Phase 1 — Foundation & Auth

**Šta gradimo:** Infrastruktura + AuthService (jedini radni backend servis)

**Tasks:**

- [ ] Repo struktura (services/, infrastructure/, docs/)
- [ ] `infrastructure/postgres/init.sql` sa svim bazama i userima
- [ ] docker-compose.dev.yml sa shared postgres + redis + seq
- [ ] AuthService.sln sa 4 projekta (API/Application/Domain/Infrastructure)
- [ ] Users + RefreshTokens entiteti i EF migracija
- [ ] Register endpoint (`POST /api/v1/auth/register`)
- [ ] Login endpoint (`POST /api/v1/auth/login`) → vraća JWT + refresh token
- [ ] Refresh token endpoint (`POST /api/v1/auth/refresh`)
- [ ] Logout endpoint sa Redis blacklist-om (`POST /api/v1/auth/logout`)
- [ ] Global exception middleware (ProblemDetails format)
- [ ] Serilog konfiguracija → Seq
- [ ] Health check endpoint (`GET /health`)

**Definition of Done:** Moguće je kreirati korisnika, ulogovati se, dobiti JWT, koristiti refresh token, i odjaviti se (
token postaje nevažeći). Sve greške vraćaju ProblemDetails format. Logovi su vidljivi u Seq.

---

### Phase 2 — Course Service Core

**Tasks:**

- [ ] CourseService.sln sa 4 projekta
- [ ] coursesdb baza je već kreirana u `init.sql` iz Phase 1 — samo dodati EF migraciju
- [ ] Courses, CourseSections, Lessons entiteti + migracije
- [ ] JWT validation middleware u CourseService (koristi isti secret)
- [ ] CRUD kurseva (samo za Instructors)
- [ ] CRUD sekcija i lekcija
- [ ] Publish/unpublish kurs
- [ ] Listanje i pretraga kurseva (javno)
- [ ] Detalji kursa (javno za previews, zaštićeno za sadržaj)

**Definition of Done:** Instruktor može kreirati, urediti i objaviti kurs. Korisnik može listati i videti javne kurseve.
JWT iz AuthService-a se validira u CourseService-u bez pozivanja AuthService-a.

---

### Phase 3 — Enrollment & Access Control

**Tasks:**

- [ ] Enrollments tabela + migracija
- [ ] Enroll endpoint (`POST /api/v1/enrollments`)
- [ ] Provjera da li je korisnik upisao kurs (middleware/guard)
- [ ] Zaštita sadržaja lekcija iza enrollment checka
- [ ] "My courses" endpoint za upisane kurseve

**Definition of Done:** Korisnik može upisati kurs (besplatan flow). Sadržaj lekcija je dostupan samo upisanim
korisnicima.

---

### Phase 4 — Lesson Consumption & Video

**Tasks:**

- [ ] Backblaze B2 integracija za video upload
- [ ] Presigned URL generisanje za video streaming (time-limited)
- [ ] Video ne direktno downloadable — samo streaming endpoint
- [ ] Lesson completion marking (`POST /api/v1/progress/lessons/{id}/complete`)

**Definition of Done:** Video se streamer kroz backend (presigned URL), nije direktno skidljiv. Lekcija može biti
označena kao završena.

---

### Phase 5 — Progress Tracking

**Tasks:**

- [ ] Progress entitet + migracija
- [ ] Endpoint za progress kursa (`GET /api/v1/progress/courses/{id}`)
- [ ] Completion percentage izračun
- [ ] Last viewed lesson tracking
- [ ] Learning dashboard podaci

**Definition of Done:** Korisnik može videti procenat završenosti kursa i poslednju gledanu lekciju.

---

### Phase 6 — Quiz System

**Tasks:**

- [ ] QuizService.sln kreiranje
- [ ] quizdb sa svim tabelama + migracije
- [ ] CRUD kvizova i pitanja (Instructor role)
- [ ] Start quiz attempt
- [ ] Submit quiz attempt + score computation
- [ ] Quiz result i history

**Definition of Done:** Instruktor može kreirati kviz sa pitanjima. Student može uraditi kviz i videti rezultat.

---

### Phase 7 — Payments (Stripe)

**Tasks:**

- [ ] PaymentService.sln kreiranje
- [ ] paymentsdb tabele + migracije
- [ ] Stripe Checkout Session kreiranje
- [ ] Stripe Webhook handler (payment.succeeded → kreirati enrollment)
- [ ] Order history endpoint
- [ ] Besplatni kursevi (price = 0 → direktna enrollacija)

**Definition of Done:** Korisnik može kupiti kurs, plaćanje se procesira kroz Stripe, enrollment se automatski kreira po
uspešnoj uplati.

---

### Phase 8 — Reviews & Wishlist

**Tasks:**

- [ ] Reviews tabela + migracija
- [ ] Leave review endpoint (samo upisani korisnici)
- [ ] Average rating na kursu
- [ ] Review moderation (Admin)
- [ ] Wishlist CRUD

**Definition of Done:** Upisani korisnik može ostaviti recenziju. Kurs prikazuje prosečnu ocenu.

---

### Phase 9 — Certificates

**Tasks:**

- [ ] Certificate generisanje (kada je kurs 100% završen)
- [ ] PDF generisanje (QuestPDF ili PuppeteerSharp)
- [ ] Upload PDF na Backblaze B2
- [ ] Download certificate endpoint
- [ ] Certificates tabela + migracija

**Definition of Done:** Kada korisnik završi kurs, generiše se PDF sertifikat koji može da preuzme.

---

### Phase 10 — Administration

**Tasks:**

- [ ] Admin dashboard API endpointi
- [ ] User management (list, ban, promote to instructor)
- [ ] Course management (force publish/archive)
- [ ] Payment overview
- [ ] Basic audit log (ko je šta uradio, timestamp)

**Definition of Done:** Admin može da upravlja korisnicima, kursevima i vidi pregled plaćanja.

---

### Phase 11 — Production Hardening

**Tasks:**

- [ ] Sve Health check endpointi verifikovani
- [ ] Prometheus metrics setup po servisu
- [ ] Grafana dashboardi (CPU, memory, response time, error rate, DB latency)
- [ ] pg_dump backup skripta → Backblaze B2 (cron job)
- [ ] Restore procedure dokumentovana i testirana
- [ ] Rate limiting (ASP.NET Core built-in)
- [ ] Security review (OWASP Top 10 checklist)
- [ ] GitHub Actions CI/CD pipeline za sve servise

**Definition of Done:** Sve servise imaju monitoring, backupi rade automatski, CI/CD pipeline deployuje na Hostinger
VPS.

---

## 11. Deployment

### Hostinger VPS Specifikacija

- 2 vCPU, 8 GB RAM, 100 GB SSD
- Ubuntu 24.04 LTS
- Docker + Docker Compose

### docker-compose.yml (produkcija)

```yaml
services:
  nginx:
    image: nginx:alpine
    ports: ["80:80", "443:443"]
    volumes:
      - ./nginx/nginx.conf:/etc/nginx/nginx.conf
      - ./certbot/conf:/etc/letsencrypt

  frontend:
    image: ghcr.io/${REPO}/frontend:latest
    depends_on: [nginx]

  auth-service:
    image: ghcr.io/${REPO}/auth-service:latest
    environment:
      - ConnectionStrings__AuthDb=${AUTH_DB_CONNECTION}
      - JwtSettings__SecretKey=${JWT_SECRET_KEY}
      - Redis__Connection=${REDIS_CONNECTION}
    depends_on: [postgres, redis]

  course-service:
    image: ghcr.io/${REPO}/course-service:latest
    environment:
      - ConnectionStrings__CourseDb=${COURSE_DB_CONNECTION}
      - JwtSettings__SecretKey=${JWT_SECRET_KEY}
      - Redis__Connection=${REDIS_CONNECTION}
    depends_on: [postgres, redis]

  quiz-service:
    image: ghcr.io/${REPO}/quiz-service:latest
    environment:
      - ConnectionStrings__QuizDb=${QUIZ_DB_CONNECTION}
      - JwtSettings__SecretKey=${JWT_SECRET_KEY}
    depends_on: [postgres]

  payment-service:
    image: ghcr.io/${REPO}/payment-service:latest
    environment:
      - ConnectionStrings__PaymentDb=${PAYMENT_DB_CONNECTION}
      - Stripe__SecretKey=${STRIPE_SECRET_KEY}
      - Stripe__WebhookSecret=${STRIPE_WEBHOOK_SECRET}
    depends_on: [postgres]

  postgres:
    image: postgres:16
    environment:
      POSTGRES_PASSWORD: ${POSTGRES_ROOT_PASSWORD}
      AUTH_DB_PASSWORD: ${AUTH_DB_PASSWORD}
      COURSE_DB_PASSWORD: ${COURSE_DB_PASSWORD}
      QUIZ_DB_PASSWORD: ${QUIZ_DB_PASSWORD}
      PAYMENT_DB_PASSWORD: ${PAYMENT_DB_PASSWORD}
    volumes:
      - postgres-data:/var/lib/postgresql/data
      - ./infrastructure/postgres/init.sh:/docker-entrypoint-initdb.d/init.sh

  redis:
    image: redis:7-alpine
    command: redis-server --requirepass ${REDIS_PASSWORD}
    volumes: [redis-data:/data]

  seq:
    image: datalust/seq
    environment:
      ACCEPT_EULA: Y

volumes:
  postgres-data:
  redis-data:
```

### Nginx Routing

```nginx
server {
    listen 80;
    server_name espanolconvagabundos.com;

    location /api/v1/auth/   { proxy_pass http://auth-service:8080; }
    location /api/v1/courses/ { proxy_pass http://course-service:8080; }
    location /api/v1/quizzes/ { proxy_pass http://quiz-service:8080; }
    location /api/v1/payments/ { proxy_pass http://payment-service:8080; }
    location /                 { proxy_pass http://frontend:80; }
}
```

---

## 12. Monitoring & Operations

### Logging (Serilog → Seq)

Svaki servis:

```csharp
builder.Host.UseSerilog((ctx, cfg) =>
    cfg.ReadFrom.Configuration(ctx.Configuration)
       .WriteTo.Seq(ctx.Configuration["SeqUrl"]!));
```

Log levels: Information (normalan flow), Warning (neočekivan input), Error (exception), Critical (servis pao).

### Metrics (Prometheus + Grafana)

- `prometheus-net.AspNetCore` NuGet package u svakom servisu
- `/metrics` endpoint po servisu
- Grafana dashboardi: CPU, memory, HTTP request rate, p95 response time, DB query latency, error rate (5xx)

### Backup strategija

```bash
# Nightly cron na serveru (2:00 AM)
pg_dump -h localhost -U authuser authdb | gzip > authdb_$(date +%Y%m%d).sql.gz
# Upload na Backblaze B2
rclone copy authdb_*.sql.gz b2:backup-bucket/postgres/
```

**Retention:** Daily backups — 30 dana, Weekly — 12 nedelja, Monthly — 12 meseci.

**Restore procedure:** Redovno testirati restore u staging okruženju (jednom mesečno).

---

## 13. CI/CD (GitHub Actions)

### Pipeline po servisu (`.github/workflows/auth-service.yml`)

```
Trigger: push to dev ili main (u services/AuthService/**)

Steps:
1. Build   → dotnet build
2. Test    → dotnet test
3. Docker Build → docker build -t ghcr.io/{repo}/auth-service:{tag}
4. Push    → docker push (samo za main branch)
5. Deploy  → docker-compose pull && docker-compose up -d auth-service
             (migracije se pokreću automatski na startup via MigrateAsync)
6. Health  → curl http://server/api/v1/auth/health
7. Rollback→ ako health check failuje, rollback na prethodnu verziju

Napomena: Eksplicitni `dotnet ef database update` korak u CI/CD prelazimo u Phase 11
kada migracije postanu kritičan produkcijski proces koji zahteva zasebnu kontrolu.
```

---

## 14. Secrets Management

### Development

- `appsettings.Development.json` — ne commitovati sensitive podatke
- `.env` fajlovi — dodati u `.gitignore`
- Commitovati `appsettings.Development.json.example` i `.env.example` kao template

### Produkcija

- Environment variables u Docker Compose ili Docker Secrets
- Nikad secrets u docker-compose.yml koji ide u git
- `docker-compose.yml` koristi `${VAR}` syntax — vrednosti su na serveru u `.env` fajlu van repozitorijuma

### .gitignore pravila

```
.env
.env.*
!.env.example
appsettings.Development.json
!appsettings.Development.json.example
*.pfx
*.p12
```

---

## 15. Future Roadmap

Ove funkcionalnosti su van scope za inicijalni development, ali treba ih imati na umu pri arhitekturalnim odlukama.

| Feature                            | Zahteva                                                                   |
|------------------------------------|---------------------------------------------------------------------------|
| AI Tutor                           | Nova tabela: Conversations, ConversationMessages; Claude API integracija  |
| Speech Recognition / Pronunciation | Nova tabela: PronunciationAttempts, SpeechRecordings; audio storage na B2 |
| Flashcards                         | Nova tabela: Decks, Flashcards, UserFlashcardProgress                     |
| Mobile App                         | React Native ili Flutter; isti backend API                                |
| Live Classes                       | LiveSessions, SessionAttendees; WebRTC ili Zoom/Daily.co integracija      |
| Notifications                      | Notifications, UserNotifications; email (SendGrid) + push                 |
| Teacher Analytics                  | Course engagement metrics, completion rates                               |
| Kubernetes                         | Migracija sa Docker Compose; HPA za auto-scaling                          |

---

## 16. Architecture Decision Records (ADRs)

### ADR-001: Database Topology — Shared PostgreSQL Instance

**Status:** Accepted | **Date:** Jun 2025

**Kontekst:** 4 mikroservisa, svaki treba sopstvenu logičku bazu. Single VPS (2 vCPU, 8 GB RAM).

**Odluka:** Jedan PostgreSQL Docker kontejner, 4 baze unutra (`authdb`, `coursesdb`, `quizdb`, `paymentsdb`), svaka sa
zasebnim PostgreSQL userom.

**Zašto ne 4 kontejnera:** Svaki Postgres process troši ~200 MB RAM baseline — 4 kontejnera = ~800 MB samo za DB.
Logička izolacija (zasebni user/baza) je dovoljna dok smo na jednom serveru.

**Implementacija:** `infrastructure/postgres/init.sql` kreira sve baze i usere pri prvom pokretanju kontejnera. Svaki
servis pristupa isključivo svojoj bazi.

**Migracija ka fizičkoj izolaciji:** Kada (ako) naraste potreba za zasebnim serverima po servisu, jedino što se menja je
`Host=` u connection stringu svakog servisa.

---

### ADR-002: Redis — Od Phase 1, Minimalni Use-casevi

**Status:** Accepted | **Date:** Jun 2025

**Kontekst:** Redis nije neophodan, ali postoje use-casevi koji ga opravdavaju od samog starta. RAM overhead je ~50 MB.

**Odluka:** Dodati Redis u Phase 1 infrastrukturu. Koristiti samo za:

1. **Refresh token blacklist** — `blacklist:{token}` key sa TTL = token expiry. Logout stvarno invalidira token.
2. **OTP kodovi** (email verifikacija, password reset) — TTL automatski briše expired kodove, nema cleanup job-a.
3. **Rate limiting** (Phase 1+) — `AspNetCoreRateLimit` sa Redis backend-om.

Dalje se dodaje po potrebi: course catalog cache (Phase 2), distributed lock za payment enrollment (Phase 7).

**Zašto ne Postgres za blacklist:** Svaka validacija refresh tokena morala bi da hita DB. Redis to radi u O(1) sa
TTL-om, nema periodičnog cleanup job-a.

---

*Poslednje ažuriranje: Jun 2025*
