# Zoom, pilotmeny og profiler

V3 adds automatic startup, quick settings, a compact green FPV HUD and room
passthrough to the zoom/profile tools introduced in v2. Use v3 or the current
Unity source for these features; downloading the older v2 release does not
include them. See [v3 release notes and validation](RELEASE_v3.md).

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
The menu always opens on **PILOTINNSTILLINGER**, a quick panel for recentering
and returning to the pilot view, head-follow/hold, HUD visibility, recording,
zoom, explicit RC/VR camera ownership, readability and saving for next startup.
**ALLE INNSTILLINGER** opens one catalog with direct access to every advanced
page. **TILBAKE** returns from a detail page to that catalog, then to quick
settings. **LUKK / PILOTVISNING** closes from any page, including while startup
is waiting for a connection. Advanced pages retain these functions:

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
13. **KONTROLLER OG HJELP:** current controller bindings and menu/window help.

There is no mandatory startup profile picker. Closing and reopening settings
always returns to the quick panel, including after rebinding the menu button.
Venstre spaks bildejustering og øvrige pilotsnarveier er sperret mens menyen
er åpen. Pekerne vises i menyen selv om de er skjult under vanlig pilotering.

Spakknoppen viser faktisk spakutslag. Modellene og pekerne har separate av/på-valg.
Systemknapper som Quest/Meta eies av runtime og kan ikke ombindes av programmet.

## FPV pilot information (after v2)

At **1×**, a short green horizon and at most two nearby pitch references overlay
the Atlas video; the existing compass circle is retained. The line spans about
30° and the short references about 12° at the tested display geometry, with
lighter decoration to keep the image clear. The cue estimates camera attitude
from the ROV IMU and reported mount target, using the configured display FOV;
it is not physical servo feedback or calibrated lens geometry.

Main-video zoom hides the horizon lines and labels and shows
`HORIZON AT 1× ONLY`. The fixed reticle, pilot information and compass remain.
Returning to 1× restores the cue only with fresh pose data; missing data shows
`HORIZON UNAVAILABLE`. Detail-window zoom alone does not hide the main cue.

