using System.Collections;
using UnityEngine;

public class Stone : MonoBehaviour
{
    public int x;
    public int y;
    public int blockWidth = 1;

    private BoardManager board;
    private Vector3 dragStartMouseWorldPos;
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

        BoxCollider2D col = GetComponent<BoxCollider2D>();
        if (col != null)
        {
            col.size = new Vector2(blockWidth, 1f);
            col.offset = Vector2.zero;
        }

        if (outline != null)
        {
            outline.transform.localScale = new Vector3(blockWidth, 1, 1);
            outline.color = Color.black;
        }

        if (body != null)
        {
            body.transform.localScale = new Vector3(blockWidth - 0.1f, 0.9f, 1);
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
        pos.x += (blockWidth - 1) * 0.5f;
        transform.position = pos;
    }

    void OnMouseDown()
    {
        if (board.IsGameOver || board.IsBusy) return; // ★操作禁止

        dragging = true;
        dragStartMouseWorldPos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
        movableRange = board.GetMovableRange(this);
    }

    void OnMouseDrag()
    {
        if (!dragging) return;

        Vector3 currentMouseWorldPos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
        float distWorldX = currentMouseWorldPos.x - dragStartMouseWorldPos.x;
        int gridDist = Mathf.RoundToInt(distWorldX);

        int targetX = x + gridDist;
        targetX = Mathf.Clamp(targetX, movableRange.x, movableRange.y);

        Vector3 pos = board.GridToWorld(targetX, y);
        pos.x += (blockWidth - 1) * 0.5f;
        transform.position = pos;
    }

    void OnMouseUp()
    {
        if (!dragging) return;
        dragging = false;

        float currentWorldLeftX = transform.position.x - (blockWidth - 1) * 0.5f;
        int targetX = board.WorldToGridX(currentWorldLeftX);

        int dx = targetX - x;

        if (dx != 0)
        {
            board.TryMoveStoneMultiple(this, dx);
            board.PushUpAndDrop();
        }
        else
        {
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