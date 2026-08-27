using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Immutable uniform grid over 2D items, stored compressed-sparse-row. Built once on the
/// main thread and queried concurrently by chunk workers, which is safe because it is read-only.
/// Query results may contain the same item twice when it straddles cells; callers that
/// reduce to a minimum distance do not care.
/// </summary>
public sealed class SpatialGrid
{
    readonly Rect bounds;
    readonly float cellSize;
    readonly int columns;
    readonly int rows;
    readonly int[] cellStart;
    readonly int[] items;

    SpatialGrid(Rect bounds, float cellSize, int columns, int rows, int[] cellStart, int[] items)
    {
        this.bounds = bounds;
        this.cellSize = cellSize;
        this.columns = columns;
        this.rows = rows;
        this.cellStart = cellStart;
        this.items = items;
    }

    public static SpatialGrid Build(int itemCount, Func<int, Rect> boundsOf, Rect worldBounds, float cellSize)
    {
        cellSize = Mathf.Max(cellSize, 0.01f);
        int columns = Mathf.Max(1, Mathf.CeilToInt(worldBounds.width / cellSize));
        int rows = Mathf.Max(1, Mathf.CeilToInt(worldBounds.height / cellSize));

        int[] counts = new int[columns * rows];

        for (int i = 0; i < itemCount; i++)
        {
            Rect itemBounds = boundsOf(i);
            int minX, minY, maxX, maxY;
            CellRange(itemBounds, worldBounds, cellSize, columns, rows, out minX, out minY, out maxX, out maxY);

            for (int y = minY; y <= maxY; y++)
            {
                for (int x = minX; x <= maxX; x++)
                {
                    counts[y * columns + x]++;
                }
            }
        }

        int[] cellStart = new int[columns * rows + 1];
        int running = 0;
        for (int c = 0; c < columns * rows; c++)
        {
            cellStart[c] = running;
            running += counts[c];
        }
        cellStart[columns * rows] = running;

        int[] items = new int[running];
        int[] cursor = new int[columns * rows];

        for (int i = 0; i < itemCount; i++)
        {
            Rect itemBounds = boundsOf(i);
            int minX, minY, maxX, maxY;
            CellRange(itemBounds, worldBounds, cellSize, columns, rows, out minX, out minY, out maxX, out maxY);

            for (int y = minY; y <= maxY; y++)
            {
                for (int x = minX; x <= maxX; x++)
                {
                    int cell = y * columns + x;
                    items[cellStart[cell] + cursor[cell]] = i;
                    cursor[cell]++;
                }
            }
        }

        return new SpatialGrid(worldBounds, cellSize, columns, rows, cellStart, items);
    }

    static void CellRange(Rect itemBounds, Rect worldBounds, float cellSize, int columns, int rows,
        out int minX, out int minY, out int maxX, out int maxY)
    {
        minX = Mathf.Clamp(Mathf.FloorToInt((itemBounds.xMin - worldBounds.xMin) / cellSize), 0, columns - 1);
        maxX = Mathf.Clamp(Mathf.FloorToInt((itemBounds.xMax - worldBounds.xMin) / cellSize), 0, columns - 1);
        minY = Mathf.Clamp(Mathf.FloorToInt((itemBounds.yMin - worldBounds.yMin) / cellSize), 0, rows - 1);
        maxY = Mathf.Clamp(Mathf.FloorToInt((itemBounds.yMax - worldBounds.yMin) / cellSize), 0, rows - 1);
    }

    public void Query(Vector2 point, float radius, List<int> results)
    {
        results.Clear();

        Rect query = Rect.MinMaxRect(point.x - radius, point.y - radius, point.x + radius, point.y + radius);
        int minX, minY, maxX, maxY;
        CellRange(query, bounds, cellSize, columns, rows, out minX, out minY, out maxX, out maxY);

        for (int y = minY; y <= maxY; y++)
        {
            for (int x = minX; x <= maxX; x++)
            {
                int cell = y * columns + x;
                int start = cellStart[cell];
                int end = cellStart[cell + 1];

                for (int i = start; i < end; i++)
                {
                    results.Add(items[i]);
                }
            }
        }
    }
}
