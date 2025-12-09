using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class EightCutRule : IRule
{
    public bool CanApply(List<Card> played, GameState state)
    {
        if (played == null || played.Count == 0) return false;
        return played.Exists(c => c.Rank == 8);
    }

    public void Apply(List<Card> played, GameState state)
    {
        state.IsEightCut = true;
        state.KeepTurn = true;
    }

    /// <summary>
    /// 8切り発動時の場札クリア処理
    /// GameManagerから呼び出される
    /// </summary>
    public IEnumerator ExecuteEightCut(GameManager gm, Transform tableArea, PlayerBase currentPlayer)
    {
        gm.EnqueueMessage("8切り!");

        yield return new WaitForSeconds(1.0f);

        // 場札をすべて削除
        foreach (Transform child in tableArea)
        {
            Object.Destroy(child.gameObject);
        }

        // 場の状態をリセット
        gm.lastPlayedCards.Clear();
        gm.ResetPassCount();

        // 一時的な革命状態をリセット
        gm.ResetTempRevolution();

        // スキップカウントもリセット
        gm.ResetPendingSkipCount();

        // 8切りを出したプレイヤーがまだあがっていなければ、もう一度そのプレイヤーのターンに
        if (gm.GetRemainingPlayers().Contains(currentPlayer))
        {
            // 現在のプレイヤーのインデックスを再設定
            gm.SetCurrentTurnIndex(gm.GetPlayers().IndexOf(currentPlayer));

            // ★修正点: ターンを再開する処理を呼び出す
            gm.StartTurn();
        }
    }
}