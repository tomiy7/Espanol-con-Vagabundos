# Frontend ↔ backend: kako stvarno radi upis, plaćanje i pristup

> Za front. Zamenjuje delove `docs/API-CONTRACT.md` (grana `feature/frontend`) koji pretpostavljaju
> „kupi kurs → uplata → upis u Courses“. Poslovna odluka: **prvo raspoređivanje u grupu, pa uplata.**
> Kad se nešto promeni na backendu, menja se ovaj fajl.

---

## 1. Šta se menja u odnosu na `API-CONTRACT.md`

| U `API-CONTRACT.md` | Kako jeste |
|---|---|
| „Kupi kurs“ → `POST /payments` sa `courseId` | Kurs se **ne kupuje** dugmetom. Učenik se prijavljuje (Google forma ili upis preko Groups-a), profesor ga raspoređuje, a **uplatnica stiže sama** kad grupa dobije datum prvog časa. `POST /payments` ostaje samo za **e-knjigu**. |
| Jedna cena kursa (`price`) | Tri cene po osobi: individualno, par, grupa (+ e-knjiga). 🔜2 |
| Enrollment tabela u Courses, `GET /courses/my`, `GET /courses/{id}/access` | Upise vodi **Groups**: `GET /enrollments/mine`, `GET /enrollments/access?courseId=`. Courses **ne pravi** svoje upise. |
| Admin potvrdi uplatu → nastaje upis | Upis postoji pre uplate. Potvrda uplate samo menja status uplate. Neplaćeni se **ne izbacuju** automatski. |
| Lekcija: `403 NOT_ENROLLED` ako nije kupio | Lekcija se otvara po rasporedu grupe (3 lekcije nedeljno od starta). 🔜2 |
| Greške `{ "error", "message" }` sa porukom na srpskom | **ProblemDetails sa `code`** (vidi §7). Front mapira `code` na svoju srpsku poruku. |
| Payments na portu 5004 | 5004 je zauzeo Notifications. Payments dobija **5005**. 🔜3 |
| Admin potvrđuje uplate, admin unosi akcije | **Profesor i admin** (osoblje) imaju ista prava za uplate i akcije. 🔜3 |

---

## 2. Tok za kurs (korak po korak)

1. **Prijava za kurs.** Jedno od:
   - osoblje dodaje učenika u grupu: `POST /groups/{groupId}/members` `{ "userId": "..." }` ✅
   - učenik sam ulazi u otvorenu grupu: `GET /groups/available?courseId=` pa `POST /enrollments/group` `{ "groupId": "..." }` ✅
   - individualni časovi: `POST /enrollments/individual` `{ "courseId", "professorId" }` ✅
   - par: `POST /enrollments/invite-partner` `{ "courseId", "inviteeUserId" }`; pozvani dobija mejl sa linkom
     `/invitations/{token}` → `GET /invitations/{token}`, `POST /invitations/{token}/accept|decline` ✅
2. **Profesor postavlja datum prvog časa:** `PUT /groups/{groupId}/planned-start` `{ "plannedStartAt": "2026-12-08T17:00:00Z" }` ✅
   (preporuka: slati sa zonom ili u UTC). `GET /groups/{id}` vraća `plannedStartAt` i `confirmedAt`.
3. **Grupa je potvrđena** (ima dovoljno članova i datum) → Payments izdaje uplatnicu svakom članu,
   učenik dobija mejl sa linkom. 🔜3
4. **Učenik plaća** (uplatnica / IPS QR). Vidi svoje uplate: `GET /payments/my`, PDF: `GET /payments/{id}/slip`. 🔜3
5. **Osoblje potvrđuje uplatu:** `POST /payments/{id}/confirm`. 🔜3 (sada postoji, samo za admina)
6. **Profesor pokreće grupu:** `POST /groups/{groupId}/start` ✅ → lekcije se otvaraju 3 nedeljno.

Učenik koji kasnije uđe u već potvrđenu grupu dobija svoju uplatnicu. Ako profesor pomeri datum potvrđene
grupe, učenici dobijaju mejl, a rokovi neplaćenih rata se pomeraju. 🔜3

## 3. Tok za e-knjigu

`POST /payments` `{ "courseId", "productType": "Ebook", "payerName" }` — **bez `amount`/`currency`**, cenu čita server. 🔜3
Kurs uključuje e-knjigu, pa ko ima kurs ne kupuje je posebno.

---

## 4. Pristup kursu i „Moji kursevi“

### ✅ `GET /enrollments/mine` [prijavljen]
Zamenjuje `GET /courses/my`.
```json
[
  {
    "id": "enrollment-guid",
    "courseId": "guid",
    "courseName": "Španski A1",
    "type": "Group",
    "status": "Active",
    "groupId": "guid",
    "createdAt": "2026-11-20T10:00:00Z",
    "activatedAt": "2026-12-08T17:00:00Z",
    "cancelledAt": null,
    "completedAt": null
  }
]
```
`type`: `Individual` · `Pair` · `Group`
`status`: `Searching` (čeka grupu) · `Ready` (grupa popunjena, čeka start) · `Active` · `Frozen` · `Completed` · `Cancelled`
`courseName` je `null` ako Courses ne odgovara — prikazati svoj tekst.

### ✅ `GET /enrollments/access?courseId={id}` [prijavljen]
Zamenjuje `GET /courses/{id}/access` („Kupi kurs“ / „Nastavi učenje“).
```json
{ "hasAccess": true, "enrollmentId": "guid", "unlockedLessons": 6, "accessUntil": null }
```
`hasAccess` je `true` samo za aktivan upis (grupa je počela). Za upis koji čeka grupu: `GET /enrollments/mine`.
Posle završetka kursa materijal ostaje dostupan još 30 dana (podešavanje `Enrollment:CompletedAccessDays`):
tada je `accessUntil` datum isteka — za poruku „Sadržaj kursa možete da vidite do ...“. Za aktivan upis je `null`.
Zamrznut upis: nema pristup (za dogovor).

