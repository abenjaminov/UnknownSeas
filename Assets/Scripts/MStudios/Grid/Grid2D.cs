﻿using System;
using System.Collections.Generic;
using System.Linq;
using Ships;
using UnityEngine;
using Random = UnityEngine.Random;

namespace MStudios.Grid
{
    public class Grid2D<T> : IGrid2D
    {
        private readonly List<GridObject2D<T>> _gridObjectInstances = new List<GridObject2D<T>>();
        private readonly List<Vector2Int> _occupiedCells = new List<Vector2Int>();
        private readonly T[,] _grid;
        private readonly T _defaultValue;

        public readonly int rows;
        public readonly int columns;

        public readonly Vector2 gridPosition;
        private IGridVisual<T> _visualization;
        
        public Grid2D(Vector2 position, int rows, int columns, IGridVisual<T> visualization, T defaultValue = default(T))
        {
            _visualization = visualization;
            gridPosition = position;
            this.rows = rows;
            this.columns = columns;
            this._defaultValue = defaultValue;
            
            _grid = new T[rows, columns];

            for (int i = 0; i < rows; i++)
            {
                for (int j = 0; j < columns; j++)
                {
                    _grid[i, j] = defaultValue;
                }
            }
        }

        public bool HasCellWithValue(T value)
        {
            if (value.Equals(default(T)))
            {
                // TODO : Return for default values
                return true;
            }
            else
            {
                return _gridObjectInstances.Any(x => x.HasCellWithValue(value));
            }
        }
        
        public Vector2Int GetRandomLocalCellPositionByValue(T value)
        {
            var matchingCells = _grid.Where(x => x.Equals(value)).ToList();
            
            var randomIndex = Random.Range(0, matchingCells.Count);
            var selectedObject = matchingCells[randomIndex];

            return selectedObject;
        }
        
        public bool IsCellEmpty(Vector2 worldPosition)
        {
            var posInGridReferenceFrame = ClampToLocalGridPosition(worldPosition);
            return _grid[posInGridReferenceFrame.y, posInGridReferenceFrame.x].Equals(_defaultValue);
        }

        public T GetValueAt(Vector2 worldPosition)
        {
            var gridObject = GetObjectAt(worldPosition);

            if (gridObject == null)
            {
                return default(T);
            }
            else
            {
                return gridObject.GetValueAt(ClampToLocalGridPosition(worldPosition));
            }
        }

        private GridObject2D<T> GetObjectAt(Vector2 worldPosition)
        {
            var positionInGridReferenceFrame = ClampToLocalGridPosition(worldPosition);

            var gridObject = _gridObjectInstances.FirstOrDefault(o => o.IsPositionOnObject(positionInGridReferenceFrame));
            return gridObject;
        }

        public bool CanPutDownObject(GridObject2DData objectData, Vector2 worldPosition)
        {
            var positionInGridReferenceFrame = ClampToLocalGridPosition(worldPosition);
            foreach (var occupiedOffsetOnObject in objectData.occupiedOffsets)
            {
                var point = positionInGridReferenceFrame + occupiedOffsetOnObject;
                if (_gridObjectInstances.Any(o => o.IsPositionOnObject(point))) return false;
            }

            return true;
        }
        
        public void PutDownObject(GridObject2DData objectData, Vector2 worldPosition, T value = default(T))
        {
            var positionOnGrid = SnapToLocalGridPosition(worldPosition, objectData);
            var newGridObject = new GridObject2D<T>("Grid Object", objectData,value);
            newGridObject.SetLocalPosition(new Vector2Int(positionOnGrid.x, positionOnGrid.y));

            foreach (var position in newGridObject.gridReferenceFrameCellPositions)
            {
                _grid[position.y, position.x] = newGridObject.GetValueAt(position);
            }

            _gridObjectInstances.Add(newGridObject);
            
            _visualization.Refresh();
        }

        public void ClearGridObjectAt(Vector2Int worldPosition)
        {
            var positionOnGrid = ClampToLocalGridPosition(worldPosition);
            var gridObject = _gridObjectInstances.FirstOrDefault(x => x.gridReferenceFrameCellPositions.Contains(positionOnGrid));

            if (gridObject == null) return;

            foreach (var position in gridObject.gridReferenceFrameCellPositions)
            {
                _grid[position.y, position.x] = this._defaultValue;
            }
            
            _gridObjectInstances.Remove(gridObject);
            _visualization.Refresh();
        }

