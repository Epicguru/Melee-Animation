using ModRequestAPI.Models;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine.Networking;

namespace ModRequestAPI;

public class ModRequestClient
{
    public const string API_URL = "https://hjpwdfmbh9.execute-api.eu-west-2.amazonaws.com/Prod/";

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

    public async Task<bool> TryPostModRequests(IEnumerable<MissingModRequest> requests)
    {
        string url = API_URL + "api/modreporting/report-missing-mods";

        var content = JsonConvert.SerializeObject(requests);

        using var req = new UnityWebRequest(url, "POST");
        ///SetHeaders(req);
        req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(content)) { contentType = "application/json" };
        req.downloadHandler = new DownloadHandlerBuffer();
        await SendRequest(req);

        if (req.isHttpError || req.isNetworkError)
            throw new Exception($"[Code:{req.responseCode}] {req.downloadHandler.text}");

        return true;
    }
}