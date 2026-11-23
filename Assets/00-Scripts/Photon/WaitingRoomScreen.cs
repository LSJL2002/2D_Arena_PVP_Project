using Fusion;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class WaitingRoomScreen : MonoBehaviour
{
    [SerializeField] private TMP_Text roomNameText;
    [SerializeField] private TMP_Text firstPlayerName;
    [SerializeField] private TMP_Text secondPlayerName;
    [SerializeField] private Button startGameButton;
    [SerializeField] private Button closeButton;
    [SerializeField] private LobbyScreenPresenter presenter;
    private bool isLeaving;

    private void Awake()
    {
        startGameButton.onClick.AddListener(OnStartGameClicked);
    }
    private void OnEnable()
    {
        isLeaving = false;
        closeButton.interactable = true; // it was turned off the last time we left

        if (NetworkManager.Instance == null) return;

        var runner = NetworkManager.Instance.Runner;
        roomNameText.text = runner.IsRunning ? runner.SessionInfo.Name : "";

        NetworkManager.Instance.RunnerShutdown += OnRunnerShutdown;
    }

    private void OnDisable()
    {
        if (NetworkManager.Instance != null)
        {
            NetworkManager.Instance.RunnerShutdown -= OnRunnerShutdown;
        }
    }

    private void OnRunnerShutdown(ShutdownReason reason)
    {
        if (isLeaving) return;

        Debug.Log($"Room closed by host ({reason}), returning to previous screen");
        presenter.NavigateBack();
    }
    private void Update()
    {
        firstPlayerName.text = "Waiting...";
        secondPlayerName.text = "Waiting...";

        foreach (PlayerData p in PlayerData.All)
        {
            string n = p.Name.ToString();
            if (p.Slot == 1)
            {
                firstPlayerName.text = n;
            }
            if(p.Slot == 2)
            {
                secondPlayerName.text = n;
            }
        }
    }
    private void OnStartGameClicked()
    {
        Debug.Log("Start Game button clicked.");
    }
    public async void OnCloseClicked()
    {
        if (isLeaving) return;
        isLeaving = true;
        closeButton.interactable = false;      // stop double clicks
        await NetworkManager.Instance.Leave(); // removes the room (or leaves it, for the joiner)
        presenter.NavigateBack();
    }
}
