# API ugovor: frontend ↔ backend (MVP nedelja)

Ovo je dogovor šta backend vraća i šta frontend očekuje. Ako nešto treba drugačije, **promeni ovde i javi**, pa frontend prati ovaj fajl.

Oznake: ✅ već postoji · 🔧 postoji, treba izmena · 🆕 treba napraviti

## Opšta pravila

- Svi ID-jevi su GUID (string). Kursevi se na frontendu otvaraju po **id**, ne po slug-u.
- JSON polja su camelCase (podrazumevano u ASP.NET-u). Enum-i se šalju kao tekst (`"Pending"`, `"Course"`).
- Zaštićeni endpointi: `Authorization: Bearer <accessToken>`.
- Greške uvek u obliku koji već koristite:
  ```json
  { "error": "NOT_ENROLLED", "message": "Nemaš pristup ovom kursu." }
  ```
  `message` frontend prikazuje korisniku, pa neka bude na srpskom gde je moguće.
- Statusi: `401` = nema tokena ili je istekao (frontend odjavljuje korisnika) · `403` = prijavljen, ali nema pristup · `404` = ne postoji ili nije objavljeno.
- CORS: dozvoliti `http://localhost:3000` (i kasnije produkcijski domen) na **sva tri** servisa.

Adrese u frontendu (`frontend/.env.local`):

| Servis | Promenljiva | Lokalno |
|---|---|---|
| Auth | `NEXT_PUBLIC_AUTH_URL` | `http://localhost:5001` |
| Courses | `NEXT_PUBLIC_COURSES_URL` | `http://localhost:5002` |
| Groups | `NEXT_PUBLIC_GROUPS_URL` | `http://localhost:5003` |
| Payments | `NEXT_PUBLIC_PAYMENTS_URL` | `http://localhost:5004` |

**PaymentsService još nije u `docker-compose.override.yml`** (nema port, bazu ni JWT), pa ne radi. Dodati po uzoru na `courses.api`, na portu **5004** (5003 je zauzeo Groups):
```yaml
  payments.api:
    container_name: payments.api
    ports:
      - "5004:8080"
    environment:
      - ConnectionStrings__Default=Host=paymentsdb;Port=5432;Database=paymentsdb;Username=postgres;Password=${DB_PASSWORD}
      - Jwt__Secret=${JWT_SECRET}
      - ASPNETCORE_ENVIRONMENT=Development
    depends_on:
      - paymentsdb
    networks:
      - backend
```
Napomena: u `appsettings.Development.json` PaymentsService-a JWT ključ je drugačiji od onog u Auth-u, a baza je na 5437 umesto 5436. Preko compose-a se koristi isti `JWT_SECRET` kao Auth. Migracije se ne pokreću same, treba ih primeniti na `paymentsdb`.

---

## Auth: AuthService

### ✅ `POST /auth/login`, `POST /auth/register`, `GET /auth/check-username`, `GET /auth/check-email`
Ostaju kako jesu.

### 🔧 `GET /auth/me` [Authorize]
Radi, ali **ne vraća `gender`** (polje postoji u DTO-u, samo se ne popunjava u `Me()`). Dodati `Gender = user.Gender`.

---

## Kupovina: PaymentsService

### 🔧 `POST /payments` [Authorize]
**Izmena:** frontend više ne šalje `amount` i `currency`. Server sam čita cenu kursa (ili e-book-a), da korisnik ne može da promeni cenu u pregledaču.

Zahtev:
```json
{
  "courseId": "8f3c...-guid",
  "productType": "Course",
  "payerName": "Ana Anić"
}
```
`productType`: `"Course"` ili `"Ebook"`.

Odgovor `201` (kao sada):
```json
{
  "paymentId": "guid",
  "referenceNumber": "97 123-456",
  "qrCodeImageBase64": "iVBORw0...",
  "uplatnicaPdfBase64": "JVBERi0..."
}
```
Greške: `400` `INVALID_PAYMENT` · `404` `COURSE_NOT_FOUND` · `409` `ALREADY_ENROLLED` (već ima kurs, ne treba ponovo da plaća).

**Uplatnica na čekanju:** sada, ako korisnik već ima `Pending` uplatu za isti kurs, vraća se `400` sa porukom na engleskom ("Active payment already in pending..."). Frontend to za sada prepoznaje po tekstu poruke. Bolje: **vratiti postojeću uplatu** (isti odgovor kao gore, sa QR kodom i PDF-om), da korisnik može ponovo da preuzme uplatnicu. Ako to nije moguće, vratiti `409` sa `"error": "PAYMENT_PENDING"` i porukom na srpskom.

> **Za dogovor:** valuta. Sajt prikazuje cene u **€**, uplatnica je u **RSD**. Odlučite jedno, frontend prikazuje ono što server vrati u `GET /payments/my`.