While the HUD is enabled, battery, arm state, **PILOT GAIN** and **LIGHT CMD**
remain visible. Gain is the vehicle's live joystick gain. L1/L2 are commanded
light outputs, with zero displayed as OFF, not measured lamp brightness.
Unavailable or stale values show a dash; battery percent is not inferred from
voltage. B still hides the entire HUD. **HUD OG LESBARHET** controls presentation;
see [HUD geometry and telemetry](https://github.com/Sub-Horizon-NTNU/Unity-vr-pipeline/blob/v3/UnityProject/Assets/ROV/VIDEO_VIEW.md#flytende-pilot-hud)
for field meanings and calibration limits.

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

In **ROV → View → Manual calibration**, or the legacy **F4** panel while XR is
inactive, expand **Zoom / VR menu / profiles / bindings**. During active XR,
use right-stick click/F3 settings; the duplicate desktop overlay is hidden.
Enter a name and choose **Save named profile**. The list can load each profile;
**Refresh profile list** finds files copied from another PC.

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

VR retains camera ownership until the pilot selects **KAMERA: VR / VELG RC**
in quick settings or presses **P**. The same choice is available under
**ALLE INNSTILLINGER → KAMERA OG KOMPASS**. When RC is selected, choose
**KAMERA: RC / VELG VR** to take over again. A only holds the screen and tilt
target; it does not hand control to RC. Interruptions and RC/VR switching
preserve head calibration; X/R explicitly recenter.

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

Power loss, a crash or stopping Play can interrupt restoration. In VR use
**ALLE INNSTILLINGER → KAMERA OG KOMPASS → Gjenopprett RC-kamerastyring**,
or stop Play and select **ROV → Restore camera RC mappings from recovery file**.
Neste VR-overtakelse gjenoppretter også en uferdig journal før et nytt øyeblikksbilde.
Behold journalen og bruk samme PC og BlueOS-adresse til verdiene er tilbakeført.
Hvis journalen er utilgjengelig, må de dokumenterte originalverdiene gjenopprettes
manuelt på ROV-en før vanlig kameratilt brukes. Ikke slett en uferdig journal.

Ved mistet fokus, tracking, heartbeat eller fersk IMU stoppes nye tiltmål uten
å nullstille hodenøytral eller gi RC en utilsiktet overtakelse. Ved retur brukes
ferskt rapportert tiltmål som start for den eksisterende hastighetsbegrensningen.
ACK må være ny, for riktig kommando og avsender; bufrede ACK-er teller bare én gang.
Fire sekunder uten ny godkjenning stopper sending og utløser eksisterende begrensede
forsøk. Optional desktop diagnostics show the last stop reason, ACK age, HTTP
send count and PWM11; the shipped scene hides that diagnostic panel by default.
Rapportert vinkel/PWM er autopilotens beregnede mål, ikke fysisk servofeedback.

Testene av RC-journal, delvis feil, eksterne endringer og konkurrerende lokale
instanser bruker falske parameterverdier uten ROV-trafikk. Sperring/tilbakeføring
er også kontrollert mot dagens ROV med Play stoppet, uten å sende tiltmål.
Praktisk samtidig joystick-/headsettest og kontroll av mekanisk servobevegelse gjenstår.

## Automatic startup and control help (after v2)

Startup loads the existing local `rov-video-view.json`; bundled defaults are
used only when that local file is absent. The pilot view starts without a
welcome screen or Start button. Head-camera control waits for active XR, a
ready tracked headset, VR focus and fresh BlueOS heartbeat, plus fresh attitude
when pitch compensation needs it. The existing camera calibration, RC gate,
command acknowledgement and recovery rules still apply. An explicit RC choice
is preserved. Automatic startup never arms the vehicle or controls thrusters.

Look forward and press left **X** to recenter. Open settings with right-stick
click or **F3** whenever needed, even while waiting for readiness. A panel
deliberately opened by the pilot remains open when the connection becomes
ready. Named profiles remain under **ALLE INNSTILLINGER → PROFILER**. Loading
one does not replace the startup JSON until **LAGRE TIL NESTE OPPSTART** or Y/F5.

**Kontroller og hjelp** viser tre deler:

- Pilotering: venstre/høyre hånd med knapper og aktive funksjoner. Oversikten
  følger ombindingene i profilen. Venstre spaks bildejustering forklares separat.
- Meny og peker: åpne meny, peke/velge, justere verdier og lagre.
- Flytt vinduer: gripeknapp, side/høyde, avstand, pitch/yaw og lagring. Den
  valgte gripeknappen og avstandsmodifikatoren vises fra innstillingene.

This help page is available through **ALLE INNSTILLINGER → KONTROLLER OG HJELP**.
It disappears when settings close; **TILBAKE** returns to the catalog.

## Rette opp meny og pekere (etter v2)

- **Begge grip-knappene i 0,7 sekunder:** henter menyen vannrett foran deg ved
  1,25 m, nullstiller menyens pitch/yaw og sentrerer begge sporede pekere mot
  midten. Hold hendene i en behagelig pekestilling mens du gjør dette. Snarveien
  virker også når menyen er lukket. Den brukes når du ikke holder et vindu.
- **RETT OPP** øverst i menyen gjør det samme. PC-panelet har også denne knappen.
- **Høyre spakklikk mens du holder et vindu:** nullstiller vinklene på det valgte
  meny-/zoomvinduet og beholder posisjonen. Høyre spak vinkler fortsatt fritt.
- Pekerknappene under **Pekere** sentrerer nå mot selve menymidten med hensyn til
  kontrollerens posisjon. Når menyen er lukket, brukes et punkt 2 m foran blikket.

Dette endrer ikke hodenøytral, videosentrering eller servoens kontrolløkt.
Normal menu placement can be saved in the profile. Placement changes made while
automatic startup is still waiting are temporary. The two-grip recovery shortcut
is reserved; other pilot bindings wait while that combination is held.

Pekeren krever både posisjons- og rotasjonssporing. Ved bare rotasjonsprediksjon
skjules pekeren i stedet for å bruke en gammel håndposisjon. Menyen viser en
melding når høyre kontroller mangler full sporing. Gammel pekerreferanse forkastes
ved XR-resentrering, endrede pekerinnstillinger og bytte mellom aim- og grip-pose. Følsomhetens yaw beregnes
rundt dagens hoderetning, slik at en stor vending ikke forsterker hele verdensvinkelen.

Problemet under utetesten er ikke gjenskapt. Meta oppgir at sterkt lys og direkte
sollys kan svekke Quest 2-kontrollernes infrarøde sporing:
[Controllers Best Practices](https://developers.meta.com/vr/design/controllers-best-practices/).
