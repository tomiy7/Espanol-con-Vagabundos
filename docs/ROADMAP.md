# Español con Vagabundos — Roadmap (backend)

> Živi dokument: šta je urađeno i šta je ostalo. Kad se stavka završi ili se pojavi nova,
> menjati ovde. Arhitekturalne odluke su u [ARCHITECTURE.md](ARCHITECTURE.md).

Poslednja izmena: 2026-11-24

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

### 1. Groups ↔ Payments
- Uplatnica kad je grupa popunjena i ima datum prvog časa (`GroupBecameReady` / `GroupStarted` već nose članove).
- Pravila su još nacrt: jedna uplata ili dve rate, ručna potvrda uplate (profesor/admin), povraćaj.
  Rokovi i procenti su podešavanja, ne konstante u kodu.

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
