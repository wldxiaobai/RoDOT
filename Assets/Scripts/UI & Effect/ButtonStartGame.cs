using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Button))]
public class ButtonStartGame : MonoBehaviour
{
    private Button _button;

    private void Awake()
    {
        _button = GetComponent<Button>();
    }

    private void LateUpdate()
    {
        UpdateButtonState();
    }

    private void UpdateButtonState()
    {
        if (_button == null || SaveManeger.Instance == null)
        {
            return;
        }

        var hasData = SaveManeger.IsInitialized && SaveManeger.Instance.HasSaveData();
        _button.interactable = hasData;
    }
}
