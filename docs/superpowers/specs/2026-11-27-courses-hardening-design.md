# Courses: tri cene, zaštita lekcija, testovi i CI

Deo 2 od 3 za Groups ↔ Payments. Directus ostaje alat kojim osoblje unosi kurseve, lekcije i cene.

## Zašto
- Payments (deo 3) mora da zna cenu kursa po tipu upisa i cenu e-knjige, sa servera.
- `GET /courses/lessons/{id}` vraća ceo sadržaj i link snimka **bez prijave** — plaćeni sadržaj je javan.
- Courses nema testove, CI, `/health`, i ima nepotrebne pakete (AutoMapper, preview `Microsoft.Extensions.Configuration` 11).

## Model prodaje (od korisnika)
- Samo e-knjiga (`ebook_price`) ili kurs; kurs uključuje e-knjigu.
- Kurs ima tri cene: individualno (najskuplje), par, grupa.
- Kasnije: popusti (akcijski dani) i paketi — računaju se u Payments-u pri izdavanju uplatnice
  (redovna cena → popust → konačan iznos, sve upisano u uplatnicu). Courses čuva samo redovne cene.

## Ne-ciljevi (ova runda)
- Progres (`/progress/...` iz front ugovora) — sledeći korak posle ovog, po upisu iz Groups-a.
- Napredak kroz lekcije i `CourseCompleted` — poseban posao; prvo dogovor šta je „završen kurs“ (završni test?).
- Puni slojevi kao u Groups: Courses samo čita Directus tabele, slojevi ne bi doneli ništa.
- Isporuka e-knjige kupcu (preuzimanje posle uplate) — uz Payments/e-knjigu.

## Pristupi za zaštitu lekcija
- **A (izabrano): Courses pita Groups** `GET /enrollments/access?courseId=` prosleđujući korisnikov token.
  Groups već vraća `hasAccess` i `unlockedLessons`; nije potrebna servisna autentikacija.
- B: Courses drži kopiju upisa iz događaja. Odbačeno: dupliranje stanja i pravila otključavanja.

## Dizajn
- **Cene (šema u Directus-u, ne EF):** polja `price_individual`, `price_pair`, `price_group` (decimal)
  u kolekciji `courses`. Cena je **iznos koji plaća jedan učenik** (i za par). Polja dodaje korisnik u Directus-u
  (uputstvo u PR-u), pre nego što se pusti nova verzija Courses-a.
  Entitet ih mapira kao `decimal?`: nova polja su NULL dok ih osoblje ne popuni, a nenullable mapiranje bi
  srušilo `GET /courses`.
- **Javni API (samo dodavanje):** lista i detalj kursa dobijaju `prices { individual, pair, group, ebook }`
  (null = cena nije uneta); `price` ostaje dok front ne pređe. Uklanjanje kolone `price` je poseban korak uz odobrenje.
- **Za Payments:** `GET /courses/{id}/pricing` po ID-u, **i za neobjavljene kurseve** (grupa može da traje i kad
  je kurs skinut iz ponude; kasni član i dalje treba uplatnicu). 404 samo ako kurs ne postoji.
  Odgovor: `{ courseId, currency, individual, pair, group, ebook }`; neuneta cena je `null` i o tome odlučuje
  Payments (javlja osoblju). `currency` iz podešavanja `Pricing:Currency` (podrazumevano RSD).
- **E-knjiga:** javni detalj kursa više ne vraća `ebookPdf` (ID fajla u Directus-u). Korisnik proverava da javna
  uloga u Directus-u nema čitanje fajlova.
- **Lekcija:** `GET /courses/lessons/{id}` traži prijavu.
  - profesor/admin: uvek;
  - učenik: Groups kaže `hasAccess` i **pozicija** lekcije (1, 2, 3... među lekcijama kursa sortiranim po `Order`;
    Directus sort može početi od 0, imati rupe ili null) `<= unlockedLessons`; inače 403 `LESSON_LOCKED`
    (bez sadržaja); nema pristupa kursu → 403 `NO_COURSE_ACCESS`;
  - Groups ne odgovara → 503 `ACCESS_CHECK_UNAVAILABLE` (ne puštamo sadržaj „na slepo“).
  - lekcija ne postoji, nije `is_published`/`is_visible`, ili kurs nije objavljen → 404 `LESSON_NOT_FOUND`.
  - Pozicija za otključavanje se broji samo među objavljenim i vidljivim lekcijama kursa.
  - Odgovor (iz front ugovora): dodaje `courseId`; sekcije i zadaci sortirani po `order`; `content` kao JSON
    objekat (ako u bazi nije validan JSON, vraća se kao string).
  - Lista lekcija u detalju kursa ostaje javna (naslovi, bez sadržaja), samo objavljene i vidljive.
- **Greške:** ProblemDetails sa `code`, kao u Groups (front mapira `code` na srpsku poruku).
- **Front:** sve promene opisane u `docs/FRONTEND-INTEGRATION.md`.
- **Operativno:** `/health` (baza), Dockerfile proveren, uklonjeni AutoMapper i preview paket.

## Testovi i CI
- Integracioni testovi sa pravim Postgres-om; šema se pravi iz EF modela (`EnsureCreated`) jer je u produkciji pravi Directus.
- Groups se u testovima zamenjuje lažnim klijentom (pristup, zaključano, Groups pao).
- Slučajevi: anonimno, učenik sa/bez pristupa, zaključana lekcija, osoblje, nepostojeća lekcija/kurs,
  neobjavljen kurs se ne vidi, cene u odgovoru (i NULL cene), `Order` sa rupama/od 0, nevalidan token,
  `ebookPdf` nije u javnom odgovoru.
- `.github/workflows/courses.yml`: build, testovi, docker build + smoke `/health`.
