# Real call signs for the LSPDFR install

Every agency in your install, the real department it is built from, how that department actually
builds a radio call sign — and the call sign each of your own cars should carry.

**✅ documented** = taken from the department's own manual, its published radio material, or a
consistent scanner-community consensus. **⚠ roleplay** = the department does not publish this (or has
no such system at all), so the entry is a plausible construction rather than a citation.

---

## The whole install at a glance

| GTA agency | Real department | Call sign shape | Example |
|---|---|---|---|
| LSPD | **LAPD** (Los Angeles Police Dept) | division – unit letter – beat | `1-ADAM-12` |
| LSSD | **LASD** (LA County Sheriff) | station number, letters for specialised units | `60`, `240`, `806` |
| SAHP | **CHP** (California Highway Patrol) | office – beat, letters for shift/role | `25-75`, `25-80M` |
| NYSP | **NYSP** (New York State Police) | zone – troop – 2-digit ID | `1A19` |
| NOOSE | LAPD SWAT / federal tactical | plain-language tactical unit (`TAC 1`) | ⚠ |
| FIB | **FBI** | resident agency / squad (not published) | ⚠ |
| IAA | **CIA** | none — no radio call signs at all | ⚠ |
| DOA | **DEA** | group-based (`Group 1`) | ⚠ |
| SASPA | **CDCR** (California prison custody) | post / function (`Transport 1`) | ⚠ |
| SASP · SAPR | **NPS** / California State Parks | district name + number | `Whiskey 12` |
| LSFD | **LAFD** / LA County Fire | apparatus type + station number | `Engine 27`, `RA 27` |

---

## Your cars, with the call sign each one should carry

Every car in **[ELS] Los Santos Police Vanilla Pack** — the pack's own folders are named so you can
find the file each one lives in — plus the two vanilla LSFD vehicles that are not part of it.

| Model | In the pack | Agency | Call sign | Reads as |
|---|---|---|---|---|
| `police` | `VEHICLE FILES` | LSPD | **1-ADAM-12** ✅ | one-twelve, Central, two-man car |
| `police2` | `VEHICLE FILES` | LSPD | **1-ADAM-14** ✅ | second Central car |
| `police3` | `VEHICLE FILES` | LSPD | **6-LINCOLN-20** ✅ | Hollywood, **one**-man car |
| `police4` | `VEHICLE FILES` | LSPD / unmarked | **1-ZEBRA-3** ✅ | unmarked patrol |
| `police5` | `VEHICLE FILES` (Gresley base) | LSPD | **1-ADAM-19** ✅ | Central, third car |
| `police6` | `VEHICLE FILES` (Granger base) | LSPD | **3-ADAM-31** ✅ | Southwest division |
| `policet` | `Bus` | LSPD | **1-BOY-4** ✅ | **B** = prisoner transportation |
| `sheriff` | `County` | LSSD | **60** ✅ | Santa Clarita Valley patrol car |
| `sheriff2` | `County` | LSSD | **61** ✅ | second car, same station |
| `sheriff3` | `County` (Fugitive base) | LSSD | **62** ✅ | third car, same station |
| `sheriff4` | `County` | LSSD | **63** ✅ | fourth car, same station |
| `policeold1` | `policeold1-policeold2` | NYSP | **1A19** ✅ | Zone 1, Troop A, unit 19 |
| `policeold2` | `policeold1-policeold2` | NYSP | **1A27** ✅ | Zone 1, Troop A, unit 27 |
| `pranger` | `County` | SAPR · SASP | **Whiskey 12** ⚠ | ranger district + number |
| `polmav` | `MISC` | LSPD Air Support | **Air 1** ⚠ | air unit |
| `predator` | `MISC` | NOOSE marine | **271B** ✅ | LASD **B** = patrol boat |
| `riot` | `MISC` | NOOSE | **TAC 2** ⚠ | tactical, plain language |
| `policeinsurgent` | `MISC` | NOOSE | **TAC 3** ⚠ | tactical, plain language |
| `ambulance` (livery 2) | *vanilla* | LSFD EMS | **RA 27** ✅ | rescue ambulance 27 |
| `firetruk` | *vanilla* | LSFD Fire | **Engine 27** ✅ | engine out of Station 27 |

