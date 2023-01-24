using UnityEngine;

namespace Assets.Materials
{
    [ExecuteAlways]
    public class TrailDrawer : MonoBehaviour
    {
        public MeshRenderer Renderer;
        [Range(0f, 1f)]
        public float Alpha = 0.5f;

        private void LateUpdate()
        {
            Renderer.material.SetFloat("_Alpha", Alpha);
        }
    }
}
