# Zoom, pilotmeny og profiler

Disse funksjonene er lagt til etter v1. Den publiserte v1-builden er uendret.
Åpne prosjektet i Unity og bruk Play, eller bygg på nytt for å teste dem.

Separat Windows-testbuild: **ROV → Release → Build Windows pilot tools preview**,
deretter `./scripts/Prepare-PilotPreview.ps1` fra repoets toppmappe. Skriptet
gjenbruker GStreamer-runtime fra den lokale v1-builden; alternativt oppgi
`-RuntimeRoot` med en komplett MSVC x64-runtime. Start med
`Builds/pilot-tools-preview/Start-preview.cmd`. Dette overskriver ikke v1.
Preview bruker samme lokale start-JSON og profilmappen som v1, så lagre
gjerne en navngitt kopi av dagens oppsett før eksperimentering.
Hvis brannmuren blokkerer den nye EXE-en, kjør som administrator fra
preview-mappen: `./Setup-Firewall.ps1 -ProgramPath ./Unity-vr-pipeline-preview.exe`.

## Zoom

Digital zoom beskjærer den eksisterende videoteksturen. Ingen ny RTP-port,
GStreamer-pipeline, dekoder eller kamera-/autopilotkommando opprettes.
Hovedbildet starter på **1×**. Detaljvinduet er avslått som standard og starter
på **3×** når det aktiveres. Begge bruker samme valgte punkt i originalbildet.
Detaljzoom er total forstørrelse mot originalen, ikke en multiplikator på hovedzoom.

Lukk menyen, pek høyre kontroller mot et sted på hovedbildet og trykk høyre
avtrekker for å velge punkt. Punktet kan også flyttes med innstillingene
«Target left/right» og «Target bottom/top» (0–1). **G** velger med høyre
kontroller hvis den spores, ellers med synsretningen. Punktet er en fast
bildekoordinat; det sporer ikke et objekt når ROV-en beveger seg.

Høyre spak opp/ned endrer hovedzoom. **J** tilbakestiller til 1× og midtpunkt.
I menyens Zoom-side kan hovedzoom og detaljzoom justeres hver for seg, 1–8×.
Ved bildekanten forskyves utsnittet så hele vinduet viser gyldige kildepiksler.
Høyde/bredde og bildeorientering beholdes.

Bruk gjerne 1× på hovedbildet og 2–3× i detaljvinduet til inspeksjon. Ved 3×
er utsnittet omtrent 640×360 av kildens 1920×1080 piksler; ved 8× er det
240×135. Forstørrelse gir ingen nye detaljer. Detaljvinduet følger samme
forankring som pilot-HUD-en, og har justerbar sideplassering, høyde, avstand,
bredde, pitch, yaw og roll. Flere tegneflater gir noe ekstra GPU-arbeid,
selv om videodekoderen deles. Quest-komfort og ytelse må testes i headsettet.

## VR-meny

Trykk **høyre spakklikk** eller **F3** for å åpne/lukke. Menyen plasseres foran
nåværende synsretning når den åpnes og står så i ro i rommet. Den sender
ingen pause-/frigivingskommando til servoen. Hodefølging fortsetter med sine
vanlige sperrer. Lås med A før åpning, eller bruk menyens Hodefølging-valg,
hvis dere ønsker å låse kameraet mens innstillinger justeres.

Pek med en av kontrollerne og trykk avtrekkeren. På verdifelt kan du holde
avtrekkeren og dra venstre/høyre over feltet. Pilene øverst bytter side:

1. **Zoom:** hovedzoom, detaljvindu, detaljzoom, målpunkt og kontrollervisning.
2. **Detail placement:** plassering, størrelse og vinkling av detaljvinduet.
3. **Video:** bildestørrelse, krumming, avstand, høyde, helning og hodefølging.
4. **HUD:** synlighet, instrumentgrupper, tekstskala, opasitet og forankring.
5. **HUD placement:** velg instrumentboks og juster plassering, skala og vinkler.
6. **Compass / Tilt:** kompassgeometri, IMU-/servoutjevning, servofart og pitch-kompensasjon.
7. **Profiles:** velg, last eller lagre profiler; lagre start-JSON; juster og flytt menyen.
8. **Bindings:** velg handling og bytt kontrollerknapp/tast.

