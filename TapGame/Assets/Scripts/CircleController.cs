using UnityEngine;

/// <summary>
/// 円（TargetCircle）1個分の挙動を管理するクラス。
/// タップ検出・消滅・スコア加算の「1回分の操作」を責任範囲とし、
/// 生成や消滅の管理は SpawnerManager / DeadZone に委ねている。
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Collider2D))]
public class CircleController : MonoBehaviour
{
    // コンボ倍率はScoreManagerが計算するため、ここでは基礎点のみ持つ。
    // 円ごとに点数を変えたい場合もここを変えるだけで対応できる。
    [SerializeField] private int baseScoreValue = 10;

    // 出現演出にかける時間（秒）。
    [SerializeField] private float appearanceDuration = 0.25f;

    // タップされて消滅する際に生成するパーティクルエフェクトのプレハブ。
    [SerializeField] private GameObject tapEffectPrefab;

    // タップされて消滅する際に描画する、風船が破裂したスプライト。
    [SerializeField] private Sprite burstSprite;

    // 破裂画像が完全に消えるまでの時間（秒）。
    [SerializeField] private float burstFadeDuration = 0.5f;

    // 破裂画像の描画サイズ。エディタから拡大縮小してサイズ調整ができるようにする。
    [SerializeField] private Vector3 burstScale = Vector3.one;

    private Camera mainCamera;
    private SpriteRenderer spriteRenderer;
    private Vector3 originalLocalScale;
    private float lifetimeLimit;
    private const float ShrinkThresholdRatio = 0.2f;

    [Header("Floating Animation")]
    [SerializeField] private bool enableFloating = true;
    [SerializeField] private float floatSpeed = 2f;
    [SerializeField] private float floatAmplitude = 0.5f;

    private Vector3 initialPosition;

    // 消滅演出がすでに開始しているかどうかのフラグ。
    // 連打などにより1つの円に対して2回タップ処理が走るのを防止する。
    private bool isDisappearing = false;

    private void Awake()
    {
        // GetComponent はコストが高いため、初回のみ取得してフィールドにキャッシュする。
        mainCamera = Camera.main;
        spriteRenderer = GetComponent<SpriteRenderer>();

        // インスペクターで設定されている既定のサイズを基準値として記憶しておく。
        originalLocalScale = transform.localScale;
    }

    private void Start()
    {
        initialPosition = transform.position;
        // 生成された直後に、小さなサイズから大きくなる出現アニメーションを開始する。
        StartCoroutine(AnimateAppearance());
    }

    private void Update()
    {
        // ゲームが一時停止中（timeScale=0）はタップ判定を行わない。
        // 結果画面遷移中に誤って円がタップされるのを防ぐため。
        if (Time.timeScale == 0f) return;

        if (enableFloating && !isDisappearing)
        {
            float newY = initialPosition.y + Mathf.Sin(Time.time * floatSpeed) * floatAmplitude;
            transform.position = new Vector3(transform.position.x, newY, transform.position.z);
        }

        HandleTapInput();
    }

    /// <summary>
    /// 外部から寿命を注入するための初期化メソッド（新規追加）。
    /// </summary>
    /// <param name="limit">難易度に応じて計算された寿命（秒）</param>
    public void Initialize(float limit)
    {
        lifetimeLimit = limit;
        // ライフタイムの監視を開始する
        StartCoroutine(MonitorLifetime());
    }

