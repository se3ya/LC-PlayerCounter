using System;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using BepInEx;
using BepInEx.Logging;
using UnityEngine;

namespace PlayerCounter
{
  [BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
  public class Plugin : BaseUnityPlugin
  {
    public static Plugin Instance { get; private set; } = null!;
    internal static ManualLogSource Log { get; private set; } = null!;

    private void Awake()
    {
      Instance = this;
      Log = base.Logger;

      Log.LogInfo($"Initializing {MyPluginInfo.PLUGIN_NAME}");

      HeartbeatHost.EnsureExists();

      Log.LogInfo($"{MyPluginInfo.PLUGIN_NAME} is loaded!");
    }

    public static string LoadOrCreatePlayerId()
    {
      const string prefsKey = "MBLCP_PlayerId";
      string id = PlayerPrefs.GetString(prefsKey, string.Empty);

      if (string.IsNullOrEmpty(id))
      {
        id = $"{SystemInfo.deviceUniqueIdentifier}_{UnityEngine.Random.Range(1000, 9999)}";
        PlayerPrefs.SetString(prefsKey, id);
        PlayerPrefs.Save();
        Log.LogDebug("Generated new player ID");
      }

      return id;
    }
  }

  public class HeartbeatHost : MonoBehaviour
  {
    private static HeartbeatHost? _instance;
    private static readonly HttpClient Http = new HttpClient();
    private const string ApiUrl = "https://mblcp-player-tracker-still-snow-8423.fly.dev/heartbeat";
    private const int IntervalMs = 30000;

    private Timer? _timer;
    private string _playerId = string.Empty;
    private int _isSending = 0;

    public static void EnsureExists()
    {
      if (_instance != null) return;

      var go = new GameObject("MBLCP_HeartbeatHost");
      go.hideFlags = HideFlags.HideAndDontSave;
      DontDestroyOnLoad(go);
      _instance = go.AddComponent<HeartbeatHost>();
    }

    private void Awake()
    {
      if (_instance != null && _instance != this)
      {
        Destroy(gameObject);
        return;
      }
      _instance = this;
    }

    private void Start()
    {
      _playerId = Plugin.LoadOrCreatePlayerId();
      Plugin.Log.LogDebug($"Player ID: {_playerId}");

      _timer = new Timer(async _ => await SendHeartbeat(), null, 0, IntervalMs);

      Application.quitting += OnAppQuitting;
    }

    private async Task SendHeartbeat()
    {
      Plugin.Log.LogDebug("Ping sent to server");

      if (Interlocked.Exchange(ref _isSending, 1) == 1)
      {
        Plugin.Log.LogDebug("Heartbeat already in progress, skipping");
        return;
      }

      try
      {
        string jsonData = "{\"modpack\":\"MBLCP\"}";
        using var content = new StringContent(jsonData, Encoding.UTF8, "application/json");
        using var request = new HttpRequestMessage(HttpMethod.Post, ApiUrl) { Content = content };
        request.Headers.Add("X-Player-ID", _playerId);

        using var response = await Http.SendAsync(request);
        if (!response.IsSuccessStatusCode)
        {
          string body = await response.Content.ReadAsStringAsync();
          Plugin.Log.LogDebug($"Server returned {(int)response.StatusCode}: {body}");
          return;
        }

        string result = await response.Content.ReadAsStringAsync();
        Plugin.Log.LogDebug($"Heartbeat successful: {result}");
      }
      catch (HttpRequestException httpEx)
      {
        Plugin.Log.LogDebug($"Network error: {httpEx.Message}");
      }
      catch (TaskCanceledException)
      {
        Plugin.Log.LogDebug("Heartbeat timed out");
      }
      catch (Exception ex)
      {
        Plugin.Log.LogDebug($"Error: {ex.GetType().Name} - {ex.Message}");
      }
      finally
      {
        Interlocked.Exchange(ref _isSending, 0);
      }
    }

    private void OnAppQuitting()
    {
      DisposeTimer();
    }

    private void OnDestroy()
    {
      Plugin.Log.LogInfo("Heartbeat host destroyed, disposing timer");
      DisposeTimer();
      Application.quitting -= OnAppQuitting;
    }

    private void DisposeTimer()
    {
      try { _timer?.Dispose(); } catch { }
      _timer = null;
    }
  }
}
