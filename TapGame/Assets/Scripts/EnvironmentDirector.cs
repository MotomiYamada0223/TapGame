using UnityEngine;

/// <summary>
/// ゲーム内の環境演出（背景の遷移、太陽の移動など）を管理するクラス。
/// TimeManager から残り時間を取得し、進行度に応じて背景や太陽の状態を動的に更新する。
/// 他のロジック（TimeManager や GameManager）と切り離すことで、演出の追加・変更が容易になるように設計。
/// </summary>
public class EnvironmentDirector : MonoBehaviour
{
    [Header("References")]
    [Tooltip("時間の進行状況を取得するための TimeManager への参照")]
    [SerializeField] private TimeManager timeManager;
    
    [Tooltip("夕方の背景画像（フェードインさせる対象）")]
    [SerializeField] private SpriteRenderer eveningBackground;
    
    [Tooltip("移動させる太陽の画像")]
    [SerializeField] private SpriteRenderer sunSprite;

    [Header("Sun Settings")]
    [Tooltip("太陽の移動開始位置の X 座標")]
    [SerializeField] private float sunStartX = 5f;
    [Tooltip("太陽の移動終了位置の X 座標")]
    [SerializeField] private float sunEndX = -5f;
    [Tooltip("太陽の軌道のベースとなる Y 座標")]
    [SerializeField] private float sunBaseY = 2f;
    [Tooltip("太陽が描く弧の高さ（最大 Y 上昇幅）")]
    [SerializeField] private float sunArcHeight = 3f;

    [Header("Background Transition Settings")]
    [Tooltip("夕方の背景へのフェードを開始する残り時間（秒）")]
    [SerializeField] private float transitionStartRemainingTime = 10f;
    [Tooltip("フェードを完了するまでにかかる時間（秒）")]
    [SerializeField] private float transitionDuration = 3f;

    private float gameDuration = 30f;

    private void Start()
    {
        // 昼の背景から開始するため、夕方の背景は最初透明（アルファ 0）に設定しておく。
        // シーン上で設定を忘れてもゲーム開始時に確実にリセットされるようにするための安全策。
        if (eveningBackground != null)
        {
            Color color = eveningBackground.color;
            color.a = 0f;
            eveningBackground.color = color;
        }

        // TimeManager がアタッチされていない場合、自動的にシーン内から探すことで
        // Inspector での参照セットアップ漏れによるエラーを防ぐ。
        if (timeManager == null)
        {
            timeManager = FindObjectOfType<TimeManager>();
        }

        // 進行度を正しく計算するため、TimeManager からゲームの全体時間を取得する。
        if (timeManager != null)
        {
            gameDuration = timeManager.GameDuration;
        }
    }

    private void Update()
    {
        // 参照が見つからない場合は処理を行わないことで、NullReferenceException を防ぐ。
        if (timeManager == null) return;

        float remaining = timeManager.RemainingTime;
        
        // 残り時間からゲーム全体の「進行度（0.0 〜 1.0）」を算出する。
        // これにより、ゲームの総時間が変わっても演出のタイミングが自動で調整される。
        float progress = 1f - (remaining / gameDuration);

        UpdateSunPosition(progress);
        UpdateBackgroundTransition(remaining);
    }

    /// <summary>
    /// 太陽の位置をゲームの進行度に応じて更新する。
    /// 右から左へ弧を描くように移動させることで、時間の経過を視覚的に表現する。
    /// </summary>
    /// <param name="progress">ゲームの進行度（0.0: 開始, 1.0: 終了）</param>
    private void UpdateSunPosition(float progress)
    {
        if (sunSprite == null) return;

        // X座標は進行度に応じて開始位置から終了位置へ直線的に補間する。
        float x = Mathf.Lerp(sunStartX, sunEndX, progress);
        
        // Y座標は進行度を 0〜π の範囲として Mathf.Sin に渡すことで、
        // 進行度 0.5 のとき（ゲーム中盤）に最も高くなる半円の弧を描かせる。
        float y = sunBaseY + Mathf.Sin(progress * Mathf.PI) * sunArcHeight;
        
        // Z座標は Inspector で設定された元の値を維持し、他の描画順を崩さないようにする。
        sunSprite.transform.position = new Vector3(x, y, sunSprite.transform.position.z);
    }

    /// <summary>
    /// 残り時間に応じて、夕方の背景画像をフェードインさせる。
    /// 急な背景の切り替わりによる違和感をなくすための処理。
    /// </summary>
    /// <param name="remaining">現在の残り時間</param>
    private void UpdateBackgroundTransition(float remaining)
    {
        if (eveningBackground == null) return;

        // フェード開始時間を下回ったら、夕方の背景のアルファ値を更新する。
        if (remaining <= transitionStartRemainingTime)
        {
            // 残り時間をもとに「フェードの進行度（0.0 〜 1.0）」を計算する。
            // 経過時間をフェード所要時間で割ることで、線形なフェードインを実現する。
            float fadeProgress = (transitionStartRemainingTime - remaining) / transitionDuration;
            
            // 計算結果が 1.0 を超えても色が不自然にならないよう、0〜1 の間にクランプする。
            fadeProgress = Mathf.Clamp01(fadeProgress);

            // SpriteRenderer の色は直接 Color 構造体の要素を変更できないため、
            // 一度変数で受けてからアルファ値を書き換え、再代入する。
            Color color = eveningBackground.color;
            color.a = fadeProgress;
            eveningBackground.color = color;
        }
    }
}