    /// <summary>
    /// マウスとタッチ、両方の入力を受け付けるための振り分け処理。
    /// エディタ開発（マウス）とモバイル実機（タッチ）の両方で動作させるために共存させている。
    /// </summary>
    private void HandleTapInput()
    {
        // --- マウス入力（Unity エディタ・PC ビルド向け） ---
        if (Input.GetMouseButtonDown(0))
        {
            // スクリーン座標をワールド座標に変換してから当たり判定を行う。
            // UI 座標系とワールド座標系が異なるため、変換は必須。
            Vector2 tapWorldPos = mainCamera.ScreenToWorldPoint(Input.mousePosition);
            TryTap(tapWorldPos);
            return; // マウスとタッチを同フレームで二重処理しないよう早期リターン。
        }

        // --- タッチ入力（スマートフォン・タブレット向け） ---
        if (Input.touchCount > 0)
        {
            // 複数本指タップに対応するため、すべての指を個別に処理する。
            foreach (Touch touch in Input.touches)
            {
                // Began フェーズのみ処理することで、長押し中の連続処理を防ぐ。
                if (touch.phase == TouchPhase.Began)
                {
                    Vector2 tapWorldPos = mainCamera.ScreenToWorldPoint(touch.position);
                    TryTap(tapWorldPos);
                }
            }
        }
    }

    /// <summary>
    /// 指定されたワールド座標にタップが当たっているか判定し、
    /// ヒットしていた場合はスコア加算とオブジェクトの消滅を行う。
    /// 「この円に当たったか」という判断責任をこのクラス自身が持つ。
    /// </summary>
    /// <param name="tapWorldPos">ワールド座標に変換済みのタップ位置。</param>
    private void TryTap(Vector2 tapWorldPos)
    {
        // 既に消滅プロセスが走っている場合は重ねて処理しない。
        if (isDisappearing) return;

        // Physics2D.OverlapPoint で点がコライダーと重なっているか高速に判定する。
        // Raycast と違い 2D 専用のため、2D 物理エンジンを使うこのプロジェクトに適している。
        Collider2D hitCollider = Physics2D.OverlapPoint(tapWorldPos);

        // 他の円やUIではなく「この円」に当たったときだけ処理する。
        // gameObject との同一性チェックにより、隣接する別の円への誤発火を防ぐ。
        if (hitCollider != null && hitCollider.gameObject == gameObject)
        {
            // 多重処理を防ぐフラグを立てる。
            isDisappearing = true;

            // 演出中に再度タップ判定が機能してしまわないよう、コライダーを無効化する。
            Collider2D colliderComponent = GetComponent<Collider2D>();
            if (colliderComponent != null)
            {
                colliderComponent.enabled = false;
            }

            // 演出中の円が重力で下へ落下し続けるのを防ぐため、物理演算のシミュレーションを一時停止し、その場で固定する。
            Rigidbody2D rigidbodyComponent = GetComponent<Rigidbody2D>();
            if (rigidbodyComponent != null)
            {
                rigidbodyComponent.simulated = false;
            }

            // ScoreManager が存在する場合のみ、スコアとコンボを加算する。（既存の連携を完全に維持）
            if (ScoreManager.Instance != null)
            {
                ScoreManager.Instance.AddScoreWithCombo(baseScoreValue);
            }

            // 円が消滅したので、SpawnerManager に通知して新しい円を補充する。（既存の連携を完全に維持）
            if (SpawnerManager.Instance != null)
            {
                SpawnerManager.Instance.HandleCircleDestroyed();
            }

            // 音声マネージャーが存在する場合は、風船が割れるSEを再生する。
            if (SoundManager.Instance != null)
            {
                SoundManager.Instance.PlaySeTap();
            }

            // タップ成功時のパーティクルエフェクトが設定されていれば生成する。
            if (tapEffectPrefab != null)
            {
                // 円の現在位置にエフェクトオブジェクトをインスタンス化する。
                GameObject effectInstance = Instantiate(tapEffectPrefab, transform.position, Quaternion.identity);

                // 設定ミスによるメモリリークを避けるため、一定時間（2.0秒）経過後に確実に自動破棄する。
                Destroy(effectInstance, 2.0f);
            }

            // 風船が破裂した画像が設定されている場合はエフェクトオブジェクトを生成する。
            if (burstSprite != null)
            {
                // 1. エフェクト表示用のゲームオブジェクトを動的に作成し、フェードアウト用コンポーネントを追加する。
                // 円自体が即座にDestroyされるため、演出専用の独立したオブジェクトを作る必要がある。
                GameObject burstObject = new GameObject("BalloonBurstEffect", typeof(SpriteRenderer), typeof(SpriteFadeOut));

                // 2. 位置とサイズ（スケール）をエディタで設定された値に合わせる。
                burstObject.transform.position = transform.position;
                burstObject.transform.localScale = burstScale;

                // 3. スプライト画像を設定する。
                SpriteRenderer effectRenderer = burstObject.GetComponent<SpriteRenderer>();
                effectRenderer.sprite = burstSprite;
                effectRenderer.sortingLayerID = spriteRenderer.sortingLayerID;
                effectRenderer.sortingOrder = spriteRenderer.sortingOrder;

                // 4. フェードアウトコンポーネントを初期化して演出を開始する。
                SpriteFadeOut fadeOutComponent = burstObject.GetComponent<SpriteFadeOut>();
                fadeOutComponent.Initialize(burstFadeDuration);
            }

            // 【指示による変更】消滅アニメーションのコルーチン呼び出しを廃止し、即座にオブジェクトを破棄する
            Destroy(gameObject);
        }
    }

