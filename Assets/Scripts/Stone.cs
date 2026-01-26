using System.Collections;
using UnityEngine;

public class Stone : MonoBehaviour
{
    public int x;
    public int y;
    public int blockWidth = 1; // ブロックの横幅

    private BoardManager board;
    private Vector3 dragStartMouseWorldPos; // マウスのワールド座標開始点
    private bool dragging = false;
    private Vector2Int movableRange;

    public SpriteRenderer body;
    public SpriteRenderer outline;

    void Awake()
    {
        if (body == null)
            body = transform.Find("Body").GetComponent<SpriteRenderer>();
        if (outline == null)
            outline = transform.Find("Outline").GetComponent<SpriteRenderer>();
    }

    public void Init(BoardManager bm, int gx, int gy, int w, Color? color = null)
    {
        board = bm;
        blockWidth = w;
        
        SetGrid(gx, gy);

        // --- 1. 当たり判定（Collider）のサイズ修正 ---
        BoxCollider2D col = GetComponent<BoxCollider2D>();
        if (col != null)
        {
            // 幅に合わせて当たり判定を広げる
            col.size = new Vector2(blockWidth, 1f);
            col.offset = Vector2.zero; // 重心を真ん中に
        }

        // --- 2. 見た目の修正 ---
        float padding = 0.1f;

        // Outline（黒背景）
        if (outline != null)
        {
            outline.transform.localScale = new Vector3(blockWidth, 1, 1);
            outline.color = Color.black;
        }

        // Body（色部分）
        if (body != null)
        {
            body.transform.localScale = new Vector3(blockWidth - padding, 1f - padding, 1);
            body.color = color ?? new Color(Random.value, Random.value, Random.value);
        }
    }

    public void SetGrid(int gx, int gy)
    {
        x = gx;
        y = gy;
        UpdatePosition();
    }

    void UpdatePosition()
    {
        Vector3 pos = board.GridToWorld(x, y);
        // 幅がある場合、中心位置をずらす
        pos.x += (blockWidth - 1) * 0.5f; 
        transform.position = pos;
    }

    void OnMouseDown()
    {
        dragging = true;
        // マウスのスクリーン座標をワールド座標に変換して記録
        dragStartMouseWorldPos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
        
        // 現在の移動可能範囲を取得
        movableRange = board.GetMovableRange(this);
    }

    void OnMouseDrag()
    {
        if (!dragging) return;

        // 現在のマウス位置（ワールド座標）
        Vector3 currentMouseWorldPos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
        
        // マウスがどれだけ動いたか（ワールド座標での差分）
        float distWorldX = currentMouseWorldPos.x - dragStartMouseWorldPos.x;

        // グリッド単位（1.0f）なので、四捨五入すれば移動マス数になる
        int gridDist = Mathf.RoundToInt(distWorldX);

        int targetX = x + gridDist;

        // 移動範囲内に制限 (Clamp)
        targetX = Mathf.Clamp(targetX, movableRange.x, movableRange.y);

        // --- ドラッグ中の見た目の追従 ---
        Vector3 pos = board.GridToWorld(targetX, y);
        pos.x += (blockWidth - 1) * 0.5f;
        transform.position = pos;
    }

    void OnMouseUp()
    {
        if (!dragging) return;
        dragging = false;

        // 現在の見た目の位置から、一番近いグリッド座標を確定させる
        // (左端の座標を逆算して求める)
        float currentWorldLeftX = transform.position.x - (blockWidth - 1) * 0.5f;
        int targetX = board.WorldToGridX(currentWorldLeftX);
        
        int dx = targetX - x;

        // 移動していれば処理実行
        if (dx != 0)
        {
            board.TryMoveStoneMultiple(this, dx);
            board.PushUpAndDrop();
        }
        else
        {
            // 元の場所にスナップして戻す
            MoveToGridAnimated(x, y, 0.1f);
        }
    }

    public void MoveToGridAnimated(int targetX, int targetY, float duration = 0.2f)
    {
        StartCoroutine(MoveToGridCoroutine(targetX, targetY, duration));
    }

    IEnumerator MoveToGridCoroutine(int targetX, int targetY, float duration)
    {
        Vector3 startPos = transform.position;
        Vector3 targetBasePos = board.GridToWorld(targetX, targetY);
        Vector3 endPos = targetBasePos + Vector3.right * ((blockWidth - 1) * 0.5f);

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            transform.position = Vector3.Lerp(startPos, endPos, t);
            yield return null;
        }

        transform.position = endPos;
        x = targetX;
        y = targetY;
    }
}