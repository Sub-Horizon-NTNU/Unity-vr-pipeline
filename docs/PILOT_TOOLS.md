# Zoom, pilotmeny og profiler

Disse funksjonene inngår i v2. Den publiserte v1-builden er uendret.
Last ned v2 fra GitHub Releases, pakk ut hele mappen og kjør `Start-v2.cmd`.

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

Pek med høyre kontroller og trykk høyre avtrekker. På verdifelt kan du holde
avtrekkeren og dra venstre/høyre over feltet. Feltet beholdes under draget,
så en liten bevegelse opp/ned ikke begynner å justere raden ved siden av.
Menyen åpner på **Hjem**, med kort for Profiler, Kontroller og hjelp, Zoom,
Instrumenter, Videoopptak og Tilpass oppsettet. **Hjem** og **? Hjelp** ligger
alltid nederst. Detaljvalgene under finnes via kortene og Tilpass oppsettet:

1. **Zoom:** hovedzoom, detaljvindu, detaljzoom, målpunkt og kontrollervisning.
2. **Detail placement:** plassering, størrelse og vinkling av detaljvinduet.
3. **Video:** bildestørrelse, krumming, avstand, høyde, helning og hodefølging.
4. **HUD:** synlighet, instrumentgrupper, tekstskala, opasitet og forankring.
5. **HUD placement:** velg instrumentboks og juster plassering, skala og vinkler.
6. **Compass / Tilt:** kompassgeometri, IMU-/servoutjevning, servofart og pitch-kompensasjon.
7. **Profiles:** velg, last eller lagre profiler; lagre start-JSON; juster og flytt menyen.
8. **Bindings:** velg handling og bytt kontrollerknapp/tast.
9. **Pointers:** følsomhet, siktevinkel, utjevning og sentrering av pekerne.
10. **Move windows:** gripeknapp, flyttehastighet og menyplassering.
11. **Menu placement:** menyens pitch, yaw, høyde, sideplassering og avstand.
12. **Recording:** start/stopp MP4-opptak, opptakskilde, oppløsning og vertikal vending.

Etter profilvalget ved oppstart kan menyen lukkes med LUKK, også etter ombinding av menyknappen.
Venstre spaks bildejustering og øvrige pilotsnarveier er sperret mens menyen
er åpen. Pekerne vises i menyen selv om de er skjult under vanlig pilotering.

Spakknoppen viser faktisk spakutslag. Modellene og pekerne har separate av/på-valg.
Systemknapper som Quest/Meta eies av runtime og kan ikke ombindes av programmet.

## Peker og flytting av vinduer

Et hvitt siktekors med mørk bakplate og cyan ring vises der pekeren treffer
menyen eller detaljvinduet. Markøren tegnes foran vinduet, og strålen stopper
ved markøren. Hele vinduet får en cyan ramme ved treff og en gullfarget ramme
med «HELD» når det holdes. Under flytting følger markøren det opprinnelige
gripepunktet, også når vinduet flyttes bort fra håndens sikteretning.

**Venstre avtrekker foran griper vinduer; høyre avtrekker velger.**
Pek på menyen eller detaljvinduet, trykk og hold venstre avtrekker, og flytt
venstre spak. Spak høyre/venstre flytter sideveis; opp/ned flytter i høyden.
Hold i tillegg venstre eller høyre grip-knapp for å bruke venstre spak
opp/ned til avstand. Høyre spak justerer yaw (sideveis) og pitch (opp/ned),
uten å flytte vinduets sentrum. Slipp avtrekkeren for å plassere vinduet.
Vinduet hopper ikke til pekerens senter når du griper det.

Gripeknappen kan endres til venstre grip eller venstre spakklikk. Ved grip
som gripeknapp brukes høyre grip som avstandsmodifikator.
Flyttehastighet, rotasjonshastighet og funksjonen av/på finnes i Move windows
og PC-panelet. Håndens pitch/yaw flytter som standard ikke vinduet etter
gripepunktet er valgt. Valget «Window follows hand ray» aktiverer dette igjen.
Flytteaksene holdes vannrette, uavhengig av vinduets helning.
Under flytting er venstre spaks bildekalibrering og øvrige pilotvalg sperret.
Ved tapt sporing eller VR-fokus slippes vinduet; slipp og trykk gripeknappen
igjen etter at sporingen er tilbake.

Detaljvinduet følger HUD-ens koordinatsystem som før. Menyen står i ro i
rommet mens den er åpen. Dens side-/høyde-/avstandsverdier brukes relativt
til synsretningen neste gang den åpnes. **Lagre JSON eller en profil** for
å beholde plasseringen og vinklene mellom oppstarter.

Kontrollerne har en enkel Touch-lignende markør med grep, topp, knapper,
spak og sporingsring. Dette er en illustrasjon, ikke Metas offisielle modell.
Pekeren bruker OpenXR-sikteposen direkte gjennom Input System, med eldre
XR-siktepose og til slutt håndtakets pose som reserve.

