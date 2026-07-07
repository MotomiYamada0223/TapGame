using UnityEngine;

/// <summary>
/// 円が画面外（デッドゾーン）に落ちたことを検知し、後処理を行うクラス。
/// このオブジェクトには BoxCollider2D（Is Trigger = ON）を設定し、
/// 画面下部の見えない領域に配置することで「落下検知エリア」として機能させる。
///
/// 【責任範囲】
///   - 落下した円の破棄
///   - コンボリセット（ミスのペナルティ）の通知
///   - 新たな円の生成依頼
/// 各処理の実装は専門のクラスに委ねることで、このクラスは「検知と通知」だけに集中できる。
/// </summary>
[RequireComponent(typeof(BoxCollider2D))]
public class DeadZone : MonoBehaviour
{
    // タグによるフィルタリングで、コイン・エフェクト・UIなど他のオブジェクトを
    // 誤って拾わないようにしている。
    private const string CircleTag = "TargetCircle";

    /// <summary>
    /// Is Trigger が ON のコライダーに他の Collider2D が入ったときに自動で呼ばれる。
    /// 「TargetCircle タグを持つオブジェクトが落下してきた」ことをここで検出する。
    /// </summary>
    private void OnTriggerEnter2D(Collider2D collision)
    {
        // 対象外のオブジェクトは早期リターンで無視する。
        // こうすることで、以降の処理が「円だと確定した状態」で書けて読みやすくなる。
        if (!collision.CompareTag(CircleTag)) return;

        // Destroy を呼ぶと同フレームに gameObject への参照が無効になる可能性があるため、
        // 後続の処理で collision を参照しないよう先に破棄する。
        Destroy(collision.gameObject);

        // 円を落とした＝ミスなので、コンボをゼロに戻す（ペナルティ）。
        // スコア自体は減らさず「連鎖ボーナスを途切れさせる」設計。
        if (ScoreManager.Instance != null)
        {
            ScoreManager.Instance.ResetCombo();
        }

        // 「円が消えた」ことを SpawnerManager に通知し、新しい円の補充を依頼する（条件A）。
        // DeadZone 自身が Instantiate を呼ばないのは、生成責任を SpawnerManager に一元化するため。
        if (SpawnerManager.Instance != null)
        {
            SpawnerManager.Instance.HandleCircleDestroyed();
        }
    }
}