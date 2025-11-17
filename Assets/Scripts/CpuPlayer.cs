using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class CpuPlayer : PlayerBase
{
    public override List<Card> SelectCards(List<Card> fieldCards)
    {
        // CPUはランダムでカードを1枚出す例
        if (Hand.Count == 0) return new List<Card>();

        int index = Random.Range(0, Hand.Count);
        var selected = new List<Card> { Hand[index] };
        Hand.RemoveAt(index);
        return selected;
    }
}
