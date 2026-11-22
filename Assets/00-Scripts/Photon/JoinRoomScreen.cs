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
    [Tooltip("Optional: password used when joining a locked room.")]
    [SerializeField] private TMP_InputField passwordInput;
    [Tooltip("Optional: shows 'Connecting...', 'No rooms found', errors.")]
    [SerializeField] private TMP_Text statusText;

    private readonly List<RoomListItem> spawnedItems = new();
    private bool isJoining;

    private void Awake()
    {
        if (refreshButton != null)
        {
            refreshButton.onClick.AddListener(Refresh);
        }
    }

    private void OnEnable()
    {
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

    private async void OnJoinClicked(SessionInfo session)
    {
        if (isJoining) return;
        isJoining = true;
        SetStatus($"Joining {session.Name}...");

        string password = passwordInput != null ? passwordInput.text : string.Empty;
        bool ok = await NetworkManager.Instance.JoinRoom(session.Name, password);

        isJoining = false;
        if (ok)
        {
            presenter.NavigateTo(LobbyScreenType.WaitingRoomScreen);
        }
        else
        {
            SetStatus("Could not join room (full, closed, or wrong password).");
            // A failed StartGame shuts the runner down; reconnect to the lobby so the list keeps updating.
            await NetworkManager.Instance.JoinLobby();
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
