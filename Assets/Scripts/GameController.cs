using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UniRx;
using UnityEditor;
using System;
using TMPro;

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
}

public struct Donut
{
    public DonutGameData gameData;
    public DonutDrawData drawData;
}

public struct ClearLine
{
    public int[] indices;
}

public struct ShiftResult
{
    public int x;
    public SwapOperation[] swaps;
    public int[] createdFlavors;
}

public interface IOperation {}

public class EmptyOperation : IOperation {}

public class SwapOperation : IOperation
{
    public int sourceIdx;
    public int targetIdx;
}

public class ClearOperation : IOperation
{
    public List<ClearLine> lines;
}

public class ShiftOperation : IOperation
{
    public IEnumerable<int> columns;
}

public class GameController : MonoBehaviour
{
    public int gridWidth = 8;
    public int gridHeight = 8;
    public float cellSize = 1.0f;

    public DonutFlavor[] flavors;

    public TMP_Text scoreText;

    Donut[] items;
    IntReactiveProperty score = new(0);
    int drawScore = 0;

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

    void FindClearableLines(int startInd, int stride, int length, ref List<ClearLine> lines)
    {
        int ind = startInd;
        int count = 0;

        List<int> streak = new();
        int streakType = -1;

        for (; count < length; count++, ind += stride)
        {
            DonutGameData donut = items[ind].gameData;

            // If streak broken
            if (streak.Count > 0 && (streakType != donut.flavor || !donut.active))
            {
                if (streak.Count >= 3)
                {
                    lines.Add(new ClearLine {
                        indices = streak.ToArray()
                    });
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
            lines.Add(new ClearLine {
                indices = streak.ToArray()
            });
        }
    }

    void FindClearableLines(out List<ClearLine> outLines)
    {
        outLines = new List<ClearLine>();
        
        for (int x = 0; x < gridWidth; x++)
        {
            FindClearableLines(x, gridWidth, gridHeight, ref outLines);
        }

        for (int y = 0; y < gridHeight; y++)
        {
            FindClearableLines(y*gridWidth, 1, gridWidth, ref outLines);
        }
    }

    void FindShiftableColumns(out IEnumerable<int> columns)
    {
        columns = items.Select((donut, idx) => (donut, column: idx % gridWidth)).Where(p => !p.donut.gameData.active).Select(p => p.column).Distinct();
    }

    void ClearLines(List<ClearLine> lines)
    {
        foreach (var line in lines)
        {
            foreach(int index in line.indices) {
                items[index].gameData.active = false;
            }
        }
    }

    IEnumerable<ShiftResult> ShiftDown(IEnumerable<int> columns) {

        List<ShiftResult> result = new List<ShiftResult>();
        foreach (var column in columns)
        {
            result.Add(ShiftDownColumn(column));
        }

        return result;
    }

    ShiftResult ShiftDownColumn(int x)
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

        return new ShiftResult {
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

        // Advance game until at rest
        FindClearableLines(out var lines);
        if (lines.Count > 0)
        {
            do {
                ClearLines(lines);
                FindShiftableColumns(out var columnsToShift);
                ShiftDown(columnsToShift);
                FindClearableLines(out lines);
            } while (lines.Count > 0);
        }

        for (int i = 0; i < gridWidth*gridHeight; i++)
        {
            items[i].drawData = new DonutDrawData {
                position = CellIndexToWorldPos(i),
                scale = Vector3.one,
                flavor = flavors[items[i].gameData.flavor]
            };
        }
    }

    void SwapColors(SwapOperation swap)
    {
        DonutFlavor temp = items[swap.sourceIdx].drawData.flavor;
        items[swap.sourceIdx].drawData.flavor = items[swap.targetIdx].drawData.flavor;
        items[swap.targetIdx].drawData.flavor = temp;
    }

    void SwapScales(SwapOperation swap)
    {
        Vector3 temp = items[swap.sourceIdx].drawData.scale;
        items[swap.sourceIdx].drawData.scale = items[swap.targetIdx].drawData.scale;
        items[swap.targetIdx].drawData.scale = temp;
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

    IEnumerator AnimateShift(IEnumerable<ShiftResult> shiftRes, float duration)
    {        
        foreach(var column in shiftRes)
        {
            foreach (var swap in column.swaps)
            {
                SwapColors(swap);
                SwapScales(swap);
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

            foreach(var column in shiftRes)
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

    IEnumerator AnimateLegalMove(SwapOperation swap, float duration)
    {
        yield return AnimateSwap(swap, duration);
    }

    IEnumerator AnimateIllegalMove(SwapOperation swap, float duration)
    {
        yield return AnimateSwap(swap, duration);
        yield return AnimateSwap(swap, duration);
    }

    IEnumerator AnimateScore(int currentScore, int newScore, float duration)
    {
        float elapsed = 0.0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp(elapsed / duration, 0.0f, 1.0f);
            drawScore = (int)Mathf.Lerp(currentScore, newScore, t);

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
        InitializeBoard();

        var sourceTileStream = Observable.EveryUpdate().Where(_ => Input.GetMouseButtonDown(0)).Select(_ => GetMouseOverCell());
        var targetTileStream = Observable.EveryUpdate().Where(_ => Input.GetMouseButtonUp(0)).Select(_ => GetMouseOverCell());
        var swapStream = Observable.Merge(sourceTileStream, targetTileStream).Buffer(2)
        .Where(pair => AreNeighbors(pair[0], pair[1]) && items[pair[0]].gameData.active && items[pair[1]].gameData.active)
        .Select(pair => new SwapOperation { sourceIdx = pair[0], targetIdx = pair[1] } as IOperation);

        float stepRate = 0.2f;
        var stepStream = Observable.EveryUpdate().Sample(TimeSpan.FromSeconds(stepRate)).Select(_ => {
            FindShiftableColumns(out var columnsToShift);
            if (columnsToShift.Count() > 0)
            {
                return new ShiftOperation {
                    columns = columnsToShift
                };
            }

            FindClearableLines(out var lines);
            if (lines.Count != 0)
            {
                return new ClearOperation {
                    lines = lines
                } as IOperation;
            }

            return new EmptyOperation();
        });

        var animStream = Observable.Merge(swapStream, stepStream).
        Where(operation => operation is not EmptyOperation)
        .Select(operation => {
            if (operation is SwapOperation swap)
            {
                Swap(swap);
                FindClearableLines(out var lines);
                var legalMove = lines.Any(line => line.indices.Contains(swap.sourceIdx) || line.indices.Contains(swap.targetIdx));
                // Illegal move, so swap back
                if (!legalMove)
                {
                    Swap(swap);
                    return Observable.FromCoroutine(() => AnimateIllegalMove(swap, 0.2f));
                }

                return Observable.FromCoroutine(() => AnimateLegalMove(swap, 0.2f));
            }
            else if (operation is ClearOperation clear)
            {
                ClearLines(clear.lines);

                int baseScore = 100;
                int totalScore = 0;
                foreach(var line in clear.lines)
                {
                    int multiplier = line.indices.Length - 2;
                    totalScore += baseScore*multiplier;
                }
                score.Value += totalScore;

                var clearList = clear.lines.SelectMany(line => line.indices).Distinct();
                return Observable.FromCoroutine(() => AnimateClear(clearList.ToArray(), 0.2f));
            }

            var shift = operation as ShiftOperation;
            var shiftRes = ShiftDown(shift.columns);
            return Observable.FromCoroutine(() => AnimateShift(shiftRes, 0.2f));
        })
        .Concat()
        .Subscribe();

        score.Buffer(2,1)
        .Select(pair => {
            return Observable.FromCoroutine(() => AnimateScore(pair[0], pair[1], 0.5f));
        })
        .Concat()
        .Subscribe();
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

        scoreText.text = drawScore.ToString();
    }
}
