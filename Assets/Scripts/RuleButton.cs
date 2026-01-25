using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class NewBehaviourScript : MonoBehaviour
{
    public Button targetButton;
    public Color changedColor = Color.yellow;

    [SerializeField] private TextMeshProUGUI labelText;
    [SerializeField] private string labelSuffixOn = "ÅFON";
    [SerializeField] private string labelSuffixOff = "ÅFOFF";

    private Color originalColor;
    private bool isChanged = false;
    private string ruleKey;
    private string baseLabel;

    void Start()
    {
        if (targetButton == null)
        {
            targetButton = GetComponent<Button>();
        }

        if (targetButton == null)
        {
            return;
        }

        ColorBlock cb = targetButton.colors;
        originalColor = cb.normalColor;

        if (labelText == null)
        {
            labelText = GetComponentInChildren<TextMeshProUGUI>();
        }

        if (labelText != null)
        {
            baseLabel = labelText.text;
        }

        if (SoloRuleSettings.TryGetRuleKey(gameObject.name, out ruleKey))
        {
            isChanged = SoloRuleSettings.GetRuleEnabled(ruleKey);
            ApplyColor(isChanged);
        }
    }

    public void ToggleColor()
    {
        if (targetButton == null)
        {
            targetButton = GetComponent<Button>();
        }
        if (targetButton == null)
        {
            return;
        }

        if (SoloRuleSettings.TryGetRuleKey(gameObject.name, out var resolvedKey))
        {
            ruleKey = resolvedKey;
            isChanged = SoloRuleSettings.ToggleRule(ruleKey);
        }
        else
        {
            isChanged = !isChanged;
        }

        ApplyColor(isChanged);
    }

    private void ApplyColor(bool enabled)
    {
        if (targetButton == null)
        {
            return;
        }

        var cb = targetButton.colors;
        var color = enabled ? changedColor : originalColor;
        cb.normalColor = color;
        cb.highlightedColor = color;
        cb.pressedColor = color;
        cb.selectedColor = color;
        targetButton.colors = cb;

        if (labelText != null && !string.IsNullOrEmpty(baseLabel) && ruleKey != "CpuLevel")
        {
            labelText.text = baseLabel + (enabled ? labelSuffixOn : labelSuffixOff);
        }
    }

    void Update()
    {
    }
}