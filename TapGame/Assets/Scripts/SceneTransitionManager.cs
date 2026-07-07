using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// フェードイン・フェードアウト演出を使ってシーンを切り替えるクラス。
/// SceneManager.LoadScene を直接呼ぶと瞬間切り替えになり体験が悪くなるため、
/// このクラスを介することで視覚的なつなぎを提供している。
///
/// DontDestroyOnLoad でシーンをまたいで生存することで、
/// フェードアウト中にシーンが切り替わっても Image の参照が失われないようにしている。
/// </summary>
public class SceneTransitionManager : MonoBehaviour
{
    // どのシーンからでも遷移を依頼できるよう、シングルトンとして公開する。
    public static SceneTransitionManager Instance { get; private set; }

    // フェード用の全画面 Image。黒背景が徐々に透明になる / なる演出に使う。
    // Canvas の最前面に配置しておくことでゲームオブジェクトを覆い隠せる。
    [SerializeField] private Image fadeImage;

    // フェードの長さを Inspector で調整できるようにする。
    // 短すぎると演出が気づかれず、長すぎるとテンポが悪くなるため、
    // プロジェクトに合わせて調整できる柔軟性を持たせている。
    [SerializeField] private float fadeDuration = 1.0f;

    // フェードするときの画像が常に最前面に描画されるよう、Canvas の設定を調整する変数。
    [SerializeField] private int fadeCanvasSortingOrder = 999;

    // フェード画像の透明を定義する変数。0=完全透明、1=完全不透明。
    [SerializeField] private float fadeAlphaOpaque = 1f;
    [SerializeField] private float fadeAlphaTransparent = 0f;

    // 遷移中の多重呼び出しを防ぐフラグ。
    // フェード中に別のボタンが押されても2重に遷移が始まらないようにするための安全弁。
    private bool isTransitioning = false;

    private void Awake()
    {
        // シングルトンとして登録し、シーンをまたいで生存させる。
        // DontDestroyOnLoad しないと、シーン遷移中にこのオブジェクトが
        // 破棄されてフェード演出が途中で止まってしまう。
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);

            // フェード画像が他のUI（タイトルテキストやボタンなど）よりも必ず手前に描画されるように調整する。
            SetupFadeCanvas();
        }
        else
        {
            // 2つ目以降のインスタンスは不要なので即破棄する。
            // シーンを再ロードすると新しいインスタンスが生成されるが、
            // DontDestroyOnLoad で残った古いものを優先するための処理。
            Destroy(gameObject);
        }
    }

    /// <summary>
    /// フェード画像が常にすべてのUI要素の最前面に表示されるよう、 Canvas の設定を調整する。
    /// 他のUI Canvasよりも描画順（Sorting Order）が高くなるように設定し、
    /// 同一 Canvas 内での優先度（Sibling Index）も最前面に設定する。
    /// </summary>
    private void SetupFadeCanvas()
    {
        if (fadeImage == null) return;

        // フェード画像が配置されている親 Canvas を検索する。
        Canvas canvas = fadeImage.GetComponentInParent<Canvas>();
        if (canvas != null)
        {
            // 他のUI Canvas（タイトル画面など）より手前に重ねて描画させるため、描画優先度を十分に高い値にする。
            canvas.sortingOrder = fadeCanvasSortingOrder;
        }

        // 万が一同一 Canvas 内に他のUI要素が存在する場合に備え、描画順を最後（最前面）に配置する。
        fadeImage.transform.SetAsLastSibling();
    }

    /// <summary>
    /// フェード付きで指定シーンへ遷移する。外部から呼び出す唯一の公開メソッド。
    /// 内部ではコルーチンを使い、フェードアウト → ロード → フェードインの順に処理する。
    /// </summary>
    /// <param name="sceneName">遷移先のシーン名（Build Settings に登録されているもの）。</param>
    public void TransitionToScene(string sceneName)
    {
        // 既に遷移中なら何もしない。
        // ボタンを連打されたときに SceneManager.LoadScene が複数回呼ばれるのを防ぐ。
        if (isTransitioning) return;
        StartCoroutine(TransitionCoroutine(sceneName));
    }

    /// <summary>
    /// フェードアウト・シーンロード・フェードインを順番に実行するコルーチン。
    /// yield return で処理を一時停止できるコルーチンを使うことで、
    /// 非同期の演出をシンプルなフロー（上から下に読む）で記述できている。
    /// </summary>
    private IEnumerator TransitionCoroutine(string sceneName)
    {
        isTransitioning = true;

        // フェード中にプレイヤーがタップして円に触れないよう、
        // raycastTarget を true にして入力をブロックする。
        fadeImage.raycastTarget = true;

        // フェードアウト：透明(0) → 不透明(1)
        yield return StartCoroutine(Fade(fadeAlphaTransparent, fadeAlphaOpaque));

        // 画面が完全に黒くなってからシーンをロードすることで、
        // ロード中の一瞬だけ前のシーンが見えてしまう「チラつき」を防いでいる。
        yield return SceneManager.LoadSceneAsync(sceneName);

        // フェードイン：不透明(1) → 透明(0)
        yield return StartCoroutine(Fade(fadeAlphaOpaque, fadeAlphaTransparent));

        // 遷移が完全に終わったら入力を再び受け付ける。
        fadeImage.raycastTarget = false;
        isTransitioning = false;
    }

    /// <summary>
    /// fadeImage のアルファ値を指定した開始値から終了値へ、fadeDuration 秒かけて補間するコルーチン。
    /// フェードアウトとフェードインのどちらにも使えるよう、引数で向きを制御している。
    /// </summary>
    /// <param name="startAlpha">アルファの開始値（0=透明, 1=不透明）。</param>
    /// <param name="targetAlpha">アルファの終了値（0=透明, 1=不透明）。</param>
    private IEnumerator Fade(float startAlpha, float targetAlpha)
    {
        float elapsedTime = 0f;
        Color color = fadeImage.color;

        while (elapsedTime < fadeDuration)
        {
            // Time.deltaTime ではなく unscaledDeltaTime を使う。
            // timeScale が 0（ゲームポーズ中）でもフェードアニメーションが止まらないようにするため。
            // ゲーム終了時は timeScale を 0 にしているため、この選択が重要になる。
            elapsedTime += Time.unscaledDeltaTime;

            // Lerp で開始値から終了値を線形補間し、滑らかなフェード変化を作る。
            color.a = Mathf.Lerp(startAlpha, targetAlpha, elapsedTime / fadeDuration);
            fadeImage.color = color;

            // 1フレーム待機することで、毎フレーム少しずつアルファ値を変化させる。
            yield return null;
        }

        // Lerp は数値誤差で 100% に達しないことがあるため、
        // ループ後に目標値を確実にセットして完全な透明/不透明を保証する。
        color.a = targetAlpha;
        fadeImage.color = color;
    }
}