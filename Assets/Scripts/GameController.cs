using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UniRx;
using UnityEditor;

public struct DonutGameData
{
    public int flavor;
    public bool active;
}

public struct DonutDrawData
{
    public DonutFlavor flavor;
    public Vector3 position;
    public Vector3 scale;
    public bool blocked;
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

public struct ColumnShiftOperation
{
    public int x;
    public SwapOperation[] swaps;
    public int[] createdFlavors;
}

public struct StepResult
{
    public ColumnShiftOperation[] shiftOps;
    public int[] cleared;
}

public class GameController : MonoBehaviour
{
    public int gridWidth = 8;
    public int gridHeight = 8;
    public float cellSize = 1.0f;

    public DonutFlavor[] flavors;

    Donut[] items;

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

    void ClearLines(List<int[]> lines)
    {
        foreach (var line in lines)
        {
            // TODO: Add points
            foreach(int index in line) {
                items[index].gameData.active = false;
            }
        }
    }

    IEnumerable<ColumnShiftOperation> ShiftDown(IEnumerable<int> columns) {

        List<ColumnShiftOperation> result = new List<ColumnShiftOperation>();
        foreach (var column in columns)
        {
            result.Add(ShiftDownColumn(column));
        }

        return result;
    }

    ColumnShiftOperation ShiftDownColumn(int x)
    {
        var swaps = new List<SwapOperation>();
        int emptyCount = 0;
        for (int y = 0; y < gridHeight; y++)
        {
            int sourceIdx = x + gridWidth * y;

            if (!items[sourceIdx].gameData.active)
            {
                emptyCount++;
                continue;
            }

            if (emptyCount == 0) continue;
            
            int targetIdx = sourceIdx - gridWidth*emptyCount;
            var swap = new SwapOperation { sourceIdx = sourceIdx, targetIdx = targetIdx };
            Swap(swap);
            swaps.Add(swap);
        }

        int createStartPos = gridHeight - emptyCount;
        List<int> createdFlavors = new List<int>();

        for (int y = createStartPos; y < gridHeight; y++) {
            int index = x + y*gridWidth;
            int flavor = UnityEngine.Random.Range(0, flavors.Length);
            items[index].gameData = new DonutGameData {
                flavor = flavor,
                active = true
            };

            createdFlavors.Add(flavor);
        }

        return new ColumnShiftOperation {
            x = x,
            swaps = swaps.ToArray(),
            createdFlavors = createdFlavors.ToArray()
        };
    }

    void InitializeBoard()
    {
        for (int i = 0; i < gridWidth*gridHeight; i++)
        {
            if (items[i].gameData.active) continue;

            int flavor = UnityEngine.Random.Range(0, flavors.Length);
            items[i].gameData = new DonutGameData {
                flavor = flavor,
                active = true
            };
        }
    }

    void InitDrawData()
    {
        for (int i = 0; i < gridWidth*gridHeight; i++)
        {
            items[i].drawData = new DonutDrawData {
                position = CellIndexToWorldPos(i),
                scale = Vector3.one,
                flavor = flavors[items[i].gameData.flavor],
                blocked = false
            };
        }
    }

    void SwapColors(SwapOperation swap)
    {
        DonutFlavor temp = items[swap.sourceIdx].drawData.flavor;
        items[swap.sourceIdx].drawData.flavor = items[swap.targetIdx].drawData.flavor;
        items[swap.targetIdx].drawData.flavor = temp;
    }

    IEnumerator AnimateSwap(SwapOperation swap, float duration)
    {
        SwapColors(swap);

        Vector3 srcPos = CellIndexToWorldPos(swap.sourceIdx);
        Vector3 tgtPos = CellIndexToWorldPos(swap.targetIdx);

        float elapsed = 0.0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp(elapsed / duration, 0.0f, 1.0f);
            items[swap.sourceIdx].drawData.position = Vector3.Lerp(tgtPos, srcPos, t);
            items[swap.targetIdx].drawData.position = Vector3.Lerp(srcPos, tgtPos, t);
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

    IEnumerator AnimateShift(ColumnShiftOperation[] shiftOps, float duration)
    {
        foreach(var column in shiftOps)
        {
            foreach (var swap in column.swaps)
            {
                SwapColors(swap);
                items[swap.targetIdx].drawData.scale = Vector3.one;
            }

            int createdCount = column.createdFlavors.Length;
            for (int i = 0; i < createdCount; i++)
            {
                int y = i + gridHeight - createdCount;
                int index = column.x + y*gridWidth;
                items[index].drawData.flavor = flavors[column.createdFlavors[i]];
            }
        }

        float elapsed = 0.0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp(elapsed / duration, 0.0f, 1.0f);

            foreach(var column in shiftOps)
            {
                foreach(var swap in column.swaps)
                {
                    Vector3 srcPos = CellIndexToWorldPos(swap.sourceIdx);
                    Vector3 tgtPos = CellIndexToWorldPos(swap.targetIdx);

                    items[swap.targetIdx].drawData.position = Vector3.Lerp(srcPos, tgtPos, t);
                }

                int createdCount = column.createdFlavors.Length;
                for (int i = 0; i < createdCount; i++)
                {
                    int y = i + gridHeight - createdCount;
                    int index = column.x + y*gridWidth;
                    items[index].drawData.scale = Vector3.one;

                    Vector3 tgtPos = CellIndexToWorldPos(index);
                    Vector3 srcPos = tgtPos;
                    srcPos.y += createdCount * cellSize;

                    items[index].drawData.position = Vector3.Lerp(srcPos, tgtPos, t);
                }
            }
            
            yield return null;
        }
    }

