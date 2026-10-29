using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR;

namespace ROVVR
{
    public sealed partial class RovVideoViewCalibration
    {
        private bool startupPending, startupInitialized;
        private string activeProfileName = "", namingSource;
        private string namingDraft = "", namingError = "";
        private int profileOffset, helpTab;
        private bool keyboardShift = true;
        private Text namingDisplay;
        private bool flatMenuPreview;
        private readonly Color pilotAccent = new Color(0.22f, 0.87f, 0.9f);
        private readonly Color pilotMuted = new Color(0.56f, 0.69f, 0.77f);

        private void BeginPilotStartup()
        {
            startupPending = startupInitialized = true;
            if (tilt != null) tilt.StartupSelectionPending = true;
            RefreshProfiles();
        }
        private void UpdatePilotStartup()
        {
            if (tilt == null) return;
            TryCompleteAutomaticStartup(XRSettings.isDeviceActive, IsHeadReady(), tilt.ControlFocused,
                tilt.HeadTracked, tilt.BlueOsLive,
                !tilt.HeadFollowingEnabled || !tilt.CompensateRovPitch || tilt.AttitudeLive);
        }
        // Readiness releases only the startup gate. The controller retains RC ownership,
        // neutral calibration, acknowledgement, interruption and recovery decisions.
        private bool TryCompleteAutomaticStartup(bool xrActive, bool headReady, bool controlFocused,
            bool headTracked, bool rovLive, bool attitudeReady)
        {
            if (!startupInitialized || !startupPending || !xrActive || !headReady
                || !controlFocused || !headTracked || !rovLive || !attitudeReady) return false;
            CompletePilotStartup("");
            return true;
        }
        private void CompletePilotStartup(string name)
        {
            if (!string.IsNullOrEmpty(name) && !TryLoadNamedProfile(name)) return;
            startupPending = false;
            if (tilt != null) tilt.StartupSelectionPending = false;
            pilotMessage = "Klar. Se rett frem og trykk " + BindingText(RovPilotAction.Center) + " for å sentrere.";
            // An explicitly opened settings panel stays open when the connection becomes ready.
            ResetPilotInputEdges();
        }
        private void GoPilotPage(int page)
        {
            pilotMessage = "";
            if (!pilotMenuOpen) TogglePilotMenu();
            pilotPage = page; BuildMenuPage();
        }
        private string ProfileLabel => string.IsNullOrEmpty(activeProfileName) ? "Sist lagrede oppsett" : RovProfileLibrary.FriendlyName(activeProfileName);
        private string BindingText(RovPilotAction action)
        {
            foreach (var b in pilot.bindings)
                if (b.action == action) return ButtonLabel(b.button) + (b.key == RovPilotKey.None ? "" : " / " + b.key);
            return "Ikke tilordnet";
        }
        public static string ButtonLabel(RovPilotButton button)
        {
            switch (button)
            {
                case RovPilotButton.None: return "Ingen VR-knapp";
                case RovPilotButton.LeftStick: return "V. spakklikk"; case RovPilotButton.RightStick: return "H. spakklikk";
                case RovPilotButton.LeftMenu: return "V. menyknapp";
                case RovPilotButton.LeftTrigger: return "V. avtrekker"; case RovPilotButton.RightTrigger: return "H. avtrekker";
                case RovPilotButton.LeftGrip: return "V. grip"; case RovPilotButton.RightGrip: return "H. grip";
                case RovPilotButton.RightUp: return "H. spak opp"; case RovPilotButton.RightDown: return "H. spak ned";
                default: return button.ToString();
            }
        }
        private string PageHint()
        {
            if (startupPending) return "Starter automatisk når headset, VR-fokus og ROV er klare.\n" + (tilt == null ? "Kamerastyring er ikke tilkoblet." : tilt.Status);
            if (pilotPage == 13) return "Oversikten bruker dine aktive knappevalg.\nTilbake tar deg til innstillingene.";
            if (pilotPage == 6 || pilotPage == 15) return "Profiler inneholder bilde, HUD og knappevalg.\nLagre en ny profil før du deler eller endrer et oppsett.";
            if (pilotPage == 12) return BindingText(RovPilotAction.Menu) + ": tilbake til pilotvisning.\nHodefølging styrer bare kameraet. Armering styres separat.";
            return "Endringer vises med en gang. Lagre for å beholde dem.\nTilbake: alle innstillinger / Lukk: pilotvisning";
        }
        private PilotMenuItem PilotCard(string title, string subtitle, float x, float y, float width, float height, Action select, bool selected = false)
        {
            var item = MenuButton(pilotMenuPage, "", x, y, width, height, select);
            item.label.text = "";
            var stripe = PilotNode("Accent", item.rect, -width / 2 + 4, 0, 5, height - 14).gameObject.AddComponent<Image>();
            stripe.material = pilotUiMaterial; stripe.color = selected ? new Color(1f, 0.75f, 0.25f) : pilotAccent; stripe.raycastTarget = false;
            var heading = PilotLabel(item.rect, title, 8, height * 0.18f, width - 44, height * 0.4f, 28);
            heading.resizeTextForBestFit = true; heading.resizeTextMinSize = 18; heading.resizeTextMaxSize = 28;
            heading.alignment = TextAnchor.MiddleLeft;
            var caption = PilotLabel(item.rect, subtitle, 8, -height * 0.22f, width - 44, height * 0.42f, 19);
            caption.alignment = TextAnchor.MiddleLeft; caption.color = pilotMuted;
            return item;
        }
        private void BuildPilotHome()
        {
            PilotLabel(pilotMenuPage, ProfileLabel, 0, 285, 820, 38, 20).color = pilotMuted;
            QuickSetting(() => "SENTRER OG TILBAKE", -212, 210, () => { CenterViewAndTilt(); TogglePilotMenu(); });
            QuickSetting(() => headFollowingEnabled ? "HODEFØLGING: PÅ / LÅS" : "KAMERA LÅST / FØLG HODET", 212, 210,
                () => SetHeadFollowingEnabled(!headFollowingEnabled));
            QuickSetting(() => hudEnabled ? "HUD: PÅ / SKJUL" : "HUD: AV / VIS", -212, 105, () => hudEnabled = !hudEnabled);
            QuickSetting(() => recorder != null && recorder.Recording ? "STOPP OPPTAK" : "START OPPTAK", 212, 105, ToggleRecording);
            var zoom = MenuButton(pilotMenuPage, "", 0, 0, 820, 62, () => {});
            zoom.caption = () => "ZOOM   " + pilot.mainZoom.ToString("F2") + "x   [ −       + ]";
            zoom.drag = true; zoom.select = x => pilot.mainZoom = Mathf.Lerp(1f, 8f, Mathf.Clamp01(x));
            if (tilt != null) QuickSetting(() => tilt.RcControlSelected ? "KAMERA: RC / VELG VR" : "KAMERA: VR / VELG RC", -212, -105,
                () => tilt.SelectRcControl(!tilt.RcControlSelected));
            QuickSetting(() => "HUD OG LESBARHET", 212, -105, () => GoPilotPage(3));
            QuickSetting(() => "ALLE INNSTILLINGER", -212, -230, () => GoPilotPage(16));
            QuickSetting(() => "LAGRE TIL NESTE OPPSTART", 212, -230, SaveCalibration);
        }
        private void QuickSetting(Func<string> caption, float x, float y, Action action)
        {
            var item = MenuButton(pilotMenuPage, "", x, y, 396, 82, action);
            item.caption = caption;
            item.label.resizeTextForBestFit = true;
            item.label.resizeTextMinSize = 18; item.label.resizeTextMaxSize = 24;
        }
        private void BuildAdvancedHome()
        {
            int[] pages = {2, 0, 3, 5, 6, 7, 1, 4, 8, 9, 10, 11, 13};
            for (int i = 0; i < pages.Length; i++)
            {
                int page = pages[i];
                MenuButton(pilotMenuPage, PilotPages[page], i % 2 == 0 ? -212 : 212,
                    275 - (i / 2) * 85, 396, 70, () => GoPilotPage(page));
            }
        }
        private void BuildProfilePicker()
        {
            RefreshProfiles();
            string chosen = profileNames.Length == 0 ? "" : SelectedProfileName;
            PilotLabel(pilotMenuPage, "AKTIVT OPPSETT  /  " + ProfileLabel, 0, 283, 820, 38, 20).color = pilotMuted;
            int firstY = 214;
            for (int i = profileOffset; i < Math.Min(profileOffset + 3, profileNames.Length); i++)
            {
                int index = i; string name = profileNames[i];
                PilotCard(RovProfileLibrary.FriendlyName(name), name == activeProfileName ? "Aktiv profil" : "Bilde, instrumenter og knappevalg", 0, firstY - (i - profileOffset) * 94, 820, 82,
                    () => { selectedProfile = index; BuildMenuPage(); }, chosen == name);
            }
            if (profileNames.Length == 0) PilotLabel(pilotMenuPage, "Ingen navngitte profiler ennå.\nDu kan lagre ditt eget oppsett her.", 0, 100, 780, 100, 24).color = pilotMuted;
            if (profileNames.Length > 3)
            {
                float navY = -100;
                MenuButton(pilotMenuPage, "< Forrige", -290, navY, 220, 48, () => { profileOffset = Math.Max(0, profileOffset - 3); BuildMenuPage(); });
                PilotLabel(pilotMenuPage, $"{profileOffset + 1}–{Math.Min(profileOffset + 3, profileNames.Length)} av {profileNames.Length}", 0, navY, 250, 48, 20);
                MenuButton(pilotMenuPage, "Neste >", 290, navY, 220, 48, () => { if (profileOffset + 3 < profileNames.Length) profileOffset += 3; BuildMenuPage(); });
            }
            MenuButton(pilotMenuPage, "LAST VALGT", -280, -190, 250, 64, () => { if (profileNames.Length > 0) LoadNamedProfile(SelectedProfileName); });
            MenuButton(pilotMenuPage, "NY PROFIL", 0, -190, 250, 64, () => BeginProfileName(null));
            MenuButton(pilotMenuPage, "GI NYTT NAVN", 280, -190, 250, 64, () => { if (profileNames.Length > 0) BeginProfileName(SelectedProfileName); });
            // Saving never overwrites an unrelated selected file: use the active named profile.
            MenuButton(pilotMenuPage, "LAGRE ENDRINGER I AKTIV PROFIL", 0, -270, 820, 56, () => { if (string.IsNullOrEmpty(activeProfileName)) BeginProfileName(null); else SaveNamedProfile(activeProfileName); });
        }
        private void BeginProfileName(string rename)
        {
            namingSource = rename;
            namingDraft = rename ?? RovProfileLibrary.NextName("Pilotprofil", profileNames);
            namingError = ""; GoPilotPage(15);
        }
        private void SaveProfileName()
        {
            try
            {
                RovProfileLibrary.ProfilePath(namingDraft);
                if (namingSource != null)
                {
                    RovProfileLibrary.Rename(namingSource, namingDraft);
                    if (activeProfileName == namingSource) activeProfileName = namingDraft;
                }
                else
                {
                    if (System.IO.File.Exists(RovProfileLibrary.ProfilePath(namingDraft))) throw new ArgumentException("Navnet er i bruk. Velg et annet navn.");
                    GetProfile().Save(RovProfileLibrary.ProfilePath(namingDraft)); activeProfileName = namingDraft;
                }
                profileName = namingDraft; RefreshProfiles(); selectedProfile = Array.IndexOf(profileNames, namingDraft);
                profileOffset = Math.Max(0, selectedProfile / 3 * 3); namingSource = null; GoPilotPage(6);
                pilotMessage = "Profil lagret: " + namingDraft;
            }
            catch (Exception e) { namingError = e.Message; if (namingDisplay != null) namingDisplay.text = namingDraft + "\n" + namingError; }
        }
        private void BuildProfileKeyboard()
        {
            namingDisplay = PilotLabel(pilotMenuPage, namingDraft + "\n" + namingError, 0, 235, 820, 95, 28);
            namingDisplay.resizeTextForBestFit = true; namingDisplay.resizeTextMinSize = 18; namingDisplay.resizeTextMaxSize = 28;
            string[] suggestions = {"Pilotprofil", "Inspeksjon", "Oversikt"};
            for (int i = 0; i < suggestions.Length; i++)
            {
                string stem = suggestions[i];
                MenuButton(pilotMenuPage, stem, (i - 1) * 275, 168, 250, 38, () => { namingDraft = RovProfileLibrary.NextName(stem, profileNames); namingDisplay.text = namingDraft; });
            }
            string[] rows = {"QWERTYUIOPÅ", "ASDFGHJKLØÆ", "ZXCVBNM1234", "567890-_ ."};
            for (int row = 0; row < rows.Length; row++)
            {
                string keys = rows[row];
                for (int col = 0; col < keys.Length; col++)
                {
                    char key = keys[col]; string text = keyboardShift ? key.ToString() : char.ToLowerInvariant(key).ToString();
                    MenuButton(pilotMenuPage, text, (col - (keys.Length - 1) / 2f) * 73, 105 - row * 66, 65, 56,
                        () => { if (namingDraft.Length < 64) namingDraft += text; namingError = ""; namingDisplay.text = namingDraft; });
                }
            }
            MenuButton(pilotMenuPage, "Slett tegn", -275, -180, 245, 55, () => { if (namingDraft.Length > 0) namingDraft = namingDraft.Substring(0, namingDraft.Length - 1); namingDisplay.text = namingDraft; });
            MenuButton(pilotMenuPage, keyboardShift ? "ABC / abc" : "abc / ABC", 0, -180, 245, 55, () => { keyboardShift = !keyboardShift; BuildMenuPage(); });
            MenuButton(pilotMenuPage, "Tøm felt", 275, -180, 245, 55, () => { namingDraft = ""; namingDisplay.text = namingDraft; });
            MenuButton(pilotMenuPage, "Avbryt", -212, -265, 396, 65, () => GoPilotPage(6));
            MenuButton(pilotMenuPage, namingSource == null ? "LAGRE NY PROFIL" : "BEKREFT NYTT NAVN", 212, -265, 396, 65, SaveProfileName);
        }
        private void BuildControllerHelp()
        {
            string[] tabs = {"PILOTERING", "MENY OG PEKER", "FLYTT VINDUER"};
            for (int i = 0; i < tabs.Length; i++) { int tab = i; MenuButton(pilotMenuPage, tabs[i], (i - 1) * 280, 270, 265, 54, () => { helpTab = tab; BuildMenuPage(); }); }
            if (helpTab == 0)
            {
                HelpPanel("VENSTRE", -212);
                HelpPanel("HØYRE", 212);
                int left = 0, right = 0;
                foreach (var b in pilot.bindings)
                {
                    if (b.button == RovPilotButton.None) continue;
                    bool l = b.button == RovPilotButton.X || b.button == RovPilotButton.Y || b.button == RovPilotButton.LeftTrigger || b.button == RovPilotButton.LeftGrip || b.button == RovPilotButton.LeftStick || b.button == RovPilotButton.LeftMenu;
                    int row = l ? left++ : right++;
                    HelpRow(l ? -212 : 212, 125 - row * 56, ButtonLabel(b.button), ActionLabel(b.action));
                }
                if (left <= 2)
                {
                    var analog = PilotLabel(pilotMenuPage,
                        pilot.stickCalibrationEnabled ? "VENSTRE SPAK · MED MENYEN LUKKET\n\nSideveis: bildebredde\nOpp/ned: avstand\nAvtrekker + sideveis: krumming\nGrip + spak: helning / høyde" : "VENSTRE SPAK\n\nBildejustering er slått av.\nFlytting av valgte vinduer er fortsatt\ntilgjengelig når funksjonen er på.",
                        -212, -93, 350, 230, 20);
                    analog.alignment = TextAnchor.MiddleLeft; analog.color = pilotMuted;
                }
                PilotLabel(pilotMenuPage, "HODET  /  Se opp og ned for å styre kameratilten.\nA/lås holder siste mål. RC velges uttrykkelig i Kamera og kompass.", 0, -287, 820, 74, 22).color = pilotMuted;
            }
            else if (helpTab == 1)
            {
                HelpWide(190, "1  ÅPNE MENYEN", BindingText(RovPilotAction.Menu));
                HelpWide(80, "2  PEK OG VELG", "Pek med høyre hånd. Trykk høyre avtrekker på et kort eller valg.");
                HelpWide(-30, "3  JUSTER EN VERDI", "Hold høyre avtrekker og dra over verdifeltet. Endringen vises straks.");
                HelpWide(-140, "4  LAGRE OPPSETTET", "Profiler → Ny profil. Bruk et navn som beskriver piloten eller oppgaven.");
                HelpWide(-250, "SKJEV MENY ELLER FEIL PEKER?", "Hold begge grip-knappene i 0,7 s. Menyen rettes opp foran deg, og begge pekere sentreres mot midten.");
            }
            else
            {
                string grab = pilot.windowGrabButton == RovWindowGrabButton.LeftTrigger ? "Venstre avtrekker" : pilot.windowGrabButton == RovWindowGrabButton.LeftGrip ? "Venstre grip" : "Venstre spakklikk";
                HelpWide(190, "1  GRIP MENYEN ELLER ZOOMVINDUET", grab + " holdes inne mens du peker på vinduet. Gullramme betyr valgt.");
                HelpWide(80, "2  FLYTT", "Venstre spak: sideveis og opp/ned. Hold gripeknappen inne.");
                HelpWide(-30, "3  ENDRE AVSTAND", (pilot.windowGrabButton == RovWindowGrabButton.LeftGrip ? "Høyre grip" : "Venstre eller høyre grip") + " + venstre spak opp/ned.");
                HelpWide(-140, "4  VINKLE OG RETT OPP", "Høyre spak: pitch/yaw. Høyre spakklikk: nullstill vinklene på valgt vindu. Slipp gripeknappen for å plassere.");
                HelpWide(-250, "RETT OPP / BEHOLD PLASSERINGEN", "Begge grip i 0,7 s: meny foran deg. Lagre via Profiler. Oppstartsmenyen kan også flyttes; den endrer ikke lagrede profiler.");
            }
        }
        private void HelpPanel(string title, float x)
        {
            var node = PilotNode("Controller help panel", pilotMenuPage, x, -10, 396, 480);
            var image = node.gameObject.AddComponent<Image>(); image.material = pilotUiMaterial; image.color = new Color(0.028f, 0.06f, 0.085f, 1); image.raycastTarget = false;
            PilotLabel(node, title, 0, 205, 350, 42, 26).color = pilotAccent;
        }
        private void HelpRow(float x, float y, string badge, string action)
        {
            var plate = PilotNode("Button badge", pilotMenuPage, x - 115, y, 125, 44);
            var image = plate.gameObject.AddComponent<Image>(); image.material = pilotUiMaterial;
            image.color = new Color(0.05f, 0.24f, 0.29f, 1f); image.raycastTarget = false;
            PilotLabel(plate, badge, 0, 0, 115, 40, 18).color = pilotAccent;
            var label = PilotLabel(pilotMenuPage, action, x + 75, y, 230, 52, 20); label.alignment = TextAnchor.MiddleLeft;
        }
        private void HelpWide(float y, string title, string body)
        {
            PilotLabel(pilotMenuPage, title, 0, y + 20, 800, 34, 23).color = pilotAccent;
            PilotLabel(pilotMenuPage, body, 0, y - 22, 800, 58, 21);
        }
        public static string ActionLabel(RovPilotAction action)
        {
            switch (action)
            {
                case RovPilotAction.Follow: return "Lås / følg hodet"; case RovPilotAction.Hud: return "Vis / skjul HUD";
                case RovPilotAction.Center: return "Sentrer bilde og tilt"; case RovPilotAction.Save: return "Lagre oppstartsoppsett";
                case RovPilotAction.Menu: return "Åpne / lukk meny"; case RovPilotAction.ZoomIn: return "Zoom inn";
                case RovPilotAction.ZoomOut: return "Zoom ut"; case RovPilotAction.AimZoom: return "Velg zoompunkt";
                case RovPilotAction.ResetZoom: return "Nullstill zoom"; default: return "Last oppstartsoppsett";
            }
        }
        private void DrawStartupDesktop()
        {
            if (!startupPending) return;
            GUILayout.Label("ATLAS / AUTOMATISK OPPSTART");
            GUILayout.Label("Bruker sist lagrede oppsett. Koble til Quest Link og ROV.\nKamerastyring starter når VR-fokus, sporing og telemetri er klare.");
            if (tilt != null) GUILayout.Label(tilt.Status);
        }
        private void DrawStartupOverlay()
        {
            if (!startupPending || flatMenuPreview || XRSettings.isDeviceActive) return;
            GUILayout.BeginArea(new Rect(Mathf.Max(12, Screen.width - 540), 24, 510, Mathf.Min(500, Screen.height - 48)), GUI.skin.box);
            DrawStartupDesktop();
            GUILayout.EndArea();
        }
    }
}
