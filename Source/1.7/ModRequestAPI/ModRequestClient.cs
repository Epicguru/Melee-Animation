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
        req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(content)) { contentType = "application/json" };
        req.downloadHandler = new DownloadHandlerBuffer();
        await SendRequest(req);

        if (req.result != UnityWebRequest.Result.Success)
            throw new Exception($"[Code:{req.responseCode}, result: {req.result}] {req.downloadHandler.text}");

        return true;
    }
}