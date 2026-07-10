using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// メインゲームのセッション全体を統括するクラス。
/// 個々の機能（タイマー・スコア・遷移）は専門クラスに委ね、
/// このクラスは「ゲームの開始・終了フロー」のオーケストレーションのみを担う。
/// </summary>
public class GameManager : MonoBehaviour
{
    // Inspector での参照設定を強制することで、
    // 実行時に「アタッチし忘れ」によるエラーを早期に発見できる。
    [SerializeField] private TimeManager timeManager;
    [SerializeField] private ScoreManager scoreManager;
    [SerializeField] private float timeScaleInitializeValue = 1.0f;
    [SerializeField] private float timeScaleStopValue = 0.0f;

    [Header("Finish Animation Settings")]
    [SerializeField] private TMPro.TextMeshProUGUI finishText;
    [SerializeField] private float finishAnimationDurationSeconds = 0.5f;
    [SerializeField] private float finishOvershootScale = 1.2f;
    [SerializeField] private float finishNormalScale = 1.0f;
    [SerializeField] private float transitionDelaySeconds = 2.0f;

    // タイムアップ処理が複数回呼ばれるのを防ぐためのフラグ。
    private bool isFinished = false;

    private const string ResultSceneName = "ResultScene";

    private void Start()
    {
        // シーンが読み込まれた直後に初期化を行う。
        // Awake ではなく Start を使うのは、他のクラスの Awake（シングルトン初期化など）が
        // 確実に完了した後に InitializeGame を実行するため。
        InitializeGame();
    }

    private void OnEnable()
    {
        // イベントの購読はオブジェクトが有効になったタイミングで行う。
        // OnDisable で必ず解除することで、シーン遷移時のメモリリークを防ぐ。
        timeManager.OnTimeUp += HandleTimeUp;
    }

    private void OnDisable()
    {
        // OnEnable で登録したイベントを確実に解除する。
        // 解除しないと、このオブジェクトが破棄された後でも
        // TimeManager からデリゲートが呼ばれ続け、null 参照エラーになる。
        timeManager.OnTimeUp -= HandleTimeUp;
    }

    /// <summary>
    /// ゲームセッションを初期状態にセットアップする。
    /// 「前のセッションの状態が残ったまま始まる」バグを防ぐために必ず呼ぶ。
    /// </summary>
    private void InitializeGame()
    {
        // ゲームオーバー後や一時停止後に timeScale が 0 のまま残っていることがあるため、
        // セッション開始時に必ず初期値に戻す。
        Time.timeScale = timeScaleInitializeValue;

        // 前のゲームセッションのスコアが引き継がれないよう、ゲーム開始時にリセットする。
        scoreManager.ResetScore();

        // Finishテキストが存在する場合は初期状態として非表示にする。
        if (finishText != null)
        {
            finishText.gameObject.SetActive(false);
            finishText.rectTransform.localScale = Vector3.zero;
        }

        // 終了フラグをリセットする。
        isFinished = false;

        // タイマーを開始する。これ以降 TimeManager が経過時間を管理する。
        timeManager.StartTimer();
    }

    /// <summary>
    /// タイムアップ時に TimeManager から呼ばれるイベントハンドラ。
    /// セッションを終了させ、リザルト画面への遷移を開始する。
    /// </summary>
    private void HandleTimeUp()
    {
        // タイムアップ処理が複数回呼ばれないよう、状態管理フラグでガードする。
        if (isFinished) return;
        isFinished = true;

        // 時間切れ後にプレイヤーが円をタップできてしまわないよう、
        // 遷移を開始する前に物理演算・Update 系の処理を止める。
        Time.timeScale = timeScaleStopValue;

        // ゲームが終了したタイミングでハイスコアを保存する。
        // 遷移後に SaveHighScore を呼ぶと ScoreManager が存在しない可能性があるため、ここで行う。
        scoreManager.SaveHighScore();

        // 演出およびシーン遷移のコルーチンを開始する。
        StartCoroutine(PlayFinishAnimationAndTransition());
    }

    /// <summary>
    /// 「Finish!」テキストのアニメーション表示と、その後のシーン遷移を制御するコルーチン。
    /// </summary>
    private System.Collections.IEnumerator PlayFinishAnimationAndTransition()
    {
        if (finishText != null)
        {
            // 初期状態: テキストのスケール（サイズ）を (0, 0, 0) に設定します。
            finishText.rectTransform.localScale = Vector3.zero;
            finishText.gameObject.SetActive(true);

            // サウンド演出: 「Finish!」テキストのアニメーション開始と全く同時にホイッスルSEを再生する。
            if (SoundManager.Instance != null)
            {
                SoundManager.Instance.PlaySeWhistle();
            }

            float elapsedTime = 0f;

            // アニメーション実行ループ
            while (elapsedTime < finishAnimationDurationSeconds)
            {
                // Time.timeScale が 0 なので、実際の時間経過（unscaledDeltaTime）を使用する。
                elapsedTime += Time.unscaledDeltaTime;

                // 進行割合（0.0 〜 1.0）を算出する。
                float progress = Mathf.Clamp01(elapsedTime / finishAnimationDurationSeconds);

                // イージングを適用してスケール値を計算する。
                float scale = CalculateCustomOutBack(progress, finishOvershootScale, finishNormalScale);
                finishText.rectTransform.localScale = new Vector3(scale, scale, scale);

                yield return null;
            }

            // 最終的に本来の通常サイズ（1.0）へ戻って止まるよう、値を確定させる。
            finishText.rectTransform.localScale = new Vector3(finishNormalScale, finishNormalScale, finishNormalScale);
        }

        // シーン遷移: 演出完了後、一定の待機時間を挟む。
        // ここでも timeScale が 0 なので WaitForSecondsRealtime を使用する。
        yield return new WaitForSecondsRealtime(transitionDelaySeconds);

        // 次のシーンへ自動的に遷移する処理を行う。
        if (SceneTransitionManager.Instance != null)
        {
            SceneTransitionManager.Instance.TransitionToScene(ResultSceneName);
        }
        else
        {
            SceneManager.LoadScene(ResultSceneName);
        }
    }

    /// <summary>
    /// 勢いよく拡大し、設定した最大倍率（オーバーシュート）に達した後、通常倍率に戻るイージング計算。
    /// 0.0〜0.7 の間で最大スケールまで拡大し、残りの時間で通常スケールに収束させる。
    /// </summary>
    /// <param name="progress">進行度（0.0 〜 1.0）</param>
    /// <param name="overshootScale">オーバーシュート時の最大スケール値</param>
    /// <param name="normalScale">最終的に落ち着く通常のスケール値</param>
    /// <returns>計算されたスケール値</returns>
    private float CalculateCustomOutBack(float progress, float overshootScale, float normalScale)
    {
        // 最初の70%の時間で最大スケール（overshootScale）まで勢いよく拡大する閾値。
        const float Threshold = 0.7f;

        if (progress < Threshold)
        {
            // 0.0 〜 1.0 に正規化
            float t = progress / Threshold;
            // OutSineのイージング（急激に上がり、滑らかに頂点へ到達する）
            return overshootScale * Mathf.Sin(t * Mathf.PI * 0.5f);
        }
        else
        {
            // 残りの30%の時間で通常スケール（normalScale）に落ち着く。
            // 0.0 〜 1.0 に正規化
            float t = (progress - Threshold) / (1f - Threshold);
            // InOutSineのイージング（滑らかに目標値へ下がる）
            return Mathf.Lerp(overshootScale, normalScale, (1f - Mathf.Cos(t * Mathf.PI)) * 0.5f);
        }
    }
}