using UnityEngine;
using Fusion;
using System.Collections.Generic;
public class CreateRoom : MonoBehaviour
{
    [SerializeField] private NetworkManager networkManager;

    public async void CreateNewRoom(string roomName, string password)
    {
        NetworkRunner runner = networkManager.Runner;

        var sessionProperties = new Dictionary<string, SessionProperty>
        {
            {"Password", password }
        };

        StartGameResult result = await runner.StartGame(new StartGameArgs
        {
            GameMode = GameMode.Host,
            SessionName = roomName,
            PlayerCount = 2,
            IsVisible = true,
            IsOpen = true,
            SessionProperties = sessionProperties
        });

        if (result.Ok)
        {
            Debug.Log("Romm created" + roomName);
        }
        else
        {
            Debug.LogError(
                "failed to create room:" + result.ShutdownReason
                );
        }
    }
}
