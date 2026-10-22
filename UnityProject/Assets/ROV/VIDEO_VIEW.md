# Quest-visning av ROV-video

Ny funksjonalitet etter v1: se [Zoom, VR-meny, profiler og bindinger](../../../docs/PILOT_TOOLS.md).
Høyre spakklikk/F3 åpner VR-menyen; høyre spak opp/ned zoomer, og høyre
avtrekker velger et zoompunkt. F4/Editor-panelet har nå også navngitte profiler.
Resten av denne siden beskriver grunnoppsettet fra v1.

Den fungerende scenen er `Assets/GstUnityPlugin/Scenes/GstUnityPluginDemo01.unity`.
Automatisk forstørrelse er fjernet etter headsettesten. Utgangspunktet er nå
110 graders horisontal bildedekning, 45 graders krumming og 2 meters avstand.
Det gir omtrent 65,4 graders vertikal dekning. Bildestørrelse og krumming er
separate innstillinger: å endre krumming beholder den horisontale vinkelen
mot sidekantene. 0 graders krumming gir en helt flat skjerm. Høyden følger
16:9-formatet langs skjermens bue. Større bilde gir større kildepiksler.
Bildestørrelsen i grader er angitt ved 2 meters avstand. Avstand flytter nå
skjermen uten å skalere den: nærmere gir større bilde, lenger unna gir mindre.
«Synlig bredde» viser den resulterende vinkelen. Avstand endrer også flatenes
stereodybde; man kan beholde en fast avstand og bruke bare bildestørrelse.

## Manuell justering i Unity og Windows-build

Åpne **ROV > View > Manual calibration**. Vinduet «ROV bilde» har skyveknapper
og tallfelt for bildestørrelse, krumming, avstand, skjermplassering opp/ned,
vinkel opp/ned og helning. «Skjermplassering (m)» flytter hele skjermen vertikalt
uten å endre rotasjonen: positivt er opp, negativt er ned, 0 er øyehøyde før
kameravinkelen legges til. De virker
både før og under Play. Endringer vises umiddelbart; velg **Lagre JSON** for å
beholde dem når Play stoppes. Den samme komponenten kan justeres i Inspector.

I Windows-build og Game-visningen viser **F4** et panel med de samme valgene.
Panelet og statusmeldinger er på PC-skjermen, ikke som VR-panel. I Quest
brukes kontrolleren beskrevet nedenfor. «ROV pitch/roll» kan slås av/på i panelet.

## Konfigurasjonsfil

Aktiv fil på denne PC-en:

`%USERPROFILE%/AppData/LocalLow/infiniteMonkey/GstUnityQuest3/rov-video-view.json`

Den ligger i `Application.persistentDataPath` og brukes av både Play og en
Windows-build med samme Company/Product. **Y / F5 / Lagre JSON** skriver filen.
Forrige versjon beholdes som `.bak`. Filen lastes ved neste Play/start eller
ved **F2 / Last JSON**. Gamle PlayerPrefs-profiler brukes ikke. Ved feil i filen
beholdes gjeldende innstillinger og feilen vises; ugyldig innhold skrives ikke
automatisk over. Se `Assets/ROV/video-view.example.json` for et eksempel.

