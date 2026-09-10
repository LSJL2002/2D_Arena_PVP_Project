using System.Collections.Generic;
using UnityEngine;

public class LobbyScreenView : MonoBehaviour
{
    [SerializeField] private GameObject mainScreen;
    [SerializeField] private GameObject createRoomScreen;
    [SerializeField] private GameObject joinRoomScreen;
    [SerializeField] private GameObject lobbyRoomScreen;

    private Dictionary<string, GameObject> screens = new();

    private void Awake()
    {
        screens.Add("MainScreen", mainScreen);
        screens.Add("CreateRoomScreen", createRoomScreen);
        screens.Add("JoinRoomScreen", joinRoomScreen);
        screens.Add("LobbyRoomScreen", lobbyRoomScreen);
        DeactivateAllScreens();
    }

    public void ShowScreen(string screenName)
    {
        foreach (var screen in screens.Values)
        {
            screen.SetActive(false);
        }
        if (screens.TryGetValue(screenName, out GameObject screenToShow))
        {
            screenToShow.SetActive(true);
        }
        else
        {
            Debug.LogWarning($"Screen '{screenName}' not found.");
        }
    }

    private void DeactivateAllScreens()
    {
        foreach (var screen in screens.Values)
        {
            screen.SetActive(false);
        }
    }
}
