using UnityEngine;

/// <summary>
/// スプライトを時間経過とともに徐々にフェードアウトさせ、完了時にオブジェクトを破棄するクラス。
/// 演出用の一時オブジェクトをメモリから自動的に解放する役割を持つ。
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
public class SpriteFadeOut : MonoBehaviour
{
    private SpriteRenderer spriteRenderer;
    private float fadeDuration;
    private float elapsedTime;

    /// <summary>
    /// フェードアウトの初期設定を行い、処理を開始する。
    /// </summary>
    /// <param name="duration">フェードアウト完了までの時間（秒）。</param>
    public void Initialize(float duration)
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        
        // ゼロ除算による表示バグを防止するための安全設計
        fadeDuration = Mathf.Max(0.001f, duration);
        elapsedTime = 0f;
    }

    private void Update()
    {
        // 1. 経過時間を記録する
        elapsedTime += Time.deltaTime;

        // 2. アルファ値を徐々に減衰させてスプライトを透明にする
        Color color = spriteRenderer.color;
        color.a = Mathf.Lerp(1f, 0f, elapsedTime / fadeDuration);
        spriteRenderer.color = color;

        // 3. フェードアウトが完了したらオブジェクトを破棄してメモリを解放する
        if (elapsedTime >= fadeDuration)
        {
            Destroy(gameObject);
        }
    }
}