| JSON-felt | Betydning | Område |
| --- | --- | --- |
| horizontalFov | Horisontal bildestørrelse ved 2 meter | 50–150 grader |
| curveDegrees | Skjermens krumming, 0 = flat | 0–150 grader |
| distance | Virtuell avstand til bildemidten | 1–4 meter |
| verticalPositionMeters | Flytt skjermen opp (+) eller ned (-) i verdensrammen | -2–2 meter |
| verticalOffsetDegrees | Vinkle skjermen opp (+) eller ned (-) | -25–25 grader |
| imageRollDegrees | Juster helning | -15–15 grader |
| anchorToRovImu | Følg ROV-ens pitch/roll og kameratilt, med fast sideretning | true / false |
| cylindricalSurface | Bruk den manuelt buede flaten | true / false |
| compensateRovPitch | Motvirk ROV-ens tipping med tiltservoen | true / false |
| pitchCompensationStrength | Styrke på pitch-korreksjonen, 1 = full beregnet | 0–1,5 |
| hudEnabled | Hele VR-HUD-en, inkludert kompassringen | true / false |
| hudShowAttitude / hudShowBattery / hudShowDepth | Individuelle instrumentgrupper | true / false |
| hudShowDiagnostics | Render-FPS, mottatt video-FPS og headset-Hz | true / false |
| hudShowCompass | Kompassring rundt brukeren nederst | true / false |
| compassRadius | Kompassringens radius | 1–3 meter |
| compassDrop | Avstand ned fra øyehøyde til kompassringen | 0,4–1,8 meter |
| hudScale / hudOpacity | HUD-tekststørrelse og synlighet | 0,7–1,5 / 0,2–1 |
| telemetrySmoothingSeconds | Utjevning av IMU/mount i videoflatens orientering | 0–0,2 s, standard 0,06 |
| servoSmoothingSeconds | Utjevning av målet for fysisk kameratilt | 0–0,5 s, standard 0,12 |
| servoMaximumSpeed | Fartsgrense på fysisk kameratilt | 5–90 grader/s, standard 45 |
| hudLayout | Individuelle bokser: status, battery, depth, cameraTilt, attitude, diagnostics | Se under |

Behold `schemaVersion: 1`. Bruk punktum som desimalskilletegn. **Standard**
setter 110/45/2 igjen; trykk Lagre JSON eller Y hvis det skal beholdes.
Personlig hoderetning lagres ikke som en fast verdensretning: bruk X i hver
Quest-økt for å velge retningen videoflaten skal stå foran deg i.

Dette er en forstørret kinovisning. Et alternativ som bevarer et rettlinjet
kameraperspektiv finnes ved å slå av `Cylindrical Surface`, sette HFOV til 80
og bruke `Apply / preview view` i komponenten `RovVideoViewCalibration`.
Den varianten ser mindre buet ut fra midten, siden projeksjonen korrigerer buen.

## Sentrering og kontroller

Start Play med Quest Link tilkoblet. Se rett frem med normal hodestilling og
trykk venstre X. Det sentrerer bildet mot nåværende retning og nullstiller
referansen som kameratilten bruker. Med pitch-kompensasjon betyr denne
hodenøytralen vannrett kameraretning i verdensrammen. Servoens relative vinkel
kan være forskjellig fra null når ROV-en står på skrå.

## Kompensasjon når ROV-en tipper

«Kompenser ROV-pitch» er aktivert som standard og kan lagres til JSON med Y.
«Kompensasjonsstyrke» justerer bare pitch-korreksjonen fra IMU, uten å endre
hoderesponsen når ROV-en er vannrett eller visningens yaw/roll. 1,00 gir full
beregnet korreksjon; 0,90 gir 90 prosent. Etter melding om fysisk overkompensasjon
er 0,90 lagret som første prøve på denne PC-en. Dette er fintrim, ikke en måling
av servoens faktiske vinkel. Juster med små steg og bruk Y/Lagre JSON.

Eksisterende konfigurasjonsfiler uten feltet bruker true. Hodets kalibrerte
pitch bestemmer ønsket kamerapitch i verdensrammen. IMU-rotasjonen brukes til
å beregne nødvendig vinkel relativt til ROV-en. Eksempel: hodet rett frem og
ROV-en 15 grader opp gir omtrent -15 graders kameratilt. Beregningen tar også
hensyn til rollens påvirkning på tiltaksen; en enkelt tiltservo stabiliserer
ikke kameraets roll eller yaw.

Kameraets avstand fra ROV-ens rotasjonssenter endrer ikke den stive kroppens
rotasjonsvinkel, men gir kameraforflytning og parallakse mot nære objekter.
Fysisk overrotasjon må skilles fra denne bildebevegelsen. Kontroller gjerne
kameraretningen mot et motiv flere meter unna under fintrim. PWM-mountens
rapporterte mål erstatter ikke en fysisk vinkelmåling.