Not in the pack but in your install: `policeb` (SAHP's motor, **25-80M** ✅), `fbi` / `fbi2` (FIB's
unmarked Granger and Buffalo, **RA-5** ⚠ / **TAC 1** ⚠), `buzzard` (NOOSE air, **AIR-2** ⚠), `pbus`
(SASPA's prison bus, **Transport 1** ⚠).

The pack also carries its own **livery templates** — `TEMPLATES\TEMPLATE - STANIER / GRANGER /
GRESLEY / INTERCEPTOR.png` and, under `County\TEMPLATES\`, `CONTENDER / FUGITIVE / LANDSTALKER /
STANIER` — which is where a unit number would be painted if you want these on the cars rather than on
the radio.

---

## LAPD — LSPD

`Division – Unit type letter – Beat`, spoken with the APCO alphabet: **1-ADAM-12** (written `1A12`).

- **Division** is the area station's number — it is the first component of the unit's Reporting
  District, so `1-ADAM-12` works out of Central (division 1) and its calls land in RD 1xx.
- **Unit type letter** is what the car does, not what it looks like.
- **Beat** is the patrol area inside the division; the same beat keeps the same number across shifts.

### Divisions ✅

| # | Division | # | Division | # | Division |
|---|---|---|---|---|---|
| 1 | Central | 9 | Van Nuys | 16 | Foothill |
| 2 | Rampart | 10 | West Valley | 17 | Devonshire |
| 3 | Southwest | 11 | Northeast | 18 | Southeast |
| 4 | Hollenbeck | 12 | 77th Street | 19 | Mission |
| 5 | Harbor | 13 | Newton | 20 | Olympic |
| 6 | Hollywood | 14 | Pacific | 21 | Topanga |
| 7 | Wilshire | 15 | North Hollywood | 22–25 | Traffic (West, Valley, Central, South) |
| 8 | West LA | | | 31 · 36 | Transit · Security Services |

### Unit letters ✅

| Letter | Unit | | Letter | Unit |
|---|---|---|---|---|
| **A** Adam | two-officer basic patrol car | | **K** | headquarters detective |
| **L** Lincoln | **one**-officer patrol car | | **R** | Metropolitan squad |
| **B** Boy | prisoner transportation | | **V** | vice |
| **M** | motorcycle | | **J** | juvenile investigation |
| **T** | traffic investigation | | **F** | felony investigation |
| **D** | air support | | **FB** | foot patrol |
| **W** | division detective | | **C** | parking control |
| **Z** | unmarked patrol | | **U** | report taking |
| **X** | extra patrol | | **Q** · **H** · **Y** | special events · special HQ · administrative HQ |

### Supervisors and desks ✅

- a beat number ending in **0** is a supervisor — `7-L-60` is a Wilshire field supervisor;
- **`L-10`** is the watch commander of that division — `6-L-10`, Hollywood;
- **`L-90`** is the station desk / base console — `6-L-90`;
- the area commanding officer is **`Commander <division>A`** — `Commander 3A` is Southwest's.

---

## LASD — LSSD

A sheriff's department does not number cars by the officer; it numbers them **by station**. The call
sign is `station number` with a unit letter or a third digit for the car. ✅

- Patrol cars of a station occupy the block `<station>0 – <station>9`. Station 2 (East LA) runs
  units `20`–`29`; station 18 (Avalon) runs `180`–`189`; station 21 (Century) runs `210`–`219`.
- Extra cars beyond that block take a letter: `21A`, `21B`, `22A` — except for the letters that mean
  something already.
- Station/unit **detectives are the 800 series** with the station number folded in — `803` is South LA
  detectives, `816` is Carson detectives.
- **700 series** is platoon/mobilisation (`701`–`718`, with Century `721` and Marina del Rey `727`),
  **400 series** is reserved for training so exercises never draw real units, **950 series** is the
  aero bureau, **240–249** is the Special Enforcement Bureau.

### Station numbers ✅

| # | Station | # | Station | # | Station |
|---|---|---|---|---|---|
| 2 | East Los Angeles | 9 | West Hollywood | 15 | Pico Rivera |
| 3 | South Los Angeles | 11 | Lancaster | 16 | Carson |
| 4 | Norwalk | 12 | Crescenta Valley | 17 | Lomita |
| 5 | Temple | 13 | Lakewood | 18 | Avalon |
| 6 | Santa Clarita | 14 | Industry | 21 | Century |
| 7 | Altadena | 8 | San Dimas | *also* | Malibu · Lost Hills · Marina del Rey · Walnut · Gorman |

### Unit letters ✅

| Letter | Unit | | Letter | Unit |
|---|---|---|---|---|
| **A · B · E · F · H · J · K · P · Q · Y** | station patrol cars | | **R** | reserve |
| **D** | station/unit desk, dispatcher (with the base identifier) | | **V** | volunteer |
| **G** | gang suppression | | **X** | out of service / special / directed patrol |
| **K9** | canine | | **Z** | contract-city employee |
| **M** | motorcycle | | **C** | captain |
| **T** | traffic | | **L** | lieutenant |
| **U** | utility | | **S** | sergeant |
| **B** (after the number) | bicycle patrol | | **I** | area commander |
| **R** (after the number) | rescue | | | |

*On your install: `60` = a Santa Clarita Valley patrol car, `240` = an SEB/SWAT unit, `806` = Santa
Clarita detectives, `60S` = its sergeant.*

---

## CHP — SAHP

`Office number – Beat`, with a trailing letter for shift or role. ✅

- **Office number** is the area office the officer is assigned to; **beat** is the road or area worked.
  `25-75` is a Hayward office unit; `62-505` is Stockton working I-5; `83-21` is Santa Fe Springs;
  `143-581` is Castro Valley working I-580.
- **Shift letters** ride along when shifts overlap: **A** days, **B** swing, **C** nights.
- **`M` suffix** is a motorcycle — `34-80M` is the Marin office's north-end motor.
- **S** is a sergeant, **L** a lieutenant — `14-3S`.
- Dispatchers call a unit by the **dispatch centre** plus the number: `Golden Gate-25-75`. The state is
  divided into dispatch centres (Humboldt, Redding, Susanville, Golden Gate, …) that each handle a
  group of area offices.

Office numbers are assigned per office and are not a single statewide published list, so `25`, `62`
and `83` above are real examples rather than a complete table.

---

## NYSP — NYSP

`Zone number – Troop letter – 2-digit identifier`, written together: **2A34**, **3K86**, **1C39**. ✅

- **Troops** A–G are the state regions; **K** and **L** are Westchester and Long Island; **T** is the
  Thruway; **M** is New York City; **H** is headquarters.
- **Zones** 1–4 are patrol (not every troop has a 4th), **5** is the Bureau of Criminal Investigation,
  **7** is other agencies dispatched by NYSP, **8** is the state Parks Police, **9** is dispatchers.
- Inside a troop's zone: `#x1`–`#x9` supervisors on some troops, `#x10`–`#x79` patrol, `#x80`–`#x83`
  canine, `#x84`–`#x86` special units, `#x87`–`#x89` traffic, `#x90`–`#x99` more patrol.
- Troop T (Thruway) uses its own four-digit personal radio ID: the leading **7** is state police, the
  second digit is the zone (1 NYC, 2 Albany, 3 Syracuse, 4 Buffalo), the last two are the person — so
  `7114` is a Thruway trooper in Zone 1.

*On your install: `1A19` = Zone 1, Troop A, unit 19.*

---

## Fire and EMS — LSFD

### LAFD ✅

Apparatus is named by **type + station number**, and the station number does the identifying:

- `Engine 9`, `Truck 9`, `Rescue 9`, `Light Force 9` — everything primary out of Station 9 carries a 9.
- Secondary apparatus out of the same station adds **200**: `Engine 209`.
- **Paramedic** rescue ambulances are the low numbers (`Rescue 9`, `Rescue 209`); **EMT** ambulances
  live in the **800/900** range (`Rescue 809` out of Station 9).
- On the radio an ambulance is **`RA-74`** — rescue ambulance.
- Command answers as `Battalion 12`, `Division 3`, `EMS 3`.

### LA County Fire ✅

Apparatus prefixes: **E** engine, **T** truck, **Q** quint, **S** squad (all ALS), **B** battalion
chief, **A** assistant chief, **U** utility, **P** brush patrol, **WT** water tender, **MIRV**, **EST**,
**SW** swiftwater, **F**/**FC** foam, **UR**/**URT**/**URR** urban search & rescue, **HM** hazmat,
**RA** medic ambulance — then the station number, e.g. `Engine 271` out of Station 271 in Malibu.

---

## Agencies that do not publish call signs

These exist in your install, and the honest answer is that the real counterpart has no public
identifier scheme — the entries above are constructed, and it is worth knowing which is which.

- **FIB — FBI.** Field offices hold federal station designators (Washington Field Office `KGB770`,
  Philadelphia Field Division `KEX640`) but agents' unit identifiers are not published. Field practice
  is squad-and-number, and *resident agencies* (satellite offices) are the closest real analogue to an
  agent car's identity — hence `RA-5`. ⚠
- **IAA — CIA.** No domestic radio call signs, no patrol function. The only public identifier attached
  to it is the licence block `KGB###`. ⚠
- **DOA — DEA.** Agents work by group (`Group 1`), and groups are the unit in practice. ⚠
- **NOOSE — SWAT.** Real tactical elements answer in plain language rather than in a numbering scheme:
  LAPD SWAT is Metro Division's D Platoon, and federal teams answer as HRT or by their own call words.
  `SWAT 1` / `TAC 2` is the convention, not a citation. ⚠
- **SASPA — CDCR.** Prison custody staff identify by **post and function**, not by car: `Post 1`,
  `Tower 2`, `Yard 3`, `Transport 1`. Institution-specific in every case. ⚠
- **SASP · SAPR — Park rangers.** The National Park Service has **no national standard**; parks assign
  their own. Yosemite's Wawona district answers as `Whiskey 12` and its Tuolumne sub-district as
  `Tango 4`; Grand Canyon's South Rim is `Sierra 3` and the Inner Canyon `Canyon 7`. Other parks stay
  numeric, with a series per function (100 HQ, 200 interpretive, 300 maintenance, 400 and 500 for two
  ranger districts) — Death Valley's protection rangers sit in the 400 series. `Whiskey 12` above is
  that pattern, not a fixed code. ⚠

---

## If you want a department that is not on this list

Almost every US police department builds a call sign the same way: **a geographic prefix, a unit-type
letter, then a number** — and many publish the scheme as a general order, which is the best kind of
source. Baton Rouge's General Order 247 is a good readable example: `PD1` chief, `HQ10–19` captains,
`1000–899` first district, `MC` motors, `K9` canine, `SI` special investigations, and officers ranked
by the numbers inside the block.

---

## Sources

- LAPD unit numbering — <https://www.ibiblio.org/jimmy/mcguinn/lapd.html>
- LAPD scanner guide: unit IDs, divisions, reporting districts — <https://www.lacaptain.com/lapd_guide.html>
- LAPD resources, unit call signs and division numbers — <https://en.wikipedia.org/wiki/Los_Angeles_Police_Department_resources>
- LASD Manual of Policy and Procedures, Vol 7 Ch 2, Radio Communication Call Numbers — <https://pars.lasd.org/Viewer/Manuals/12516?reportIndex=1>
- LASD patrol stations/units and station numbering (7-02/010.35) — <https://pars.lasd.org/Viewer/Manuals/12516/Content/12528>
- CHP call sign structure and office numbers — <http://forums.radioreference.com/threads/chp-unit-numbers.331696/>
- CHP shift/role letters — <http://forums.radioreference.com/threads/chp-callsign-letter-designations.382327/>
- CHP area offices by dispatch centre — <https://cad.chp.ca.gov/htm.net/phonealt.htm>
- NYSP identifiers, troops and zones — <https://wiki.radioreference.com/index.php/NY_State_Police_(NY)>
- NYSP Troop T personal radio IDs — <http://forums.radioreference.com/threads/nysp-troop-t-identifiers.441400/>
- National Park Service ranger unit designators — <http://forums.radioreference.com/threads/department-of-the-interior-callsign-licenses-assignments.229382/>
- Federal station designators (FBI, CIA, others) — <http://forums.radioreference.com/threads/federal-call-sign-question.299852/>
- LAFD numbering and rescue ambulances — <http://forums.radioreference.com/threads/lapd-and-fd-callsigns.462452/>
- LAFD apparatus (engine, truck, light force, task force) — <https://lafd.org/about/about-lafd/apparatus>
- LA County Fire unit prefixes and station numbering — <https://wiki.radioreference.com/index.php/Los_Angeles_County_(CA)_County_Fire>
- Baton Rouge PD radio unit identifiers (General Order 247) — <https://www.brla.gov/DocumentCenter/View/10781/General-Order-247-Radio-Unit-Identifiers>
