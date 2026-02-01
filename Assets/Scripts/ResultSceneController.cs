using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class ResultSceneController : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI[] resultTexts;
    [SerializeField] private Button playAgainButton;
    [SerializeField] private Button titleButton;

    private static readonly string[] DefaultResultObjectNames =
    {
        "Player1",
        "Player2",
        "Player3",
        "Player4"
    };

    private void Awake()
    {
        EnsureButtons();
        if (resultTexts == null || resultTexts.Length == 0)
        {
            var found = new List<TextMeshProUGUI>();
            foreach (var objectName in DefaultResultObjectNames)
            {
                var obj = GameObject.Find(objectName);
                if (obj == null)
                {
                    continue;
                }

                var text = obj.GetComponent<TextMeshProUGUI>();
                if (text != null)
                {
                    found.Add(text);
                }
            }

            resultTexts = found.ToArray();
        }

        ApplyResults();
    }

    private void OnDestroy()
    {
        if (playAgainButton != null)
        {
            playAgainButton.onClick.RemoveListener(HandlePlayAgain);
        }

        if (titleButton != null)
        {
            titleButton.onClick.RemoveListener(HandleTitle);
        }
    }

    private void EnsureButtons()
    {
        if (playAgainButton == null)
        {
            playAgainButton = FindButtonByName("Nextbutton");
        }

        if (titleButton == null)
        {
            titleButton = FindButtonByName("Titlebutton");
        }

        if (playAgainButton != null)
        {
            playAgainButton.onClick.RemoveListener(HandlePlayAgain);
            playAgainButton.onClick.AddListener(HandlePlayAgain);
        }

        if (titleButton != null)
        {
            titleButton.onClick.RemoveListener(HandleTitle);
            titleButton.onClick.AddListener(HandleTitle);
        }
    }

    private Button FindButtonByName(string objectName)
    {
        var obj = GameObject.Find(objectName);
        return obj != null ? obj.GetComponent<Button>() : null;
    }

    private void HandlePlayAgain()
    {
        GameResultData.Clear();
        SceneManager.LoadScene("MainScene");
    }

    private void HandleTitle()
    {
        GameResultData.Clear();
        SceneManager.LoadScene("title");
    }

    private void ApplyResults()
    {
        if (resultTexts == null || resultTexts.Length == 0)
        {
            return;
        }

        var results = GameResultData.LastResults;

        var sortedResults = results?
            .Select((entry, index) => new { entry, index })
            .OrderByDescending(item => item.entry.FirstPlaceCount)
            .ThenBy(item => item.index)
            .ToList();
        int currentRank = 0;
        int? previousCount = null;

        for (int i = 0; i < resultTexts.Length; i++)
        {
            var text = resultTexts[i];
            if (text == null)
            {
                continue;
            }

            if (sortedResults != null && i < sortedResults.Count)
            {
                var entry = sortedResults[i].entry;
                if (previousCount != entry.FirstPlaceCount)
                {
                    currentRank = i + 1;
                    previousCount = entry.FirstPlaceCount;
                }

                text.text = $"{currentRank}位 {entry.Name}\n1位回数:{entry.FirstPlaceCount}";
            }
            else
            {
                text.text = string.Empty;
            }
        }
    }
}
