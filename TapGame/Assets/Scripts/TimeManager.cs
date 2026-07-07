using System;
using UnityEngine;
using TMPro;

/// <summary>
/// ゲームのカウントダウンタイマーを管理するクラス。
/// 「残り時間の計測と表示」だけに責任を限定し、
/// 時間切れの処理（シーン遷移など）は GameManager に委ねている。
/// イベントを使うことで、このクラスが GameManager を直接知らなくても通知できる。
/// </summary>
public class TimeManager : MonoBehaviour
{
    // タイムアップの通知手段としてイベントを使う。
    // 直接メソッド呼び出しにすると TimeManager と GameManager が密結合になり、
    // どちらかを変更したときにもう一方も修正が必要になってしまうため、イベントで疎結合にしている。
    public event Action OnTimeUp;

    // ゲーム1セッションの制限時間。定数にすることで「30秒」という仕様が
    // コード上に散在せず、ここを変えれば一括で反映される。
    private const float GameDurationSeconds = 30f;

    // 残り時間が 0 以下になったことを判定する基準値。
    // マジックナンバー（0f）を直接書かず名前を付けることで、意図が明確になる。
    private const float TimeUpThreshold = 0f;

    private float remainingTime;

    /// <summary>
    /// 現在の残り時間を外部から取得するためのプロパティ。
    /// 演出（EnvironmentDirector など）が進行度を計算するために使用する。
    /// </summary>
    public float RemainingTime => remainingTime;

    /// <summary>
    /// ゲームの総時間（初期制限時間）を外部から取得するためのプロパティ。
    /// </summary>
    public float GameDuration => GameDurationSeconds;

    // タイマーが動いているかどうかのフラグ。
    // false の間は Update で何もしないことで、不必要な計算を避けている。
    private bool isTimerRunning = false;

    private void Update()
    {
        // タイマーが停止状態なら毎フレームの減算処理をスキップする。
        // 初期化前やゲーム終了後に余計なカウントが走らないための安全弁。
        if (!isTimerRunning) return;

        UpdateTimer();
    }

    /// <summary>
    /// ゲーム開始時に残り時間をセットしてカウントダウンを開始する。
    /// GameManager の InitializeGame() から呼ばれることを想定している。
    /// </summary>
    public void StartTimer()
    {
        remainingTime = GameDurationSeconds;

        // このフラグを true にして初めて Update 内の処理が動き出す。
        isTimerRunning = true;
    }

    /// <summary>
    /// 毎フレーム残り時間を減算し、タイムアップを判定する。
    /// Update から切り出すことで、「タイマー更新」の責任範囲を明確にしている。
    /// </summary>
    private void UpdateTimer()
    {
        remainingTime -= Time.deltaTime;

        // 残り時間がゼロ以下になったらタイムアップとして処理する。
        if (remainingTime <= TimeUpThreshold)
        {
            // 0 未満にならないようクランプすることで、UI に「-1」のような
            // おかしな値が表示されるのを防ぐ。
            remainingTime = TimeUpThreshold;

            // タイマーを止めてから通知することで、OnTimeUp の中で何らかの処理が
            // 走っても UpdateTimer が再び呼ばれないようにしている。
            isTimerRunning = false;
            OnTimeUp?.Invoke();
        }
    }
}