Under **Pointers** kan dere justere:

- Horizontal / Vertical sensitivity: standard **1,20× / 0,80×**. Lavere vertikal
  verdi gir roligere opp/ned-bevegelse, men krever større håndutslag. Øk
  verdien hvis dere vil nå hele menyen med mindre bevegelse.
- Aim pitch / yaw: fast vinkelkorreksjon. Positiv pitch peker høyere.
- Pointer smoothing: standard **0,025 s**, 0 slår av. Gjelder bare pekerne;
  videoflate, hodesporing og fysisk servo filtreres ikke av dette valget.
- Center LEFT/RIGHT pointer to gaze: hold kontrolleren komfortabelt og se
  mot ønsket senter. Funksjonen lar denne håndstillingen peke dit. Denne
  sentreringen gjelder økten; tallverdiene over lagres i JSON/profiler.

## Profiler på PC og i VR

I **ROV → View → Manual calibration**, eller **F4** i Play/build, utvid
**Zoom / VR menu / profiles / bindings**. Skriv et navn og velg **Save named
profile**. Listen under har en last-knapp for hver profil. «Refresh profile
list» finner filer som er kopiert inn fra en annen PC.

I VR: **Profiler → Ny profil** åpner et lite skjermtastatur med Æ/Ø/Å,
sletting og navneforslag som Pilotprofil, Inspeksjon og Oversikt. Nye navn
får et ledig, lesbart tillegg ved behov (for eksempel Pilotprofil 2).
Velg en profil og **Gi nytt navn** for å endre navnet uten å endre innholdet.
Eldre VR-profiler med tidsstempel vises med en lesbar dato; filene endres først
når du selv gir dem et nytt navn. **Lagre endringer i aktiv profil** beholder
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

Menyen bruker høyre avtrekker som UI-klikk mens den er åpen. Hvis en
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

## Videoopptak

Åpne **RECORDING** i VR-menyen og velg **START recording** / **STOP recording**.
De samme valgene finnes i F4-panelet under Zoom / VR menu / profiles / bindings.
En rød REC-indikator vises i VR mens opptaket går. Vent til status viser Saved
før du lukker programmet. Opptak starter aldri automatisk ved lasting av profiler.

- **PC mirror + HUD:** tar opp Unitys PC-speilbilde med HUD, zoomvindu og synlig meny.
  Dette er et flatt presentasjonsopptak, ikke stereoskopisk eller 360° video.
  I editoren tas Game-visningen opp; ha Game-fanen synlig mens dere tar opp.
- **Clean video:** tar opp hele kamerateksturen uten HUD, VR-hodebevegelse eller
  digital zoom. Ingen ny UDP-mottaker eller endring av den aktive pipeline-en.
- 1280×720 / 30 fps som standard; **Full HD** velger 1920×1080 / 30 fps.
  Velg kilde og oppløsning før Start. Endringer brukes ved neste opptak.
- **Flip recording vertically** korrigerer opp/ned ved behov. Ta et kort prøveklipp
  før presentasjonen; orienteringen kan variere med grafikkbackend og opptakskilde.
- Format: H.264 i MP4, uten lyd. Filene lagres i Windows-mappen
  **Videoer\SubHorizon ROV**. PC-panelet har **Open recordings folder**.

Enkoding og disk-I/O kjører på en bakgrunnstråd. Bare siste tilgjengelige
GPU-bilde beholdes; ved lav oppdateringsrate gjentas siste bilde for å beholde
riktig avspillingshastighet. Opptak kan likevel øke GPU-/CPU-belastningen;
bruk 720p hvis ytelsen faller. Lukking/Stop avslutter enkoderen. MP4 skrives
med fragmenter for å begrense skade ved avbrudd, men stopp normalt når mulig.

Builden bruker den medfølgende GStreamer-runtime-en med gst-launch, rawvideoparse,
x264enc, h264parse og mp4mux. Editor finner den lokale preview-/v1-runtime-en,
eller GSTREAMER_1_0_ROOT_MSVC_X86_64. **ROV → View → Verify MP4 encoder (no movement)**
kontrollerer enkoding med syntetiske bilder uten kamera- eller servokommandoer.

## Menyvinkler

Under **MENU PLACEMENT** finnes egne **Menu pitch**- og **Menu yaw**-slidere,
sammen med sideplassering, høyde og avstand. **Reset menu angles** nullstiller
helningen. De samme pitch/yaw-slidene finnes i PC-panelet. Verdiene er relative
til menyens retning ved åpning og lagres med **Save startup JSON** eller en profil.


## VR-prioritet og RC-håndover (etter v2)

