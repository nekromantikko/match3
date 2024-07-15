using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum DonutType
{
    Empty,
    Vanilla,
    Chocolate,
    Banana,
    Berlin,
    Green
}

public class GameController : MonoBehaviour
{
    public int gridWidth = 8;
    public int gridHeight = 8;
    public float cellSize = 1.0f;

    // Temp
    public Mesh mesh;
    public Material material;

    int sourceCell = -1;
    int targetCell = -1;

    DonutType[] items;

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
        Vector3 cellPos = CellIndexToWorldPos(index);

        Color color = new Color();
        switch(items[index])
        {
            case DonutType.Empty:
            return;
            case DonutType.Vanilla:
            color = Color.white;
            break;
            case DonutType.Chocolate:
            color = Color.gray;
            break;
            case DonutType.Banana:
            color = Color.yellow;
            break;
            case DonutType.Berlin:
            color = Color.red;
            break;
            case DonutType.Green:
            color = Color.green;
            break;
            default:
            break;
        }

        MaterialPropertyBlock propertyBlock = new MaterialPropertyBlock();
        propertyBlock.SetColor("_BaseColor", color);

        Graphics.DrawMesh(mesh, cellPos, Quaternion.identity, material, 0, null, 0, propertyBlock);
    }

    void ClearRow(int startInd, int stride, int length, ref List<int> clearList)
    {
        int ind = startInd;
        int count = 0;

        List<int> streak = new List<int>();
        DonutType streakType = DonutType.Empty;

        for (; count < length; count++, ind += stride)
        {
            // If streak broken
            if (streakType != items[ind] && streakType != DonutType.Empty)
            {
                if (streak.Count >= 3)
                {
                    clearList.AddRange(streak);
                }
                streak.Clear();
            }
            
            streakType = items[ind];
            if (streakType != DonutType.Empty)
            {
                streak.Add(ind);
            }
        }

        if (streak.Count >= 3)
        {
            clearList.AddRange(streak);
        }
    }

    // Returns true if clearing happened
    bool ClearRows()
    {
        List<int> clearList = new List<int>();
        for (int x = 0; x < gridWidth; x++)
        {
            ClearRow(x, gridWidth, gridHeight, ref clearList);
        }

        for (int y = 0; y < gridHeight; y++)
        {
            ClearRow(y*gridWidth, 1, gridWidth, ref clearList);
        }

        foreach (int clearIndex in clearList)
        {
            items[clearIndex] = DonutType.Empty;
        }

        return clearList.Count > 0;
    }

    // Returns whether this should be called again afterwards
    bool ApplyGravityToColumn(int x)
    {
        bool keepGoing = false;
        for (int y = 1; y < gridHeight; y++)
        {
            int index = x + gridWidth*y;

            if (items[index] == DonutType.Empty)
            {
                continue;
            }

            int belowIndex = index - gridWidth;
            if (items[belowIndex] == DonutType.Empty)
            {
                items[belowIndex] = items[index];
                items[index] = DonutType.Empty;

                keepGoing = true;
            }
        }

        return keepGoing;
    }

    void ApplyGravity() {
        for (int x = 0; x < gridWidth; x++)
        {
            while(ApplyGravityToColumn(x)) {}
        }
    }

    void FillEmptySpaces() {
        for (int i = 0; i < gridWidth*gridHeight; i++)
        {
            if (items[i] != DonutType.Empty) continue;

            int index = UnityEngine.Random.Range(1, 6);
            items[i] = (DonutType)index;
        }
    }

    // Start is called before the first frame update
    void Start()
    {
        items = new DonutType[gridWidth*gridHeight];

        FillEmptySpaces();
        while (Step()) {}
    }

    // Advances the game
    // Returns true if board was changed
    bool Step()
    {
        if (!ClearRows()) return false;
        ApplyGravity();
        FillEmptySpaces();
        return true;
    }

    bool TrySwap(int source, int target)
    {
        if (sourceCell == -1 || targetCell == -1) return false;

        DonutType temp = items[sourceCell];
        items[sourceCell] = items[targetCell];
        items[targetCell] = temp;
        return true;
    }

    // Update is called once per frame
    void Update()
    {
        Vector3 mousePos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
        int mouseOverCell = WorldPosToCellIndex(mousePos);

        if (Input.GetMouseButtonDown(0))
        {
            sourceCell = mouseOverCell;
        }
        else if (Input.GetMouseButtonUp(0))
        {
            if (TrySwap(sourceCell, targetCell)) {
                // If swapping has no effect, swap back
                if (!Step()) {
                    TrySwap(sourceCell, targetCell);
                }
                else while(Step()) {}
            }

            sourceCell = -1;
            targetCell = -1;
        }
        else if (Input.GetMouseButton(0))
        {
            if (AreNeighbors(sourceCell, mouseOverCell))
            {
                targetCell = mouseOverCell;
            }
            else targetCell = -1;
        }

        for (int i = 0; i < gridWidth * gridHeight; i++)
        {
            DrawCell(i);
        }
    }
}
