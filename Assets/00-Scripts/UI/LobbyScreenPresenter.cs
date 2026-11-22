using UnityEngine;

public enum LobbyScreenType
{
    MainScreen,
    CreateRoomScreen,
    JoinRoomScreen,
    SettingsScreen,
    LobbyRoomScreen,
    WaitingRoomScreen,
}

public class LobbyScreenPresenter : MonoBehaviour
{
    [SerializeField] private LobbyScreenView view;
    private LobbyScreenModel model;
    private void Awake()
    {
        model = new LobbyScreenModel();
    }

    private void Start()
    {
        // Show the initial screen
        NavigateTo("MainScreen");
    }

    public void NavigateTo(string screenName, object data = null)
    {
        model.NavigateTo(screenName, data);

        UpdateView();
    }
    public void NavigateTo(LobbyScreenType screen)
    {
        string screenName = screen.ToString();
        NavigateTo(screenName, null);
    }
    public void NavigateBack()
    {
        if (!model.canGoBack)
            return;

        model.NavigateBack();

        UpdateView();
    }

    private void UpdateView()
    {
        NavigationData current = model.CurrentScreen;
        //Debug.LogWarning($"Presenter got {current.ScreenName} with data: {current.Data}");

        view.ShowScreen(current.ScreenName);

        // Later:
        // view.UpdateScreen(current.ScreenName, current.Data);
    }
}
