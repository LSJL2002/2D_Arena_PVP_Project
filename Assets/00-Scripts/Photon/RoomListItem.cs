using System;
using Fusion;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Goes on the Room prefab. Shows one room in the Join Room list.
/// </summary>
public class RoomListItem : MonoBehaviour
{
    [SerializeField] private TMP_Text nameText;      // "Name"
    [SerializeField] private TMP_Text playerCountText; // "Number"
    [SerializeField] private Button joinButton;      // "JoinButton"

    public void Setup(SessionInfo session, Action<SessionInfo> onJoinClicked)
    {
        bool hasPassword = NetworkManager.HasPassword(session);

        nameText.text = hasPassword ? $"{session.Name} (locked)" : session.Name;
        playerCountText.text = $"{session.PlayerCount}/{session.MaxPlayers}";

        bool canJoin = session.IsOpen && session.PlayerCount < session.MaxPlayers;
        joinButton.interactable = canJoin;
        joinButton.onClick.RemoveAllListeners();
        joinButton.onClick.AddListener(() => onJoinClicked(session));
    }
}
