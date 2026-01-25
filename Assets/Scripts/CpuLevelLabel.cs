using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class CpuLevelLabel : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI label;
    [SerializeField] private Button button;
    [SerializeField] private Toggle normalToggle;
    [SerializeField] private Toggle strongToggle;
    [SerializeField] private Toggle ultimateToggle;
    private UnityEngine.Events.UnityAction<bool> normalListener;
    private UnityEngine.Events.UnityAction<bool> strongListener;
    private UnityEngine.Events.UnityAction<bool> ultimateListener;

    private void Awake()
    {
        if (button == null)
        {
            button = GetComponent<Button>();
        }

        if (label == null)
        {
            label = GetComponentInChildren<TextMeshProUGUI>();
        }

        UpdateLabel();

        if (button != null)
        {
            button.onClick.AddListener(OnButtonClicked);
        }

        RegisterToggle(normalToggle, 0);
        RegisterToggle(strongToggle, 1);
        RegisterToggle(ultimateToggle, 2);
    }

    private void OnEnable()
    {
        UpdateLabel();
    }

    private void OnDestroy()
    {
        if (button != null)
        {
            button.onClick.RemoveListener(OnButtonClicked);
        }

        UnregisterToggle(normalToggle, 0);
        UnregisterToggle(strongToggle, 1);
        UnregisterToggle(ultimateToggle, 2);
    }

    private void UpdateLabel()
    {
        if (label == null)
        {
            return;
        }

        var level = SoloRuleSettings.GetCpuLevel();
        UpdateToggleState(level);
        if (level == 1)
        {
            label.text = "CPUÇÃã≠Ç≥: ã≠Ç¢";
        }
        else if (level == 2)
        {
            label.text = "CPUÇÃã≠Ç≥: ç≈ã≠";
        }
        else
        {
            label.text = "CPUÇÃã≠Ç≥: ïÅí ";
        }
    }

    private void RegisterToggle(Toggle toggle, int level)
    {
        if (toggle == null)
        {
            return;
        }

        UnityEngine.Events.UnityAction<bool> handler = isOn =>
        {
            if (!isOn)
            {
                return;
            }

            SoloRuleSettings.SetCpuLevel(level);
            UpdateLabel();
        };

        toggle.onValueChanged.AddListener(handler);

        switch (level)
        {
            case 0:
                normalListener = handler;
                break;
            case 1:
                strongListener = handler;
                break;
            case 2:
                ultimateListener = handler;
                break;
        }
    }

    private void UnregisterToggle(Toggle toggle, int level)
    {
        if (toggle == null)
        {
            return;
        }

        switch (level)
        {
            case 0:
                if (normalListener != null)
                {
                    toggle.onValueChanged.RemoveListener(normalListener);
                }
                break;
            case 1:
                if (strongListener != null)
                {
                    toggle.onValueChanged.RemoveListener(strongListener);
                }
                break;
            case 2:
                if (ultimateListener != null)
                {
                    toggle.onValueChanged.RemoveListener(ultimateListener);
                }
                break;
        }
    }

    private void UpdateToggleState(int level)
    {
        if (normalToggle != null)
        {
            normalToggle.SetIsOnWithoutNotify(level == 0);
        }
        if (strongToggle != null)
        {
            strongToggle.SetIsOnWithoutNotify(level == 1);
        }
        if (ultimateToggle != null)
        {
            ultimateToggle.SetIsOnWithoutNotify(level == 2);
        }
    }

    private void OnButtonClicked()
    {
        SoloRuleSettings.CycleCpuLevel();
        UpdateLabel();
    }
}