Grensene -35/+30 grader, utjevning og hastighetsgrense beholdes. Når ønsket
vinkel ikke kan oppnås, brukes nærmeste tilgjengelige tilt. Fersk IMU er påkrevd
for kontinuerlig kompensasjon. Utdatert IMU stopper kommandoene og utløser den
eksisterende tilbakeføringen til RC-styring; gamle IMU-vinkler brukes ikke videre.

| Handling | Venstre Quest-kontroller | Tastatur |
| --- | --- | --- |
| Sentrer bildet og tiltens hodenøytral | X | C sentrerer bare bildet; R nullstiller tilt |
| Øk / reduser bildestørrelse, 2 grader | Spak høyre / venstre | F7 / F6 |
| Øk / reduser krumming, 5 grader | Hold avtrekker og spak høyre / venstre | Panelet / Inspector |
| Øk / reduser virtuell avstand, 0,1 meter | Spak opp / ned | F10 / F9 |
| Flytt skjermen opp / ned, 0,05 meter | Hold grip og spak opp / ned | Page Up / Page Down |
| Vinkel opp / ned | Panelet på PC | Panelet / Inspector |
| Juster helning, 0,5 grad | Hold grip og spak høyre / venstre | Inspector: Image Roll Degrees |
| Lagre alle visningsvalg til JSON | Y | F5 |
| Last JSON på nytt | Panelet på PC | F2 |
| HUD av/på | B på høyre kontroller (og venstre spakklikk) | Panelet / Inspector |

Høyre A slår hodefølging av/på. Av låser hele skjermens posisjon og rotasjon
i rommet og holder siste kameratilt, uten fortsatt IMU-kompensasjon. Bildet
er fortsatt live. Neste trykk på A fortsetter følging med eksisterende
hastighetsgrenser og hodenøytral. X-sentrering er sperret mens skjermen er
låst. B viser/skjuler HUD-boksene og kompasset uten å påvirke servoen.

Samme valg finnes som «Hodefølging på (A)» og «Vis HUD i VR (B)» i ROV bilde,
F4-panelet og Inspector. Y/Lagre JSON lagrer dem som headFollowingEnabled
og hudEnabled. Gamle JSON-filer starter med hodefølging på. Lagret lås får
en innledende plassering når headsettet er klart, før flaten låses.
R nullstiller fortsatt hodenøytral; P er en separat servopause som frigir til RC.
Fokus-, tracking- og forbindelsessperrene gjelder også når kameratilten holdes.
Avtrekker har prioritet over grip når begge holdes mens spaken beveges sideveis.

## Flytende pilot-HUD

HUD-en består av flate instrumentbokser med egen plassering og dybde.
«HUD følger» velger forankringen i ROV bilde / F4 / Inspector. Standardvalget
«Videoflaten» følger skjermens faktiske posisjon og rotasjon, inkludert
kameratilt og ROV-pitch/roll. Boksenes posisjoner og egen dybde beholdes;
z=2 m er ved videosenteret. «Hodet» holder boksene foran øynene, mens
«Fast X-retning» gir den tidligere uavhengige retningen fra X-sentreringen.
Valget lagres som hudAnchorMode (0=video, 1=hode, 2=X-retning) i JSON.
Eldre profiler får videofølge. HUD-pose oppdateres etter videoposen i samme
frame og før rendering. A låser også HUD-posen; B skjuler boksene og kompasset.
Kompassringen beholder separat horisontal plassering nederst.
Hver boks kan justeres under sitt navn i ROV > View > Manual calibration
eller F4-panelet:

- Høyre/venstre og høyde: -2 til +2 meter, relativt til øyeposisjonen.
- Avstand fremover: 0,6–4 meter. Nærmere gjør boksen visuelt større.
- Boksstørrelse: 0,5–2 ganger, inkludert bakgrunn og tekst.
- Vinkle mot sentrert øyeposisjon: orienterer boksen for lesing ved X-referansen.
- Ekstra pitch/yaw/roll: -60 til +60 grader.

