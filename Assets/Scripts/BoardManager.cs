using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro; 
using UnityEngine.SceneManagement; 

public class BoardManager : MonoBehaviour
{
    [Header("Settings")]
    public int width = 8;
    public int height = 12; // 画像に合わせて少し高くしました

    [Header("References")]
    public Transform leftWall;
    public Transform bottomLine;
    public GameObject stonePrefab;

    [Header("UI")]
    public TextMeshProUGUI resultText; 
    public GameObject restartButton;   

    [Header("Versus")]
    public BoardManager opponent;

    private Stone[,] grid;
    private int pendingGarbage = 0;

    public bool IsGameOver { get; private set; } = false;
    public bool IsBusy { get; private set; } = false;

    Color[] safeColors = new Color[]
    {
        Color.red, Color.blue, Color.green, Color.yellow,
        new Color(1f, 0.5f, 0f), Color.cyan, Color.magenta
    };

    void Awake()
    {
        grid = new Stone[width, height];
    }

    void Start()
    {
        if (resultText != null) resultText.gameObject.SetActive(false);
        if (restartButton != null) restartButton.SetActive(false);
        
        // ゲーム開始時に少しブロックを生成
        PushUp();
    }

    // --- 座標変換 ---
    public Vector3 GridToWorld(int x, int y)
    {
        return new Vector3(leftWall.position.x + x + 0.5f, bottomLine.position.y + y + 0.5f, 0);
    }

    public int WorldToGridX(float worldX)
    {
        return Mathf.FloorToInt(worldX - leftWall.position.x);
    }

    public bool IsInside(int x, int y)
    {
        return x >= 0 && x < width && y >= 0 && y < height;
    }

    // --- 石の移動 ---
    public void TryMoveStoneMultiple(Stone s, int dx)
    {
        if (dx == 0) return;
        int dir = dx > 0 ? 1 : -1;
        int steps = Mathf.Abs(dx);
        int cx = s.x;
        int y = s.y;
        int w = s.blockWidth;

        for (int i = 0; i < steps; i++)
        {
            int nx = cx + dir;
            if (nx < 0 || nx + w - 1 >= width) break;

            bool blocked = false;
            // 進行方向の障害物チェック
            if (dir > 0)
            {
                if (grid[nx + w - 1, y] != null) blocked = true;
            }
            else
            {
                if (grid[nx, y] != null) blocked = true;
            }

            if (blocked) break;

            // グリッド更新
            for (int k = 0; k < w; k++) grid[cx + k, y] = null;
            for (int k = 0; k < w; k++) grid[nx + k, y] = s;

            cx = nx;
        }
        s.SetGrid(cx, y);
    }

    // --- ゲーム進行フロー ---
    public void PushUpAndDrop()
    {
        if (IsBusy || IsGameOver) return;
        StartCoroutine(PushUpAndDropCoroutine());
    }

    IEnumerator PushUpAndDropCoroutine()
    {
        IsBusy = true;

        DropStones();
        yield return new WaitForSeconds(0.2f);

        bool cleared = CheckAndClearHorizontal();
        if (cleared)
        {
            yield return new WaitForSeconds(0.5f);
            DropStones();
            yield return new WaitForSeconds(0.2f);
        }

        // お邪魔ブロックがあるか、なければ通常のせり上げ
        if (pendingGarbage > 0)
        {
            if (!ExecutePushUpInternal(true)) 
            {
                // せり上げ失敗（ゲームオーバー）ならループを抜ける
                IsBusy = false;
                yield break; 
            }
        }
        else
        {
            if (!ExecutePushUpInternal(false))
            {
                IsBusy = false;
                yield break;
            }
        }

        yield return new WaitForSeconds(0.3f);

        DropStones();
        yield return new WaitForSeconds(0.2f);

        cleared = CheckAndClearHorizontal();
        if (cleared)
        {
            yield return new WaitForSeconds(0.5f);
            DropStones();
            yield return new WaitForSeconds(0.2f);
        }

        IsBusy = false;
    }

    public void ReceiveGarbage(int lines)
    {
        if (IsGameOver) return;
        pendingGarbage += lines;
        if (!IsBusy)
        {
            PushUpAndDrop();
        }
    }

