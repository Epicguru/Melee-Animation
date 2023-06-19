using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Assets.Editor
{
    [ExecuteAlways]
    [ExecuteInEditMode]
    public class AnimationDataCreator : MonoBehaviour
    {
        public static AnimationDataCreator Instance;

        public readonly List<SweepPointCollection> Sweeps = new List<SweepPointCollection>();
        public SweepAnchor[] SweepAnchors => GetComponentsInChildren<SweepAnchor>(true);
        public Rect MaxBounds { get; set; }

        public AnimationClip Clip;
        public SweepParameters SweepParams;
        public bool AllowLoadingFromJson;

        [Header("Visualizing")]
        public DisplayMode SweepDisplayMode = DisplayMode.Ghost;
        public float SweepGhostTime = 1f;
        public List<OverridePair> OverridePairs = new List<OverridePair>();
        public bool InspectAllCurves;
        public List<Vector3> Points = new List<Vector3>();

        private Material material;
        private List<DrawItem> toDraw = new List<DrawItem>();
        private List<Texture2D> textures = new List<Texture2D>();
        private List<string> texNames = new List<string>();
        private Dictionary<string, Texture2D> texMap;
        private Dictionary<string, AudioClip> audioMap = new Dictionary<string, AudioClip>();
        private Rect bounds;

        public struct DrawItem
        {
            public Matrix4x4 Matrix;
            public Color Color;
            public Texture2D Texture;
            public Mesh Mesh;

            private float depth;
            private bool hasDepth;

            public float GetDepth()
            {
                if (hasDepth)
                    return depth;

                hasDepth = true;
                depth = Matrix.MultiplyPoint3x4(Vector3.zero).y;
                return depth;
            }

            public IEnumerable<Vector3> GetCorners()
            {
                // Assumes 1x1 mesh.

                yield return Matrix.MultiplyPoint3x4(new Vector3( 0.5f, 0f,  0.5f));
                yield return Matrix.MultiplyPoint3x4(new Vector3(-0.5f, 0f,  0.5f));
                yield return Matrix.MultiplyPoint3x4(new Vector3( 0.5f, 0f, -0.5f));
                yield return Matrix.MultiplyPoint3x4(new Vector3(-0.5f, 0f, -0.5f));
            }
        }

        [Serializable]
        public struct OverridePair
        {
            public string PartName;
            public Texture2D Texture;
            public bool PreventDraw;
        }

        public enum DisplayMode
        {
            None,
            All,
            Previous,
            Ghost
        }

        public void AnimEvent(EventBase e)
        {
            switch (e)
            {
                case AudioEvent audio:

                    var clip = ResolveClip(audio.AudioPath);
                    if (clip == null)
                    {
                        Debug.LogError($"Failed to resolve {audio.AudioPath}");
                        break;
                    }

                    var go = new GameObject($"Audio: {audio.AudioPath}");
                    var src = go.AddComponent<AudioSource>();

                    src.spatialBlend = 0;
                    src.pitch = audio.PitchFactor;
                    src.panStereo = audio.LocalPosition.x * 0.25f;
                    src.loop = false;
                    src.clip = clip;
                    src.volume = audio.VolumeFactor;
                    src.Play();

                    Destroy(go, clip.length + 1);
                    break;
            }
        }

        public void ClearTextureCache()
        {
            textures.Clear();
            texNames.Clear();
            texMap?.Clear();
            audioMap.Clear();
        }

        public Texture2D ResolveTexture(string partName, string path, out bool preventDraw)
        {
            preventDraw = false;
            foreach(var value in OverridePairs)
            {
                if (value.PartName == partName)
                {
                    if (value.PreventDraw)
                        preventDraw = true;

                    if(value.Texture != null)
                        return value.Texture;
                }
            }

            if (path == null)
                return null;

            if(texMap == null)
            {
                texMap = new Dictionary<string, Texture2D>(texNames.Count);
                for(int i = 0; i < texNames.Count; i++)
                {
                    texMap.Add(texNames[i], textures[i]);
                }
            }

            if (texMap.TryGetValue(path, out var found))
                return found;

            var loaded = Resources.Load<Texture2D>(path);
            Debug.Assert(loaded != null, $"Failed to load '{path}'");

            texMap.Add(path, loaded);
            texNames.Add(path);
            textures.Add(loaded);

            return loaded;
        }

        public AudioClip ResolveClip(string clipPath)
        {
            if (audioMap.TryGetValue(clipPath, out var found))
                return found;

            found = Resources.Load<AudioClip>(clipPath);
            if (found != null)
                audioMap.Add(clipPath, found);

            return found;
        }

        public bool MirrorHorizontal, MirrorVertical;

        private bool finishedDraw;

        private void OnRenderObject()
        {
            Draw();
        }

        private void Update()
        {
            Instance = this;
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.magenta;
            var center = new Vector3(bounds.center.x, 0f, bounds.center.y);
            var size = new Vector3(bounds.width, 0.1f, bounds.height);
            Gizmos.DrawWireCube(center, size);

            Gizmos.color = Color.yellow;
            center = new Vector3(MaxBounds.center.x, 0f, MaxBounds.center.y);
            size = new Vector3(MaxBounds.width, 0.1f, MaxBounds.height);
            Gizmos.DrawWireCube(center, size);

            foreach (var p in Points)
            {
                Gizmos.DrawSphere(p, 0.01f);
            }
        }

        public void Draw()
        {
            if (material == null)
                material = Resources.Load<Material>("SpriteMat");

            if (material == null)
                return;            

            toDraw.Sort((a, b) => a.GetDepth().CompareTo(b.GetDepth()));
            var mat = material;

            bounds = default;

            foreach(var item in toDraw)
            {
                if(item.Mesh == null)
                {
                    Debug.LogError("Null mesh!");
                    continue;
                }
                if (item.Texture == null)
                {
                    Debug.LogError("Null texture!");
                    continue;
                }

                foreach (var p in item.GetCorners())
                {
                    if (bounds.x > p.x)
                    {
                        bounds.width += bounds.x - p.x;
                        bounds.x = p.x;
                    }
                    if (bounds.xMax < p.x)
                    {
                        bounds.width = p.x - bounds.x;
                    }
                    if (bounds.y > p.z)
                    {
                        bounds.height += bounds.y - p.z;
                        bounds.y = p.z;
                    }
                    if (bounds.yMax < p.z)
                    {
                        bounds.height = p.z - bounds.y;
                    }
                }

                mat.SetColor("_Color", item.Color);
                mat.SetTexture("_MainTex", item.Texture);
                mat.SetPass(0);

                Graphics.DrawMeshNow(item.Mesh, item.Matrix);
            }
            finishedDraw = true;
        } 

        public void PushToDraw(in DrawItem toDraw)
        {
            if (finishedDraw)
                this.toDraw.Clear();
            finishedDraw = false;
            this.toDraw.Add(toDraw);
        } 
    }
}
