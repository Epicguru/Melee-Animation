using System.Diagnostics;
using RimWorld;
using UnityEngine;
using Verse;

namespace AAM
{
    // SHUT UP TYNAN!11!1!!
    [StaticConstructorOnStartup]
    public static class SweepPathRenderer
    {
        public static Camera TrailCamera;
        public static RenderTexture TrailRT;
        public static double LastRenderTime;
        public static bool NeedsDraw;

        private static Stopwatch sw = new Stopwatch();
        private static readonly MaterialPropertyBlock mpb = new MaterialPropertyBlock();
        private static readonly Material mat = MaterialPool.MatFrom(GenDraw.LineTexPath, ShaderDatabase.Transparent, Color.white);

        public static void Init()
        {
            mpb.SetTexture("_MainTex", mat.mainTexture);

            var go = new GameObject("Animation Mod Trail Camera");
            go.hideFlags = HideFlags.HideInHierarchy;
            Object.DontDestroyOnLoad(go);

            EnsureRT();

            TrailCamera = go.AddComponent<Camera>();
            TrailCamera.clearFlags = CameraClearFlags.Color;
            TrailCamera.backgroundColor = default;
            TrailCamera.orthographic = true;
            TrailCamera.targetTexture = TrailRT;
            TrailCamera.enabled = false;
            TrailCamera.cullingMask = 1 << 23; // What layers does Rimworld use? I have no idea! I hope 23 is free...
        }

        private static void GetTargetResolution(out int width, out int height)
        {
            width = Mathf.Max(1, Mathf.RoundToInt(Screen.width * Core.Settings.TrailRenderResolution));
            height = Mathf.Max(1, Mathf.RoundToInt(Screen.height * Core.Settings.TrailRenderResolution));
        }

        private static void EnsureRT()
        {
            GetTargetResolution(out int w, out int h);

            if (TrailRT == null || TrailRT.width != w || TrailRT.height != h)
            {
                if (TrailRT == null)
                {
                    TrailRT = new RenderTexture(w, h, 0);
                    TrailRT.autoGenerateMips = false;
                }

                TrailRT.width = w;
                TrailRT.height = h;

                Content.BlitMaterial.SetTexture("_MainTex", TrailRT);
                Core.Log($"Created trail render texture: {w}x{h} pixels.");
            }
        }

        public static void Update()
        {
            if (!NeedsDraw)
                return;
            NeedsDraw = false;

            if (Current.ProgramState != ProgramState.Playing)
                return;

            var map = Find.CurrentMap;
            if (map == null)
                return;

            var cam = Find.Camera;
            if (cam == null)
                return;

            TrailCamera.transform.position = cam.transform.position;
            TrailCamera.transform.rotation = cam.transform.rotation;
            TrailCamera.orthographicSize = cam.orthographicSize;
            TrailCamera.backgroundColor = default;
            unchecked
            {
                TrailCamera.cullingMask = 1 << 23; // What layers does Rimworld use? I have no idea! I hope 23 is free...
            }

            sw.Restart();

            TrailCamera.Render();

            Vector3 pos = cam.transform.position;
            pos.y = AltitudeLayer.VisEffects.AltitudeFor();

            float height = cam.orthographicSize * 2;
            float width = height * ((float)Screen.width / Screen.height);

            Graphics.DrawMesh(MeshPool.plane10, Matrix4x4.TRS(pos, Quaternion.identity, new Vector3(width, 1f, height)), Content.BlitMaterial, 0, null, 0);

            sw.Stop();
            LastRenderTime = sw.Elapsed.TotalSeconds;
        }

        public static void DrawLineBetween(in Vector3 A, in Vector3 B, float len, in Color color, float yOff, float width = 0.2f, int layer = 0)
        {
            //mpb.SetColor("_Color", color);
            mpb.SetFloat("_Alpha", color.r);

            Vector3 mid = (A + B) * 0.5f;
            mid.y += yOff;
            var rot = Quaternion.Euler(0, (B - A).ToAngleFlat(), 0);
            var trs = Matrix4x4.TRS(mid, rot, new Vector3(len, 1, width));
            Graphics.DrawMesh(MeshPool.plane10, trs, Content.TrailMaterial, layer, null, 0, mpb);
        }
    }
}
