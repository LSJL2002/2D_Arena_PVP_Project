using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Goes on the CreateRoomScreen object. Starts a Fusion game as Host,
/// which makes the room visible to everyone in the session lobby.
/// </summary>
public class CreateRoomScreen : MonoBehaviour
{
    [SerializeField] private LobbyScreenPresenter presenter;
    [SerializeField] private TMP_InputField roomNameInput;
    [SerializeField] private TMP_InputField passwordInput;
    [SerializeField] private Button createButton;
    [Tooltip("Optional: shows errors.")]
    [SerializeField] private TMP_Text statusText;

    private void Awake()
    {
        createButton.onClick.AddListener(OnCreateClicked);
    }

    private async void OnCreateClicked()
    {
        string roomName = roomNameInput.text;
        if (string.IsNullOrWhiteSpace(roomName))
        {
            SetStatus("Enter a room name.");
            return;
        }

        createButton.interactable = false;
        SetStatus("Creating room...");

        bool ok = await NetworkManager.Instance.CreateRoom(roomName, passwordInput != null ? passwordInput.text : "");

        createButton.interactable = true;
        if (ok)
        {
            SetStatus(string.Empty);
            presenter.NavigateTo(LobbyScreenType.WaitingRoomScreen);
        }
        else
        {
            SetStatus("Could not create room (name may already be taken).");
        }
    }

    private void SetStatus(string message)
    {
        if (statusText != null)
        {
            statusText.text = message;
        }
    }
}
