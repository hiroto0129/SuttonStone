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
    
    // ★ここに「対戦相手」を入れる欄を追加しました！
    [Header("Versus")]
    public BoardManager opponent; 

    private Stone[,] grid;
    private int pendingGarbage = 0; // 相手から送られてきたお邪魔ブロックの数

    Color[] presetColors = new Color[]
    {
        Color.red, Color.blue, Color.green, Color.yellow, new Color(1f, 0.5f, 0f)
    };

    // 外部から今のボードの状態を知るためのプロパティ
    public bool IsBusy { get; private set; } = false;

    void Awake()
    {
        grid = new Stone[width, height];
    }

    void Start()
    {
        PushUpAndDrop();
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

    // --- ゲーム進行（せり上がり・落下・削除） ---

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
        yield return new WaitForSeconds(0.4f); // 着地待ち

        // 2. 揃っているかチェック
        bool cleared = CheckAndClearHorizontal();
        
        // ★もし消えたら、その分を落とす
        if (cleared)
        {
            yield return new WaitForSeconds(0.5f); // 消える演出待ち
            DropStones(); // 落とす！
            yield return new WaitForSeconds(0.4f); // 着地待ち
        }

        // 3. せり上げ（新しい行の追加）
        PushUp();
        yield return new WaitForSeconds(0.3f); // 上がるアニメ待ち
        
        // 4. せり上げ後の再落下
        DropStones();
        yield return new WaitForSeconds(0.4f); // 着地待ち

        // 5. 最後に「せり上がった結果」揃ったかチェック
        cleared = CheckAndClearHorizontal();
        
        // ★ここを追加！ 最後にもし消えたら、放置せずに落とす！
        if (cleared)
        {
            yield return new WaitForSeconds(0.5f); // 消える演出待ち
            DropStones(); // すぐ落とす！
            yield return new WaitForSeconds(0.4f); // 着地待ち
        }

        IsBusy = false;
    }

    // ★相手から攻撃を受け取るメソッド
    public void ReceiveGarbage(int lines)
    {
        pendingGarbage += lines;
        // ここで「お邪魔ブロックが来るぞ！」という演出を入れても良い
    }

    public bool CheckAndClearHorizontal()
    {
        bool anyCleared = false;
        int linesClearedThisTurn = 0; // 消したライン数カウント

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

                // 一列まるごと削除
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

        // ★ラインが消えていて、かつ相手がいるなら攻撃を送る！
        if (linesClearedThisTurn > 0 && opponent != null)
        {
            // 例：1列消し=0, 2列消し=1行送る...などルールはお好みで。
            // ここではシンプルに「消した列数 - 1」を送る設定にしてみます（1列だけなら送らない）
            int attackAmount = linesClearedThisTurn - 1;
            if (linesClearedThisTurn >= 2) // 2列以上同時消しなら攻撃
            {
                opponent.ReceiveGarbage(attackAmount > 0 ? attackAmount : 1);
            }
        }

        return anyCleared;
    }

    public void PushUp()
    {
        // 上へずらす
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
                        // ここでゲームオーバー処理
                        return; 
                    }

                    for(int k=0; k<s.blockWidth; k++) grid[s.x + k, y] = null;
                    for(int k=0; k<s.blockWidth; k++) grid[s.x + k, y + 1] = s;
                    s.MoveToGridAnimated(s.x, y + 1, 0.3f);
                }
            }
        }

        // 下から新しい行を追加
        GenerateNewRow();
        
        // ★相手から攻撃を受けていたら、さらにもう1行追加（お邪魔）
        if (pendingGarbage > 0)
        {
            // お邪魔行の生成（全部埋まって消しにくい行など）
            // ここでは簡易的に通常の生成を呼び出しますが、本来は「固いブロック」などを出す
            GenerateNewRow(); 
            pendingGarbage--;
        }
    }

    void GenerateNewRow()
    {
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

                if (wantBlock)
                {
                    Color blockColor = presetColors[Random.Range(0, presetColors.Length)];
                    Stone s = Instantiate(stonePrefab).GetComponent<Stone>();
                    s.Init(this, currentX, 0, w, blockColor);
                    for (int k = 0; k < w; k++) grid[currentX + k, 0] = s;
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

    // ★カーソル操作用の入れ替えメソッド
    public void SwapStonesByCursor(int x, int y)
    {
        // 右隣と入れ替える前提
        int x1 = x;
        int x2 = x + 1;

        Stone s1 = grid[x1, y];
        Stone s2 = grid[x2, y];

        // 両方とも空なら何もしない
        if (s1 == null && s2 == null) return;

        // ※本来は移動アニメーションを入れたいですが、まずはロジックだけで実装します
        
        // グリッド配列の中身を入れ替え
        grid[x1, y] = s2;
        grid[x2, y] = s1;

        // 石の座標情報を更新
        if (s1 != null)
        {
            s1.SetGrid(x2, y);
            // s1.MoveToGridAnimated(...) を呼ぶとリッチになります
        }
        
        if (s2 != null)
        {
            s2.SetGrid(x1, y);
        }

        // 入れ替えた結果、消えるかどうか判定するために処理を回す
        PushUpAndDrop();
    }

    // 指定した場所の石を取得する
    public Stone GetStoneAt(int x, int y)
    {
        if (!IsInside(x, y)) return null;
        return grid[x, y];
    }

    // 指定した場所に石を強制配置する（配列の書き換え＋座標更新）
    public void SetStoneAt(int x, int y, Stone s)
    {
        if (!IsInside(x, y)) return;
        
        grid[x, y] = s;
        if (s != null)
        {
            s.SetGrid(x, y); // 石自身の座標データも更新
        }
    }
}