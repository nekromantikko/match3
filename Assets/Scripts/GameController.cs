using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UniRx;

public struct DonutGameData
{
    public int flavor;
    public bool active;
}

public struct DonutDrawData
{
    public DonutFlavor flavor;
    public Vector3 position;
    public Vector3 scale; // Temp
}

public struct Donut
{
    public DonutGameData gameData;
    public DonutDrawData drawData;
}

public struct SwapOperation
{
    public int sourceIdx;
    public int targetIdx;
}

public struct ColumnFillOperation
{
    public SwapOperation[] swaps;
    public int emptyCount;
}

public class GameController : MonoBehaviour
{
    public int gridWidth = 8;
    public int gridHeight = 8;
    public float cellSize = 1.0f;

    public DonutFlavor[] flavors;

    Donut[] items;

    Queue<IEnumerator> animQueue = new Queue<IEnumerator>();

    Vector3 CellIndexToWorldPos(int index)
    {
        int x = index % gridWidth;
        int y = index / gridWidth;

        float xWorld = (x + 0.5f) * cellSize - (gridWidth / 2.0f);
        float yWorld = (y + 0.5f) * cellSize - (gridHeight / 2.0f);

        return new Vector3(xWorld, yWorld, 0);
    }

    int WorldPosToCellIndex(Vector3 pos)
    {
        float halfGridWidth = gridWidth / 2.0f;
        float halfGridHeight = gridHeight / 2.0f;

        if (pos.x > halfGridWidth || pos.x < -halfGridWidth || pos.y > halfGridHeight || pos.y < -halfGridHeight)
        {
            return -1;
        }

        int xGrid = Mathf.RoundToInt(((pos.x + halfGridWidth) / cellSize) - 0.5f);
        int yGrid = Mathf.RoundToInt(((pos.y + halfGridHeight) / cellSize) - 0.5f);

        return yGrid * gridWidth + xGrid;
    }

    bool AreNeighbors(int a, int b)
    {
        if (a == -1 || b == -1) return false;

        if (a == b) return false;

        // If on same row
        if (a / gridWidth == b / gridWidth)
        {
            return (a == b-1 || a == b+1);
        }

        // Same column
        if (a % gridWidth == b % gridWidth)
        {
            return (a == b-gridWidth || a == b+gridWidth);
        }

        return false;
    }

    void DrawCell(int index) {
        DonutDrawData donut = items[index].drawData;

        Matrix4x4 trs = Matrix4x4.TRS(donut.position, Quaternion.Euler(-45,0,45), donut.scale);

        Graphics.DrawMesh(donut.flavor.mesh, trs, donut.flavor.material, 0);
    }

    void FindClearableLines(int startInd, int stride, int length, ref List<int[]> lines)
    {
        int ind = startInd;
        int count = 0;

        List<int> streak = new List<int>();
        int streakType = -1;

        for (; count < length; count++, ind += stride)
        {
            DonutGameData donut = items[ind].gameData;

            // If streak broken
            if (streak.Count > 0 && (streakType != donut.flavor || !donut.active))
            {
                if (streak.Count >= 3)
                {
                    lines.Add(streak.ToArray());
                }
                streak.Clear();
            }

            if (donut.active)
            {
                streakType = donut.flavor;
                streak.Add(ind);
            }
        }

        if (streak.Count >= 3)
        {
            lines.Add(streak.ToArray());
        }
    }

    void PopulateClearList(out List<int[]> outLines)
    {
        outLines = new List<int[]>();
        
        for (int x = 0; x < gridWidth; x++)
        {
            FindClearableLines(x, gridWidth, gridHeight, ref outLines);
        }

        for (int y = 0; y < gridHeight; y++)
        {
            FindClearableLines(y*gridWidth, 1, gridWidth, ref outLines);
        }
    }

    void ClearLines(ref List<int[]> lines)
    {
        foreach (var line in lines)
        {
            // TODO: Add points
            foreach(int index in line) {
                items[index].gameData.active = false;
            }
        }
    }

    void ShiftDown(out SwapOperation[][] outSwaps) {
        List<SwapOperation[]> columnSwaps = new List<SwapOperation[]>();

        for (int x = 0; x < gridWidth; x++)
        {
            ShiftDownColumn(x, out var swaps);
            columnSwaps.Add(swaps.ToArray());
        }

        outSwaps = columnSwaps.ToArray();
    }

    void ShiftDownColumn(int x, out List<SwapOperation> outSwaps)
    {
        outSwaps = new List<SwapOperation>();

        int emptyCount = 0;
        for (int y = 0; y < gridHeight; y++)
        {
            int sourceIdx = x + gridWidth*y;
            Donut donut = items[sourceIdx];

            if (!donut.gameData.active)
            {
                emptyCount++;
                continue;
            }

            int targetIdx = sourceIdx - gridWidth*emptyCount;
            Swap(sourceIdx, targetIdx);
            outSwaps.Add(new SwapOperation { sourceIdx = sourceIdx, targetIdx = targetIdx });
        }
    }

    void FillEmptySpaces(out Dictionary<int, DonutGameData> outData) 
    {
        outData = new Dictionary<int, DonutGameData>();

        for (int i = 0; i < gridWidth*gridHeight; i++)
        {
            if (items[i].gameData.active) continue;

            int index = Random.Range(0, flavors.Length);
            items[i].gameData = new DonutGameData {
                flavor = index,
                active = true
            };

            outData.Add(i, items[i].gameData);
        }
    }

    void InitDrawData()
    {
        for (int i = 0; i < gridWidth*gridHeight; i++)
        {
            items[i].drawData = new DonutDrawData {
                position = CellIndexToWorldPos(i),
                scale = Vector3.one,
                flavor = flavors[items[i].gameData.flavor]
            };
        }
    }

