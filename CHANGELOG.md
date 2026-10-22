# Endringslogg

## Etter v1 — under utvikling

- Digital hovedzoom og valgfritt flyttbart detaljvindu som deler videoteksturen.
- Valg av zoompunkt på den buede flaten med kontrollerpeker.
- VR-meny med bilde-, HUD-, layout-, zoom-, kompass- og tiltinnstillinger.
- Enkle kontrollermarkører med spakutslag og valgfrie pekerstråler.
- Navngitte JSON-profiler og ombinding av pilotknapper/taster, lagret i profilen.
- Brannmurskript for innkommende video fra én ROV-IP til én Windows-prosess.

Den publiserte v1-builden og v1-taggen er beholdt uendret.

## v1 — 22. oktober 2026

Første delbare Windows x64-build og komplett Unity-prosjekt for Sub Horizon.

- Quest 2/Link hodepitch styrer kameratilt via BlueOS/MAVLink2REST.
- Justerbar pitch-kompensasjon, hastighet og utjevning, med tracking-/fokus-/telemetrikrav.
- Manuell bildegeometri, krumming, avstand, høyde og JSON-profiler.
- HUD følger videoflaten; individuelle boksplasseringer og kompassring.
- A låser/følger skjerm og tilt; B viser/skjuler HUD; X sentrerer; Y lagrer.
- Live innstillinger beholder servokontroll. Manglende ACK gir nytt forsøk.
- Delt startprofil, buildmeny, Windows-pakking, Git LFS og oppsettdokumentasjon.
- Windows-pluginen er bygget på nytt i Release/x64, med korrekt Win64-import og uten Visual Studio-debug-runtimeavhengighet.

Android-kilde og binærer fra upstream er bevart, men v1 inneholder ingen ny APK.