Disse lagres som position (x/y/z), scale, faceViewer og angleOffset (x/y/z)
i hudLayout.<boksnavn>. «Tilbakestill bare HUD-plassering» endrer bare boksene.
Lagre med Y, F5 eller Lagre JSON. HUD-tekststørrelse påvirker bare teksten.

Utformingen bruker en fast leseretning og tydelig hierarki: en smal statuslinje
øverst viser forbindelse og armstatus, mens varsler bare vises når noe krever
oppmerksomhet. Kameratilt er samlet til venstre, dybdeestimat til høyre,
batteri nederst til venstre og ROV-pitch/roll nederst til høyre. Store tall,
synlige enheter, dempede etiketter og små mørke underlag gir kontrast mot
videoen. Gult/rødt brukes sammen med tekst for grenser og manglende data.
Midten av videobildet er fri. Instrumentgrupper kan skjules enkeltvis.
HUD-tekststørrelse og synlighet kan justeres og lagres.

Dette er en tilpasning til ROV-pilotering av prinsipper om å begrense HUD-støy,
gruppere informasjon og unngå at symbolene stjeler oppmerksomhet fra motivet.
Kilder er NASAs Head-Up Displays and Attention Capture, FAAs veiledning om
flight deck displays og Cockpits instrumentutvalg. Den konkrete plasseringen
og lesbarheten må vurderes av pilotene i Quest under bruk.

Kantvignett, komfort-siktemerke og komfortmeny er fjernet. Eldre JSON-felt for
disse effektene ignoreres og skrives ikke på nytt. Utjevning er nå synlig i
egne slidere: bilde/IMU 0,06 s, fysisk kameratilt 0,12 s og kamerafart 45°/s
beholder det tidligere utgangspunktet. 0 s slår av det respektive tidsfilteret;
fartsgrensen gjelder fortsatt. Mer utjevning gir mer etterslep. Første gyldige
videopose settes direkte, videre filtrering skjer én gang per renderframe.
Headsettets sporing filtreres ikke av disse valgene.

HUD-en bruker én world-space Canvas med egen rotasjon og flate bokser.
Geometrien er bufret og følger transformene uten å bygges på nytt av hoderotasjon.
Tall oppdateres når den formaterte verdien endres, opptil 5 ganger/sekund.
Kompassetikettene får fast lokal orientering, i stedet for å roteres mot
headsettet hver renderframe. Ingen fullskjermseffekt, tidsbuffer eller blur
er aktivert.

Kompassringen er beholdt som en separat 360-graders ring rundt brukeren
nederst, som standard 0,9 meter under øynene med radius 1,7 meter. ROV-yaw
roterer gradmarkeringene, mens hoderotasjon ikke flytter dem. Kompasset
dreier ikke videoflaten. Høyde, radius og synlighet kan justeres.

Telemetri gjenbruker kontrollerens eksisterende REST-forespørsel. Batteri 0 fra
BATTERY_STATUS prioriteres, med SYS_STATUS som reserve. Ukjent prosent blir
-- %, ikke en gjetning fra spenningen. Den tilkoblede ROV-en sender rundt
15,4–15,5 V, men battery_remaining=-1. Dybde er tydelig merket EST. og AHRS2:
-AHRS2.altitude er et sekundærestimat, med mulig offset også på land.
Utdaterte sensorer blir --- uavhengig av heartbeat. HUD-en sender ingen
styringskommandoer.

Diagnostikk kan vise mottatt video-FPS, app-FPS og headset-Hz dersom runtime
rapporterer dette. --- betyr utilgjengelig. FPS er ikke en måling av total
kameraforsinkelse. Unity-VSync er ikke lagt til: XR-runtime styrer
synkroniseringen. Videopipelinen og kildens 30 FPS er beholdt.

