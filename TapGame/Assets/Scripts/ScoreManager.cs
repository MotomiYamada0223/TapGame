using System.Collections;
using UnityEngine;
using TMPro;

/// <summary>
/// スコアとコンボを管理し、UIへの反映を担うクラス。
/// ゲーム内の「点数計算ルール」をここに集約することで、
/// CircleController や DeadZone は計算内容を知らなくても済む設計にしている。
///
/// 【スコア計算式】
///   加算点 = ベースポイント × コンボ倍率
///   コンボ倍率 = コンボ数（ただしコンボが2未満のときは1倍）
/// </summary>
public class ScoreManager : MonoBehaviour
{
    // 複数のクラスからアクセスされるため、シングルトンとして公開する。
    public static ScoreManager Instance { get; private set; }

    // スコアとコンボは別々の TextMeshPro に表示する。
    // Inspector で差し替えられるようにして、UIレイアウト変更への柔軟性を持たせている。
    [SerializeField] private TextMeshProUGUI scoreText;
    [SerializeField] private TextMeshProUGUI comboText;

    // HighScore のキー文字列を定数にすることで、
    // ResultManager など他クラスが同じキーで確実に読み書きできるようにしている。
    public const string HighScoreSaveKey = "HighScore";

    // シーン遷移後の ResultManager が参照できるよう static で保持する。
    // DontDestroyOnLoad を使わずにデータを渡すための軽量な手段。
    public static int LastScore { get; private set; }

    private const int InitialValue = 0;
    private const int ComboMultiplierBase = 1;

    // 2コンボ以上から倍率が上がる仕様にしている。
    // 1タップ目からボーナスが付くと初心者でも高得点になりすぎるため、この閾値を設けている。
    private const int MinimumComboForBonus = 2;

    // 現在のスコアとコンボ
    private int currentScore;
    private int currentCombo;

    // スコアのアニメーションを管理するコルーチン。
    // 新しい加算が来た場合は前回の演出を停止し、表示を競合させないため保持している。
    private Coroutine scoreAnimationCoroutine;

    // スコア文字の初期位置を保存する。
    // ジャンプ後に元の座標へ戻すために使用する。
    private Vector3 scoreTextDefaultPosition;

    // スコアの増加演出にかける時間。
    // 長すぎるとテンポが悪くなるため短めに設定している。
    [SerializeField]
    private float scoreAnimationDuration = 0.25f;

    // ジャンプする高さ。
    // 少しだけ動かすことで視認性を上げつつ邪魔にならない演出にする。
    [SerializeField]
    private float scoreAndComboJumpHeight = 20f;

    // スコア文字の初期スケールを保存する。
    // 演出終了後に元のサイズへ正確に戻すため保持している。
    private Vector3 scoreAndComboTextDefaultScale;

    // スコア文字の初期色を保存する。
    // 一時的に色を変えてもデザインを崩さないようにするため。
    private Color scoreAndComboTextDefaultColor;

    // スコア演出時の最大拡大率。
    // 少しだけ拡大することで加点を強調しつつ視認性を保つ。
    [SerializeField]
    private float scorePopScale = 1.2f;

    // スコア演出時に変化させる色。
    // 黄色にすることでプレイヤーへ「加点された」ことを瞬時に伝える。
    [SerializeField]
    private Color scoreHighlightColor = Color.yellow;

    // コンボ演出用の変数
    private Coroutine comboAnimationCoroutine;
    private Vector3 comboTextDefaultPosition;
    private Vector3 comboTextDefaultScale;
    private Color comboTextDefaultColor;

    [SerializeField] private Color comboColor10 = Color.yellow;
    [SerializeField] private Color comboColor20 = new Color(1.0f, 0.5f, 0.0f); // 橙色
    [SerializeField] private Color comboColor30 = Color.red;

    private void Awake()
    {
        // シングルトンの重複防止。
        // 同名クラスが2つ存在すると、どちらの Instance が使われるか不定になるため。
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
        }

        // スコア表示の初期位置を保存する。
        // アニメーション終了後に毎回同じ位置へ戻せるようにするため。
        if (scoreText != null)
        {
            scoreTextDefaultPosition = scoreText.rectTransform.localPosition;

            // UI の初期状態を保存する。
            // 演出終了後に毎回同じ見た目へ戻すため。
            scoreAndComboTextDefaultScale = scoreText.rectTransform.localScale;
            scoreAndComboTextDefaultColor = scoreText.color;
        }