VR beholder kamerakontrollen til piloten velger **Camera control: RC** på
**Compass / Tilt**-siden, PC-statusknappen eller P. A låser fortsatt skjermen
og tiltmålet; A gir ikke håndkontrolleren kontroll. Velg VR igjen for å overta.
Hodekalibreringen bevares ved avbrudd og RC/VR-bytte; X/R sentrerer eksplisitt.

Før VR får sende tiltmål leses RC1–RC16_OPTION. Bare kamerafunksjonene
212 (roll), 213 (pitch) og 214 (yaw) sperres med verdi 0. På dagens ROV er
RC7_OPTION=214 og RC8_OPTION=213. Dette hindrer at vanlig joysticktilt bytter
mounten tilbake til RC mens VR er valgt. Andre RC-/thrusterfunksjoner endres ikke.
Dette er sperring av kamerainnganger, ikke en protokollås mot andre programmer
som sender egne MAVLink mount-kommandoer. Ikke kjør flere VR-instanser mot samme ROV.
En lokal fillås avviser en ekstra instans som bruker samme BlueOS-adresse.

**PARAM_SET lagres på autopiloten.** Originalverdiene skrives til en lokal
`rov-camera-rc-<adressehash>.json` i samme mappe som startprofilen før sperringen.
Ved RC-valg/normal avslutning gjenopprettes og kontrolleres verdiene. Ved feil
beholdes gjenopprettingsfilen og feilen vises; VR starter ikke på en ubekreftet
sperre. Eksterne endringer til andre verdier overskrives ikke automatisk.

Et strømbrudd, en krasj eller avslutning av Play kan avbryte gjenopprettingen.
Bruk da **Restore RC camera control** i VR-menyen / RC-knappen på PC, eller
stopp Play og velg **ROV → Restore camera RC mappings from recovery file**.
Neste VR-overtakelse gjenoppretter også en uferdig journal før et nytt øyeblikksbilde.
Behold journalen og bruk samme PC og BlueOS-adresse til verdiene er tilbakeført.
Hvis journalen er utilgjengelig, må de dokumenterte originalverdiene gjenopprettes
manuelt på ROV-en før vanlig kameratilt brukes. Ikke slett en uferdig journal.

Ved mistet fokus, tracking, heartbeat eller fersk IMU stoppes nye tiltmål uten
å nullstille hodenøytral eller gi RC en utilsiktet overtakelse. Ved retur brukes
ferskt rapportert tiltmål som start for den eksisterende hastighetsbegrensningen.
ACK må være ny, for riktig kommando og avsender; bufrede ACK-er teller bare én gang.
Fire sekunder uten ny godkjenning stopper sending og utløser eksisterende begrensede
forsøk. PC-status viser siste stoppårsak, ACK-alder, HTTP-sendeteller og PWM11.
Rapportert vinkel/PWM er autopilotens beregnede mål, ikke fysisk servofeedback.

Testene av RC-journal, delvis feil, eksterne endringer og konkurrerende lokale
instanser bruker falske parameterverdier uten ROV-trafikk. Sperring/tilbakeføring
er også kontrollert mot dagens ROV med Play stoppet, uten å sende tiltmål.
Praktisk samtidig joystick-/headsettest og kontroll av mekanisk servobevegelse gjenstår.

## Første skjerm og kontrollhjelp (etter v2)

Ved hver oppstart vises en profilvelger. Velg **Sist lagrede oppsett** eller en
navngitt profil, og trykk **Start med valgt oppsett**. Kameratilten sender ikke
hodebaserte mål og tar ikke RC-sperren før Start er valgt. En ugyldig profil
viser feil og lar velgeren stå åpen. På PC finnes Start-knappene også direkte
i Game-visningen, uten at F4-panelet må åpnes.

Velkomstmenyen bruker en fast, vannrett plassering foran brukeren, slik at
lagrede menyvinkler ikke gjør første valg vanskelig å nå. Etter Start brukes
profilens vanlige menyplassering. Profilvalg endrer ikke den lagrede startfilen
uten at du velger å lagre den. Eksisterende profiler og innstillinger beholdes.

**Kontroller og hjelp** viser tre deler:

- Pilotering: venstre/høyre hånd med knapper og aktive funksjoner. Oversikten
  følger ombindingene i profilen. Venstre spaks bildejustering forklares separat.
- Meny og peker: åpne meny, peke/velge, justere verdier og lagre.
- Flytt vinduer: gripeknapp, side/høyde, avstand, pitch/yaw og lagring. Den
  valgte gripeknappen og avstandsmodifikatoren vises fra innstillingene.

Dette er en hjelpeside som åpnes fra menyen, og dekker ikke pilotbildet når
menyen lukkes. Bruk **Hjem** for å gå tilbake. Mer avanserte valg er beholdt.
Praktisk lesbarhet og betjening må fortsatt prøves i Quest.