    IEnumerator AnimateStepResult(StepResult stepResult)
    {
        yield return AnimateClear(stepResult.cleared, 0.2f);
        yield return AnimateShift(stepResult.shiftOps, 0.2f);
    }

    IEnumerator AnimateLegalMove(SwapOperation swap, StepResult[] steps)
    {
        var affectedCells = steps.SelectMany(step => step.shiftOps.SelectMany(s => {
            int affectedCount = s.createdFlavors.Length + s.swaps.Length;
            int start = gridHeight - affectedCount;
            List<int> affected = new List<int>();
            for (int y = start; y < gridHeight; y++)
            {
                int index = s.x + y*gridWidth;
                affected.Add(index);
            }
            return affected;
        })).Distinct();

        while (affectedCells.Any(idx => items[idx].drawData.blocked))
        {
            yield return null;
        }

        foreach (var idx in affectedCells)
        {
            items[idx].drawData.blocked = true;
        }

        yield return AnimateSwap(swap, 0.2f);
        foreach (var step in steps)
        {
            yield return AnimateStepResult(step);
        }

        foreach (var idx in affectedCells)
        {
            items[idx].drawData.blocked = false;
        }
    }

    IEnumerator AnimateIllegalMove(SwapOperation swap)
    {
        while (items[swap.sourceIdx].drawData.blocked || items[swap.targetIdx].drawData.blocked)
        {
            yield return null;
        }

        items[swap.sourceIdx].drawData.blocked = true;
        items[swap.targetIdx].drawData.blocked = true;

        yield return AnimateSwap(swap, 0.2f);
        yield return AnimateSwap(swap, 0.2f);

        items[swap.sourceIdx].drawData.blocked = false;
        items[swap.targetIdx].drawData.blocked = false;
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
        InitializeBoard();
        while (Step(out _)) {}
        InitDrawData();

        var sourceTileStream = Observable.EveryUpdate().Where(_ => Input.GetMouseButtonDown(0)).Select(_ => GetMouseOverCell());
        var targetTileStream = Observable.EveryUpdate().Where(_ => Input.GetMouseButtonUp(0)).Select(_ => GetMouseOverCell());
        var swapStream = Observable.Merge(sourceTileStream, targetTileStream).Buffer(2)
        .Select(pair => new SwapOperation { sourceIdx = pair[0], targetIdx = pair[1] })
        .Where(swap => AreNeighbors(swap.sourceIdx, swap.targetIdx))
        .SelectMany(swap => {
            Swap(swap);

            // Illegal move, so swap back
            if (!Step(out var stepResult))
            {
                Swap(swap);
                return Observable.FromCoroutine(() => AnimateIllegalMove(swap));
            }

            List<StepResult> steps = new List<StepResult>();
            do {
                steps.Add(stepResult);
            } while (Step(out stepResult));

            return Observable.FromCoroutine(() => AnimateLegalMove(swap, steps.ToArray()));
        })
        .Subscribe();
    }

    // Advances the game
    // Returns whether anything happened (If not, move was not valid)
    bool Step(out StepResult outResult)
    {
        outResult = new StepResult();
        PopulateClearList(out var lines);
        if (lines.Count == 0) return false;

        ClearLines(lines);

        var clearList = lines.SelectMany(idx => idx).Distinct();

        var affectedColumns = clearList.Select(idx => idx % gridWidth).Distinct();
        
        var shiftOps = ShiftDown(affectedColumns);

        outResult.cleared = clearList.ToArray();
        outResult.shiftOps = shiftOps.ToArray();

        return true;
    }

    void Swap(SwapOperation swap)
    {
        if (swap.sourceIdx == -1 || swap.targetIdx == -1) return;

        ref Donut source = ref items[swap.sourceIdx];
        ref Donut target = ref items[swap.targetIdx];

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