Menyen kan alltid lukkes med CLOSE, også etter ombinding av menyknappen.
Venstre spaks bildejustering og øvrige pilotsnarveier er sperret mens menyen
er åpen. Pekerne vises i menyen selv om de er skjult under vanlig pilotering.

Kontrollerne vises som enkle markører, ikke detaljerte Quest-modeller.
Spakknoppen viser faktisk spakutslag. Modellene og pekerne har separate av/på-valg.
Systemknapper som Quest/Meta eies av runtime og kan ikke ombindes av programmet.

## Profiler på PC og i VR

I **ROV → View → Manual calibration**, eller **F4** i Play/build, utvid
**Zoom / VR menu / profiles / bindings**. Skriv et navn og velg **Save named
profile**. Listen under har en last-knapp for hver profil. «Refresh profile
list» finner filer som er kopiert inn fra en annen PC.

I VR kan «Save as new profile» lage et tidsstemplet profilnavn uten tastatur.
Velg deretter en profil for å laste eller overskrive den. Overskriving beholder
forrige versjon som `.bak`. Profiler omfatter bildegeometri, zoom, HUD,
kompensasjon, utjevning, hodefølging og knappebindinger. Lasting av en profil
kan derfor også endre den uttrykkelige hodefølging på/av-innstillingen.
Personlig hodenøytral erstattes ikke; X sentrerer eksplisitt ved behov.

Filer ligger her:

```text
%USERPROFILE%\AppData\LocalLow\infiniteMonkey\GstUnityQuest3\Profiles\<navn>.json
```

Kopier hele JSON-filen mellom PC-er og trykk Refresh. Ugyldige profiler avvises
før innstillingene brukes. Vanlig **Y/F5/Lagre JSON** lagrer fortsatt den aktive
startfilen `rov-video-view.json`. Etter lasting av en navngitt profil bruker
dere Lagre JSON hvis akkurat den skal være utgangspunkt ved neste oppstart.

## Standardbindinger

| Handling | Quest | Tastatur |
| --- | --- | --- |
| Hodefølging / lås | Høyre A | Ingen |
| HUD på/av | Høyre B | H |
| Sentrer bilde og tilt | Venstre X | C |
| Lagre aktiv start-JSON | Venstre Y | F5 |
| VR-meny | Høyre spakklikk | F3 |
| Hovedzoom inn / ut | Høyre spak opp / ned | Z / V |
| Velg zoompunkt | Høyre avtrekker | G |
| Nullstill hovedzoom / målpunkt | Ingen | J |
| Last aktiv start-JSON | Ingen | F2 |

Klikk på en bindingsverdi for å bla til neste ledige valg. None frigjør en
knapp/tast. Doble bindinger avvises; gjenopprett standardbindinger ved behov.
Lagre JSON eller en navngitt profil for å beholde valgene. Dette gjelder
pilotfunksjonene i tabellen. F4, F6–F10, Page Up/Down og kontrollerens
separate R/P-snarveier er faste og ombindes ikke her. R/P ligger i
servokontrolleren. Venstre spaks eksisterende kalibrering med grip-/trigger-
modifikatorer kan slås av i de nye innstillingene.

Menyen bruker begge avtrekkere som UI-klikk mens den er åpen. Hvis en
avtrekker/grip ombindes til en annen handling, vær oppmerksom på venstre
spaks modifikatorer; slå av venstre spakkalibrering hvis de kolliderer med
egen foretrukket bruk.

## Kontroll

**ROV → View → Verify pilot tools (no movement)** kjører zoom-/profil-/bindings-
kontroller og eksisterende regresjonstester uten nettverkstrafikk fra testene.
Etter endringer må følgende også prøves fysisk i Quest: pekerretning, begge
øyne, tekstlesbarhet, live profilbytte, lagring etter omstart og servofølging
mens HUD-/zoomverdier endres. Menyåpning og zoom skal ikke endre servoens
kontrollsesjon eller hodenøytral.
