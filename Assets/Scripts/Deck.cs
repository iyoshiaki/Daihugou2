using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using System.Linq;

public class Deck
{
    private List<Card> cards = new();
    public Deck()
    {
        foreach (Suit s in System.Enum.GetValues(typeof(Suit)))
            for (int r = 3; r <= 15; r++) cards.Add(new Card(s, r));
    }
    public void Shuffle()
    {
        var rnd = new System.Random();
        cards = cards.OrderBy(_ => rnd.Next()).ToList();
    }
    public List<List<Card>> Deal(int players)
    {
        var hands = new List<List<Card>>();
        for (int i = 0; i < players; i++) hands.Add(new List<Card>());
        for (int i = 0; i < cards.Count; i++) hands[i % players].Add(cards[i]);
        return hands;
    }
}