### ✅ `GET /payments/my` [Authorize]
```json
[
  {
    "id": "guid",
    "courseId": "guid",
    "productType": "Course",
    "amount": 12000,
    "currency": "RSD",
    "referenceNumber": "97 123-456",
    "status": "Pending",
    "createdAt": "2026-11-24T18:00:00Z",
    "confirmedAt": null
  }
]
```
`status`: `Pending` · `Completed` · `Cancelled` · `Expired`

### 🔧 `POST /payments/{paymentId}/confirm` [Admin]
Postoji. **Dodati:** kad se potvrdi uplata za `Course`, kreira se enrollment u CoursesService (interni poziv, npr. `POST /internal/enrollments`). Prvu nedelju admin potvrđuje preko Swagger-a/Postman-a.

---

## Pristup kursu: CoursesService

### 🆕 Enrollment tabela
`id`, `user_id`, `course_id`, `enrolled_at`, unique (`user_id`, `course_id`).

### 🆕 `GET /courses/my` [Authorize]
Kursevi u koje je korisnik upisan (za „Moji kursevi” na `/my-account`).
```json
[
  {
    "id": "guid",
    "name": "Španski A1",
    "thumbnail": "directus-file-guid",
    "enrolledAt": "2026-11-25T10:00:00Z"
  }
]
```

### 🆕 `GET /courses/{courseId}/access` [Authorize]
Da li korisnik ima kurs (na stranici kursa: „Kupi kurs” ili „Nastavi učenje”).
```json
{ "hasAccess": true }
```

### 🔧 `GET /courses/lessons/{lessonId}` [Authorize]
Već postoji. **Izmene:**
1. `[Authorize]` + provera enrollment-a → `403 NOT_ENROLLED` ako nema kurs.
2. `404` ako lekcija nije `is_published` / `is_visible`.
3. Dodati `courseId` u odgovor.
4. `content` sekcije vratiti kao **JSON objekat**, ne kao string (sada je `jsonb` mapiran u `string`). Ako je to komplikovano, ostavite string i javite, frontend će ga parsirati.
5. Sekcije sortirane po `order`.

```json
{
  "id": "guid",
  "courseId": "guid",
  "title": "Lekcija 1: Pozdravi",
  "order": 1,
  "recordingUrl": "https://youtu.be/...",
  "sections": [
    {
      "id": "guid",
      "title": "Uvod",
      "order": 1,
      "content": { "blocks": [ { "type": "paragraph", "data": { "text": "Hola..." } } ] }
    }
  ],
  "tasks": []
}
```

---

## Progres: CoursesService

Za sada se progres čuva samo u pregledaču. Ovim prelazi na server. Svi endpointi su [Authorize] i vraćaju `403` ako korisnik nije upisan.

### 🆕 `GET /progress/courses/{courseId}`
```json
{
  "courseId": "guid",
  "completedSectionIds": ["guid", "guid"],
  "lessons": [
    { "lessonId": "guid", "totalSections": 3, "completedSections": 2 }
  ],
  "totalSections": 12,
  "completedSections": 5,
  "percentage": 42
}
```
`lessons` sadrži sve objavljene lekcije kursa, i one bez završenih sekcija. Frontend po tome crta progres po lekcijama.

### 🆕 `POST /progress/sections/{sectionId}/complete`
Označi sekciju kao završenu. Ponovni poziv nije greška. Odgovor `204`.

### 🆕 `DELETE /progress/sections/{sectionId}/complete`
Vrati na „nije završena” (dugme već postoji na sajtu). Odgovor `204`.

---

## Directus dozvole: tek kad frontend pređe na nove endpointe

Sada stranica lekcije čita sekcije **direktno iz Directus-a**. Ako se javni pristup ugasi pre nego što frontend pređe na `GET /courses/lessons/{id}`, lekcije prestaju da rade.

Kad frontend pređe (plan: četvrtak), Public rola u Directus-u:
- `courses`: read ostaje
- `lessons`: read ostaje samo za `id`, `title`, `order`, `course_id`, `is_visible`, `is_published` (bez `recording_url`)
- `sections`, `tasks`: **bez** javnog read-a

---

## Redosled (da se ne blokiramo)

| Dan | Backend | Frontend koristi |
|---|---|---|
| Pon | cena na serveru u `POST /payments`, enrollment tabela | — |
| Uto | — | `POST /payments` |
| Sre | confirm → enrollment, `GET /courses/my` | `GET /payments/my`, `GET /courses/my` |
| Čet | zaštićen `GET /courses/lessons/{id}`, `/access`, progres | lekcije preko API-ja |
| Pet | deploy | progres preko API-ja |
