using UnityEngine;

/// <summary>
/// 円（TargetCircle）をゲーム画面に生成（スポーン）するクラス。
/// 「いつ円を出すか」という生成タイミングと「どこに出すか」という画面内ランダム座標の計算を担当し、
/// 円自体の挙動は CircleController、消滅は DeadZone に委譲している。
///
/// 生成条件は2つある：
///   条件A: 円が消滅した（寿命タイムアウト、またはタップ成功）とき → HandleCircleDestroyed() 経由
///   条件B: 前回の生成から一定時間が経過したとき → Update() 内のタイマーで管理
/// </summary>
public class SpawnerManager : MonoBehaviour
{
    // どのシーンからでも生成を依頼できるよう、シングルトンとして公開する。
    public static SpawnerManager Instance { get; private set; }

    // Prefab と生成位置は Inspector で差し替えられるようにしておく。
    // ハードコードすると、デザイン変更時にスクリプトを直す必要が生じてしまうため。
    [SerializeField] private GameObject circlePrefab;
    [SerializeField] private Transform spawnPoint;

    // 画面端ギリギリでの生成を防ぐためのパディング割合（0.0〜1.0）。
    // 値が大きいほど、画面中央寄りに生成される。
    [SerializeField] private float spawnPaddingRatio = 0.2f;

    [Header("難易度調整パラメータ")]
    // ゲーム開始時の円の出現間隔（秒）。
    [SerializeField] private float initialSpawnInterval = 3.0f;
    // 最小の出現間隔（秒）（これ以上は早くならない）。
    [SerializeField] private float minimumSpawnInterval = 0.8f;
    // 1秒経過するごとに出現間隔がどれくらい短縮されるか（秒/秒）。
    [SerializeField] private float spawnIntervalDecreaseRate = 0.05f;

    // ゲーム開始時の円の寿命（秒）。
    [SerializeField] private float initialCircleLifetime = 3.0f;
    // 最小の円の寿命（秒）（これ以上は短くならない）。
    [SerializeField] private float minimumCircleLifetime = 1.0f;
    // 1秒経過するごとに寿命がどれくらい短縮されるか（秒/秒）。
    [SerializeField] private float circleLifetimeDecreaseRate = 0.04f;

    // ゲーム開始からの累計経過時間（難易度上昇の基準値）。
    private float elapsedGameTime = 0f;

    // 10秒経過するごとにオブジェクトを追加するためのしきい値
    private float nextAdditionalSpawnTime = 10f;

    private void Awake()
    {
        // シングルトンの重複を防ぐ。
        // シーン再読み込み時に2つ目の SpawnerManager が生まれても、古い方が残るよう設計している。
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
            return;
        }

    }

    private void Start()
    {
        // ゲーム開始時に最初の円を生成する。
        SpawnCircle();
    }

    private void OnDestroy()
    {
        // このオブジェクトが破棄された際、古い参照が残らないようクリアする。
        // 残っていると次シーン以降で null 参照エラーの原因になる。
        if (Instance == this)
        {
            Instance = null;
        }
    }

    private void Update()
    {
        // ゲームが一時停止中（timeScale=0）はタイマーおよび難易度カウントを進めない。
        // 結果画面への遷移中に余分な円が生成されないための措置。
        if (Time.timeScale == 0f) return;

        // ゲームの経過時間を加算し、難易度上昇の基準を進める。
        elapsedGameTime += Time.deltaTime;

        // 10秒経過（残り時間が10秒減る）ごとに、無条件でオブジェクトを1つ追加する
        if (elapsedGameTime >= nextAdditionalSpawnTime)
        {
            SpawnCircle();
            nextAdditionalSpawnTime += 10f;
        }
    }

    /// <summary>
    /// ゲーム経過時間に基づいて、現在の円の生存可能時間（寿命）を計算する。
    /// 時間が進むにつれて寿命が短くなり、タップの猶予時間が厳しくなる。
    /// </summary>
    /// <returns>現在の円の寿命（秒）。</returns>
    private float CalculateCurrentCircleLifetime()
    {
        float lifetime = initialCircleLifetime - (elapsedGameTime * circleLifetimeDecreaseRate);
        return Mathf.Max(minimumCircleLifetime, lifetime);
    }

    /// <summary>
    /// 円を1個インスタンス化し、タイマーをリセットする内部処理。
    /// 生成した円に対して現在の難易度に応じた寿命を設定し、初期化を行う。
    /// </summary>
    private void SpawnCircle()
    {
        // Prefab の設定忘れを実行時に素早く気づけるよう、エラーログを出力する。
        if (circlePrefab == null)
        {
            Debug.LogError("[SpawnerManager] circlePrefab が Inspector で設定されていません。");
            return;
        }

        // 画面内のランダムな位置を算出して生成先とする。
        Vector3 spawnPosition = GetRandomSpawnPosition();

        GameObject circleObject = Instantiate(circlePrefab, spawnPosition, Quaternion.identity);

        // 生成した円のコントローラーを取得し、現在の難易度に基づく寿命をアサインする。
        CircleController circleController = circleObject.GetComponent<CircleController>();
        if (circleController != null)
        {
            float currentLifetime = CalculateCurrentCircleLifetime();
            circleController.Initialize(currentLifetime);
        }
    }

    /// <summary>
    /// 画面内のランダムなワールド座標を計算して返す。
    /// メインカメラのサイズとアスペクト比を基に、画面内に収まる範囲を決定する。
    /// </summary>
    /// <returns>画面内のランダムな座標（Z座標は0）。</returns>
    private Vector3 GetRandomSpawnPosition()
    {
        Camera mainCamera = Camera.main;

        // カメラが存在しない場合は、フォールバックとして spawnPoint またはデフォルトの座標を使用する。
        if (mainCamera == null)
        {
            // spawnPoint が未設定の場合は画面上方のデフォルト位置にフォールバックする。
            return spawnPoint != null ? spawnPoint.position : Vector3.up * 5f;
        }

        // カメラの orthographicSize は画面中央から上端までのワールド座標単位の高さ。
        float verticalSize = mainCamera.orthographicSize;
        // 幅は高さをアスペクト比で乗算して求める。
        float horizontalSize = verticalSize * mainCamera.aspect;

        // 画面端に見切れるのを防ぐための余白（マージン）を計算。
        float marginX = horizontalSize * spawnPaddingRatio;
        float marginY = verticalSize * spawnPaddingRatio;

        // 計算した範囲内でランダムな X, Y 座標を決定する。
        float randomX = Random.Range(-horizontalSize + marginX, horizontalSize - marginX);
        float randomY = Random.Range(-verticalSize + marginY, verticalSize - marginY);

        // カメラ自体の位置も加味することで、カメラが原点 (0, 0) から動いていても正しい位置に出現する。
        Vector3 cameraPosition = mainCamera.transform.position;
        return new Vector3(cameraPosition.x + randomX, cameraPosition.y + randomY, 0f);
    }

    /// <summary>
    /// 条件A: 円が消滅（寿命タイムアウト、またはプレイヤーによるタップ消去）したときに呼び出されるメソッド。
    /// 「円が消えたので補充する」という役割を担う。
    /// </summary>
    public void HandleCircleDestroyed()
    {
        SpawnCircle();
    }
}