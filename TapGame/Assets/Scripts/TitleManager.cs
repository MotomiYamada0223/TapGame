using UnityEngine;

/// <summary>
/// タイトル画面の UI イベントを受け取り、ゲームへの遷移を開始するクラス。
/// タイトル画面固有の処理だけを担当し、実際のシーン切り替えは
/// SceneTransitionManager に委ねることで関心を分離している。
/// </summary>
public class TitleManager : MonoBehaviour
{
    // シーン名を定数化することで、タイポや変更漏れによるバグを防ぐ。
    // 文字列リテラルをコード中に直接書くと、シーン名変更時に全箇所を探して直す必要が生じる。
    private const string GameSceneName = "GameScene";

    /// <summary>
    /// スタートボタンが押されたときに Inspector の Button.OnClick() から呼ばれるメソッド。
    /// ゲームシーンへの遷移を開始する。
    /// </summary>
    public void OnStartButtonClicked()
    {
        // SceneTransitionManager が存在すればフェード付きの上品な遷移を行う。
        // 存在しない場合（直接このシーンを開いてテストしているときなど）は
        // SceneManager で即時ロードするフォールバックを設けることで、
        // どの環境でもボタンが機能するようにしている。
        if (SceneTransitionManager.Instance != null)
        {
            SceneTransitionManager.Instance.TransitionToScene(GameSceneName);
        }
        else
        {
            UnityEngine.SceneManagement.SceneManager.LoadScene(GameSceneName);
        }
    }
}