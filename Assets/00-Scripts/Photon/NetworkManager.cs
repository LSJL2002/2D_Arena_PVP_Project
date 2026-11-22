using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Fusion;
using Fusion.Sockets;
using UnityEngine;

/// <summary>
/// Owns the NetworkRunner and wraps the Fusion calls the lobby needs:
///  - JoinLobby():  connect to the Photon session lobby so we receive the room list
///  - CreateRoom(): start a game as Host (this is what makes the room appear for others)
///  - JoinRoom():   join an existing room as Client
///
/// How the room list works in Fusion:
///  A room only shows up for a player who is *inside the session lobby*
///  (runner.JoinSessionLobby). While there, Photon calls OnSessionListUpdated
///  every time a room is created, changes, or closes. We re-broadcast that
///  through the SessionListUpdated event so any UI can listen.
/// </summary>
public class NetworkManager : MonoBehaviour, INetworkRunnerCallbacks
{
    public const string HasPasswordKey = "HasPassword";

    public static NetworkManager Instance { get; private set; }

    public NetworkRunner Runner { get; private set; }

    /// <summary>Latest room list received from Photon (empty until JoinLobby succeeds).</summary>
    public IReadOnlyList<SessionInfo> Sessions => sessions;

    public bool IsInLobby { get; private set; }

    /// <summary>Fired whenever the list of visible rooms changes.</summary>
    public event Action<IReadOnlyList<SessionInfo>> SessionListUpdated;

    /// <summary>Fired when the runner shuts down (left room, kicked, wrong password, ...).</summary>
    public event Action<ShutdownReason> RunnerShutdown;

    private readonly List<SessionInfo> sessions = new();
    private Task<StartGameResult> lobbyJoinTask;

