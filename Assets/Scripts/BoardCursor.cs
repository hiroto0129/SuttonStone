using UnityEngine;
using System.Collections;

public class BoardCursor : MonoBehaviour
{
    [Header("Settings")]
    public BoardManager myBoard;
    
    // ▼ ここはもう使わないので、インスペクターで設定しなくてOKです
    // public Transform cursorVisual; 
    private Transform visualTransform; // 内部で自分自身を入れます

    [Header("Visual Settings")]
    public SpriteRenderer cursorRenderer; 
    
    [Tooltip("通常時の枠の色（白が見やすいです）")]
    public Color idleColor = new Color(1f, 1f, 1f, 1f); 

    [Tooltip("掴んでいる時の枠の色（金色などがおすすめ）")]
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

    IEnumerator Start()
    {
        // ★ここが修正ポイント
        // 外部の物体(cursorVisual)を使わず、このスクリプトがついている
        // 「自分自身(this.transform)」を動かすことにします。
        visualTransform = this.transform;

        // 1. 自分にSpriteRendererがなければ勝手につける
        cursorRenderer = GetComponent<SpriteRenderer>();
        if (cursorRenderer == null)
        {
            cursorRenderer = gameObject.AddComponent<SpriteRenderer>();
        }

        // 2. 枠の画像を生成してセット
        if (cursorRenderer != null)
        {
            cursorRenderer.sprite = GenerateFrameSprite(64, 4);
            cursorRenderer.color = idleColor;
            
            // ブロックより手前に表示
            cursorRenderer.sortingOrder = 100;
        }

        // 3. 盤面の準備を待つ
        yield return null;

        UpdateVisualPosition();
    }

    void Update()
    {
        if (myBoard.IsBusy) return;

        // --- アニメーション演出 ---
        if (cursorRenderer != null)
        {
            if (heldStone == null)
            {
                // 【通常時】
                float alpha = 0.6f + Mathf.Sin(Time.time * 8f) * 0.4f; 
                Color c = idleColor;
                c.a = alpha;
                cursorRenderer.color = c;
            }
            else
            {
                // 【掴んでいる時】
                float scalePulse = 1.0f + Mathf.Sin(Time.time * 15f) * 0.05f; 
                cursorRenderer.color = holdColor; 
                
                float baseWidth = heldStone.blockWidth;
                visualTransform.localScale = new Vector3(baseWidth * scalePulse, 1f * scalePulse, 1);
            }
        }

        // --- 移動入力 ---
        int dx = 0;
        int dy = 0;

        if (Input.GetKeyDown(upKey)) dy = 1;
        if (Input.GetKeyDown(downKey)) dy = -1;
        if (Input.GetKeyDown(leftKey)) dx = -1;
        if (Input.GetKeyDown(rightKey)) dx = 1;

        if (dx != 0 || dy != 0)
        {
            AttemptMove(dx, dy);
        }

        // --- 掴む / 離す ---
        if (Input.GetKeyDown(actionKey) || Input.GetKeyDown(KeyCode.KeypadEnter))
        {
            if (heldStone == null) TryPickUp();
            else TryDrop();
        }
    }

    void UpdateVisualPosition()
    {
        // visualTransform (自分自身) を動かします
        if (visualTransform == null) return;

        int displayWidth = 1;
        Vector3 targetPos = myBoard.GridToWorld(cx, cy);

        if (heldStone != null)
        {
            // --- 【石を持っている時】 ---
            displayWidth = heldStone.blockWidth;
            targetPos.x += (displayWidth - 1) * 0.5f;
            heldStone.transform.position = targetPos;

            if (cursorRenderer != null) cursorRenderer.color = holdColor;
            if (cursorRenderer != null) cursorRenderer.sortingOrder = 100;
        }
        else
        {
            // --- 【石を持っていない時】 ---
            Stone target = myBoard.GetStoneAt(cx, cy);
            if (target != null)
            {
                displayWidth = target.blockWidth;
                targetPos = target.transform.position;
            }

            if (cursorRenderer != null) cursorRenderer.color = idleColor;
            
            // 掴んでいない時はサイズを戻す
            visualTransform.localScale = new Vector3(displayWidth, 1, 1);
            
            if (cursorRenderer != null) cursorRenderer.sortingOrder = 20; 
        }

        visualTransform.position = targetPos;
    }

    // 枠生成（変更なし）
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
    
    // (以下、ロジック変更なし)
    void TryPickUp()
    {
        Stone target = myBoard.GetStoneAt(cx, cy);
        if (target == null) return;
        heldStone = target;
        cx = heldStone.x;
        cy = heldStone.y;
        UpdateVisualPosition();
    }

    void TryDrop()
    {
        if (heldStone == null) return;
        heldStone = null;
        myBoard.PushUpAndDrop();
        UpdateVisualPosition();
    }

    void AttemptMove(int dx, int dy)
    {
        if (heldStone == null)
        {
            int nx = cx;
            int ny = cy + dy;
            if (dx != 0)
            {
                Stone currentStone = myBoard.GetStoneAt(cx, cy);
                if (currentStone == null) nx = cx + dx;
                else
                {
                    if (dx > 0) nx = currentStone.x + currentStone.blockWidth;
                    else nx = currentStone.x - 1;
                }
            }
            if (myBoard.IsInside(nx, ny))
            {
                cx = nx;
                cy = ny;
                UpdateVisualPosition();
            }
            return;
        }

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