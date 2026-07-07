using UnityEngine;
using TMPro;

/// <summary>
/// リザルト画面でのスコア表示とタイトルへの帰還を管理するクラス。
/// ScoreManager.LastScore を通じてゲームセッションの結果を受け取り、
/// プレイヤーに最終スコアとハイスコアを提示する。
/// </summary>
public class ResultManager : MonoBehaviour
{
    // 表示先の UI テキストを Inspector でアタッチする。
    // フィールドに持つことで、表示更新が必要なときに毎回 Find しなくて済む。
    [SerializeField] private TextMeshProUGUI currentScoreText;
    [SerializeField] private TextMeshProUGUI highScoreText;

    private const string TitleSceneName = "TitleScene";

    private void Start()
    {
        // シーン読み込み直後にスコアを表示することで、
        // プレイヤーがリザルト画面を開いた瞬間から結果を確認できる。
        DisplayScores();
    }

    /// <summary>
    /// ScoreManager と PlayerPrefs から取得したスコアを UI に表示する。
    /// 「スコアの取得」と「表示」を分離したメソッドにすることで、
    /// 将来的にアニメーション演出を挟む場合も変更箇所を最小限にできる。
    /// </summary>
    private void DisplayScores()
    {
        // static プロパティ経由でゲームシーンのスコアを受け取る。
        // シーンをまたいでオブジェクトを DontDestroyOnLoad しなくてもデータを引き継げる軽量な手段。
        int currentScore = ScoreManager.LastScore;

        // PlayerPrefs に保存されたハイスコアを読み込む。
        // 第2引数の 0 はキーが存在しない（初回プレイ）場合のデフォルト値。
        int highScore = PlayerPrefs.GetInt(ScoreManager.HighScoreSaveKey, 0);

        // UI オブジェクトが未設定でもクラッシュしないよう、null チェックを入れている。
        if (currentScoreText != null)
        {
            currentScoreText.text = $"SCORE: {currentScore}";
        }

        if (highScoreText != null)
        {
            highScoreText.text = $"HIGH SCORE: {highScore}";
        }
    }

    /// <summary>
    /// 「タイトルへ戻る」ボタンが押されたときに Inspector の Button.OnClick() から呼ばれるメソッド。
    /// タイトルシーンへの遷移を開始する。
    /// </summary>
    public void OnBackToTitleButtonClicked()
    {
        // 音声マネージャーが存在する場合は、ボタン押下時のSEを再生する。
        if (SoundManager.Instance != null)
        {
            SoundManager.Instance.PlaySeTap();
        }

        // GameManager がゲーム終了時に timeScale を 0 にしているため、ここで 1 に戻す。
        // このリセットを忘れると、タイトルや次のゲームシーンでも時間が止まったままになる。
        Time.timeScale = 1f;

        // SceneTransitionManager があればフェード付きで遷移、なければ直接ロードする。
        // テスト用にリザルトシーンを単独で開いた場合でも動作するようにするための対策。
        if (SceneTransitionManager.Instance != null)
        {
            SceneTransitionManager.Instance.TransitionToScene(TitleSceneName);
        }
        else
        {
            UnityEngine.SceneManagement.SceneManager.LoadScene(TitleSceneName);
        }
    }
}