        private Vector2Int SnapToLocalGridPosition(Vector2 centerWorldPosition, GridObject2DData objectData)
        {
            int rightOffset = (int) -objectData.rightPartWidth;
            int leftOffset = (int) objectData.leftPartWidth;
            int topOffset = (int) -objectData.topPartHeight;
            int bottomOffset = (int) objectData.bottomPartHeight;
            
            return SnapToLocalGridPosition(centerWorldPosition, leftOffset, rightOffset, bottomOffset, topOffset);
        }
        
        public Vector2Int SnapToLocalGridPosition(Vector2 centerWorldPosition, int leftOffset = 0, int rightOffset = 0, int bottomOffset = 0, int topOffset = 0)
        {
            Vector2Int localGridPosition = ClampToLocalGridPosition(centerWorldPosition, leftOffset, rightOffset, bottomOffset, topOffset);

            return localGridPosition;
        }
        
        public Vector2 SnapToWorldGridPosition(Vector2 centerWorldPosition, GridObject2DData objectData)
        {
            int rightOffset = (int) -objectData.rightPartWidth;
            int leftOffset = (int) objectData.leftPartWidth;
            int topOffset = (int) -objectData.topPartHeight;
            int bottomOffset = (int) objectData.bottomPartHeight;

            return SnapToWorldGridPosition(centerWorldPosition, leftOffset, rightOffset, bottomOffset, topOffset);
        }

        public Vector2 SnapToWorldGridPosition(Vector2 centerWorldPosition, int leftOffset = 0, int rightOffset = 0, int bottomOffset = 0, int topOffset = 0)
        {
            Vector2 localGridPosition = ClampToLocalGridPosition(centerWorldPosition, leftOffset, rightOffset, bottomOffset, topOffset);

            return localGridPosition + gridPosition;
        }

        private Vector2Int ClampToLocalGridPosition(Vector2 centerWorldPosition, int leftOffset = 0, int rightOffset = 0, int bottomOffset = 0, int topOffset = 0)
        {
            var centerLocalPosition = centerWorldPosition - gridPosition;

            Vector2Int localGridPosition = new Vector2Int
            {
                x = Mathf.Clamp(Mathf.RoundToInt(centerLocalPosition.x), leftOffset,
                    GetGridMaxBoundForObject(columns, rightOffset)),
                y = Mathf.Clamp(Mathf.RoundToInt(centerLocalPosition.y), bottomOffset,
                    GetGridMaxBoundForObject(rows, topOffset))
            };
            return localGridPosition;
        }

        public void SetValue(T value, Vector2 worldPosition)
        {
            GridObject2D<T> objectToSetValue = GetObjectAt(worldPosition);

            if (objectToSetValue == null) return;

            var localPosition = ClampToLocalGridPosition(worldPosition);

            _grid[localPosition.y, localPosition.x] = value;
            
            objectToSetValue.SetValueAt(localPosition, value);
            _visualization.Refresh();
        }

        public List<GridObject2D<T>> GetObjectsOnGrid()
        {
            return _gridObjectInstances;
        }

        private GridValue<T>[,] InitializeEmptyGridValues()
        {
            GridValue<T>[,] gridValues = new GridValue<T>[rows,columns];

            for (int i = 0; i < rows; i++)
            {
                for (int j = 0; j < columns; j++)
                {
                    gridValues[i,j] = new GridValue<T>()
                    {
                        position = new Vector2(j, i),
                        value = default(T)
                    };
                }
            }

            return gridValues;
        }

        private void SetNonEmptyValues(GridValue<T>[,] gridValues)
        {
            foreach (var objectInstance in _gridObjectInstances)
            {
                foreach (var gridReferenceFrameCellPosition in objectInstance.gridReferenceFrameCellPositions)
                {
                    gridValues[gridReferenceFrameCellPosition.y, gridReferenceFrameCellPosition.x].value =
                        objectInstance.GetValueAt(gridReferenceFrameCellPosition);
                    gridValues[gridReferenceFrameCellPosition.y, gridReferenceFrameCellPosition.x].visual =
                        objectInstance.visual;
                }
            }
        }

        private int GetGridMaxBoundForObject(int targetGridAxesSize, int objectOffset)
        {
            return targetGridAxesSize + objectOffset - 1;
        }

        private Vector2 GetLocalPosition(Vector2 worldPosition)
        {
            return worldPosition - gridPosition;
        }
    }
}