using System.Collections.Generic;
using Fusion;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Goes on the JoinRoomScreen object. When the screen opens it joins the
/// Photon session lobby, and then keeps the room list in sync with
/// NetworkManager.SessionListUpdated (Photon pushes changes automatically).
/// </summary>
public class JoinRoomScreen : MonoBehaviour
{
    [SerializeField] private LobbyScreenPresenter presenter;
    [SerializeField] private Transform roomListContent;   // ScrollView > Viewport > Content
    [SerializeField] private RoomListItem roomItemPrefab; // 02-Prefab/Room
    [SerializeField] private Button refreshButton;
    [Tooltip("Optional: shows 'Connecting...', 'No rooms found', errors.")]
    [SerializeField] private TMP_Text statusText;
    [Header("Password Input")]
    [SerializeField] private GameObject inputPasswordScreen;   // the popup panel
    [SerializeField] private TMP_InputField inputPasswordField;
    [SerializeField] private Button inputPasswordSubmitButton;
    [Tooltip("Shows which room the password is for.")]
    [SerializeField] private TMP_Text roomName;
    [Tooltip("Optional: closes the popup without joining.")]
    [SerializeField] private Button inputPasswordCancelButton;
    [Tooltip("Optional: shows 'Wrong password.' inside the popup.")]
    [SerializeField] private TMP_Text inputPasswordErrorText;

    private readonly List<RoomListItem> spawnedItems = new();
    private bool isJoining;
    private string pendingRoomName;

    private void Awake()
    {
        if (refreshButton != null)
        {
            refreshButton.onClick.AddListener(Refresh);
        }

        inputPasswordSubmitButton.onClick.AddListener(OnSubmitPassword);
        if (inputPasswordCancelButton != null)
        {
            inputPasswordCancelButton.onClick.AddListener(HidePasswordPanel);
        }
    }

    private void OnEnable()
    {
        // Always open this screen with the popup closed, even if it was left open last time.
        HidePasswordPanel();

        if (NetworkManager.Instance == null) return;

        NetworkManager.Instance.SessionListUpdated += RenderRooms;
        Refresh();
    }

    private void OnDisable()
    {
        if (NetworkManager.Instance != null)
        {
            NetworkManager.Instance.SessionListUpdated -= RenderRooms;
        }
    }

    private async void Refresh()
    {
        SetStatus("Connecting to lobby...");
        bool ok = await NetworkManager.Instance.JoinLobby();
        if (!ok)
        {
            SetStatus("Could not connect to the lobby.");
            return;
        }
        RenderRooms(NetworkManager.Instance.Sessions);
    }

    private void RenderRooms(IReadOnlyList<SessionInfo> sessions)
    {
        if (this == null) return; // screen was destroyed while awaiting

        foreach (RoomListItem item in spawnedItems)
        {
            Destroy(item.gameObject);
        }
        spawnedItems.Clear();

        foreach (SessionInfo session in sessions)
        {
            RoomListItem item = Instantiate(roomItemPrefab, roomListContent);
            item.Setup(session, OnJoinClicked);
            spawnedItems.Add(item);
            Debug.Log($"Room row created: '{session.Name}' under '{roomListContent.name}' " +
                      $"(active: {item.gameObject.activeInHierarchy}, size: {((RectTransform)item.transform).rect.size})");
        }

        SetStatus(sessions.Count == 0 ? "No rooms found." : string.Empty);
    }

    // Called by a room row's Join button. Only decides *how* to join.
    private void OnJoinClicked(SessionInfo session)
    {
        if (isJoining) return;

        if (NetworkManager.HasPassword(session))
        {
            ShowPasswordPanel(session.Name);
        }
        else
        {
            TryJoin(session.Name, string.Empty);
        }
    }

    private void OnSubmitPassword()
    {
        if (string.IsNullOrEmpty(pendingRoomName)) return;
        TryJoin(pendingRoomName, inputPasswordField.text);
    }

    private void ShowPasswordPanel(string sessionName)
    {
        pendingRoomName = sessionName;
        inputPasswordField.text = string.Empty;
        SetPasswordError(string.Empty);
        if (roomName != null)
        {
            roomName.text = sessionName;
        }
        inputPasswordScreen.SetActive(true);
    }

    private void HidePasswordPanel()
    {
        pendingRoomName = null;
        if (inputPasswordScreen != null)
        {
            inputPasswordScreen.SetActive(false);
        }
    }
    private async void TryJoin(string sessionName, string password)
    {
        if (isJoining) return; // stop double clicks starting two joins on one runner
        isJoining = true;
        inputPasswordSubmitButton.interactable = false;
        SetStatus($"Joining {sessionName}...");

        ShutdownReason result = await NetworkManager.Instance.JoinRoom(sessionName, password);

        isJoining = false;
        inputPasswordSubmitButton.interactable = true;

        if (result == ShutdownReason.Ok)
        {
            HidePasswordPanel();
            SetStatus(string.Empty);
            presenter.NavigateTo(LobbyScreenType.WaitingRoomScreen);
            return;
        }

        if (result == ShutdownReason.ConnectionRefused)
        {
            SetPasswordError("Wrong password.");
            SetStatus(string.Empty);
        }
        else
        {
            HidePasswordPanel();
            SetStatus(result == ShutdownReason.GameIsFull
                ? "Room is full."
                : "Room is no longer available.");
        }
        await NetworkManager.Instance.JoinLobby();
    }

    private void SetPasswordError(string message)
    {
        if (inputPasswordErrorText != null)
        {
            inputPasswordErrorText.text = message;
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
