using System.Collections;
using TMPro;
using Unity.VisualScripting;
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


    private void Awake()
    {
        startGameButton.onClick.AddListener(OnStartGameClicked);
    }
    private void OnEnable()
    {
        if (NetworkManager.Instance == null) return;

        var runner = NetworkManager.Instance.Runner;
        roomNameText.text = runner.IsRunning ? runner.SessionInfo.Name : "";
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
        closeButton.interactable = false;      // stop double clicks
        await NetworkManager.Instance.Leave(); // removes the room (or leaves it, for the joiner)
        presenter.NavigateBack();
    }
}
