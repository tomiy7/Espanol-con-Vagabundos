# Groups: planirani datum prvog časa i potvrđena grupa

Deo 1 od 3 za Groups ↔ Payments (1. Groups datum, 2. Courses dorada i tri cene, 3. Payments prerada).

## Zašto
Dogovoreno pravilo: uplatnica se izdaje tek kad grupa ima dovoljno članova **i** poznat datum prvog časa,
a rok za (prvu) uplatu je pre prvog časa. Groups danas zna datum tek na `Start`, kad je nastava već počela.

## Cilj
- Profesor/admin upisuje i menja **planirani datum prvog časa** dok grupa nije počela.
- Groups objavljuje jasan trenutak „grupa potvrđena“ (dovoljno članova + datum), kasno pridružene članove
  potvrđene grupe i pomeranje datuma. Payments i Notifications samo reaguju.

## Ne-ciljevi
- Pravila plaćanja (rate, procenti, rokovi, strogost) — to je Payments, kao podešavanja.
- `Start` se ne menja: i dalje radi i bez planiranog datuma (stare grupe, hitni slučajevi).

## Pristupi
- **A (izabrano): Groups javlja jedan trenutak.** „Potvrđena grupa“ je pojam grupe; rokovi ostaju u Payments-u.
- B: Groups javlja sirove činjenice, Payments drži kopiju stanja grupe i sam odlučuje. Odbačeno: dupliranje
  stanja i složen Payments za malo dobiti.

## Dizajn
- `Group.PlannedStartAt` (UTC, nullable) i `Group.ConfirmedAt` (UTC, nullable). Migracija preko EF-a.
- `Group.SchedulePlannedStart(DateTime at, DateTime now)`:
  - samo u statusu Forming ili Ready; datum mora biti u budućnosti i najviše godinu dana unapred;
  - prvi put postavljen → `FirstLessonScheduled`; promena postojećeg → `FirstLessonRescheduled(old, new)`;
    isti datum ponovo → ništa.
- Potvrda: kad je grupa Ready **i** ima `PlannedStartAt`, a `ConfirmedAt` je prazan → `ConfirmedAt = now`
  i domenski događaj `GroupConfirmed`. Proverava se posle `AddMember` i posle `SchedulePlannedStart`.
  Potvrda se dešava **jednom**; ako grupa posle padne u Forming, ostaje potvrđena (izdate uplatnice ostaju,
  otkazani član ide kroz postojeći `EnrollmentCancelled`).
- Član ušao u već potvrđenu grupu → `MemberJoinedConfirmedGroup` (njemu treba uplatnica).
- Endpoint: `PUT /groups/{id}/planned-start` `{ "plannedStartAt": "..." }`, profesor ili admin, audit zapis kao
  ostale akcije profesora. `GET` grupe vraća `plannedStartAt` i `confirmedAt`.
- Integracioni događaji (outbox, `BuildingBlocks/Messaging/Contracts/GroupsEvents.cs`):
  - `GroupConfirmed(GroupId, CourseId, GroupType, FirstLessonAt, Members)`
  - `MemberJoinedConfirmedGroup(GroupId, CourseId, GroupType, FirstLessonAt, Member)`
  - `FirstLessonRescheduled(GroupId, CourseId, OldFirstLessonAt, NewFirstLessonAt, Members)`
- Važi za sva tri tipa (Individual i Pair su grupe od 1 i 2 člana).
- Notifications: mejl učenicima kad se datum pomeri (srpski šablon). Mejl o uplatnici šalje se u delu 3.

## Odluke za Payments (deo 3), zabeležene da se ne izgube
- Rokovi se **računaju iz datuma prvog časa**; kad se datum pomeri, rokovi neplaćenih rata se pomeraju,
  plaćene ostaju. Učenik dobija mejl.
- Strogost (podrazumevano blago, menja se podešavanjem): rok koji prođe samo označava uplatu kao zakasnelu
  za profesora/admina; nema automatskog izbacivanja (već dogovoreno).
- Iznos dolazi od servera (cena iz Courses po tipu upisa, zaključana u trenutku izdavanja), nikad od klijenta.
- PDF se ne čuva: generiše se na zahtev iz sačuvanih podataka. Izgled: Nalog za uplatu (Obrazac 1) + NBS IPS QR.

## Testovi
- Domen: postavljanje/pomeranje datuma, potvrda tačno jednom, redosled (prvo datum pa članovi i obrnuto),
  kasni član, granice datuma, zabrana posle Start.
- Integracija: endpoint (prava, validacija, audit), događaji u outbox-u, zloupotrebe (prošli datum, tuđa uloga).
- Notifications: mejl za pomeren datum.

## Ostavljeno graditelju
- Tačan naziv šablona i teksta mejla; oblik audit zapisa (prati postojeće).
