using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using TMPro;

public class SevenPassRule : IRule
{
    public bool CanApply(List<Card> played, GameState state)
    {
        if (played == null || played.Count == 0) return false;
        return played.Any(c => c.Rank == 7);
    }

    public void Apply(List<Card> played, GameState state)
    {
        // 7が含まれている枚数分、次のプレイヤーにカードを渡す
        int sevenCount = played.Count(c => c.Rank == 7);
        state.SevenPassCount = sevenCount;
    }

    /// <summary>
    /// 7渡しシーケンスを開始
    /// プレイヤーの種類に応じて、UI表示またはCPU自動処理に分岐
    /// </summary>
    public IEnumerator StartSevenPassSequence(GameManager gm, PlayerBase player, int count)
    {
        // GameManagerに7渡しモードを設定
        gm.SetSevenPassMode(true, count);

        // ★修正: メッセージを具体的なアクションとして記述
        string message = $"7渡し発動! 最大{count}枚選んで次のプレイヤーに渡してください (0枚はパスボタン)";

        if (player is HumanPlayer humanPlayer)
        {
            // 人間プレイヤーの場合：UI表示してプレイヤーの選択を待つ
            // ShowSevenPassUI内でメッセージが表示されるため、即座にメッセージが表示され、
            // プレイヤーがアクションを完了するまで表示され続けます。
            gm.ShowSevenPassUI(message);
            yield break; // プレイヤーがPlayボタンを押すまで待機
        }
        else
        {
            // CPUプレイヤーの場合：自動で選択して渡す
            // ★修正: メッセージをキューではなく即時表示させ、一定時間待ってからCPU処理へ
            yield return gm.StartCoroutine(gm.ShowMessage(message, 1.5f));

            var hand = player.Hand.OrderBy(c => c.Rank).ToList();

            // 例: CPUは最も弱いカードを渡す（このロジックは現状維持とします）
            int cardsToPassCount = Mathf.Min(count, hand.Count);
            var cardsToPass = hand.Take(cardsToPassCount).ToList();

            yield return gm.StartCoroutine(ExecuteSevenPassTransfer(gm, player, cardsToPass));
        }
    }

    /// <summary>
    /// 7渡しの実際の転送処理
    /// 指定されたカードを次のプレイヤーに渡す
    /// </summary>
    public IEnumerator ExecuteSevenPassTransfer(GameManager gm, PlayerBase fromPlayer, List<Card> cards)
    {
        gm.SetSevenPassMode(false, 0); // モードを終了
        gm.HideActionMessage(); // メッセージを非表示
        gm.ResetPlayButtonUI(); // Playボタンを通常に戻す

        var players = gm.GetPlayers();
        int nextIndex = (players.IndexOf(fromPlayer) + 1) % players.Count;
        PlayerBase toPlayer = players[nextIndex];

        Debug.Log($"{fromPlayer.Name} から {toPlayer.Name} へ {cards.Count}枚 渡します");

        // カードを転送
        foreach (var card in cards)
        {
            fromPlayer.Hand.Remove(card);
            toPlayer.Hand.Add(card);

            // 人間プレイヤーの場合、UIからもカードを削除
            if (fromPlayer is HumanPlayer)
            {
                gm.RemoveCardsFromPlayerUI(new List<Card> { card });
            }
        }

        // 両プレイヤーの手札表示を更新
        gm.UpdatePlayerHandDisplay(toPlayer);
        gm.UpdatePlayerHandDisplay(fromPlayer);

        yield return new WaitForSeconds(0.8f);

        // 禁止あがりルールのチェック
        if (gm.GetForbidSpecialWin() && fromPlayer.Hand.Count == 0)
        {
            gm.EnqueueMessage("🚫 ルールにより、特殊ルール（7渡し）でのあがりは禁止されています！");
        }
        else
        {
            // 勝利判定
            gm.CheckForWin(fromPlayer);
            if (gm.IsGameOver()) yield break;
        }

        // 7渡しモードを解除
        gm.SetSevenPassMode(false, 0);
        gm.HideActionMessage();
        gm.ResetPlayButtonUI();

        // 次のターンへ
        gm.EndPlayerTurn();
    }
}