    // --- 勝敗判定 ---
    public void TriggerLose()
    {
        if (IsGameOver) return;
        IsGameOver = true;
        
        Debug.Log("GAME OVER: LIMIT REACHED");

        if (resultText != null)
        {
            resultText.text = "YOU LOSE...";
            // ★【修正点1】文字色を青に変更
            resultText.color = Color.blue; 
            resultText.gameObject.SetActive(true);
        }
        if (restartButton != null) restartButton.SetActive(true);

        if (opponent != null) opponent.TriggerWin();
    }

    public void TriggerWin()
    {
        if (IsGameOver) return;
        IsGameOver = true;
        
        if (resultText != null)
        {
            resultText.text = "YOU WIN!!";
            resultText.color = Color.yellow;
            resultText.gameObject.SetActive(true);
        }
        if (restartButton != null) restartButton.SetActive(true);
    }

    public void ReloadScene()
    {
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }


    // --- 内部ロジック ---
    public bool CheckAndClearHorizontal()
    {
        if (IsGameOver) return false;

        bool anyCleared = false;
        int linesClearedThisTurn = 0;

        for (int y = 0; y < height; y++)
        {
            bool fullLine = true;
            for (int x = 0; x < width; x++)
            {
                if (grid[x, y] == null)
                {
                    fullLine = false;
                    break;
                }
            }

            if (fullLine)
            {
                anyCleared = true;
                linesClearedThisTurn++;

                for (int x = 0; x < width; x++)
                {
                    Stone s = grid[x, y];
                    if (s != null)
                    {
                        for (int k = 0; k < s.blockWidth; k++)
                        {
                            if (IsInside(s.x + k, s.y)) grid[s.x + k, s.y] = null;
                        }
                        StartCoroutine(DestroyStoneAnimated(s));
                    }
                }
            }
        }

        if (linesClearedThisTurn > 0 && opponent != null)
        {
            opponent.ReceiveGarbage(linesClearedThisTurn);
        }

        return anyCleared;
    }
    
    public void PushUp()
    {
        if (IsBusy || IsGameOver) return;
        ExecutePushUpInternal(false);
    }

    // 戻り値をboolにして、失敗(ゲームオーバー)したか分かるようにする
    private bool ExecutePushUpInternal(bool isGarbage)
    {
        // ★修正ポイント：ShiftStonesUpを呼び出すだけにする（中身を書かない）
        if (!ShiftStonesUp()) 
        {
             return false; // ゲームオーバーになったので中断
        }

        GenerateNewRow(isGarbage);

        if (isGarbage && pendingGarbage > 0)
        {
            pendingGarbage--;
        }
        return true;
    }

    // ★修正ポイント：ShiftStonesUpメソッドはここ（ExecutePushUpInternalの外）に定義する
    // ★修正：ブロックを1段上げる処理
    bool ShiftStonesUp()
    {
        // 1. まずブロックをすべて1段上げる
        for (int y = height - 2; y >= 0; y--)
        {
            HashSet<Stone> processedStones = new HashSet<Stone>();
            for (int x = 0; x < width; x++)
            {
                Stone s = grid[x, y];
                if (s != null && !processedStones.Contains(s))
                {
                    processedStones.Add(s);
                    
                    for (int k = 0; k < s.blockWidth; k++) grid[s.x + k, y] = null;
                    for (int k = 0; k < s.blockWidth; k++) grid[s.x + k, y + 1] = s;
                    
                    s.MoveToGridAnimated(s.x, y + 1, 0.3f);
                }
            }
        }

        
        // 2. ゲームオーバー判定
        // ★修正：判定ラインを「height - 3」に変更
        // これで「上から3段目」にブロックが入ったら負けになります
        for (int x = 0; x < width; x++)
        {
            if (grid[x, height - 3] != null) 
            {
                TriggerLose();
                return false; 
            }
        }

        return true; 
    }
void GenerateNewRow(bool isGarbage)
    {
        // --- お邪魔ブロック専用ロジック：穴の位置を完全ランダムにする ---
        if (isGarbage)
{
    // 穴の幅をランダムに決める (例: 1～3マス)
    int holeSize = Random.Range(1, 4); 

    // 穴の開始位置を決める (幅からはみ出さないように調整)
    int holeX = Random.Range(0, width - holeSize + 1);

    // 左側を埋める
    FillRangeWithGarbage(0, holeX);

    // 右側を埋める (開始位置 + 穴のサイズ から再開)
    FillRangeWithGarbage(holeX + holeSize, width);
    
    return;
}

        // --- 通常のブロック生成ロジック（前回と同じ） ---
        int currentX = 0;
        bool hasGap = false;

        while (currentX < width)
        {
            int remainingSpace = width - currentX;
            // 通常時は70%の確率でブロック生成
            bool wantBlock = (Random.value < 0.7f);

            if (!hasGap && remainingSpace == 1) wantBlock = false;

            if (wantBlock)
            {
                int maxW = Mathf.Min(4, remainingSpace);
                int w = Random.Range(1, maxW + 1);

                if (!hasGap && w == remainingSpace)
                {
                    w = remainingSpace - 1;
                    if (w <= 0) wantBlock = false;
                }

                if (wantBlock && w > 0)
                {
                    Color c = safeColors[Random.Range(0, safeColors.Length)];
                    CreateBlock(currentX, w, c); // 共通化した関数を使う
                    currentX += w;
                }
                else
                {
                    currentX++;
                    hasGap = true;
                }
            }
            else
            {
                currentX++;
                hasGap = true;
            }
        }
    }

