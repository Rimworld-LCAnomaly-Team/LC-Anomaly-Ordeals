using System;
using LCAnomalyOrdeals.Defs;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace LCAnomalyOrdeals.Presentation
{
    public sealed class GameComponent_DuskPresentation : GameComponent
    {
        private bool active; private string variant; private string phase; private float elapsedSeconds;
        public GameComponent_DuskPresentation(Game game) { }
        public static void ShowStart(string ordealVariant) => Current.Game?.GetComponent<GameComponent_DuskPresentation>()?.Begin(ordealVariant, "Start");
        public static void ShowEnd(string ordealVariant, bool success) => Current.Game?.GetComponent<GameComponent_DuskPresentation>()?.Begin(ordealVariant, success ? "Success" : "Failure");
        public static void FilterCurrentInput()
        {
            GameComponent_DuskPresentation component = Current.Game?.GetComponent<GameComponent_DuskPresentation>();
            if (component == null || !component.active || component.elapsedSeconds >= Examinations.DuskOrdealWorker.ActiveSettings().presentationInputBlockSeconds || Event.current == null || Event.current.type == EventType.Layout || Event.current.type == EventType.Repaint) return;
            Event.current.Use();
        }
        public override void GameComponentUpdate()
        {
            if (!active) return; elapsedSeconds += Time.unscaledDeltaTime; DuskOrdealSettings settings = Examinations.DuskOrdealWorker.ActiveSettings();
            if (elapsedSeconds >= settings.presentationFadeInSeconds + settings.presentationHoldSeconds + settings.presentationFadeOutSeconds) active = false;
        }
        public override void GameComponentOnGUI()
        {
            if (!active || Event.current == null) return;
            try
            {
                DuskOrdealSettings settings = Examinations.DuskOrdealWorker.ActiveSettings(); DuskOrdealPresentation presentation = settings.PresentationFor(variant);
                if (presentation == null) { active = false; return; }
                if (elapsedSeconds < settings.presentationInputBlockSeconds && Event.current.type != EventType.Layout && Event.current.type != EventType.Repaint) Event.current.Use();
                if (Event.current.type == EventType.Repaint) Draw(settings, presentation);
            }
            catch (Exception exception) { active = false; Log.ErrorOnce("[LC Anomaly Ordeals] Dusk presentation failed safely: " + exception, 198604273); }
        }
        public override void ExposeData()
        {
            Scribe_Values.Look(ref active, "LCOrdealDuskPresentationActive", false); Scribe_Values.Look(ref variant, "LCOrdealDuskPresentationVariant"); Scribe_Values.Look(ref phase, "LCOrdealDuskPresentationPhase"); Scribe_Values.Look(ref elapsedSeconds, "LCOrdealDuskPresentationElapsedSeconds", 0f);
        }
        private void Begin(string ordealVariant, string presentationPhase)
        {
            DuskOrdealPresentation presentation = Examinations.DuskOrdealWorker.ActiveSettings().PresentationFor(ordealVariant); if (presentation == null) return;
            variant = ordealVariant; phase = presentationPhase; elapsedSeconds = 0f; active = true; SoundFor(presentation)?.PlayOneShotOnCamera();
        }
        private void Draw(DuskOrdealSettings settings, DuskOrdealPresentation presentation)
        {
            float alpha = PresentationAlpha(settings); Rect screen = new Rect(0f, 0f, UI.screenWidth, UI.screenHeight); Color accent = presentation.color.ToColor;
            Widgets.DrawBoxSolid(screen, new Color(0f, 0f, 0f, settings.presentationOverlayAlpha * alpha));
            float bandHeight = Mathf.Clamp(UI.screenHeight * 0.34f, 220f, 390f); Rect band = new Rect(0f, (UI.screenHeight - bandHeight) * 0.5f, UI.screenWidth, bandHeight);
            Widgets.DrawBoxSolid(band, new Color(accent.r, accent.g, accent.b, settings.presentationBandAlpha * alpha)); Widgets.DrawBoxSolid(new Rect(0f, band.y, UI.screenWidth, 3f), new Color(accent.r, accent.g, accent.b, alpha)); Widgets.DrawBoxSolid(new Rect(0f, band.yMax - 3f, UI.screenWidth, 3f), new Color(accent.r, accent.g, accent.b, alpha));
            string kicker, title, body; ResolveText(presentation, out kicker, out title, out body);
            Color oldColor = GUI.color; TextAnchor oldAnchor = Text.Anchor; GameFont oldFont = Text.Font; bool oldWrap = Text.WordWrap; GUI.color = new Color(1f, 1f, 1f, alpha); Text.Anchor = TextAnchor.MiddleCenter; Text.WordWrap = true;
            GUIStyle kickerStyle = new GUIStyle(Text.CurFontStyle) { alignment = TextAnchor.MiddleCenter, fontSize = Mathf.Clamp(Mathf.RoundToInt(UI.screenHeight * 0.025f), 18, 30), fontStyle = FontStyle.Bold }; GUI.Label(new Rect(80f, band.y + 22f, UI.screenWidth - 160f, 44f), kicker, kickerStyle);
            GUIStyle titleStyle = new GUIStyle(Text.CurFontStyle) { alignment = TextAnchor.MiddleCenter, fontSize = Mathf.Clamp(Mathf.RoundToInt(UI.screenHeight * 0.055f), 32, 66), fontStyle = FontStyle.Bold }; GUI.Label(new Rect(80f, band.y + 64f, UI.screenWidth - 160f, 96f), title, titleStyle);
            Text.Font = GameFont.Small; Text.Anchor = TextAnchor.MiddleCenter; GUI.Label(new Rect(UI.screenWidth * 0.18f, band.y + 158f, UI.screenWidth * 0.64f, bandHeight - 180f), body, Text.CurFontStyle);
            GUI.color = oldColor; Text.Anchor = oldAnchor; Text.Font = oldFont; Text.WordWrap = oldWrap;
        }
        private void ResolveText(DuskOrdealPresentation presentation, out string kicker, out string title, out string body)
        {
            if (phase == "Success") { kicker = presentation.successKickerKey.Translate(); title = presentation.successTitleKey.Translate(); body = presentation.successTextKey.Translate(); }
            else if (phase == "Failure") { kicker = presentation.failureKickerKey.Translate(); title = presentation.failureTitleKey.Translate(); body = presentation.failureTextKey.Translate(); }
            else { kicker = presentation.startKickerKey.Translate(); title = presentation.startTitleKey.Translate(); body = presentation.startTextKey.Translate(); }
        }
        private SoundDef SoundFor(DuskOrdealPresentation presentation) => phase == "Success" ? presentation.successSound : phase == "Failure" ? presentation.failureSound : presentation.startSound;
        private float PresentationAlpha(DuskOrdealSettings settings)
        {
            if (settings.presentationFadeInSeconds > 0f && elapsedSeconds < settings.presentationFadeInSeconds) return Mathf.Clamp01(elapsedSeconds / settings.presentationFadeInSeconds);
            float fadeOutStart = settings.presentationFadeInSeconds + settings.presentationHoldSeconds; return settings.presentationFadeOutSeconds > 0f && elapsedSeconds > fadeOutStart ? 1f - Mathf.Clamp01((elapsedSeconds - fadeOutStart) / settings.presentationFadeOutSeconds) : 1f;
        }
    }
}
