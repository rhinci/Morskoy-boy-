using System;
using System.Collections.Generic;
using System.Drawing;
using SeaBattle.Enums;
using SeaBattle.Models;

namespace SeaBattle.Models
{
    public class GameBoard
    {
        private CellState[,] _cells; 
        private List<Ship> _ships; 
        private bool _allShipsPlaced;
        private bool _allShipsSunk;

        private const int BOARD_SIZE = 10;

        public GameBoard()
        {
            _cells = new CellState[BOARD_SIZE, BOARD_SIZE];

            for (int x = 0; x < BOARD_SIZE; x++)
            {
                for (int y = 0; y < BOARD_SIZE; y++)
                {
                    _cells[x, y] = CellState.Empty;
                }
            }

            _ships = new List<Ship>();

            _allShipsPlaced = false;
            _allShipsSunk = false;
        }

        public CellState GetCellState(int x, int y)
        {
            if (x < 0 || x >= BOARD_SIZE || y < 0 || y >= BOARD_SIZE)
                return CellState.Empty;

            return _cells[x, y];
        }

        public List<Ship> Ships { get { return _ships; } }


        public bool AllShipsPlaced
        {
            get { return _allShipsPlaced; }
        }

        public bool AllShipsSunk
        {
            get { return _allShipsSunk; }
        }

        public bool IsValidShipPlacement(Ship ship, int startX, int startY, bool isHorizontal)
        {
            if (isHorizontal)
            {
                if (startX + ship.Size > BOARD_SIZE) return false;
            }
            else
            {
                if (startY + ship.Size > BOARD_SIZE) return false;
            }

            int minX = Math.Max(startX - 1, 0);
            int maxX = isHorizontal ? Math.Min(startX + ship.Size + 1, BOARD_SIZE) : Math.Min(startX + 2, BOARD_SIZE);
            int minY = Math.Max(startY - 1, 0);
            int maxY = isHorizontal ? Math.Min(startY + 2, BOARD_SIZE) : Math.Min(startY + ship.Size + 1, BOARD_SIZE);

            for (int x = minX; x < maxX; x++)
            {
                for (int y = minY; y < maxY; y++)
                {
                    if (_cells[x, y] == CellState.Ship)
                    {
                        return false;
                    }
                }
            }

            for (int i = 0; i < ship.Size; i++)
            {
                int x = isHorizontal ? startX + i : startX;
                int y = isHorizontal ? startY : startY + i;

                if (_cells[x, y] != CellState.Empty)
                {
                    return false;
                }
            }

            return true;
        }

        public bool PlaceShip(Ship ship, int startX, int startY, bool isHorizontal)
        {
            if (!IsValidShipPlacement(ship, startX, startY, isHorizontal))
                return false;

            ship.Positions.Clear();

            for (int i = 0; i < ship.Size; i++)
            {
                int x = isHorizontal ? startX + i : startX;
                int y = isHorizontal ? startY : startY + i;

                _cells[x, y] = CellState.Ship;

                ship.Positions.Add(new Point(x, y));
            }

            _ships.Add(ship);

            return true;
        }

        public CellState ReceiveShot(int x, int y)
        {
            if (x < 0 || x >= BOARD_SIZE || y < 0 || y >= BOARD_SIZE)
                return CellState.Miss;

            CellState currentState = _cells[x, y];

            switch (currentState)
            {
                case CellState.Empty:
                    _cells[x, y] = CellState.Miss;
                    return CellState.Miss;

                case CellState.Ship:
                    _cells[x, y] = CellState.Hit;

                    Ship hitShip = FindShipAt(x, y);
                    if (hitShip != null)
                    {
                        hitShip.Hits.Add(new Point(x, y));

                        if (hitShip.IsSunk)
                        {
                            MarkAreaAroundSunkenShip(hitShip);
                            return CellState.Sunk;
                        }
                    }
                    return CellState.Hit;

                default:
                    return currentState; 
            }
        }

        private Ship FindShipAt(int x, int y)
        {
            foreach (Ship ship in _ships)
            {
                foreach (Point position in ship.Positions)
                {
                    if (position.X == x && position.Y == y)
                    {
                        return ship;
                    }
                }
            }
            return null;
        }

        public void MarkAreaAroundSunkenShip(Ship ship)
        {
            foreach (Point position in ship.Positions)
            {

                for (int dx = -1; dx <= 1; dx++)
                {
                    for (int dy = -1; dy <= 1; dy++)
                    {
                        int nx = position.X + dx;
                        int ny = position.Y + dy;

                        if (nx >= 0 && nx < BOARD_SIZE && ny >= 0 && ny < BOARD_SIZE)
                        {
                            if (_cells[nx, ny] == CellState.Empty)
                            {
                                _cells[nx, ny] = CellState.Miss;
                            }
                        }
                    }
                }
            }

            foreach (Point position in ship.Positions)
            {
                _cells[position.X, position.Y] = CellState.Sunk;
            }

            CheckAllShipsSunk();
        }

        private void CheckAllShipsSunk()
        {
            foreach (Ship ship in _ships)
            {
                if (!ship.IsSunk)
                {
                    _allShipsSunk = false;
                    return;
                }
            }
            _allShipsSunk = true;
        }

        public void AutoPlaceAllShips()
        {

            _ships.Clear();
            for (int x = 0; x < BOARD_SIZE; x++)
            {
                for (int y = 0; y < BOARD_SIZE; y++)
                {
                    _cells[x, y] = CellState.Empty;
                }
            }

            int[] shipSizes = { 4, 3, 3, 2, 2, 2, 1, 1, 1, 1 };

            Random random = new Random();

            foreach (int size in shipSizes)
            {
                Ship ship = new Ship(size);
                bool placed = false;
                int attempts = 0;

                while (!placed && attempts < 100)
                {
                    int startX = random.Next(BOARD_SIZE);
                    int startY = random.Next(BOARD_SIZE);
                    bool isHorizontal = random.Next(2) == 0;

                    placed = PlaceShip(ship, startX, startY, isHorizontal);
                    attempts++;
                }

                if (!placed)
                {
                    AutoPlaceAllShips();
                    return;
                }
            }

            _allShipsPlaced = true;
        }


        public void ResetBoard()
        {
            for (int x = 0; x < BOARD_SIZE; x++)
            {
                for (int y = 0; y < BOARD_SIZE; y++)
                {
                    _cells[x, y] = CellState.Empty;
                }
            }

            _ships.Clear();

            _allShipsPlaced = false;
            _allShipsSunk = false;
        }
    }
}