using UnityEngine;
using Fusion;

public class NetworkManager : MonoBehaviour
{
    public static NetworkManager Instance { get; private set; }
    public NetworkRunner Runner { get; private set; }

    // Gives one NetworkRunner instance
    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        Runner = gameObject.AddComponent<NetworkRunner>();
    }

    public async void JoinLobby()
    {
        var result = await Runner.JoinSessionLobby(
            SessionLobby.ClientServer
            );

        if (result.Ok)
        {
            Debug.Log("Joined Lobby");
        }
        else
        {
            Debug.LogError(
                "Failed to join lobby:" + result.ShutdownReason);
        }
    }
}