    // ★追加：指定した範囲をお邪魔ブロック(グレー)で埋めるヘルパー関数
    void FillRangeWithGarbage(int startX, int endX)
    {
        int current = startX;
        while (current < endX)
        {
            int space = endX - current;
            // 1～4、または残りスペースに合わせて幅を決める
            int w = Random.Range(1, Mathf.Min(4, space) + 1);
            
            CreateBlock(current, w, Color.gray);
            current += w;
        }
    }

    // ★追加：ブロック生成処理を共通化（コードをスッキリさせるため）
    void CreateBlock(int x, int w, Color c)
    {
        Stone s = Instantiate(stonePrefab, transform).GetComponent<Stone>();
        s.Init(this, x, 0, w, c);
        for (int k = 0; k < w; k++)
        {
            if (x + k < width) grid[x + k, 0] = s;
        }
    }

    public void DropStones()
    {
        HashSet<Stone> processedFrame = new HashSet<Stone>();
        // 下からではなく、上から順に落とす判定をしたほうが安全
        for (int y = 1; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                Stone s = grid[x, y];
                if (s == null) continue;
                if (processedFrame.Contains(s)) continue;
                processedFrame.Add(s);

                int targetY = y;
                // どこまで落ちれるかチェック
                for (int checkY = y - 1; checkY >= 0; checkY--)
                {
                    bool canFit = true;
                    for (int k = 0; k < s.blockWidth; k++)
                    {
                        if (grid[s.x + k, checkY] != null)
                        {
                            canFit = false;
                            break;
                        }
                    }
                    if (canFit) targetY = checkY;
                    else break;
                }

                if (targetY != y)
                {
                    for (int k = 0; k < s.blockWidth; k++) grid[s.x + k, y] = null;
                    for (int k = 0; k < s.blockWidth; k++) grid[s.x + k, targetY] = s;
                    s.MoveToGridAnimated(s.x, targetY, 0.2f);
                }
            }
        }
    }

    IEnumerator DestroyStoneAnimated(Stone s)
    {
        if (s == null) yield break;
        var col = s.GetComponent<Collider2D>();
        if (col) col.enabled = false;

        Vector3 startPos = s.transform.position;
        Vector3 endPos = startPos + Vector3.up * 0.5f;
        float duration = 0.2f;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            if (s == null) yield break;
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            s.transform.position = Vector3.Lerp(startPos, endPos, t);
            yield return null;
        }
        if (s != null) Destroy(s.gameObject);
    }

    public Vector2Int GetMovableRange(Stone s)
    {
        int y = s.y;
        int w = s.blockWidth;
        int minX = s.x;
        int maxX = s.x;

        for (int x = s.x - 1; x >= 0; x--)
        {
            if (grid[x, y] != null) break;
            minX = x;
        }

        for (int x = s.x + w; x < width; x++)
        {
            if (grid[x, y] != null) break;
            maxX = x - (w - 1);
        }
        return new Vector2Int(minX, maxX);
    }

    public Stone GetStoneAt(int x, int y)
    {
        if (!IsInside(x, y)) return null;
        return grid[x, y];
    }

    public void SetStoneAt(int x, int y, Stone s)
    {
        if (!IsInside(x, y)) return;
        grid[x, y] = s;
        if (s != null) s.SetGrid(x, y);
    }
}