using System.Collections.Generic;
using UnityEngine;

public static class SoloRuleSettings
{
    private const string KeyPrefix = "SoloRule.";
    private const string SoloModeKey = "SoloRule.Mode";

    private static readonly Dictionary<string, string> ButtonToKey = new()
    {
        { "kakaumei", "Revolution" },
        { "8giri", "EightCut" },
        { "7watasi", "SevenPass" },
        { "5skip", "FiveSkip" },
        { "10sute", "TenDiscard" },
        { "11back", "ElevenBack" },
        { "4dome", "FourStop" },
        { "kinsiagari", "ForbidSpecialWin" },
        { "miyakoochi", "MiyakoOchi" },
        { "sibari", "Bind" },
        { "kaidan", "Stair" },
        { "supe3", "Spade3Return" },
        { "CPUlevel", "CpuLevel" },
        { "4single", "FourSingle" },
        { "6trade", "SixTrade" },
        { "9fo-su", "NineForce" },
        { "11sairensu", "ElevenSilence" },
        { "12penaruthi", "TwelvePenalty" },
        { "baria", "Barrier" },
        { "freezthe12", "FreezeTwelve" },
        { "suutorock", "SuitLock" },
        { "jokerstop", "JokerStop" },
        { "daikonran", "GreatChaos" },
        { "tenho_tiho", "TenhouChiho" },
        { "6dome", "SixStop" }
    };

    private static readonly Dictionary<string, bool> DefaultStates = new()
    {
        { "Revolution", true },
        { "EightCut", true },
        { "SevenPass", true },
        { "FiveSkip", true },
        { "TenDiscard", true },
        { "ElevenBack", true },
        { "FourStop", true },
        { "SixStop", true },
        { "ForbidSpecialWin", false },
        { "MiyakoOchi", true },
        { "Bind", true },
        { "Stair", true },
        { "Spade3Return", true },
        { "CpuLevel", false },
        { "FourSingle", true },
        { "SixTrade", true },
        { "NineForce", true },
        { "ElevenSilence", true },
        { "TwelvePenalty", true },
        { "Barrier", true },
        { "FreezeTwelve", true },
        { "SuitLock", true },
        { "JokerStop", true },
        { "GreatChaos", true },
        { "TenhouChiho", false }
    };

    public static bool TryGetRuleKey(string buttonName, out string key)
    {
        return ButtonToKey.TryGetValue(buttonName, out key);
    }

    public static bool IsSoloModeActive => PlayerPrefs.GetInt(SoloModeKey, 0) == 1;

    public static void SetSoloMode(bool enabled)
    {
        PlayerPrefs.SetInt(SoloModeKey, enabled ? 1 : 0);
        PlayerPrefs.Save();
    }

    public static bool GetRuleEnabled(string key)
    {
        var defaultValue = DefaultStates.TryGetValue(key, out var value) && value;
        return PlayerPrefs.GetInt(KeyPrefix + key, defaultValue ? 1 : 0) == 1;
    }

    public static void SetRuleEnabled(string key, bool enabled)
    {
        PlayerPrefs.SetInt(KeyPrefix + key, enabled ? 1 : 0);
        PlayerPrefs.Save();
    }

    public static bool ToggleRule(string key)
    {
        var nextValue = !GetRuleEnabled(key);
        SetRuleEnabled(key, nextValue);
        return nextValue;
    }
}