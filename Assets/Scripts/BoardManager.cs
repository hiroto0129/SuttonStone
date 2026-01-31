using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BoardManager : MonoBehaviour
{
    [Header("Settings")]
    public int width = 8;
    public int height = 10;

    [Header("References")]
    public Transform leftWall;
    public Transform bottomLine;
    public GameObject stonePrefab;
    
    [Header("Versus")]
    public BoardManager opponent; 

    private Stone[,] grid;
    private int pendingGarbage = 0; // 相手から送られてきたお邪魔ブロックの数

    Color[] presetColors = new Color[]
    {
        Color.red, Color.blue, Color.green, Color.yellow, new Color(1f, 0.5f, 0f)
    };

    public bool IsBusy { get; private set; } = false;

    void Awake()
    {
        grid = new Stone[width, height];
    }

    void Start()
    {
        // 最初は少し盤面を作っておく
        PushUp();

    }
    
    void Update()
    {
        // 自動せり上がりはOFF
    }

    // --- 座標変換系 ---
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
            if (dir > 0)
            {
                if (grid[nx + w - 1, y] != null) blocked = true;
            }
            else
            {
                if (grid[nx, y] != null) blocked = true;
            }

            if (blocked) break;

            for (int k = 0; k < w; k++) grid[cx + k, y] = null;
            for (int k = 0; k < w; k++) grid[nx + k, y] = s;

            cx = nx;
        }
        s.SetGrid(cx, y);
    }

    // --- ゲーム進行 ---

    public void PushUpAndDrop()
    {
        if (IsBusy) return;
        StartCoroutine(PushUpAndDropCoroutine());
    }

   IEnumerator PushUpAndDropCoroutine()
    {
        IsBusy = true;

        // 1. まず浮いている石を落とす
        DropStones();
        yield return new WaitForSeconds(0.2f);

        // 2. 揃っているかチェック
        bool cleared = CheckAndClearHorizontal();
        
        if (cleared)
        {
            yield return new WaitForSeconds(0.5f); // 消える演出待ち
            DropStones(); // 落とす
            yield return new WaitForSeconds(0.2f); // 着地待ち
        }

        // ★修正点：if - else にして、「お邪魔優先」または「通常」のどちらか1回だけ実行する
        if (pendingGarbage > 0)
        {
            // お邪魔ストックがあるなら、お邪魔を1段上げる（通常ブロックは上げない）
            ExecutePushUpInternal(true);
        }
        else
        {
            // ストックがないなら、通常通り1段上げる（ここが抜けていました）
            ExecutePushUpInternal(false);
        }
        
        yield return new WaitForSeconds(0.3f); // 上がるアニメ待ち
        
        // 4. せり上げ後の再落下
        DropStones();
        yield return new WaitForSeconds(0.2f); // 着地待ち

        // 5. 最後に再チェック
        cleared = CheckAndClearHorizontal();
        
        if (cleared)
        {
            yield return new WaitForSeconds(0.5f);
            DropStones();
            yield return new WaitForSeconds(0.2f);
        }

        IsBusy = false;
    }
    // 攻撃受け取り
    public void ReceiveGarbage(int lines)
    {
        pendingGarbage += lines;
        
        // 相手が操作中でなければ、メインの処理を開始する
        if (!IsBusy)
        {
            // ★修正前：ExecutePushUpInternal(true); // これだと「上げる」だけで「落とす」処理がない
            
            // ★修正後：PushUpAndDrop(); を呼ぶ
            // これなら [上げる] -> [アニメ待ち] -> [落とす(DropStones)] まで全部やってくれます
            PushUpAndDrop(); 
        }
    }

    public bool CheckAndClearHorizontal()
    {
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

    bool ShiftStonesUp()
    {
        for (int y = height - 1; y >= 0; y--)
        {
            HashSet<Stone> processedStones = new HashSet<Stone>();
            for (int x = 0; x < width; x++)
            {
                Stone s = grid[x, y];
                if (s != null && !processedStones.Contains(s))
                {
                    processedStones.Add(s);
                    
                    if (y + 1 >= height) 
                    { 
                        Debug.Log("GAME OVER"); 
                        return false; 
                    }

                    for(int k=0; k<s.blockWidth; k++) grid[s.x + k, y] = null;
                    for(int k=0; k<s.blockWidth; k++) grid[s.x + k, y + 1] = s;
                    
                    s.MoveToGridAnimated(s.x, y + 1, 0.3f);
                }
            }
        }
        return true;
    }

    // 外部から手動で呼ぶ用（デバッグボタン等）は「通常ブロック」として呼ぶ
    public void PushUp()
    {
        if (IsBusy) return;
        ExecutePushUpInternal(false);
    }

    // ★修正：引数 isGarbage を追加し、どちらか1段だけを生成するように変更
    private void ExecutePushUpInternal(bool isGarbage)
    {
        // 1. せり上げ実行
        if (!ShiftStonesUp()) return;

        // 2. 行生成
        // お邪魔モードならお邪魔行、そうでなければ通常行
        GenerateNewRow(isGarbage); 

        // お邪魔として生成したなら、ストックを減らす
        if (isGarbage && pendingGarbage > 0)
        {
            pendingGarbage--;
        }
    }

    void GenerateNewRow(bool isGarbage)
    {
        int currentX = 0;
        bool hasGap = false;

        // 色のリストをここで定義して、インスペクター等の影響を受けないようにする
        Color[] safeColors = new Color[]
        {
            Color.red, 
            Color.blue, 
            Color.green, 
            Color.yellow, 
            new Color(1f, 0.5f, 0f), // オレンジ
            Color.cyan, 
            Color.magenta
        };
        
        while (currentX < width)
        {
            int remainingSpace = width - currentX;
            
            // お邪魔行なら「基本ブロックを置く」が、たまに(10%くらい)ランダムで隙間を作る
            bool wantBlock = isGarbage ? (Random.value > 0.1f) : (Random.value < 0.7f);

            // 通常行で、残り1マスかつまだ隙間がないなら、強制的に隙間を作る（埋まり防止）
            if (!isGarbage && !hasGap && remainingSpace == 1) wantBlock = false;
            
            // お邪魔行でも、最後まで隙間がなかったら最後の1マスは強制的に空ける
            if (isGarbage && !hasGap && remainingSpace == 1) wantBlock = false;

            if (wantBlock)
            {
                int maxW = Mathf.Min(4, remainingSpace);
                
                // お邪魔なら1〜maxW、通常も1〜maxW（形状はランダム）
                int w = Random.Range(1, maxW + 1);

                // まだ隙間がなく、かつ「残り幅全部」を埋めようとした場合
                if (!hasGap && w == remainingSpace)
                {
                    w = remainingSpace - 1;
                    if (w <= 0) wantBlock = false; 
                }

                if (wantBlock && w > 0)
                {
                    Color c;
                    if (isGarbage)
                    {
                        c = Color.gray; // お邪魔なら確実にグレー
                    }
                    else
                    {
                        // 通常ならリストからランダム（絶対にグレーにならない）
                        c = safeColors[Random.Range(0, safeColors.Length)];
                    }
                    
                    Stone s = Instantiate(stonePrefab, transform).GetComponent<Stone>();
                    s.Init(this, currentX, 0, w, c);
                    
                    for (int k = 0; k < w; k++) 
                    {
                        if (currentX + k < width) grid[currentX + k, 0] = s;
                    }
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
                    for(int k=0; k<s.blockWidth; k++) grid[s.x + k, y] = null;
                    for(int k=0; k<s.blockWidth; k++) grid[s.x + k, targetY] = s;
                    s.MoveToGridAnimated(s.x, targetY, 0.2f);
                }
            }
        }
    }

    public bool IsBoardEmpty()
    {
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                if (grid[x, y] != null) return false;
            }
        }
        return true;
    }

    IEnumerator DestroyStoneAnimated(Stone s)
    {
        if(s == null) yield break;
        var col = s.GetComponent<Collider2D>();
        if(col) col.enabled = false;

        Vector3 startPos = s.transform.position;
        Vector3 endPos = startPos + Vector3.up * 0.5f;
        float duration = 0.2f;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            if(s == null) yield break; 
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            s.transform.position = Vector3.Lerp(startPos, endPos, t);
            yield return null;
        }
        if(s != null) Destroy(s.gameObject);
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
        if (s != null)
        {
            s.SetGrid(x, y);
        }
    }
}