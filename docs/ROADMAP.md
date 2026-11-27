# Español con Vagabundos — Roadmap (backend)

> Živi dokument: šta je urađeno i šta je ostalo. Kad se stavka završi ili se pojavi nova,
> menjati ovde. Arhitekturalne odluke su u [ARCHITECTURE.md](ARCHITECTURE.md).

Poslednja izmena: 2026-11-27

---

## Urađeno

| Oblast | Šta | PR |
|---|---|---|
| Groups | Upisi (individualno, par, grupa), grupe, termini, start, zamrzavanje, otkazivanje, završetak | #5 |
| Groups | Konkurentnost na nivou baze (verzije, xmin, advisory lock), ProblemDetails greške sa kodom | #5 |
| Groups | Docker image, `/health`, smoke testovi, CI | #5 |
| Messaging | RabbitMQ biblioteka (`BuildingBlocks/Messaging`), outbox, potrošač sa ponavljanjem i dead-letter redom | #6 |
| Groups | Testovi otkaza (baza, broker, Auth/Courses), chaos skripta `scripts/chaos-groups.ps1` | #6 |
| Groups | Provera ulaza i testovi zloupotrebe (tokeni, nevalidni podaci, zaobilaženje pravila) | #7 |
| Notifications | Mejlovi učenicima (srpski, šabloni na jednom mestu), Mailpit lokalno, zaštita od duplikata | #8 |

---

## Sledeće (redosled)

### 1. Groups ↔ Payments (tri dela)
1. **Groups: planirani datum prvog časa** — `PUT /groups/{id}/planned-start`, događaji `GroupConfirmed`,
   `MemberJoinedConfirmedGroup`, `FirstLessonRescheduled` (+ mejl). Dizajn:
   [specs/2026-11-25-groups-first-lesson-date-design.md](superpowers/specs/2026-11-25-groups-first-lesson-date-design.md).
2. **Courses dorada** — tri cene po osobi (polja u Directus-u), `GET /courses/{id}/pricing`, zaštićene lekcije
   (pristup iz Groups-a), ProblemDetails, `/health`, testovi, CI. Directus ostaje (Courses samo čita).
   Dizajn: [specs/2026-11-27-courses-hardening-design.md](superpowers/specs/2026-11-27-courses-hardening-design.md).
   Za front: [FRONTEND-INTEGRATION.md](FRONTEND-INTEGRATION.md).
3. **Payments prerada** — uplatnica iz događaja (iznos sa servera, zaključan pri izdavanju), cela suma ili dve rate,
   rokovi od datuma prvog časa (pomeraju se za neplaćene), blaga strogost (zakasnela = oznaka za osoblje),
   PDF na zahtev u izgledu Naloga za uplatu (Obrazac 1) + NBS IPS QR, testovi, CI, compose.
   Pravila su podešavanja, ne konstante u kodu.
   Akcije/popusti (po datumima) i kasnije paketi u Payments-u; unose ih profesor i admin.
4. ✅ **Profesori ≈ admin u Groups-u** — ručno zatvaranje grupe, izmena dodeljenog termina i audit log i za profesore.
5. **Courses: progres** (`/progress/...` iz front ugovora) i `CourseCompleted` — prvo dogovor šta je „završen kurs“.
6. ✅ **Pristup posle završetka kursa** (Groups) — završen upis i dalje vidi materijal određeni period
   (`Enrollment:CompletedAccessDays`, podrazumevano 30 dana od `CompletedAt`); `/enrollments/access` vraća i `accessUntil`, a front prikazuje
   „Sadržaj kursa možete da vidite do ...“. Istorija upisa (aktivni, zamrznuti, završeni) ostaje u `GET /enrollments/mine`.
   **Otvoreno:** da li zamrznut učenik vidi sadržaj.
7. **Prikaz cena u drugim valutama** — srednji kurs NBS, samo informativno (plaća se u RSD).

### 2. Bezbednosna runda
- **Service-to-service autentikacija.** Notifications i Groups sada čitaju korisnike iz Auth-a statičkim
  admin tokenom (`AUTH_SERVICE_ADMIN_TOKEN`) koji ističe za 30 minuta. Posle toga mejlovi i imena prestaju
  da rade. **Blokira produkciju.**
- Rate limit (pogađanje tokena pozivnica, spam upisa) i ograničenje veličine zahteva.
- JWT sa issuer/audience (TODO u Groups `Program.cs`), eventualno RS256.

### 3. Otvorena poslovna pitanja (prvo dogovor, pa kod)
- Zamrznut učenik kad se grupa završi: kako nastavlja? Operacija **prebacivanja u drugu grupu** ne postoji.
- Ručni „complete“ grupe: da li automatski završava i sve aktivne članove?
- Istek maksimalnog zamrzavanja (30 dana): podsetnik profesoru, automatska odluka ili ništa?
- Prihvatanje pozivnice preko linka iz mejla: sada bez prijave (token je dovoljan). Tražiti prijavu kao pozvani korisnik?

### 4. Isti nivo kvaliteta za ostale servise
- Auth, Courses, Payments: testovi zloupotrebe i otkaza kao za Groups (vidi `Services/Groups/GroupsService.Integration.Tests/Security` i `Resilience`).
- Courses: ukloniti neiskorišćen AutoMapper.

### 5. Deploy
- Migracije u produkciji kao eksplicitan korak (sada se same primenjuju samo u Development).
- Hosting, čuvanje tajni (ne `.env` na serveru), backup baza, HTTPS.
- Pravi mejl: sopstveni domen, SPF/DKIM/DMARC, provajder (npr. Brevo, Amazon SES), `FRONTEND_BASE_URL`.
  U kodu se ništa ne menja, samo podešavanja (`SMTP_*`).

---

## Paralelno (druge grane)

- **Courses objavljuje `CourseCompleted`** (ugovor u `BuildingBlocks/Messaging/Contracts/CoursesEvents.cs`).
  Bez toga Groups ne zna da je učenik završio kurs. Courses takođe pita Groups `GET /enrollments/access`
  za broj otključanih lekcija.
- **Front i admin panel.** Endpoint-i za panel (grupe, uplate, poslati mejlovi, audit) po potrebi panela.
  Mejlovi profesorima ako zatrebaju.

---

## Kasnije (nije hitno)

- Praćenje rada: centralni logovi, metrike (poruke koje čekaju u outbox-u, `.dead` redovi) i alarmi.
- GDPR: anonimizacija korisnika umesto brisanja.
- Podsetnici („čas je sutra“, „poziv ističe za 1 dan“) — traži planer zadataka.
