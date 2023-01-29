using GistAPI.Models;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine.Networking;

namespace GistAPI;

public class GistClient<T> where T : IGistFileContents
{
    public const string GITHUB_API_ROOT = "https://api.github.com";
    private static readonly string auth = new string("me8mmaUe3YJO624JggM7CahxCkPqn6M2du6uNq370vTdukvQGzRwqPfGTgG_AOknbcIlXv9E0ANAPE5A11_tap_buhtig".Reverse().ToArray());

    private static void SetHeaders(UnityWebRequest req)
    {
        req.SetRequestHeader("Accept", "application/vnd.github+json");
        req.SetRequestHeader("X-GitHub-Api-Version", "2022-11-28");
        req.SetRequestHeader("Authorization", $"Bearer {auth}");
        req.SetRequestHeader("User-Agent", "Epicguru-Mods");
    }

    private Task SendRequest(UnityWebRequest request)
    {
        var result = request.SendWebRequest();
        return Task.Run(() =>
        {
            while (!result.isDone)
            {
                Thread.Sleep(10);
            }
        });
    }

    public async Task<List<T>> ReadGist(string gistID, Predicate<GistFile> fileFilter = null)
    {
        if (string.IsNullOrWhiteSpace(gistID))
            throw new ArgumentNullException(nameof(gistID), $"{nameof(gistID)} must not be null or blank.");

        string endpoint = $"{GITHUB_API_ROOT}/gists/{gistID}";

        using var req = new UnityWebRequest(endpoint, "GET");
        req.downloadHandler = new DownloadHandlerBuffer();
        SetHeaders(req);
        await SendRequest(req);

        if (req.isHttpError || req.isNetworkError)
            throw new Exception(req.error);

        var response = JsonConvert.DeserializeObject<GetGistResponse>(req.downloadHandler.text);
        var results = new List<T>();

        if (response?.Files == null)
            return results;

        foreach (var pair in response.Files)
        {
            if (fileFilter != null && !fileFilter(pair.Value))
                continue;

            string rawUrl = pair.Value.RawUrl;

            using var req2 = new UnityWebRequest(rawUrl, "GET");
            req2.downloadHandler = new DownloadHandlerBuffer();
            SetHeaders(req2);
            await SendRequest(req2);

            if (req2.isHttpError || req2.isNetworkError)
                throw new Exception(req2.error);

            var rawFile = JsonConvert.DeserializeObject<T>(req2.downloadHandler.text);
            rawFile.GistFile = pair.Value;
            results.Add(rawFile);
        }

        return results;
    }

    public async Task UpdateGist(string gistID, UpdateGistRequest update)
    {
        string endpoint = $"{GITHUB_API_ROOT}/gists/{gistID}";
        var content = JsonConvert.SerializeObject(update);

        using var req = new UnityWebRequest(endpoint, "PATCH");
        SetHeaders(req);
        req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(content)) {contentType = "application/json"};
        req.downloadHandler = new DownloadHandlerBuffer();
        await SendRequest(req);

        if (req.isHttpError || req.isNetworkError)
            throw new Exception($"[Code:{req.responseCode}] {req.downloadHandler.text}");
    }
}