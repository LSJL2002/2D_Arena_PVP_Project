using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class NameSettings : MonoBehaviour
{
    [SerializeField] private TMP_InputField nameInput;
    [SerializeField] private Button saveButton;
    [SerializeField] private TMP_Text statusText;
    private Coroutine clearRoutine;

    private void Awake()
    {
        saveButton.onClick.AddListener(OnSaveClicked);
        nameInput.text = PlayerPrefs.GetString("PlayerName", "");
    }

    private void OnSaveClicked()
    {
        string playerName = nameInput.text;
        if (string.IsNullOrWhiteSpace(playerName))
        {
            SetStatus("Enter a valid name.");
            return;
        }
        PlayerPrefs.SetString("PlayerName", playerName);
        Debug.Log($"Player name set to : {PlayerPrefs.GetString("PlayerName")}");
        SetStatus("Name saved!");
    }

    private void SetStatus(string message)
    {
        statusText.text = message;
        if (clearRoutine != null)
        {
            StopCoroutine(clearRoutine);
        }
        clearRoutine = StartCoroutine(ClearAfterDelay());
    }

    private IEnumerator ClearAfterDelay()
    {
        yield return new WaitForSeconds(3f);
        statusText.text = "";
    }
}