    void SwapColors(int sourceIdx, int targetIdx)
    {
        DonutFlavor temp = items[sourceIdx].drawData.flavor;
        items[sourceIdx].drawData.flavor = items[targetIdx].drawData.flavor;
        items[targetIdx].drawData.flavor = temp;
    }

    IEnumerator AnimateSwap(int sourceIdx, int targetIdx, float duration)
    {
        SwapColors(sourceIdx, targetIdx);

        Vector3 srcPos = CellIndexToWorldPos(sourceIdx);
        Vector3 tgtPos = CellIndexToWorldPos(targetIdx);

        float elapsed = 0.0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp(elapsed / duration, 0.0f, 1.0f);
            items[sourceIdx].drawData.position = Vector3.Lerp(tgtPos, srcPos, t);
            items[targetIdx].drawData.position = Vector3.Lerp(srcPos, tgtPos, t);
            yield return null;
        }
    }

    IEnumerator AnimateClear(int[] clearList, float duration)
    {
        float elapsed = 0.0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp(elapsed / duration, 0.0f, 1.0f);
            foreach(int index in clearList)
            {
                items[index].drawData.scale = Vector3.Lerp(Vector3.one, Vector3.zero, t);
            }
            yield return null;
        }
    }

    IEnumerator AnimateFill(SwapOperation[][] swaps, Dictionary<int, DonutGameData> newData, float duration)
    {
        // TODO: Super ugly, clean up
        for (int x = 0; x < gridWidth; x++)
        {
            var column = swaps[x];

            foreach(var swap in column)
            {
                SwapColors(swap.sourceIdx, swap.targetIdx);
                items[swap.targetIdx].drawData.scale = Vector3.one;
            }

            for (int y = column.Length; y < gridHeight; y++)
            {
                int index = x + y*gridWidth;
                items[index].drawData.flavor = flavors[newData[index].flavor];
            }
        }

        float elapsed = 0.0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp(elapsed / duration, 0.0f, 1.0f);

            for (int x = 0; x < gridWidth; x++)
            {
                var column = swaps[x];

                // TODO: Precompute this & positions
                int emptyCount = gridHeight - column.Length;
                foreach(var swap in column)
                {
                    Vector3 srcPos = CellIndexToWorldPos(swap.sourceIdx);
                    Vector3 tgtPos = CellIndexToWorldPos(swap.targetIdx);

                    items[swap.targetIdx].drawData.position = Vector3.Lerp(srcPos, tgtPos, t);
                }

                for (int y = column.Length; y < gridHeight; y++)
                {
                    int index = x + y*gridWidth;
                    items[index].drawData.scale = Vector3.one;

                    Vector3 tgtPos = CellIndexToWorldPos(index);
                    Vector3 srcPos = tgtPos;
                    srcPos.y += emptyCount * cellSize;

                    items[index].drawData.position = Vector3.Lerp(srcPos, tgtPos, t);
                }
            }
            
            yield return null;
        }
    }

    int GetMouseOverCell()
    {
        Vector3 mousePos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
        return WorldPosToCellIndex(mousePos);
    }

    // Start is called before the first frame update
    void Start()
    {
        // Set up board
        items = new Donut[gridWidth*gridHeight];
        FillEmptySpaces(out _);
        while (Step()) {}
        InitDrawData();

        var sourceTileStream = Observable.EveryUpdate().Where(_ => Input.GetMouseButtonDown(0)).Select(_ => GetMouseOverCell());
        var targetTileStream = Observable.EveryUpdate().Where(_ => Input.GetMouseButtonUp(0)).Select(_ => GetMouseOverCell());
        var swapStream = Observable.Merge(sourceTileStream, targetTileStream).Buffer(2)
        .Select(pair => new SwapOperation { sourceIdx = pair[0], targetIdx = pair[1] })
        .Where(swap => AreNeighbors(swap.sourceIdx, swap.targetIdx))
        .Synchronize();

        swapStream.Subscribe(swap => {
            Swap(swap.sourceIdx, swap.targetIdx);
            animQueue.Enqueue(AnimateSwap(swap.sourceIdx, swap.targetIdx, 0.2f));
            // If swapping has no effect, swap back
            if (!Step(true))
            {
                Swap(swap.sourceIdx, swap.targetIdx);
                animQueue.Enqueue(AnimateSwap(swap.sourceIdx, swap.targetIdx, 0.2f));
            }

            // Keep stepping until board is static
            while (Step(true)) {}
        });

        StartCoroutine(AnimLoop());
    }

    IEnumerator AnimLoop()
    {
        while(true)
        {
            if (animQueue.TryDequeue(out var anim))
            {
                yield return anim;
            }
            else yield return null;
        }
    }

    // Advances the game
    // Returns true if board was changed
    bool Step(bool animate = false)
    {
        PopulateClearList(out var lines);
        if (lines.Count == 0) return false;

        ClearLines(ref lines);
        ShiftDown(out var swaps);
        FillEmptySpaces(out var newData);

        if (animate)
        {
            animQueue.Enqueue(AnimateClear(lines.SelectMany(x => x).Distinct().ToArray(), 0.2f));
            animQueue.Enqueue(AnimateFill(swaps, newData, 0.2f));
        }

        return true;
    }

    void Swap(int sourceIdx, int targetIdx)
    {
        if (sourceIdx == -1 || targetIdx == -1) return;

        ref Donut source = ref items[sourceIdx];
        ref Donut target = ref items[targetIdx];

        DonutGameData temp = source.gameData;
        source.gameData = target.gameData;
        target.gameData = temp;
    }

    // Update is called once per frame
    void Update()
    {
        for (int i = 0; i < gridWidth * gridHeight; i++)
        {
            DrawCell(i);
        }
    }
}
