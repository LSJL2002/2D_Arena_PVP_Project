using UnityEngine.UI;
using UnityEngine;
[RequireComponent(typeof(Button))]
public class LobbyNavigationButton : MonoBehaviour
{
    [SerializeField] private LobbyScreenType targetScreen;

    private void Start()
    {
        LobbyScreenPresenter presenter = Object.FindAnyObjectByType<LobbyScreenPresenter>();
        Button button = GetComponent<Button>();

        if (button != null && presenter != null)
        {
            button.onClick.AddListener(() => presenter.NavigateTo(targetScreen));
        }
    }
}
