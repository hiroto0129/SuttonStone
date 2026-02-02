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
    public int height = 12;

    [Header("References")]
    public Transform leftWall;
    public Transform bottomLine;
    public GameObject stonePrefab;

    [Header("UI (In-Game)")]
    public TextMeshProUGUI resultText; 
    public GameObject restartButton; 
    // ★タイトル画面やヘルプ画面の変数は削除しました（GameControllerへ移動）

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
        
        // ★自動でスタートしないように変更
        IsGameOver = true; // GameControllerから合図があるまで動かない
    }

    // ★追加：GameControllerから呼ばれる「試合開始」の合図
    public void GameStart()
    {
        IsGameOver = false;
        IsBusy = false;
        PushUp(); // ここで初めてブロック生成＆せり上げ開始
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

        if (pendingGarbage > 0)
        {
            if (!ExecutePushUpInternal(true)) 
            {
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

    private bool ExecutePushUpInternal(bool isGarbage)
    {
        if (!ShiftStonesUp()) 
        {
             return false;
        }

        GenerateNewRow(isGarbage);

        if (isGarbage && pendingGarbage > 0)
        {
            pendingGarbage--;
        }
        return true;
    }

    bool ShiftStonesUp()
    {
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

        // 判定ライン：height - 3
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
        if (isGarbage)
        {
            int holeSize = Random.Range(1, 4); 
            int holeX = Random.Range(0, width - holeSize + 1);

            FillRangeWithGarbage(0, holeX);
            FillRangeWithGarbage(holeX + holeSize, width);
            return;
        }

        int currentX = 0;
        bool hasGap = false;

        while (currentX < width)
        {
            int remainingSpace = width - currentX;
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
                    CreateBlock(currentX, w, c);
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

    void FillRangeWithGarbage(int startX, int endX)
    {
        int current = startX;
        while (current < endX)
        {
            int space = endX - current;
            int w = Random.Range(1, Mathf.Min(4, space) + 1);
            
            CreateBlock(current, w, Color.gray);
            current += w;
        }
    }

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
        for (int y = 1; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                Stone s = grid[x, y];
                if (s == null) continue;
                if (processedFrame.Contains(s)) continue;
                processedFrame.Add(s);

                int targetY = y;
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