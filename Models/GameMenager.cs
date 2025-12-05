using System;
using System.Collections.Generic;
using SeaBattle.Enums;
using SeaBattle.Models;

namespace SeaBattle.Models
{
    public class GameManager
    {
        private GameState _gameState;
        private GameBoard _myBoard;
        private GameBoard _enemyBoard;
        private bool _isMyTurn;
        private bool _isHost;
        private List<string> _gameLog;

        public event Action GameStateChanged; 
        public event Action BoardUpdated;
        public event Action<string> MessageReceived;

        public GameManager()
        {
            _myBoard = new GameBoard();
            _enemyBoard = new GameBoard();

            _gameState = GameState.Placement;
            _isMyTurn = false;

            _gameLog = new List<string>();
            _isHost = false;

            AddToLog("Игра создана, можно начать расстановку кораблей");
        }

        public GameState CurrentGameState
        {
            get { return _gameState; }
            private set
            {
                if (_gameState != value)
                {
                    _gameState = value;
                    OnGameStateChanged();
                }
            }
        }

        public bool IsMyTurn
        {
            get { return _isMyTurn; }
            private set
            {
                _isMyTurn = value;
                OnGameStateChanged();
            }
        }

        public bool IsHost
        {
            get { return _isHost; }
            set { _isHost = value; }
        }

        public GameBoard MyBoard { get { return _myBoard; } }

        public GameBoard EnemyBoard { get { return _enemyBoard; } }

        public List<string> GameLog { get { return _gameLog; } }

        public void StartAsHost()
        {
            IsHost = true;
            AddToLog("Вы создали игру, ожидаем подключения противника");
            CurrentGameState = GameState.WaitingForConnection;
        }

        public void ConnectAsClient(string ipAddress, int port)
        {
            IsHost = false;
            AddToLog($"Подключаемся к {ipAddress}:{port}...");
            CurrentGameState = GameState.WaitingForConnection;

            // Пока заглушка
        }

        public void StartGame(bool iGoFirst)
        {
            AddToLog("Игра началась");

            if (iGoFirst)
            {
                IsMyTurn = true;
                CurrentGameState = GameState.MyTurn;
                AddToLog("Ваш ход первый");
            }
            else
            {
                IsMyTurn = false;
                CurrentGameState = GameState.EnemyTurn;
                AddToLog("Противник ходит первым, ожидайте");
            }

            OnBoardUpdated();
        }

        public bool PlaceShip(int size, int startX, int startY, bool isHorizontal)
        {

            if (CurrentGameState != GameState.Placement)
            {
                AddToLog("Сейчас нельзя расставлять корабли, игра уже началась");
                return false;
            }

            Ship ship = new Ship(size);

            bool success = _myBoard.PlaceShip(ship, startX, startY, isHorizontal);

            if (success)
            {
                AddToLog($"Корабль размером {size} размещен на ({startX},{startY})");
                OnBoardUpdated();

                CheckIfAllShipsPlaced();
            }
            else
            {
                AddToLog($"Не удалось разместить корабль на ({startX},{startY})");
            }

            return success;
        }

        public void AutoPlaceAllShips()
        {
            if (CurrentGameState != GameState.Placement)
                return;

            _myBoard.AutoPlaceAllShips();
            AddToLog("Все корабли расставлены автоматически");
            OnBoardUpdated();

            CheckIfAllShipsPlaced();
        }

        private void CheckIfAllShipsPlaced()
        {

            if (_myBoard.Ships.Count >= 10) //пока заглушка
            {
                AddToLog("Все корабли расставлены! Готовы к игре.");
            }
        }

        public CellState SendShot(int x, int y)
        {
            if (CurrentGameState != GameState.MyTurn)
            {
                AddToLog("Сейчас не ваш ход!");
                return CellState.Empty;
            }

            CellState currentState = _enemyBoard.GetCellState(x, y);
            if (currentState == CellState.Miss ||
                currentState == CellState.Hit ||
                currentState == CellState.Sunk)
            {
                AddToLog($"Вы уже стреляли в ({x},{y})!");
                return currentState;
            }

            AddToLog($"Выстрел по ({x},{y})...");

            Random random = new Random();
            CellState[] possibleResults = { CellState.Miss, CellState.Hit };
            CellState simulatedResult = possibleResults[random.Next(2)];

            return ProcessShotResult(x, y, simulatedResult);
        }

        public CellState ProcessShotResult(int x, int y, CellState result)
        {
            if (result == CellState.Miss)
            {
                _enemyBoard.ReceiveShot(x, y);
                AddToLog($"Промах по ({x},{y})!");

                IsMyTurn = false;
                CurrentGameState = GameState.EnemyTurn;
            }
            else if (result == CellState.Hit || result == CellState.Sunk)
            {
                _enemyBoard.ReceiveShot(x, y);

                if (result == CellState.Sunk)
                {
                    AddToLog($"Потоплен корабль на ({x},{y})!");
                }
                else
                {
                    AddToLog($"Попадание по ({x},{y})!");
                }

                IsMyTurn = true;
                CurrentGameState = GameState.MyTurn;
            }

            OnBoardUpdated();

            if (_enemyBoard.AllShipsSunk)
            {
                EndGame(true);
            }

            return result;
        }

        public CellState ProcessEnemyShot(int x, int y)
        {
            AddToLog($"Противник стреляет по ({x},{y})...");

            CellState result = _myBoard.ReceiveShot(x, y);

            if (result == CellState.Miss)
            {
                AddToLog("Противник промахнулся!");

                IsMyTurn = true;
                CurrentGameState = GameState.MyTurn;
            }
            else if (result == CellState.Hit || result == CellState.Sunk)
            {
                if (result == CellState.Sunk)
                {
                    AddToLog("Противник потопил наш корабль!");
                }
                else
                {
                    AddToLog("Противник попал!");
                }

                IsMyTurn = false;
                CurrentGameState = GameState.EnemyTurn;
            }

            OnBoardUpdated();

            if (_myBoard.AllShipsSunk)
            {
                EndGame(false);
            }

            return result;
        }

        private void EndGame(bool iWon)
        {
            CurrentGameState = GameState.GameOver;

            if (iWon)
            {
                AddToLog("ПОБЕДА");
            }
            else
            {
                AddToLog("ПОРАЖЕНИЕ");
            }

            SaveGameLog();
        }

        private void AddToLog(string message)
        {
            string timestamp = DateTime.Now.ToString("HH:mm:ss");
            string logEntry = $"[{timestamp}] {message}";

            _gameLog.Add(logEntry);
            OnMessageReceived(logEntry);

            if (_gameLog.Count > 1000)
            {
                _gameLog.RemoveAt(0);
            }
        }

        private void SaveGameLog()
        {
            // Пока заглушка
            AddToLog("Лог игры сохранен (заглушка)");
        }

        public void ResetGame()
        {
            _myBoard.ResetBoard();
            _enemyBoard.ResetBoard();
            _gameLog.Clear();

            CurrentGameState = GameState.Placement;
            IsMyTurn = false;

            AddToLog("Игра сброшена. Начните новую партию.");
            OnBoardUpdated();
        }

        // Методы для вызова событий

        private void OnGameStateChanged()
        {
            GameStateChanged?.Invoke();
        }

        private void OnBoardUpdated()
        {
            BoardUpdated?.Invoke();
        }

        private void OnMessageReceived(string message)
        {
            MessageReceived?.Invoke(message);
        }

        public void LoadGameFromLog(string filePath)
        {
            AddToLog($"Загружаем игру из {filePath} (заглушка)");
            ResetGame();
        }
    }
}