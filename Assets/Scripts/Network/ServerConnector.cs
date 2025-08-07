// ServerConnector.cs
using System;
using System.IO;
using System.Collections.Generic;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json;

public class ServerConnector : MonoBehaviour
{
    public Uri serverUri = new("ws://209.38.196.95:8000/ws");
    private ClientWebSocket socket;

    public event Action<InitPayload> OnInit;
    public event Action<MineResult> OnMineResult;

    public InitPayload LastInit { get; private set; }

    public string testDataPath = "test_data.json"; // Pfad für Offline-Daten

    async void Start()
    {
        socket = new ClientWebSocket();
        bool connected = false;

        try
        {
            await socket.ConnectAsync(serverUri, CancellationToken.None);
            connected = socket.State == WebSocketState.Open;
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"Keine Verbindung zum Server möglich: {ex.Message}");
        }

        if (connected)
        {
            _ = ReceiveLoop();              // weiter mit Live-Updates
        }
        else
        {
            LoadOfflineData(testDataPath); // Fallback: lokale Datei einlesen
        }
    }

    async Task ReceiveLoop()
    {
        var buffer = new byte[8192];

        while (socket.State == WebSocketState.Open)
        {
            var result = await socket.ReceiveAsync(buffer, CancellationToken.None);

            if (result.Count > 0)
            {
                var json = System.Text.Encoding.UTF8.GetString(buffer, 0, result.Count);
                // Daten für späteres Offline-Debugging anhängen
                System.IO.File.AppendAllText(testDataPath, json + Environment.NewLine);
                HandleMessage(json);
            }
        }
    }

    void HandleMessage(string json)
    {
        var root = JObject.Parse(json);
        string method = root["method"]?.Value<string>();

        switch (method)
        {
            case "init":
                {
                    var payload = root["args"].ToObject<InitPayload>();
                    LastInit = payload;     // merken, damit RegisterInitListener sofort liefern kann
                    OnInit?.Invoke(payload);
                    break;
                }

            case "mine_result":
                {
                    var mine = root["args"].ToObject<MineResult>();
                    OnMineResult?.Invoke(mine);
                    break;
                }

                // weitere Methoden …
        }
    }

    /// <summary>
    /// Liest Zeile für Zeile aus einer JSONL-Datei und simuliert Server-Nachrichten.
    /// </summary>
    void LoadOfflineData(string path)
    {
        if (!System.IO.File.Exists(path))
        {
            Debug.LogError($"Offline-Datei '{path}' nicht gefunden.");
            return;
        }

        //foreach (var line in System.IO.File.ReadLines(path))
        //{
        //    if (string.IsNullOrWhiteSpace(line)) continue;
        //    HandleMessage(line);
        //}

        var sr = new StreamReader(path);
        var json = sr.ReadToEnd();
        sr.Close();
        HandleMessage(json);
        

        Debug.Log($"Offline-Daten aus '{path}' geladen.");
    }

    public async void Send<T>(string method, T args)
    {
        if (socket == null || socket.State != WebSocketState.Open)
        {
            Debug.LogWarning("Socket ist nicht verbunden – Nachricht wird verworfen.");
            return;
        }

        var wrapper = new MessageWrapper(method, JsonUtility.ToJson(args));
        var bytes = System.Text.Encoding.UTF8.GetBytes(JsonUtility.ToJson(wrapper));
        await socket.SendAsync(bytes, WebSocketMessageType.Text, true, CancellationToken.None);
    }

    public void RegisterInitListener(Action<InitPayload> cb)
    {
        if (LastInit != null)   // Init kam schon? → sofort liefern
            cb(LastInit);

        OnInit += cb;           // danach normal abonnieren
    }
}
