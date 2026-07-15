using System;
using LCAnomalyOrdeals.Defs;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace LCAnomalyOrdeals.Presentation
{
    public sealed class GameComponent_MidnightPresentation : GameComponent
    {
        private bool active; private string variant; private string phase; private float elapsedSeconds;
        public GameComponent_MidnightPresentation(Game game) { }
        public static void ShowStart(string value) => Current.Game?.GetComponent<GameComponent_MidnightPresentation>()?.Begin(value, "Start");
        public static void ShowEnd(string value, bool success) => Current.Game?.GetComponent<GameComponent_MidnightPresentation>()?.Begin(value, success ? "Success" : "Failure");
        public static void FilterCurrentInput()
        {
            GameComponent_MidnightPresentation component = Current.Game?.GetComponent<GameComponent_MidnightPresentation>();
            if (component == null || !component.active || component.elapsedSeconds >= Examinations.MidnightOrdealWorker.ActiveSettings().presentationInputBlockSeconds || Event.current == null || Event.current.type == EventType.Layout || Event.current.type == EventType.Repaint) return; Event.current.Use();
        }
        public override void GameComponentUpdate() { if (!active) return; elapsedSeconds += Time.unscaledDeltaTime; MidnightOrdealSettings settings = Examinations.MidnightOrdealWorker.ActiveSettings(); if (elapsedSeconds >= settings.presentationFadeInSeconds + settings.presentationHoldSeconds + settings.presentationFadeOutSeconds) active = false; }
        public override void GameComponentOnGUI()
        {
            if (!active || Event.current == null) return;
            try { MidnightOrdealSettings settings = Examinations.MidnightOrdealWorker.ActiveSettings(); MidnightOrdealPresentation presentation = settings.PresentationFor(variant); if (presentation == null) { active = false; return; } if (elapsedSeconds < settings.presentationInputBlockSeconds && Event.current.type != EventType.Layout && Event.current.type != EventType.Repaint) Event.current.Use(); if (Event.current.type == EventType.Repaint) Draw(settings, presentation); }
            catch (Exception exception) { active = false; Log.ErrorOnce("[LC Anomaly Ordeals] Midnight presentation failed safely: " + exception, 198604274); }
        }
        public override void ExposeData() { Scribe_Values.Look(ref active, "LCOrdealMidnightPresentationActive", false); Scribe_Values.Look(ref variant, "LCOrdealMidnightPresentationVariant"); Scribe_Values.Look(ref phase, "LCOrdealMidnightPresentationPhase"); Scribe_Values.Look(ref elapsedSeconds, "LCOrdealMidnightPresentationElapsedSeconds", 0f); }
        private void Begin(string value, string presentationPhase) { MidnightOrdealPresentation presentation = Examinations.MidnightOrdealWorker.ActiveSettings().PresentationFor(value); if (presentation == null) return; variant = value; phase = presentationPhase; elapsedSeconds = 0f; active = true; SoundFor(presentation)?.PlayOneShotOnCamera(); }
        private void Draw(MidnightOrdealSettings settings, MidnightOrdealPresentation presentation)
        {
            float alpha = Alpha(settings); Rect screen = new Rect(0f, 0f, UI.screenWidth, UI.screenHeight); Color accent = presentation.color.ToColor; Widgets.DrawBoxSolid(screen, new Color(0f, 0f, 0f, settings.presentationOverlayAlpha * alpha));
            float bandHeight = Mathf.Clamp(UI.screenHeight * 0.36f, 240f, 410f); Rect band = new Rect(0f, (UI.screenHeight - bandHeight) * 0.5f, UI.screenWidth, bandHeight); Widgets.DrawBoxSolid(band, new Color(accent.r, accent.g, accent.b, settings.presentationBandAlpha * alpha)); Widgets.DrawBoxSolid(new Rect(0f, band.y, UI.screenWidth, 4f), new Color(accent.r, accent.g, accent.b, alpha)); Widgets.DrawBoxSolid(new Rect(0f, band.yMax - 4f, UI.screenWidth, 4f), new Color(accent.r, accent.g, accent.b, alpha));
            string kicker, title, body; ResolveText(presentation, out kicker, out title, out body); Color oldColor = GUI.color; TextAnchor oldAnchor = Text.Anchor; GameFont oldFont = Text.Font; bool oldWrap = Text.WordWrap; GUI.color = new Color(1f, 1f, 1f, alpha); Text.Anchor = TextAnchor.MiddleCenter; Text.WordWrap = true;
            GUIStyle kickerStyle = new GUIStyle(Text.CurFontStyle) { alignment = TextAnchor.MiddleCenter, fontSize = Mathf.Clamp(Mathf.RoundToInt(UI.screenHeight * 0.027f), 19, 32), fontStyle = FontStyle.Bold }; GUI.Label(new Rect(80f, band.y + 24f, UI.screenWidth - 160f, 46f), kicker, kickerStyle);
            GUIStyle titleStyle = new GUIStyle(Text.CurFontStyle) { alignment = TextAnchor.MiddleCenter, fontSize = Mathf.Clamp(Mathf.RoundToInt(UI.screenHeight * 0.06f), 36, 72), fontStyle = FontStyle.Bold }; GUI.Label(new Rect(80f, band.y + 70f, UI.screenWidth - 160f, 104f), title, titleStyle);
            Text.Font = GameFont.Small; Text.Anchor = TextAnchor.MiddleCenter; GUI.Label(new Rect(UI.screenWidth * 0.16f, band.y + 172f, UI.screenWidth * 0.68f, bandHeight - 194f), body, Text.CurFontStyle); GUI.color = oldColor; Text.Anchor = oldAnchor; Text.Font = oldFont; Text.WordWrap = oldWrap;
        }
        private void ResolveText(MidnightOrdealPresentation p, out string kicker, out string title, out string body) { if (phase == "Success") { kicker = p.successKickerKey.Translate(); title = p.successTitleKey.Translate(); body = p.successTextKey.Translate(); } else if (phase == "Failure") { kicker = p.failureKickerKey.Translate(); title = p.failureTitleKey.Translate(); body = p.failureTextKey.Translate(); } else { kicker = p.startKickerKey.Translate(); title = p.startTitleKey.Translate(); body = p.startTextKey.Translate(); } }
        private SoundDef SoundFor(MidnightOrdealPresentation p) => phase == "Success" ? p.successSound : phase == "Failure" ? p.failureSound : p.startSound;
        private float Alpha(MidnightOrdealSettings settings) { if (settings.presentationFadeInSeconds > 0f && elapsedSeconds < settings.presentationFadeInSeconds) return Mathf.Clamp01(elapsedSeconds / settings.presentationFadeInSeconds); float fadeOutStart = settings.presentationFadeInSeconds + settings.presentationHoldSeconds; return settings.presentationFadeOutSeconds > 0f && elapsedSeconds > fadeOutStart ? 1f - Mathf.Clamp01((elapsedSeconds - fadeOutStart) / settings.presentationFadeOutSeconds) : 1f; }
    }
}