---

## 5. Courses 🔜2

> **Pre puštanja nove verzije Courses-a:** u Directus-u, kolekcija `courses`, dodati tri polja tipa
> **Decimal**, **nisu obavezna**, tačno ovih naziva: `price_individual`, `price_pair`, `price_group`.
> Bez njih katalog i lekcije vraćaju 500. Provera: `curl localhost:5002/courses` → 200.
> Front sada čita kurseve **direktno iz Directus-a** (`/items/courses/{id}`) — tada javna uloga mora da sme
> da čita i nova polja cena.

### `GET /courses`, `GET /courses/{courseId}`, `GET /courses/{slug}` (javno)
Dobijaju cene (sve po osobi, `null` = još nije uneta). `price` ostaje privremeno, front treba da pređe na `prices`:
```json
"prices": { "individual": 15000, "pair": 11000, "group": 8000, "ebook": 1500 }
```
Javni detalj kursa **više ne vraća `ebookPdf`** (ID fajla e-knjige bio je javan).

### `GET /courses/{courseId}/pricing` (javno)
Cene po ID-u kursa (koristi ga Payments; radi i za kurs skinut iz ponude):
```json
{ "courseId": "guid", "currency": "RSD", "individual": 15000, "pair": 11000, "group": 8000, "ebook": 1500 }
```

### `GET /courses/lessons/{lessonId}` [prijavljen]
- osoblje: uvek; učenik: samo otključane lekcije kursa na koji je upisan.
- `403` `NO_COURSE_ACCESS` (nije upisan / grupa nije počela) · `403` `LESSON_LOCKED` (još nije otključana)
  · `404` `LESSON_NOT_FOUND` (ne postoji ili nije objavljena) · `503` `ACCESS_CHECK_UNAVAILABLE` (pokušati ponovo).
- Odgovor dobija `courseId`; sekcije sortirane po `order`; `content` kao JSON objekat. ❓ ako se ispostavi
  da Directus čuva nešto što nije validan JSON, ostaje string.

### Progres ❓
`/progress/...` iz `API-CONTRACT.md` je u planu za Courses (po upisu iz Groups-a), ali **posle** dela 2.
Do tada progres ostaje u pregledaču.

---

## 6. Plaćanja 🔜3 (nacrt, detalji kad krene deo 3)

- `GET /payments/my` — uplate učenika: iznos, popust, rok, status (`Pending`, `Completed`, `Overdue`...).
- `GET /payments/{id}/slip` — PDF uplatnice (Nalog za uplatu, obrazac 1, sa IPS QR kodom). PDF se ne čuva,
  pravi se na zahtev, pa se uvek može ponovo preuzeti.
- Plaćanje odjednom ili u dve rate; rokovi od datuma prvog časa.
- `GET /pricing/quote?courseId=&type=` — cena sa važećom akcijom (za prikaz na sajtu).
- Akcije (popusti po datumima) i kasnije paketi: unose ih profesor i admin kroz admin panel.
- **Valuta:** plaća se u **RSD** (IPS QR i uplatnica rade samo u dinarima). Kasnije: prikaz cene i u drugim
  valutama (npr. €) po srednjem kursu NBS, samo informativno — backend će davati kurs, front preračunava za prikaz.

---

## 7. Greške (Groups sada, Courses 🔜2, Payments 🔜3)

```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.1",
  "title": "Bad Request",
  "status": 400,
  "detail": "First lesson date must be in the future.",
  "code": "CANNOT_SCHEDULE",
  "traceId": "00-..."
}
```
- **Front gleda `code`** i prikazuje svoju poruku na srpskom. `detail` je na engleskom, za developere, i može da se menja.
- `401` = token istekao/nema ga (odjava) · `403` = nema prava · `404` = ne postoji · `409` = konflikt (npr. dva
  istovremena upisa na poslednje mesto, pokušati ponovo).
- Spisak kodova za Groups: `Services/Groups/GroupsService.API/Errors/ErrorCodes.cs`.

---

## 8. Uloge

| Akcija | Učenik | Profesor | Admin |
|---|---|---|---|
| Svoji upisi, pristup, lekcije | ✅ | ✅ | ✅ |
| Grupe: pravljenje, članovi, termini, datum, start, zamrzavanje | | ✅ | ✅ |
| Zatvaranje grupe, izmena već dodeljenog termina, audit log (`GET /audit`) | | ✅ | ✅ |
| Potvrda uplate, akcije/popusti 🔜3 | | ✅ | ✅ |

Profesori imaju ista prava kao admin. Jedina razlika u Groups-u: profesor otvara grupu samo sa sobom kao
profesorom u terminima, admin može da upiše bilo kog profesora.

---

## 9. Ostalo iz `API-CONTRACT.md`

- ✅ Groups → Auth putanja za imena (`/auth/admin/users/{id}`) je ispravljena; za imena u lokalnom radu treba
  `AUTH_SERVICE_ADMIN_TOKEN` u `.env` (privremeno, do bezbednosne runde).
- ❓ `GET /auth/me` bez `gender`, `PATCH /auth/admin/users/{id}/role`, profesor čita `GET /auth/admin/users/{id}` —
  Auth još nije pregledan u ovoj rundi.
- **Directus dozvole:** kad front pređe na `GET /courses/lessons/{id}`, javna uloga u Directus-u gubi čitanje
  `sections`, `tasks` i `lessons.recording_url` (kako i piše u `API-CONTRACT.md`), i **fajlova** (e-knjiga).