Skjermens retning følger ROV-ens MAVLink ATTITUDE pitch/roll, kombinert med
rapportert mount-pitch. ROV-ens yaw brukes ikke til å flytte videoflaten:
når ROV-en svinger til høyre eller venstre, endres motivet i videoen, mens
flaten beholder sideretningen som ble valgt med X. Brukeren slipper derfor
å vri hodet etter ROV-ens sving for å beholde bildesenteret foran seg.
Hodets yaw eller roll roterer heller ikke videoflaten; brukeren kan fortsatt
se rundt på den større flaten. Ved aktiv pitch-kompensasjon beholdes
verdensrammens pitch, slik at opprinnelig ROV-pitch ikke legges på igjen
etter at servoen har kompensert den. Hodepitch styrer fortsatt fysisk tilt.
Skjermens pitch følger rapportert tilt, begrenset til -35/+30 grader; den
fortsetter ikke med ubegrenset hodepitch etter at kameraet når grensen.

Implementasjonen gjelder den kontrollerte konfigurasjonen MNT1_TYPE=7, ustabilisert
PWM-mount: kameraets orientering beregnes som ROV-rotasjon ganger mount-pitch.
MAVLink-gimbalflaggenes standardverdier brukes ikke til å anta at denne enkle
PWM-servoen er en stabilisert gimbal. ArduPilot rapporterer mountens mål, uten
encoderfeedback. Denne verdien er derfor et estimat på kameravinkelen.

REST-telemetri leses omtrent 10 ganger/sekund. Separate tellere krever to ulike
IMU- og mountoppdateringer før data brukes; uendrede data utløper etter 0,75
sekund. Ved manglende data holdes siste visningsretning, uten å bytte tilbake
til hoderotasjon. Rotasjonen bruker den opprinnelige faste tidskonstanten på 0,06 sekunder. Dette
kompenserer ikke for tidsforskjellen mellom videoframes, IMU og servobevegelse.
Headsetkameraet beholder vanlig sporing. Skjermens posisjon følger fortsatt
hodets translasjon for å unngå å simulere parallakse fra et kamera som står i ro.

## Kamera og oppløsning

BlueOS rapporterer `H264 USB Camera`, USB `32e4:9422`, serienummer `2020032801`,
med H.264 1920 x 1080 ved 30 fps på `/dev/video2`. Identifikasjonen samsvarer
med Blue Robotics Low-Light HD USB Camera. Det er en modellidentifikasjon
basert på USB-data, ikke en verifisert måling av linsen. Modellens oppgitte
horisontale synsfelt er 80 grader; linse, kamerahus og vann kan endre faktisk
synsfelt. Ingen brukbar CAMERA_INFORMATION med linsekalibrering ble funnet.

Standardvisningen 110/45 gir omtrent 14,7 kildepiksler per horisontal grad i
midten og 17,5 i gjennomsnitt. Den tidligere 270-graders prøven ga ca. 7,1 og
ble opplevd som for innzoomet og for krum. I den
perspektivkorrigerte 80-graders varianten er det omtrent 20 i bildesenteret.
Økt avstand gjør nå bildet mindre og øker antall kildepiksler per visningsgrad,
men gir ikke flere detaljer i selve kilden. 2 meter er et praktisk utgangspunkt for visningskomfort;
det finnes ingen unik meteravstand som maksimerer oppløsningen.

Teksturen bruker Bilinear og Clamp, uten mips: pluginen oppdaterer bare
mipnivå 0. Kildens 16:9-format beholdes langs skjermens bue. Sylindervisning
endrer perspektivet sammenlignet med et rettlinjet kamera; bredere bilde gir
mer dekning i headsettet, med samme antall kildepiksler. Videoen er mono,
så skjermavstand gir ikke stereodybde i selve motivet. Quest-runtime styrer
øyenes projeksjon og IPD; kameraets vanlige Unity-FOV overstyres ikke for XR.

## Kontroll og tilbakeføring

`ROV > View > Verify projection (no movement)` kontrollerer geometri,
perspektiv, bueradius, UV, normaler, trekantretning, MAVLink-akser, IMU-ferskhet,
tiltgrenser og tilpasning til øyets projeksjon.
Oppsettet tar en kopi av scenen inkludert daværende ulagrede sceneendringer i
`Assets/ROV/Backups/BeforeVideoViewCalibration.unity` før første installasjon.
Kopien inneholder tidligere plassering og størrelse. En egen kopi før IMU-
endringen finnes i `Assets/ROV/Backups/BeforeImuView.unity`. Arbeidsscenen er lagret
med det nye oppsettet. Ingen ny Windows-eksekverbar er bygget.

