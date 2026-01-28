using UnityEngine;

public class BoardCursor : MonoBehaviour
{
    [Header("Settings")]
    public BoardManager myBoard;
    public Transform cursorVisual;

    [Header("Controls")]
    public KeyCode upKey;
    public KeyCode downKey;
    public KeyCode leftKey;
    public KeyCode rightKey;
    public KeyCode actionKey; // 掴む・離す

    // カーソル位置
    private int cx = 0;
    private int cy = 0;

    // 掴んでいる石
    private Stone heldStone = null;

    void Start()
    {
        UpdateVisualPosition();
    }

    void Update()
    {
        if (myBoard.IsBusy) return;

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
        if (Input.GetKeyDown(actionKey))
        {
            if (heldStone == null)
            {
                TryPickUp();
            }
            else
            {
                TryDrop();
            }
        }
    }

    // ★重要：ここにあった古い AttemptMove を削除し、下の新しいものだけにしました

    void UpdateVisualPosition()
    {
        if (cursorVisual == null) return;

        // --- デフォルトの設定（空きマスの場合） ---
        int displayWidth = 1;
        Vector3 targetPos = myBoard.GridToWorld(cx, cy);

        // --- 状況に応じてサイズと位置を補正 ---
        
        if (heldStone != null)
        {
            // パターンA：【石を持っている時】
            displayWidth = heldStone.blockWidth;

            // 基準位置(cx)から、幅の分だけ中心を右にずらす
            targetPos.x += (displayWidth - 1) * 0.5f;
            
            // 持っている石もカーソルに合わせて動かす
            heldStone.transform.position = targetPos;
        }
        else
        {
            // パターンB：【石を持っていない時】
            Stone target = myBoard.GetStoneAt(cx, cy);

            if (target != null)
            {
                // 石があるなら、その石の幅に合わせる
                displayWidth = target.blockWidth;

                // 位置も「石の場所」にピタッと吸着させる
                targetPos = target.transform.position;
            }
        }

        // --- カーソルの見た目を適用 ---
        cursorVisual.position = targetPos;
        cursorVisual.localScale = new Vector3(displayWidth, 1, 1);
    }

    void TryPickUp()
    {
        Stone target = myBoard.GetStoneAt(cx, cy);
        if (target == null) return;

        heldStone = target;

        // ★★★ 修正点：ここが重要！ ★★★
        // 掴んだ瞬間、カーソル(cx, cy)を、その石の「本当の左端(x, y)」に強制的に合わせます。
        // これで「右側を掴んでも、左端基準で動かせる」ようになります。
        cx = heldStone.x;
        cy = heldStone.y;
        
        // カーソルの見た目も、左端に合わせて再描画（カクっと左に吸着する動きになります）
        UpdateVisualPosition();
        
        // 必要なら：掴んだことがわかるように少し音を鳴らすなどの処理
    }

    void TryDrop()
    {
        if (heldStone == null) return;

        // 手を離す
        heldStone = null;

        // 揃ったかチェック！
        myBoard.PushUpAndDrop();
    }
    
    // 移動の試行
    void AttemptMove(int dx, int dy)
    {
        // --- 1. 石を持っていない時の移動（★ここを修正！） ---
        if (heldStone == null)
        {
            int nx = cx;
            int ny = cy + dy; // 縦移動はそのまま

            // 横移動の計算
            if (dx != 0)
            {
                // 今、足元に石があるか調べる
                Stone currentStone = myBoard.GetStoneAt(cx, cy);

                if (currentStone == null)
                {
                    // 足元が空っぽなら、普通に1マス移動
                    nx = cx + dx;
                }
                else
                {
                    // ★足元に石がある場合：ブロックの幅分スキップする！
                    if (dx > 0)
                    {
                        // 右へ：そのブロックの「右端の隣」へジャンプ
                        nx = currentStone.x + currentStone.blockWidth;
                    }
                    else
                    {
                        // 左へ：そのブロックの「左端の隣」へジャンプ
                        nx = currentStone.x - 1;
                    }
                }
            }

            // 画面外に出ていなければ移動確定
            if (myBoard.IsInside(nx, ny))
            {
                cx = nx;
                cy = ny;
                UpdateVisualPosition();
            }
            return;
        }

        // --- 2. 石を持っている時の移動（ここは前回と同じ） ---
        
        // 仕様：縦移動は禁止
        if (dy != 0) return; 

        // 移動先の左端座標
        int targetX = cx + dx;
        int w = heldStone.blockWidth; 

        // 画面外チェック
        if (targetX < 0 || targetX + w > myBoard.width) return;

        // 衝突判定
        for (int k = 0; k < w; k++)
        {
            Stone obstacle = myBoard.GetStoneAt(targetX + k, cy);
            if (obstacle != null && obstacle != heldStone)
            {
                return; // 動けない
            }
        }

        // 盤面データを更新
        // 1. まず、古い場所にいる自分を消す
        for (int k = 0; k < w; k++)
        {
            myBoard.SetStoneAt(cx + k, cy, null);
        }

        // 2. カーソル座標更新
        cx = targetX;
        
        // 3. 新しい場所に自分を登録
        for (int k = 0; k < w; k++)
        {
            myBoard.SetStoneAt(cx + k, cy, heldStone);
        }

        // ★座標補正（右側を持っていた場合のズレ防止）
        if (heldStone != null)
        {
            heldStone.SetGrid(cx, cy);
        }

        // 4. 見た目の更新
        UpdateVisualPosition();
    }
}