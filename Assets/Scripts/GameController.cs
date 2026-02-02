using UnityEngine;
using UnityEngine.SceneManagement;

public class GameController : MonoBehaviour
{
    [Header("Players")]
    public BoardManager player1; // プレイヤー1のボード
    public BoardManager player2; // プレイヤー2のボード

    [Header("UI")]
    public GameObject titlePanel; // タイトル画面
    public GameObject helpPanel;  // 説明画面

    void Start()
    {
        // ゲーム開始時：タイトルを表示してゲームを止めておく
        ShowTitle();
    }

    // --- タイトル・説明画面の制御 ---
    public void ShowTitle()
    {
        if (titlePanel != null) titlePanel.SetActive(true);
        if (helpPanel != null) helpPanel.SetActive(false);
    }

    // スタートボタンが押されたら呼ばれる
    public void OnStartButtonClicked()
    {
        if (titlePanel != null) titlePanel.SetActive(false);
        if (helpPanel != null) helpPanel.SetActive(false);

        // ★ここで両方のプレイヤーに「開始！」と合図を送る
        if (player1 != null) player1.GameStart();
        if (player2 != null) player2.GameStart();
    }

    public void OnHelpButtonClicked()
    {
        if (helpPanel != null) helpPanel.SetActive(true);
    }

    public void OnCloseHelpButtonClicked()
    {
        if (helpPanel != null) helpPanel.SetActive(false);
    }

    // リスタートボタン用（シーン全体を読み込み直す）
    public void OnRetryButtonClicked()
    {
        // 今開いているシーンの名前を取得して、もう一度読み込み直す（＝リセット）
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }
}