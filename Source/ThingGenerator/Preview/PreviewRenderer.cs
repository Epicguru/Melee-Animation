using System;
using UnityEngine;
using Object = UnityEngine.Object;

namespace AAM.Preview;

public class PreviewRenderer : IDisposable
{
    public static readonly Vector3 CameraPosition = new Vector3(1000, 0, 1000);

    public RenderTexture Texture { get; private set; }

    private Camera cam;

    public PreviewRenderer(int w, int h)
    {
        UpdateTexture(w, h);

        var go = new GameObject("Preview Camera");
        Object.DontDestroyOnLoad(go);
        go.transform.position = CameraPosition;
        // Rimworld's coordinate system requires this rotation.
        go.transform.rotation = Quaternion.LookRotation(Vector3.up, Vector3.forward);

        cam = go.AddComponent<Camera>();
        cam.targetTexture = Texture;
    }

    public void UpdateTexture(int w, int h)
    {
        if (Texture != null)
        {
            Object.Destroy(Texture);
            cam.targetTexture = null;
        }

        if (w > 0 && h > 0)
        {
            Texture = new RenderTexture(w, h, 1, RenderTextureFormat.Default, 0);
            cam.targetTexture = Texture;
        }
    }

    public void Dispose()
    {
        if (cam == null)
            return;

        // Clear RT
        UpdateTexture(0, 0);

        // Destroy camera.
        Object.Destroy(cam.gameObject);
        cam = null;
    }
}
