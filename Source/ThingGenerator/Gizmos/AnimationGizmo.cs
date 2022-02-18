using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace AAM.Gizmos
{
    public class AnimationGizmo : Gizmo
    {
        public override GizmoResult GizmoOnGUI(Vector2 topLeft, float maxWidth, GizmoRenderParms parms)
        {
            var butRect = new Rect(topLeft.x, topLeft.y, this.GetWidth(maxWidth), 75f);

            // Mouseover and highlight.
            MouseoverSounds.DoRegion(butRect, SoundDefOf.Mouseover_Command);
            if (parms.highLight)
                QuickSearchWidget.DrawStrongHighlight(butRect.ExpandedBy(12f));

            bool clicked = false;
            bool mouseOver = Mouse.IsOver(butRect);

            if (Widgets.ButtonInvisible(butRect, true))
            {
                clicked = true;
            }

            Widgets.DrawBox(butRect);

            return new GizmoResult(clicked ? GizmoState.Interacted : mouseOver ? GizmoState.Mouseover : GizmoState.Clear);
        }

        public override float GetWidth(float maxWidth)
        {
            return Mathf.Max(240, maxWidth);
        }
    }
}