    // Only used by the host, to check the password of joining players.
    private string hostPassword;
    [SerializeField] private PlayerData playerDataPrefab;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        CreateRunner();
    }

    // A NetworkRunner cannot be reused after Shutdown, so we make a fresh one each time.
    // It lives on its own GameObject because Fusion destroys the runner's GameObject
    // on shutdown, and we don't want that to take this NetworkManager with it.
    private void CreateRunner()
    {
        if (Runner != null)
        {
            Runner.RemoveCallbacks(this);
            Destroy(Runner.gameObject);
        }

        var runnerObject = new GameObject("NetworkRunner");
        DontDestroyOnLoad(runnerObject);
        Runner = runnerObject.AddComponent<NetworkRunner>();
        Runner.ProvideInput = true;
        Runner.AddCallbacks(this);
        IsInLobby = false;
        lobbyJoinTask = null;
    }

    // ------------------------------------------------------------------ Lobby

    /// <summary>
    /// Connects to the session lobby so this player receives the room list.
    /// Safe to call multiple times; only connects once.
    /// </summary>
    public async Task<bool> JoinLobby()
    {
        if (IsInLobby)
        {
            // Already connected: just push the current list to listeners again.
            SessionListUpdated?.Invoke(sessions);
            return true;
        }

        // Still inside a room from before: leave it first (a runner does one thing at a time).
        if (lobbyJoinTask == null && Runner.IsRunning)
        {
            await Leave();
        }

        // Reuse an in-flight request instead of starting a second one.
        lobbyJoinTask ??= Runner.JoinSessionLobby(SessionLobby.ClientServer);

        StartGameResult result = await lobbyJoinTask;
        lobbyJoinTask = null;

        if (result.Ok)
        {
            IsInLobby = true;
            Debug.Log($"Joined session lobby (region: {Runner.LobbyInfo.Region})");
        }
        else
        {
            Debug.LogError($"Failed to join lobby: {result.ShutdownReason} {result.ErrorMessage}");
        }
        return result.Ok;
    }

    // ------------------------------------------------------------------ Rooms

    /// <summary>Creates a room (this player becomes the Host). Password may be empty.</summary>
    public async Task<bool> CreateRoom(string roomName, string password, int maxPlayers = 2)
    {
        if (string.IsNullOrWhiteSpace(roomName))
        {
            Debug.LogWarning("Room name is empty");
            return false;
        }

        hostPassword = password ?? string.Empty;

        // Never put the password itself into SessionProperties:
        // everyone in the lobby can read those. Only advertise that one exists.
        var properties = new Dictionary<string, SessionProperty>
        {
            { HasPasswordKey, !string.IsNullOrEmpty(hostPassword) }
        };

        StartGameResult result = await Runner.StartGame(new StartGameArgs
        {
            GameMode = GameMode.Host,
            SessionName = roomName.Trim(),
            PlayerCount = maxPlayers,
            IsVisible = true,   // must be true to appear in other players' room lists
            IsOpen = true,
            SessionProperties = properties,
            EnableClientSessionCreation = false,
        });

        if (result.Ok)
        {
            IsInLobby = false; // the runner is now in a game, not the lobby
            Debug.Log($"Room created: {roomName} (region: {Runner.SessionInfo.Region})");
        }
        else
        {
            Debug.LogError($"Failed to create room: {result.ShutdownReason} {result.ErrorMessage}");
        }
        return result.Ok;
    }

    /// <summary>Joins an existing room by name (this player becomes a Client).</summary>
    public async Task<bool> JoinRoom(string roomName, string password = "")
    {
        StartGameResult result = await Runner.StartGame(new StartGameArgs
        {
            GameMode = GameMode.Client,
            SessionName = roomName,
            // Sent to the host, which checks it in OnConnectRequest.
            ConnectionToken = Encoding.UTF8.GetBytes(password ?? string.Empty),
            EnableClientSessionCreation = false, // fail instead of creating the room if it vanished
        });

        if (result.Ok)
        {
            IsInLobby = false;
            Debug.Log($"Joined room: {roomName} (region: {Runner.SessionInfo.Region})");
        }
        else
        {
            Debug.LogError($"Failed to join room: {result.ShutdownReason} {result.ErrorMessage}");
        }
        return result.Ok;
    }

    /// <summary>Leaves the current room or lobby and gets a fresh runner ready.</summary>
    public async Task Leave()
    {
        if (Runner != null && Runner.IsRunning)
        {
            await Runner.Shutdown();
        }
    }

    // ------------------------------------------------------- Runner callbacks

    public void OnSessionListUpdated(NetworkRunner runner, List<SessionInfo> sessionList)
    {
        sessions.Clear();
        foreach (SessionInfo session in sessionList)
        {
            if (session.IsValid && session.IsVisible)
            {
                sessions.Add(session);
            }
        }
        Debug.Log($"Room list updated: {sessions.Count} room(s) visible");
        SessionListUpdated?.Invoke(sessions);
    }

    public void OnConnectRequest(NetworkRunner runner, NetworkRunnerCallbackArgs.ConnectRequest request, byte[] token)
    {
        // Runs on the Host when a client tries to join.
        if (string.IsNullOrEmpty(hostPassword))
        {
            request.Accept();
            return;
        }

        string given = token != null ? Encoding.UTF8.GetString(token) : string.Empty;
        if (given == hostPassword)
        {
            request.Accept();
        }
        else
        {
            request.Refuse();
        }
    }

    public void OnShutdown(NetworkRunner runner, ShutdownReason shutdownReason)
    {
        Debug.Log($"Runner shut down: {shutdownReason}");
        hostPassword = null;
        sessions.Clear();
        CreateRunner();
        RunnerShutdown?.Invoke(shutdownReason);
    }

    public void OnPlayerJoined(NetworkRunner runner, PlayerRef player) 
    { 
        if(!runner.IsServer) return; // Only the host should spawn player data

        PlayerData data = runner.Spawn(playerDataPrefab, inputAuthority: player);
        data.Slot = (player == runner.LocalPlayer) ? 1 : 2; // 1 is host and 2 is local player on the host
    }
    public void OnPlayerLeft(NetworkRunner runner, PlayerRef player) { }
    public void OnInput(NetworkRunner runner, NetworkInput input) { }
    public void OnInputMissing(NetworkRunner runner, PlayerRef player, NetworkInput input) { }
    public void OnConnectedToServer(NetworkRunner runner) { }
    public void OnDisconnectedFromServer(NetworkRunner runner, NetDisconnectReason reason) { }
    public void OnConnectFailed(NetworkRunner runner, NetAddress remoteAddress, NetConnectFailedReason reason) { }
    public void OnUserSimulationMessage(NetworkRunner runner, SimulationMessagePtr message) { }
    public void OnCustomAuthenticationResponse(NetworkRunner runner, Dictionary<string, object> data) { }
    public void OnHostMigration(NetworkRunner runner, HostMigrationToken hostMigrationToken) { }
    public void OnReliableDataReceived(NetworkRunner runner, PlayerRef player, ReliableKey key, ArraySegment<byte> data) { }
    public void OnReliableDataProgress(NetworkRunner runner, PlayerRef player, ReliableKey key, float progress) { }
    public void OnSceneLoadDone(NetworkRunner runner) { }
    public void OnSceneLoadStart(NetworkRunner runner) { }
    public void OnObjectExitAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
    public void OnObjectEnterAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
}
