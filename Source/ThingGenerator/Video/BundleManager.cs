using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

namespace AAM.Video;

public static class BundleManager
{
    public static int LoadedBundleCount { get; private set; }
    private static readonly Dictionary<string, BundleHandle> handles = new Dictionary<string, BundleHandle>();

    public static BundleHandle GetHandle(string bundleName)
    {
        // Get existing:
        if (handles.TryGetValue(bundleName, out var found))
            return found;

        // Make new:
        var handle = new BundleHandle()
        {
            BundleName = bundleName,
            LoadedState = LoadedState.Unloaded
        };

        handles.Add(bundleName, handle);
        return handle;
    }

    public class BundleHandle
    {
        public string BundleName { get; internal set; }
        public AssetBundle Bundle { get; internal set; }
        public LoadedState LoadedState { get; internal set; }

        private Task<AssetBundle> loadTask;

        public void Load()
        {
            if (LoadedState != LoadedState.Unloaded)
            {
                Core.Error($"Cannot start loading when state is {LoadedState}");
                return;
            }

            LoadedState = LoadedState.Loading;
            if (loadTask != null)
                throw new Exception("Bad state: task is not null when it should be.");

            loadTask = Task.Run(() => LoadBundleAsync(BundleName));

            // When finished:
            loadTask.ContinueWith(t =>
            {
                loadTask = null;
                if (t.IsFaulted)
                {
                    Bundle = null;
                    Core.Error($"Failed to download bundle '{BundleName}':", t.Exception);
                    LoadedState = LoadedState.Unloaded;
                    return;
                }

                LoadedBundleCount++;
                Bundle = t.Result;

                var oldState = LoadedState;
                LoadedState = LoadedState.Loaded;

                // Was unloading requested while loading?
                if (oldState == LoadedState.Unloading)
                {
                    Unload();
                }
            });
        }

        public void Unload()
        {
            if (LoadedState is LoadedState.Unloaded or LoadedState.Unloading)
            {
                Core.Error($"Cannot unload when state is {LoadedState}");
                return;
            }

            if (LoadedState == LoadedState.Loaded)
            {
                // Unload immediately.
                LoadedState = LoadedState.Unloaded;
                Bundle.Unload(true);
                Bundle = null;
                LoadedBundleCount--;
            }
            else
            {
                // State is loading, so just change the state and the loading task will automatically unload once done.
                LoadedState = LoadedState.Unloading;
            }
        }

        public T GetFirstAsset<T>() where T : UnityEngine.Object
        {
            if (Bundle == null)
                return null;

            return Bundle.LoadAsset<T>(Bundle.GetAllAssetNames()[0]);
        }

        private static async Task<AssetBundle> LoadBundleAsync(string bundleName)
        {
            var timer = Stopwatch.StartNew();

            string path = Path.Combine(Core.ModContent.RootDir, "Bundles", Content.GetPlatformName(), bundleName);
            var req = AssetBundle.LoadFromFileAsync(path);
            while (!req.isDone)
            {
                await Task.Delay(15);
            }

            if (req.assetBundle == null)
                throw new Exception($"Bundle {bundleName} failed to load from {path}");

            timer.Stop();
            Core.Log($"Downloaded {bundleName} in {timer.ElapsedMilliseconds}ms");
            return req.assetBundle;
        }
    }

    public enum LoadedState
    {
        Unloaded,
        Loading,
        Loaded,
        Unloading
    }
}