        if (comboText != null)
        {
            comboTextDefaultPosition = comboText.rectTransform.localPosition;
            comboTextDefaultScale = comboText.rectTransform.localScale;
            comboTextDefaultColor = comboText.color;
        }
    }

    private void OnDestroy()
    {
        // 破棄後に古い参照が残ると null 参照エラーの原因になるためクリアする。
        if (Instance == this)
        {
            Instance = null;
        }
    }

    /// <summary>
    /// ゲームセッション開始時にスコアとコンボを初期状態に戻す。
    /// GameManager の InitializeGame() から呼ばれることを想定している。
    /// </summary>
    public void ResetScore()
    {
        currentScore = InitialValue;
        LastScore = InitialValue; // シーン遷移後の ResultManager 用にリセットしておく。
        currentCombo = InitialValue;

        if (scoreAnimationCoroutine != null)
        {
            StopCoroutine(scoreAnimationCoroutine);
            scoreAnimationCoroutine = null;
            if (scoreText != null)
            {
                scoreText.rectTransform.localPosition = scoreTextDefaultPosition;
                scoreText.rectTransform.localScale = scoreAndComboTextDefaultScale;
                scoreText.color = scoreAndComboTextDefaultColor;
            }
        }

        if (comboAnimationCoroutine != null)
        {
            StopCoroutine(comboAnimationCoroutine);
            comboAnimationCoroutine = null;
            if (comboText != null)
            {
                comboText.rectTransform.localPosition = comboTextDefaultPosition;
                comboText.rectTransform.localScale = comboTextDefaultScale;
            }
        }

        // 初期化直後の UI 表示を「0」に更新する。
        UpdateScoreUI();
        UpdateComboUI();
    }

    /// <summary>
    /// タップ成功時にコンボをインクリメントし、コンボ倍率を乗算したスコアを加算する。
    /// 「タップ成功 = コンボが続いている」という状態を記録する役割も兼ねている。
    /// </summary>
    /// <param name="basePoints">この円をタップしたときのベースポイント。円ごとに異なってもよい。</param>
    public void AddScoreWithCombo(int basePoints)
    {
        // タップするたびにコンボを1増やす。
        currentCombo++;

        // コンボ倍率を決定する。
        // 閾値未満のコンボ（最初の1タップ）では1倍のまま、閾値以上でコンボ数を倍率にする。
        // これにより「続けるほど得点効率が上がる」という明確なゲーム性を生み出している。
        int multiplier = currentCombo >= MinimumComboForBonus ? currentCombo : ComboMultiplierBase;
        int pointsToAdd = basePoints * multiplier;
        int previousScore = currentScore;

        currentScore += pointsToAdd;

        // シーン遷移後の ResultManager がこの値を参照できるよう、常に最新値を同期する。
        LastScore = currentScore;

        // スコア更新時に数値演出とジャンプ演出を同時に開始する。
        // 演出開始中にさらに加点された場合は最新状態へ更新し直す。
        if (scoreAnimationCoroutine != null)
        {
            StopCoroutine(scoreAnimationCoroutine);
        }

        scoreAnimationCoroutine =
            StartCoroutine(AnimateScore(previousScore, currentScore));
        
        UpdateComboUI();
        UpdateScoreUI();

        // 10コンボごとにジャンプ演出を行う
        if (currentCombo > 0 && currentCombo % 10 == 0)
        {
            if (comboAnimationCoroutine != null)
            {
                StopCoroutine(comboAnimationCoroutine);
            }
            comboAnimationCoroutine = StartCoroutine(AnimateCombo());
        }
    }

    /// <summary>
    /// 円が画面外に落ちた（ミスした）ときにコンボをゼロに戻す。
    /// スコア自体は減らさず、これ以降のタップ加算を「1倍スタート」に戻すのが目的。
    /// DeadZone から呼ばれることを想定している。
    /// </summary>
    public void ResetCombo()
    {
        currentCombo = InitialValue;

        if (comboAnimationCoroutine != null)
        {
            StopCoroutine(comboAnimationCoroutine);
            comboAnimationCoroutine = null;
            if (comboText != null)
            {
                comboText.rectTransform.localPosition = comboTextDefaultPosition;
                comboText.rectTransform.localScale = comboTextDefaultScale;
            }
        }

        // コンボが消えたことをプレイヤーが画面で確認できるよう、即座に UI を更新する。
        UpdateComboUI();
    }

    /// <summary>
    /// ゲーム終了時に、現セッションのスコアが歴代最高を上回っていれば永続保存する。
    /// HandleTimeUp() から呼ばれることを想定している。
    /// </summary>
    public void SaveHighScore()
    {
        int savedHighScore = PlayerPrefs.GetInt(HighScoreSaveKey, InitialValue);

        // 記録を更新した場合のみ書き込む。
        // PlayerPrefs.Save() はディスク I/O を伴うため、必要なときだけ呼ぶ。
        if (currentScore > savedHighScore)
        {
            PlayerPrefs.SetInt(HighScoreSaveKey, currentScore);
            PlayerPrefs.Save();
        }
    }

    /// <summary>
    /// スコアの現在値を画面上のテキストに反映させる。
    /// 表示ロジックをここに集中させることで、スコアが変わるたびに各所で text を書かずに済む。
    /// </summary>
    private void UpdateScoreUI()
    {
        // UI オブジェクトが未設定でもクラッシュしないよう、null チェックを入れている。
        // デバッグ中に UI を省いた状態でプレイしたいケースがあるため。
        if (scoreText != null)
        {
            scoreText.text = currentScore.ToString();
        }
    }
    /// <summary>
    /// コンボの現在値を画面上のテキストに反映させる。
    /// 表示ロジックをここに集中させることで、スコアが変わるたびに各所で text を書かずに済む。
    /// </summary>
    private void UpdateComboUI()
    {
        if (comboText != null)
        {
            // コンボが閾値未満のときはコンボ表示を空にしてUIをすっきり保つ。
            // 「0 COMBO!」のような意味のない表示を出さないための条件分岐。
            comboText.text =
                currentCombo >= MinimumComboForBonus
                ? $"{currentCombo} COMBO!"
                : string.Empty;

            // コンボ数に応じて色を変化させる
            if (currentCombo >= 30)
            {
                comboText.color = comboColor30;
            }
            else if (currentCombo >= 20)
            {
                comboText.color = comboColor20;
            }
            else if (currentCombo >= 10)
            {
                comboText.color = comboColor10;
            }
            else
            {
                comboText.color = comboTextDefaultColor;
            }
        }
    }


    /// <summary>
    /// スコア更新時の演出を行う。
    /// 数値を徐々に増やしながら文字を軽くジャンプさせることで、
    /// 加点をプレイヤーへ分かりやすく伝えることを目的としている。
    /// </summary>
    private IEnumerator AnimateScore(int startScore, int endScore)
    {
        // UI未設定時でもゲームが停止しないようにする。
        if (scoreText == null)
        {
            yield break;
        }

        RectTransform rectTransform = scoreText.rectTransform;

        float elapsedTime = 0f;

        while (elapsedTime < scoreAnimationDuration)
        {
            elapsedTime += Time.deltaTime;

            float progress = Mathf.Clamp01(elapsedTime / scoreAnimationDuration);

            // 数値を徐々に増やすことで、加点を視覚的に認識しやすくする。
            int displayScore =
                Mathf.RoundToInt(Mathf.Lerp(startScore, endScore, progress));

            scoreText.text = displayScore.ToString();

            // サイン波を利用して自然なジャンプに見せる。
            float jumpOffset =
                Mathf.Sin(progress * Mathf.PI) * scoreAndComboJumpHeight;

            rectTransform.localPosition =
                scoreTextDefaultPosition + Vector3.up * jumpOffset;

            // 拡大縮小を加えることで、スコア増加時のインパクトを強調する。
            // ジャンプと同じタイミングで最大サイズになるようサイン波を利用している。
            float scaleValue =
                Mathf.Lerp(1.0f, scorePopScale, Mathf.Sin(progress * Mathf.PI));

            rectTransform.localScale =
                scoreAndComboTextDefaultScale * scaleValue;

            // 色を一瞬だけ黄色へ変化させることで、
            // プレイヤーが加点されたことを直感的に認識できるようにする。
            scoreText.color =
                Color.Lerp(scoreAndComboTextDefaultColor,
                           scoreHighlightColor,
                           Mathf.Sin(progress * Mathf.PI));

            yield return null;
        }

        // 最終状態を保証する。
        // 演出途中で終了してもUIが崩れないよう初期状態へ戻している。
        scoreText.text = endScore.ToString();

        rectTransform.localPosition = scoreTextDefaultPosition;
        rectTransform.localScale = scoreAndComboTextDefaultScale;
        scoreText.color = scoreAndComboTextDefaultColor;

        scoreAnimationCoroutine = null;
    }

    /// <summary>
    /// 10コンボごとのジャンプ演出を行う。
    /// スコアのジャンプと同じ動き（サイン波での移動と拡大）を適用する。
    /// </summary>
    private IEnumerator AnimateCombo()
    {
        if (comboText == null)
        {
            yield break;
        }

        RectTransform rectTransform = comboText.rectTransform;
        float elapsedTime = 0f;

        while (elapsedTime < scoreAnimationDuration)
        {
            elapsedTime += Time.deltaTime;
            float progress = Mathf.Clamp01(elapsedTime / scoreAnimationDuration);

            // サイン波を利用して自然なジャンプに見せる。
            float jumpOffset = Mathf.Sin(progress * Mathf.PI) * scoreAndComboJumpHeight;
            rectTransform.localPosition = comboTextDefaultPosition + Vector3.up * jumpOffset;

            // 拡大縮小を加えることで、インパクトを強調する。
            float scaleValue = Mathf.Lerp(1.0f, scorePopScale, Mathf.Sin(progress * Mathf.PI));
            rectTransform.localScale = comboTextDefaultScale * scaleValue;

            yield return null;
        }

        // 演出途中で終了してもUIが崩れないよう初期状態へ戻す。
        rectTransform.localPosition = comboTextDefaultPosition;
        rectTransform.localScale = comboTextDefaultScale;

        comboAnimationCoroutine = null;
    }
}