using UnityEngine;

/// <summary>
/// ゲーム内の音声（BGMおよびSE）の再生を一元管理するクラス。
/// サウンド関係の処理をこのクラスに集約することで、各クラスの責務を明確にする。
/// </summary>
public class SoundManager : MonoBehaviour
{
    // 他のクラスから簡単にアクセスできるよう、シングルトンとして公開する。
    public static SoundManager Instance { get; private set; }

    // BGM再生用のオーディオソース。
    // Inspectorから設定することで、ボリュームなどの細かな調整を可能にする。
    [SerializeField] private AudioSource bgmSource;
    
    // SE再生用のオーディオソース。
    // BGM用とは分けることで、SEだけ音量を変えたり同時に鳴らしたりしやすくする。
    [SerializeField] private AudioSource seSource;

    // ゲーム中に流れるメインBGMのクリップ。
    [SerializeField] private AudioClip bgmMain;
    
    // 風船をタップして割った際のSEクリップ。
    [SerializeField] private AudioClip seTap;

    // ゲーム終了時（タイムアップ）に再生するホイッスルのSEクリップ。
    [SerializeField] private AudioClip seWhistle;

    private void Awake()
    {
        // シングルトンの重複防止処理。
        // 同一シーン内に複数存在する場合は後から生成されたものを破棄する。
        if (Instance == null)
        {
            Instance = this;
            
            // シーン遷移後もBGMを途切れさせず鳴らし続けるため、破棄されないようにする。
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void Start()
    {
        // ゲームシーンが開始されたタイミングで、BGMを自動再生する。
        PlayBgmMain();
    }

    private void OnDestroy()
    {
        // オブジェクト破棄時に古い参照が残らないようクリアする。
        if (Instance == this)
        {
            Instance = null;
        }
    }

    /// <summary>
    /// メインBGMの再生を開始する。
    /// </summary>
    public void PlayBgmMain()
    {
        if (bgmSource != null && bgmMain != null)
        {
            bgmSource.clip = bgmMain;
            // BGMはループ再生させる必要があるため、loopフラグをオンにする。
            bgmSource.loop = true;
            bgmSource.Play();
        }
    }

    /// <summary>
    /// 風船をタップした際のSEを再生する。
    /// 短い間に複数回タップされる可能性を考慮し、PlayOneShotを使用して音が途切れないようにする。
    /// </summary>
    public void PlaySeTap()
    {
        if (seSource != null && seTap != null)
        {
            seSource.PlayOneShot(seTap);
        }
    }

    /// <summary>
    /// ゲーム終了時（タイムアップ）のホイッスルSEを再生する。
    /// </summary>
    public void PlaySeWhistle()
    {
        if (seSource != null && seWhistle != null)
        {
            seSource.PlayOneShot(seWhistle);
        }
    }
}
