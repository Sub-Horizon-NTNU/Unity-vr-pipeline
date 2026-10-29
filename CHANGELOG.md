# Endringslogg

## Etter v2 — brukervennlighet og servostyring

- Profilvelger ved oppstart, med uttrykkelig Start før kameratilten begynner å følge hodet.
- Hjem-side med funksjonskort og egen inngang til avanserte valg.
- Kontrollhjelp for pilotering, meny/peker og vindusflytting, tilpasset aktive knappevalg.
- VR-tastatur, lesbare navneforslag og navnebytte for profiler.

- VR-prioritet via kontrollert sperring av RC-kamerainnganger, med eksplisitt VR/RC-valg, gjenopprettingsjournal og verifisert tilbakeføring.
- Hodenøytral bevares ved fokus-/tracking-/IMU-avbrudd og RC/VR-bytte.
- Løpende ACK-ferskhet, avvisning av bufrede kvitteringer og tydeligere stoppdiagnostikk.

## v2 — 24. oktober 2026

- MP4-opptak av ren kameravideo eller PC-speilbilde med HUD, fra VR-meny og PC-panel; 720p/1080p ved 30 fps.
- Egen Menu placement-side med pitch/yaw-slidere, lagret i JSON/profiler.
- Stabil vindusflytting med venstre spak; høyre spak justerer pitch/yaw rundt vinduets sentrum.
- Håndfølging er valgfri; grip + venstre spak justerer avstand. Hastighet og vinkler lagres i profiler.
- Peker med siktekors på eget UI-lag, stråle som stopper på markøren og tydelig ramme rundt valgt vindu.
- Tydelig pekerpunkt på meny/detaljvideo og markering når et vindu gripes.
- Venstre avtrekker griper vinduer; venstre spak flytter, grip + spak endrer dybde.
- Touch-lignende kontrollermarkører, OpenXR-siktepose, justerbar pekerfølsomhet og siktevinkel.
- Menyslideren beholder valgt rad under drag, så vertikal håndbevegelse ikke endrer andre verdier.

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

