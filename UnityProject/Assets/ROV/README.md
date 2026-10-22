# Quest 2 styrer kameratilt

Kalibrering av den buede videoskjermen og venstre Quest-kontroller er beskrevet i [VIDEO_VIEW.md](VIDEO_VIEW.md).

Åpne `Assets/GstUnityPlugin/Scenes/GstUnityPluginDemo01.unity` og start Play med Quest Link / Air Link tilkoblet. Den fungerende GStreamer-pipelinen og CurvedVideoScreen beholdes. `ROVVR.RovHeadTiltController` er installert på `XRRig/Camera Offset/Main Camera`.

Se rett frem under kalibreringen (ett sekund etter at headsettet og BlueOS er tilgjengelige). Deretter følger kameratilten hodebevegelsen opp/ned. Grensene er -35 til +30 grader. Bevegelsen jevnes ut og begrenses til 45 grader/sekund. Det sendes opptil 20 oppdateringer/sekund, med bare én tiltforespørsel i arbeid om gangen.

IMU-pitch-kompensasjon er nå aktivert: hodets kalibrerte pitch angir ønsket vinkel i verdensrammen, og servoen motvirker når ROV-en tipper frem/bak. Ved 15 graders ROV-pitch opp og hodet rett frem blir tiltmålet omtrent -15 grader. Beregningen tar hensyn til rollens effekt på tiltaksen, men denne ene servoen styrer bare pitch. Fersk IMU er påkrevd; foreldede IMU-data stopper sending og utløser eksisterende tilbakeføring til RC. Vinkel- og hastighetsgrensene beholdes. «Kompenser ROV-pitch» i bildepanelet / JSON-feltet compensateRovPitch slår funksjonen av/på. Se rett frem og bruk venstre X før praktisk test.

| Handling | Quest-kontroller (høyre) | Tastatur |
| --- | --- | --- |
| Hodefølging på/av; av låser skjerm og holder kameratilt | A | Innstillingen «Hodefølging på (A)» |
| Vis / skjul HUD og kompass | B | Innstillingen «Vis HUD i VR (B)» |
| Bruk nåværende hodevinkel som rett frem | Venstre X sentrerer også bildet | R |
| Separat servopause / gjenoppta (frigir til RC) | | P |
| Vis / skjul status på PC-skjermen | | F8 |

Ved gjenopptak etter P-servopause kalibreres rett frem på nytt. A beholder i stedet hodenøytral og siste tiltmål, uten å frigi mount. Mens A-låsen er aktiv, følger verken skjermen eller den ønskede kameratilten hodebevegelse/IMU; videobildet og telemetrien er fortsatt live. Nytt trykk på A fortsetter vanlig følging med de eksisterende hastighetsgrensene. A- og B-valgene lagres med Y/Lagre JSON som `headFollowingEnabled` og `hudEnabled`.

Manglende tracking, manglende bruker i headsettet (når enheten rapporterer dette), mistet VR-fokus, P-pause eller foreldet heartbeat stopper tiltoppdateringene også når A-låsen er aktiv. VR-fokus leses direkte fra Meta-runtime, slik at styringen fungerer selv når Unity-vinduet på PC-en ikke er fremst. Quest-systemmenyen kan fortsatt ta VR-fokus og stoppe styringen. Komponenten forsøker å gi kameratilten tilbake til RC/joystick-styring. Ved brutt nettverk eller brå avslutning kan tilbakeføringen ikke garanteres; ingen gamle målvinkler køes for senere sending. Headsettets pose leses direkte fra XR-enheten, ikke fra scene-kameraets globale rotasjon.

Inspector på Main Camera viser komponentens BlueOS-adresse, gain, vinkelgrenser, hastighet og `Invert Pitch`. ArduSub har allerede `SERVO11_REVERSED=1`; ikke reverser en gang til med mindre fysisk test viser feil retning. Oppsettet endrer ingen permanente ROV-parametere og sender ingen thruster-/armeringskommandoer.

Live innstillinger for kompensasjon, styrke, utjevning/fart og HUD frigir ikke servoen eller nullstiller hodenøytral. Nye mål følger samme vinkel- og fartsgrenser. Ved manglende kommandokvittering forsøker styringen på nytt etter 1–8 sekunders ventetid, fortsatt under de vanlige fokus-/tracking-/telemetrikravene. Uttrykkelig avvisning fra autopiloten blir stående som feil til pause/gjenopptak. I Editor virker tastatursnarveiene R/P bare med Game-visningen i fokus, uten tekstredigering.

## Verifisert forbindelse

- BlueOS/MAVLink2REST: `http://192.168.2.2:6040`
- Prosjektets HTTP-tilgang (`insecureHttpOption`) er aktivert fordi den lokale BlueOS-tjenesten bruker HTTP. Dette gjelder også vanlige Windows-builds.
- MAVLink-mål: system 1, komponent 1. Avsender: system 245, komponent 191.
- `COMMAND_LONG` / `MAV_CMD_DO_MOUNT_CONTROL` (205): pitch i param1, MAVLink Targeting (2) i param7. RC Targeting (3) ved tilbakeføring.
- Den installerte MAVLink2REST 0.11.23 avviser JSON med `MAV_CMD_DO_GIMBAL_MANAGER_PITCHYAW`. Derfor benyttes den kompatible kommandoen 205, testet mot denne ROV-en.
- ArduSub 4.5.7, Navigator, utgang 11, `MNT1_TYPE=7` (ustabilisert PWM-mount).
- Små kameratester ga: 0 grader = 1446 us, +5 grader = 1340 us, -5 grader = 1551 us. COMMAND_ACK var ACCEPTED. Utgangspunktet ble gjenopprettet etter testen.
- Rapportert mount-vinkel og PWM er ikke en ekstern måling av den fysiske kameravinkelen.
- Praktisk test 21. oktober 2026: Brukeren bekreftet at det fysiske ROV-kameraet følger hodebevegelsen i riktig retning. En blokkering fra PC-vindusfokus ble rettet ved å bruke Quest-øktens VR-fokus og inputfokus. Live-avlesning viste godtatte kommandoer og endrede PWM-verdier mens `Application.isFocused` var false. Da VR-fokus forsvant, stoppet antallet sendte tiltkommandoer.
- Etter endring av C#-kode under Play kan OpenXR 1.14.3 miste session-tilstanden. Stopp og start Play igjen før VR-testing fortsetter; unngå kodeinnlasting midt i en praktisk test.

## Unity-verifisering og gjeninstallasjon

`ROV > Verify head tracking and MAVLink format (no movement)` kjører 60 sjekker av VR-/PC-fokus, opp/ned-retning, pitch-kompensasjon, yaw/roll, kalibrering, grenser, hastighet, MAVLink-format og heartbeat-cache. Dette sender ingen kommandoer til ROV-en.

`ROV > Verify BlueOS connection (no movement)` leser heartbeat og utgang 11 gjennom UnityWebRequest, slik kjøretidskomponenten gjør.

`ROV > Inspect live headset and tilt status (no movement)` lagrer aktuell tracking, VR-fokus, styringsstatus og antall sendte kommandoer i `Temp/rov-head-tilt-live.json`. Komponenten logger også statusendringer som `[ROV tilt]` i Console.

`ROV > Install Quest camera tilt in working scene` installerer komponenten på Main Camera i den åpne fungerende scenen og legger denne scenen først i Build Settings. Øvrige scenes oppføringer beholdes. Menyen kan brukes igjen uten å legge til duplikater.
