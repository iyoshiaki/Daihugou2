using System.Collections.Generic;

public class Player
{
    public string Name;
    public List<Card> Hand = new List<Card>();

    public Player(string name)
    {
        Name = name;
    }
}
