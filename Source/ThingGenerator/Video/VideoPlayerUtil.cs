using RimWorld;
using UnityEngine;
using UnityEngine.Video;
using Verse;

namespace AAM.Video;

public static class VideoPlayerUtil
{
    private static readonly GameObject go;
    private static readonly VideoPlayer player;

    static VideoPlayerUtil()
    {
        go = new GameObject("Video player GO")
        {
            hideFlags = HideFlags.HideInHierarchy | HideFlags.DontSave
        };
        Object.DontDestroyOnLoad(go);

        player = go.AddComponent<VideoPlayer>();
        player.playOnAwake = false;

        player.errorReceived += OnError;
        player.started += OnStarted;
        player.prepareCompleted += Player_prepareCompleted;
    }

    private static void Player_prepareCompleted(VideoPlayer source)
    {
        Messages.Message("Video prepared", MessageTypeDefOf.SilentInput, false);
    }

    private static void OnStarted(VideoPlayer source)
    {
        Messages.Message("Started playback", MessageTypeDefOf.SilentInput, false);

    }

    private static void OnError(VideoPlayer source, string message)
    {
        Messages.Message($"Video player error: {message}", MessageTypeDefOf.SilentInput, false);

    }

    /// <summary>
    /// Get the texture for a video at the url.
    /// If the video is not ready or found, it will return null.
    /// </summary>
    public static Texture GetVideoTexture(string url)
    {
        player.url = url;
        player.isLooping = true;
        player.renderMode = VideoRenderMode.APIOnly;
        player.audioOutputMode = VideoAudioOutputMode.None;
        player.source = VideoSource.Url;

        //string msg = $"Prepared: {player.isPrepared}, playing: {player.isPlaying}, src: {player.source}";
        //Messages.Message(msg, MessageTypeDefOf.SilentInput, false);

        //if (!player.isPrepared)
        //{
        //    player.Prepare();
        //    return null;
        //}

        if (Input.GetKeyDown(KeyCode.Space))
        {
            Messages.Message("Attempt start play...", MessageTypeDefOf.SilentInput, false);
            player.Play();
        }

        return player.texture;
    }
}
