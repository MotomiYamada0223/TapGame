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

        // タイマーを開始する。これ以降 TimeManager が経過時間を管理する。
        timeManager.StartTimer();
    }

    /// <summary>
    /// タイムアップ時に TimeManager から呼ばれるイベントハンドラ。
    /// セッションを終了させ、リザルト画面への遷移を開始する。
    /// </summary>
    private void HandleTimeUp()
    {
        // 時間切れ後にプレイヤーが円をタップできてしまわないよう、
        // 遷移を開始する前に物理演算・Update 系の処理を止める。
        Time.timeScale = timeScaleStopValue;

        // ゲームが終了したタイミングでハイスコアを保存する。
        // 遷移後に SaveHighScore を呼ぶと ScoreManager が存在しない可能性があるため、ここで行う。
        scoreManager.SaveHighScore();

        // SceneTransitionManager があればフェード付きで遷移、なければ直接ロードする。
        // 直接起動・テストシーンなど SceneTransitionManager が存在しない環境でも動作させるため。
        if (SceneTransitionManager.Instance != null)
        {
            SceneTransitionManager.Instance.TransitionToScene(ResultSceneName);
        }
        else
        {
            SceneManager.LoadScene(ResultSceneName);
        }
    }
}