Kilder:

- [Blue Robotics kameraspesifikasjon](https://bluerobotics.com/store/sensors-cameras/cameras/cam-usb-low-light-r1/)
- [Opprinnelig rapport med samme USB-ID og serienummer](https://discuss.bluerobotics.com/t/low-light-hd-usb-camera-random-disconnect-pi-3b/9370)
- [Meta om avstand og visningskomfort](https://developers.meta.com/vr/design/display/)
- [MAVLink ATTITUDE: akser og radianer](https://mavlink.io/en/messages/common.html#ATTITUDE)
- [ArduPilot PWM-mount: rapporterer mål uten encoderfeedback](https://github.com/ArduPilot/ardupilot/blob/Copter-4.5.7/libraries/AP_Mount/AP_Mount_Servo.cpp#L113)
- [Meta: redusere optisk bevegelse og bruke vignett](https://developers.meta.com/vr/resources/locomotion-design-reduce-optic-flow/)
- [Unity: XR ignorerer vSyncCount og targetFrameRate](https://docs.unity3d.com/6000.1/Documentation/ScriptReference/Application-targetFrameRate.html)
- [MAVLink batterifelt og ukjentverdier](https://mavlink.io/en/messages/common.html#BATTERY_STATUS)
- [Blue Robotics om Cockpits AHRS2-dybde](https://discuss.bluerobotics.com/t/depth-estimate-suddenly-changing-while-positioning-system-enabled/22810/10)
- [GStreamer RTP jitterbuffer og forsinkelse](https://gstreamer.freedesktop.org/documentation/rtpmanager/rtpjitterbuffer.html)


- [NASA: Head-Up Displays and Attention Capture](https://ntrs.nasa.gov/citations/20040065771)
- [FAA: Design and Evaluation of Flight Deck Displays](https://hfcc.dot.gov/publications/docs/GeneralGuidance/zz_FAA_GeneralGuidanceDoc_Chapter_03_Section_08.html)
- [Cockpit: instrumenter og grensesnitt](https://blueos.cloud/cockpit/docs/latest/usage/advanced/)

## Feil kurs også i Cockpit

Ved kontroll 21. oktober 2026 viste GPS_RAW_INT NO_GPS, 0 satellitter og
lat/lon 0. ATTITUDE-yaw og GLOBAL_POSITION_INT.hdg viste begge omtrent 302°.
VR-kompasset bruker autopilotens ATTITUDE-yaw. Vanlig GPS-cog er bevegelsesretning,
ikke vinkelen ROV-en peker i; den kan ikke brukes som kompass i stillstand.

Når Cockpit også viser feil retning, kontroller kilden før en visnings-offset:

1. Kontroller fysisk Navigator-montering og at AHRS_ORIENTATION samsvarer med
   kortets retning i forhold til ROV-ens front. En feil på omtrent 90/180° kan
   skyldes orientering, men må verifiseres fysisk.
2. Sammenlign kjente retninger unna stål, PC, telefon og strømforsyninger.
   Feil som varierer med retning eller elektrisk belastning kan tyde på magnetisk
   påvirkning. Ikke prøv thrusterne på land for å sjekke dette.
3. Gjennomfør akselerometer-/kompasskalibrering med korrekt orientering og
   avarmert ROV. Mission Planner: Setup > Mandatory Hardware > Compass >
   Onboard Mag Calibration. Følg ArduSubs prosedyre for fysisk rotasjon og
   restart. Kalibreringen er ikke startet av Unity-endringen.

Ingen autopilotparametere eller kompasskalibrering er endret, og det er ikke
lagt til en skjult VR-offset. Absolutt feil må måles mot en kjent retning.

- https://ardupilot.org/sub/docs/common-compass-setup-advanced.html
- https://ardupilot.org/sub/docs/common-compass-calibration-in-mission-planner.html
- https://mavlink.io/en/messages/common.html#GPS_RAW_INT

## HUD-utforming og live innstillinger (22. oktober 2026)

HUD-kortene bruker nå en mørk marineblå bakgrunn med avrundede hjørner,
tynn kant, lyse tall, separate cyan-fargede enheter og små vektorikoner.
Statuskapselen viser forbindelse, hodestyring og armstatus; varsler vises
under kapselen. Boksplassering, skala, synlighet og kompassringen beholdes.
Ingen blur eller fullskjermseffekt er lagt til. Liberation Sans er inkludert
som lokal ressurs med OFL-lisens. Designreferanser:

- Garmin GMI 20: https://www.garmin.com/es-ES/p/126694/
- Blue Robotics Cockpit: https://bluerobotics.com/introducing-cockpit/

På brukerens ønske er styrings- og visningsendringene fra HUD-arbeidet
tilbakeført til koden før denne redesignen. Den nye HUD-grafikken, flytende
boksene, kompasset og den lagrede JSON-profilen er beholdt. Tilbakeføringen
er gjort fra de konkrete kildeendringene i endringsloggen, ikke fra en eldre
scene som ville ha erstattet brukerens kalibrering.

Etter tilbakeføringen ba brukeren rette stopp ved live innstillinger. Bytte
av «Kompenser ROV-pitch» endrer nå neste begrensede tiltmål, uten å frigi
mount, avbryte sending eller nullstille hodenøytral. Kompensasjonsstyrke,
servofilter/fart, HUD og profilinnlasting beholder også kontrollsesjonen.
Manglende fersk IMU ved aktiv kompensasjon stopper fortsatt styringen.

ApplyGeometry beholder eksisterende mesh ved HUD-, servo- og plasseringsvalg;
bare endret bildegeometri/projeksjon lager nytt mesh. Editor-snarveiene R/P
behandles bare når Game-visningen har fokus og ingen tekstfelt redigeres.
Den native Meta-fokuspolicyen for selve servostyringen er uendret.

Manglende COMMAND_ACK i fire sekunder avslutter gjeldende forsøk og prøver
igjen etter 1, 2, 4 og deretter maksimalt 8 sekunders ventetid. Midlertidig
avvisning behandles på samme måte. Hodenøytral beholdes ved dette forsøket;
tracking, fokus og fersk telemetri er fortsatt påkrevd før sending gjenopptas.
Uttrykkelig avvisning som DENIED, UNSUPPORTED eller FAILED låser fortsatt
styringen til eksplisitt pause/gjenopptak; innstillinger omgår ikke avvisning.

Sju innstillingsregresjoner er gjeninnført, og åtte nye kontroller dekker
kvittering, forsinket gjenopptak, bevart kalibrering og harde avvisninger.
Testene starter ingen nettverkstrafikk. A-lås/B-HUD og øvrige tester beholdes.
Fysisk servofunksjon ved live justering må bekreftes i Quest.

Uavklart fysisk kameratilt 22. oktober: kontinuerlig VR-logg viste aksepterte
kommandoer, tiltmål omtrent -28 til +23 grader og rapportert servo11-PWM
968–2029 us. Brukeren rapporterte likevel fysisk stillstand. Med Unity Play
stoppet ga opprinnelige direkte MAV_CMD_DO_MOUNT_CONTROL -5/0/+5 grader
ACCEPTED og1551/1446/1340 us. Visuell sammenligning i Cockpit viste ingen
synlig kamerabevegelse. Kontrollert SERVO11_FUNCTION=7, MIN800, MAX2200,
REVERSED1, SERVO_GPIO_MASK0, BRD_SAFETY_DEFLT0, BRD_SAFETY_MASK0 og MNT1_TYPE7;
ingen av disse parameterne er endret. Tilten er til slutt satt tilbake til0
grader og RC Targeting. Fysisk funksjon er ikke bekreftet. Neste avklaring er
vanlig kameratilt fra Cockpit/håndkontroller og ved behov måling av faktisk
signal/strøm ved kameratilten; ikke anta at rapportert PWM beviser bevegelse.
