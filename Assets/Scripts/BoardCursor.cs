using UnityEngine;
using System.Collections;

public class BoardCursor : MonoBehaviour
{
    [Header("Settings")]
    public BoardManager myBoard;

    private Transform visualTransform;

    [Header("Visual Settings")]
    public SpriteRenderer cursorRenderer;
    public Color idleColor = new Color(1f, 1f, 1f, 1f);
    public Color holdColor = new Color(1f, 0.8f, 0.0f, 1f);

    [Header("Controls")]
    public KeyCode upKey;
    public KeyCode downKey;
    public KeyCode leftKey;
    public KeyCode rightKey;
    public KeyCode actionKey;

    private int cx = 0;
    private int cy = 0;
    private Stone heldStone = null;

    private int startCx = 0;
    private int startCy = 0;

    IEnumerator Start()
    {
        visualTransform = this.transform;

        cursorRenderer = GetComponent<SpriteRenderer>();
        if (cursorRenderer == null)
        {
            cursorRenderer = gameObject.AddComponent<SpriteRenderer>();
        }

        if (cursorRenderer != null)
        {
            cursorRenderer.sprite = GenerateFrameSprite(64, 4);
            cursorRenderer.color = idleColor;
            cursorRenderer.sortingOrder = 100;
        }

        yield return null;
        UpdateVisualPosition();
    }

    void Update()
    {
        // ★重要：ゲームオーバーなら操作不可
        if (myBoard.IsGameOver) return;
        if (myBoard.IsBusy) return;

        // --- アニメーション ---
        if (cursorRenderer != null)
        {
            if (heldStone == null)
            {
                float alpha = 0.6f + Mathf.Sin(Time.time * 8f) * 0.4f;
                Color c = idleColor;
                c.a = alpha;
                cursorRenderer.color = c;
            }
            else
            {
                float scalePulse = 1.0f + Mathf.Sin(Time.time * 15f) * 0.05f;
                cursorRenderer.color = holdColor;
                float baseWidth = heldStone.blockWidth;
                visualTransform.localScale = new Vector3(baseWidth * scalePulse, 1f * scalePulse, 1);
            }
        }

        // --- 入力 ---
        int dx = 0;
        int dy = 0;

        if (Input.GetKeyDown(upKey)) dy = 1;
        if (Input.GetKeyDown(downKey)) dy = -1;
        if (Input.GetKeyDown(leftKey)) dx = -1;
        if (Input.GetKeyDown(rightKey)) dx = 1;

        if (dx != 0 || dy != 0) AttemptMove(dx, dy);

        if (Input.GetKeyDown(actionKey) || Input.GetKeyDown(KeyCode.KeypadEnter))
        {
            if (heldStone == null) TryPickUp();
            else TryDrop();
        }
    }

    void UpdateVisualPosition()
    {
        if (visualTransform == null) return;
        int displayWidth = 1;
        Vector3 targetPos = myBoard.GridToWorld(cx, cy);

        if (heldStone != null)
        {
            displayWidth = heldStone.blockWidth;
            targetPos.x += (displayWidth - 1) * 0.5f;
            heldStone.transform.position = targetPos;
            if (cursorRenderer != null)
            {
                cursorRenderer.color = holdColor;
                cursorRenderer.sortingOrder = 100;
            }
        }
        else
        {
            Stone target = myBoard.GetStoneAt(cx, cy);
            if (target != null)
            {
                displayWidth = target.blockWidth;
                targetPos = target.transform.position;
            }

            if (cursorRenderer != null)
            {
                cursorRenderer.color = idleColor;
                cursorRenderer.sortingOrder = 20;
            }
            visualTransform.localScale = new Vector3(displayWidth, 1, 1);
        }
        visualTransform.position = targetPos;
    }

    Sprite GenerateFrameSprite(int size, int borderThickness)
    {
        Texture2D tex = new Texture2D(size, size);
        tex.filterMode = FilterMode.Point;
        tex.wrapMode = TextureWrapMode.Clamp;
        Color[] colors = new Color[size * size];

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                bool isBorder = (x < borderThickness) || (x >= size - borderThickness) ||
                                (y < borderThickness) || (y >= size - borderThickness);
                if (isBorder) colors[y * size + x] = Color.white;
                else colors[y * size + x] = Color.clear;
            }
        }
        tex.SetPixels(colors);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
    }

    void TryPickUp()
    {
        Stone target = myBoard.GetStoneAt(cx, cy);
        if (target == null) return;
        heldStone = target;
        cx = heldStone.x;
        cy = heldStone.y;
        startCx = cx;
        startCy = cy;
        UpdateVisualPosition();
    }

    void TryDrop()
    {
        if (heldStone == null) return;
        heldStone = null;
        if (cx != startCx || cy != startCy) myBoard.PushUpAndDrop();
        UpdateVisualPosition();
    }

    void AttemptMove(int dx, int dy)
    {
        if (heldStone == null)
        {
            // カーソル移動（縦）
            if (dy != 0)
            {
                int ny = cy + dy;

                // 1. 枠外チェック（既存）
                if (!myBoard.IsInside(cx, ny)) return;

                // 2. 【追加】高さ制限チェック
                // 「一番高いブロックの位置」を取得
                int maxY = myBoard.GetMaxStoneY();

                // 上に移動しようとしている時、一番高いブロックより上に行こうとしたら止める
                // ※もし「ブロックのすぐ上の空きマスまでは行けるようにしたい」なら
                //   ny > maxY + 1 としてください。
                //   今回は「ブロックを選ぶ」目的なので、ブロックがある高さまでで止めます。
                if (ny > maxY) return;

                cy = ny;
                UpdateVisualPosition();
                return;
            }

            // 横移動（石を飛ばす）
            if (dx != 0)
            {
                Stone currentStone = myBoard.GetStoneAt(cx, cy);
                int startX = cx;

                if (dx > 0)
                {
                    if (currentStone != null) startX = currentStone.x + currentStone.blockWidth;
                    else startX = cx + 1;

                    for (int x = startX; x < myBoard.width; x++)
                    {
                        Stone found = myBoard.GetStoneAt(x, cy);
                        if (found != null)
                        {
                            cx = found.x;
                            UpdateVisualPosition();
                            return;
                        }
                    }
                }
                else
                {
                    startX = cx - 1;
                    for (int x = startX; x >= 0; x--)
                    {
                        Stone found = myBoard.GetStoneAt(x, cy);
                        if (found != null)
                        {
                            cx = found.x;
                            UpdateVisualPosition();
                            return;
                        }
                    }
                }
            }
            return;
        }

        // 石を持っている時
        if (dy != 0) return;
        int targetX = cx + dx;
        int w = heldStone.blockWidth;

        if (targetX < 0 || targetX + w > myBoard.width) return;

        for (int k = 0; k < w; k++)
        {
            Stone obstacle = myBoard.GetStoneAt(targetX + k, cy);
            if (obstacle != null && obstacle != heldStone) return;
        }

        for (int k = 0; k < w; k++) myBoard.SetStoneAt(cx + k, cy, null);
        cx = targetX;
        for (int k = 0; k < w; k++) myBoard.SetStoneAt(cx + k, cy, heldStone);
        if (heldStone != null) heldStone.SetGrid(cx, cy);

        UpdateVisualPosition();
    }
}