    /// <summary>
    /// 生成時、円が小さなサイズ（スケール0）から既定のサイズへ徐々に拡大する演出を行うコルーチン。
    /// </summary>
    private System.Collections.IEnumerator AnimateAppearance()
    {
        float elapsedTime = 0f;

        // 出現アニメーションの開始時はスケールを完全にゼロにする。
        transform.localScale = Vector3.zero;

        // 設定された演出時間の間、フレーム補間で徐々にスケールを元のサイズに戻す。
        while (elapsedTime < appearanceDuration)
        {
            elapsedTime += Time.deltaTime;
            transform.localScale = Vector3.Lerp(Vector3.zero, originalLocalScale, elapsedTime / appearanceDuration);
            yield return null;
        }

        // ループ後の極小の補間誤差をリセットし、確実に既定のサイズに着地させる。
        transform.localScale = originalLocalScale;
    }

    /// <summary>
    /// ゲーム内時間（Time.deltaTime）で生存時間をカウントするコルーチン（新規追加）。
    /// 寿命の終盤（残り時間 20% の期間）に入ると、サイズを originalLocalScale から Vector3.zero に向けて Lerp で縮小させる。
    /// タップされずに寿命が尽きた場合、ミスとして各マネージャーに通知した上で自身を破棄する。
    /// </summary>
    private System.Collections.IEnumerator MonitorLifetime()
    {
        float elapsedTime = 0f;

        // 縮小演出を開始するタイミング（秒）を計算
        float shrinkStartTime = lifetimeLimit * (1f - ShrinkThresholdRatio);

        while (elapsedTime < lifetimeLimit)
        {
            // ゲーム内時間（Time.deltaTime）で生存時間をカウント
            elapsedTime += Time.deltaTime;

            // 寿命の終盤（残り時間 20% の期間）に入った場合
            if (elapsedTime >= shrinkStartTime)
            {
                // 縮小フェーズ内での経過割合（0.0 〜 1.0）を計算
                float shrinkProgress = (elapsedTime - shrinkStartTime) / (lifetimeLimit - shrinkStartTime);

                // サイズを originalLocalScale から Vector3.zero に向けて Lerp で縮小
                transform.localScale = Vector3.Lerp(originalLocalScale, Vector3.zero, shrinkProgress);
            }

            yield return null;
        }

        // 既にプレイヤーのタップによって破棄・消滅プロセスが開始している場合は二重処理を避ける
        if (isDisappearing) yield break;

        // タイムアウトによる消滅フラグを立てる
        isDisappearing = true;

        // タップされずに寿命が尽きた場合（ミス時の処理）
        // 1. ScoreManager にコンボリセットを通知
        if (ScoreManager.Instance != null)
        {
            ScoreManager.Instance.ResetCombo();
        }

        // 2. SpawnerManager に補充を通知
        if (SpawnerManager.Instance != null)
        {
            SpawnerManager.Instance.HandleCircleDestroyed();
        }

        // 3. 自身を Destroy
        Destroy(gameObject);
    }
}