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

    // ★追加：掴み始めた場所を覚えておく変数
    private int startCx = 0;
    private int startCy = 0;

    IEnumerator Start()
    {
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
            cursorRenderer.sortingOrder = 100; // ブロックより手前に表示
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
    
    void TryPickUp()
    {
        Stone target = myBoard.GetStoneAt(cx, cy);
        if (target == null) return;
        heldStone = target;
        cx = heldStone.x;
        cy = heldStone.y;
        
        // ★変更点：掴んだ瞬間の場所を記録しておく
        startCx = cx;
        startCy = cy;

        UpdateVisualPosition();
    }

    void TryDrop()
    {
        if (heldStone == null) return;
        heldStone = null;

        // ★変更点：場所が変わったときだけ、盤面処理(PushUpAndDrop)を呼ぶ
        // （元の場所に戻しただけなら、何もしない）
        if (cx != startCx || cy != startCy)
        {
            myBoard.PushUpAndDrop();
        }

        UpdateVisualPosition();
    }

    void AttemptMove(int dx, int dy)
    {
        // ----------------------------------------------------
        // パターンA：石を持っていない時（カーソル移動）
        // ----------------------------------------------------
        if (heldStone == null)
        {
            // --- 縦移動（変更なし） ---
            if (dy != 0)
            {
                int ny = cy + dy;
                if (myBoard.IsInside(cx, ny))
                {
                    cy = ny;
                    UpdateVisualPosition();
                }
                return;
            }

            // --- 横移動（★ここを修正：空白を飛ばすロジック） ---
            if (dx != 0)
            {
                Stone currentStone = myBoard.GetStoneAt(cx, cy);
                
                // 検索開始位置を決める
                // 右へ行くなら、今の石の右端の次のマスからスタート
                // 左へ行くなら、今の石の左端の前のマスからスタート
                int startX = cx;

                if (dx > 0) // 右へ
                {
                    if (currentStone != null) startX = currentStone.x + currentStone.blockWidth;
                    else startX = cx + 1;

                    // 右端までループして石を探す
                    for (int x = startX; x < myBoard.width; x++)
                    {
                        Stone found = myBoard.GetStoneAt(x, cy);
                        if (found != null)
                        {
                            cx = found.x; // 見つけた石の位置へ飛ぶ
                            UpdateVisualPosition();
                            return; // 移動完了
                        }
                    }
                }
                else // 左へ
                {
                    startX = cx - 1;

                    // 左端(0)までループして石を探す
                    for (int x = startX; x >= 0; x--)
                    {
                        Stone found = myBoard.GetStoneAt(x, cy);
                        if (found != null)
                        {
                            cx = found.x; // 見つけた石の位置へ飛ぶ（石の左上が原点なのでxでOK）
                            UpdateVisualPosition();
                            return; // 移動完了
                        }
                    }
                }
            }
            return;
        }

        // ----------------------------------------------------
        // パターンB：石を持っている時（移動先を選ぶ）
        // ※持っている時は、空白に置きたいこともあるので、今まで通り1マスずつ動かします
        // ----------------------------------------------------
        if (dy != 0) return; // 持っている時は縦移動禁止（ルール次第ですが一旦禁止のまま）

        int targetX = cx + dx;
        int w = heldStone.blockWidth; 
        
        // 盤面の外に出ないかチェック
        if (targetX < 0 || targetX + w > myBoard.width) return;
        
        // 移動先に他の石がないかチェック（障害物判定）
        for (int k = 0; k < w; k++)
        {
            Stone obstacle = myBoard.GetStoneAt(targetX + k, cy);
            if (obstacle != null && obstacle != heldStone) return;
        }

        // --- グリッド情報の書き換え ---
        // 1. 今の場所を空にする
        for (int k = 0; k < w; k++) myBoard.SetStoneAt(cx + k, cy, null);
        
        // 2. 座標更新
        cx = targetX;
        
        // 3. 新しい場所に自分を置く
        for (int k = 0; k < w; k++) myBoard.SetStoneAt(cx + k, cy, heldStone);
        
        if (heldStone != null) heldStone.SetGrid(cx, cy);
        
        UpdateVisualPosition();
    }
}