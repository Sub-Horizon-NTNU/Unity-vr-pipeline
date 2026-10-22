Første delbare Sub Horizon ROV VR-pakke for **Windows x64 + Quest 2 Link/Air Link**.

- **Windows-x64.zip**: pakk ut hele mappen og start `Start-v1.cmd`. GStreamer 1.28.7-runtime er inkludert. Meta Horizon Link og eventuelt Microsoft VC++ x64-runtime må være installert.
- **project.zip**: komplett Unity-prosjekt med faktiske Git LFS-filer. Åpne `UnityProject` med Unity **6000.1.7f1**; Package Manager henter Meta/OpenXR og øvrige pakker.
- **SHA256SUMS.txt**: kontrollsummer for ZIP-filene.

Hovedscene: `Assets/GstUnityPlugin/Scenes/GstUnityPluginDemo01.unity`.
ROV: BlueOS `192.168.2.2`, MAVLink2REST port6040, H.264/RTP video på UDP5601.

**A:** lås/følg skjerm og kameratilt. **B:** HUD av/på. **X:** sentrer. **Y:** lagre.
Innstillinger i F4 eller Editor-menyen ROV → View → Manual calibration.

Byggeren kjører 158 kodekontroller. Kodesjekker og oppstartstest på utviklings-PC-en erstatter ikke praktisk headset-/ROV-test på mottakermaskinen.
Den ferdige Windows-spilleren er startet med bare den medfølgende GStreamer-runtime i søkebanen og mottok 1920×1080 RGBA-video. Windows-pluginen er bygget på nytt i Release/x64 uten avhengighet til Visual Studio-debug-runtime. En fersk Git-kloning med LFS er også kontrollert.
Dette er ikke en APK. Original GstUnityPlugin-lisens/kreditering er bevart; tredjepartsruntime har egne medfølgende lisenser.
