using RimWorld;
using UnityEngine;
using UnityEngine.Video;
using Verse;

namespace AAM.Video;

[IgnoreHotSwap]
public static class VideoPlayerUtil
{
    private static readonly GameObject go;
    private static readonly VideoPlayer player;
    private static BundleManager.BundleHandle currentBundleHandle;

    static VideoPlayerUtil()
    {
        go = new GameObject("Video player GO")
        {
            hideFlags = HideFlags.HideInHierarchy | HideFlags.DontSave
        };
        Object.DontDestroyOnLoad(go);

        player = go.AddComponent<VideoPlayer>(); 
        player.playOnAwake = false;
    }

    /// <summary>
    /// Get the texture for a video at the url.
    /// If the video is not ready or found, it will return null.
    /// </summary>
    public static Texture GetVideoTexture(string vidName, out BundleManager.LoadedState state)
    {
        state = BundleManager.LoadedState.Unloaded;
        if (vidName == null)
            return null;

        if (currentBundleHandle != null && currentBundleHandle.BundleName == vidName)
        {
            // Check if video has finished loading:
            if (player.clip == null && currentBundleHandle.LoadedState == BundleManager.LoadedState.Loaded)
            {
                // Apply loaded video.
                var loaded = currentBundleHandle.GetFirstAsset<VideoClip>();
                if (loaded == null)
                    return player.texture;

                player.time = 0;
                player.isLooping = true;
                player.renderMode = VideoRenderMode.APIOnly;
                player.audioOutputMode = VideoAudioOutputMode.None;
                player.clip = loaded;

                if (!player.isPlaying)
                    player.Play();
            }

            state = currentBundleHandle.LoadedState;
            return player.texture;
        }

        // Stop old:
        player.Stop();
        player.clip = null;
        currentBundleHandle?.Unload();

        // Load new:
        currentBundleHandle = BundleManager.GetHandle(vidName);
        currentBundleHandle.Load();

        return player.texture;
    }

    public static Texture GetStaticTexture(string textureName, out BundleManager.LoadedState state)
    {
        state = BundleManager.LoadedState.Unloaded;
        if (textureName == null)
            return null;

        if (currentBundleHandle != null && currentBundleHandle.BundleName == textureName)
        {
            state = currentBundleHandle.LoadedState;
            if (currentBundleHandle.LoadedState == BundleManager.LoadedState.Loaded)
                return currentBundleHandle.GetFirstAsset<Texture2D>();
            return null;
        }

        currentBundleHandle?.Unload();
        currentBundleHandle = BundleManager.GetHandle(textureName);
        currentBundleHandle.Load();
        return null;
    }
}
