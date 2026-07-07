using UnityEngine;

/// <summary>
/// ボタンが押されたときのエフェクト（パーティクルと破裂画像のフェードアウト）を制御するクラス。
/// ボタンの見た目を切り替え、フェード演出を行った後にオブジェクトを整理する役割を持つ。
/// </summary>
public class ButtonEffectController : MonoBehaviour
{
    // インスペクターでパーティクルのプレハブを割り当てます
    // ボタン押下時に華やかな演出を出すために使用します
    [SerializeField] private GameObject particlePrefab;

    // インスペクターで風船が破裂した画像（スプライト）を割り当てます
    // ボタンの見た目を破裂した状態に切り替えるために使用します
    [SerializeField] private Sprite burstSprite;

    // 破裂画像が完全に消えるまでの時間（秒）を設定します
    // 演出の長さを調整して遷移のテンポと同期させます
    [SerializeField] private float fadeDuration = 0.5f;

    // 破裂画像のサイズをエディター側で調整するための変数を設定します
    [SerializeField] private Vector3 burstSize = new Vector3(100f, 100f, 1f);

    /// <summary>
    /// ボタンがクリックされたときに呼び出すメソッド。
    /// パーティクルを生成し、破裂画像のフェードアウトアニメーションを開始する。
    /// </summary>
    public void OnButtonClick()
    {
        // 1. パーティクルプレハブが設定されているか確認して生成する
        if (particlePrefab != null)
        {
            // UI Canvasのスケール干渉を防ぐため、親を指定せずにルートに生成する
            GameObject effect = Instantiate(particlePrefab, null);

            // エフェクトの位置をボタンと完全に一致させる
            effect.transform.position = transform.position;

            // UIのサイズスケールに影響されないようサイズをリセットする
            effect.transform.localScale = Vector3.one;
        }

        // 2. 破裂演出用の画像が設定されているか確認する
        if (burstSprite != null)
        {
            // 元のボタンのサイズを取得する
            RectTransform buttonRect = GetComponent<RectTransform>();
            Vector2 buttonSize = buttonRect != null ? buttonRect.sizeDelta : new Vector2(100f, 100f);

            // ボタン自体の画像とボタン機能を無効化する
            // オブジェクト自体を非アクティブにするとコルーチンが止まってしまうため、
            // コンポーネント単位で無効化して非表示・操作不能にする
            UnityEngine.UI.Image buttonImage = GetComponent<UnityEngine.UI.Image>();
            if (buttonImage != null)
            {
                buttonImage.enabled = false;
            }

            UnityEngine.UI.Button buttonComponent = GetComponent<UnityEngine.UI.Button>();
            if (buttonComponent != null)
            {
                buttonComponent.interactable = false;
            }

            // ボタンの子オブジェクトにあるテキストも即座に非表示にする
            TMPro.TextMeshProUGUI buttonText = GetComponentInChildren<TMPro.TextMeshProUGUI>();
            if (buttonText != null)
            {
                buttonText.enabled = false;
            }


            // フェードアウトコルーチンを開始する
            StartCoroutine(AnimateBurstEffect(transform.position, buttonSize));
        }
        else
        {
            // 破裂画像がない場合は、即座にボタンを非アクティブにして終了する
            gameObject.SetActive(false);
        }
    }

    /// <summary>
    /// 風船が破裂した画像を生成し、徐々にフェードアウトさせながら破棄するコルーチン。
    /// </summary>
    /// <param name="spawnPosition">破裂画像を配置するワールド座標。</param>
    /// <param name="originalSize">元のボタンのサイズ。</param>
    /// <returns>コルーチンの実行状態。</returns>
    private System.Collections.IEnumerator AnimateBurstEffect(Vector3 spawnPosition, Vector2 originalSize)
    {
        // 1. エフェクト表示用のゲームオブジェクトを動的に作成する
        // UIの描画順序を狂わせないため、ボタンと同じ親（Canvas）の下に生成する
        GameObject burstObject = new GameObject("BalloonBurstEffect", typeof(UnityEngine.UI.Image));
        burstObject.transform.SetParent(transform.parent, false);

        // 2. 位置、サイズ、スケールをボタンと完全に一致させる
        // 違和感なく破裂画像に切り替わるようにするため
        RectTransform rectTransform = burstObject.GetComponent<RectTransform>();
        rectTransform.position = spawnPosition;
        rectTransform.sizeDelta = originalSize;
        rectTransform.localScale = burstSize;

        // 3. 破裂画像のスプライトを割り当てる
        UnityEngine.UI.Image image = burstObject.GetComponent<UnityEngine.UI.Image>();
        image.sprite = burstSprite;

        // 4. フェードアウト処理
        // 指定された時間（fadeDuration）をかけて、徐々に透明にする
        float elapsedTime = 0f;
        Color color = image.color;
        while (elapsedTime < fadeDuration)
        {
            elapsedTime += Time.unscaledDeltaTime; // ポーズ中（timeScale=0）でも動くようにする
            float alpha = Mathf.Lerp(1f, 0f, elapsedTime / fadeDuration);
            color.a = alpha;
            image.color = color;
            yield return null;
        }

        // 5. 演出が終了したため、一時的なエフェクトオブジェクトを破棄する
        Destroy(burstObject);

        // 6. ボタン自体のオブジェクトも完全に非表示（または破棄）にする
        gameObject.SetActive(false);
    }
}