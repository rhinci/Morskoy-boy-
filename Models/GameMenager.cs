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

        private NetworkManager _networkManager;
        private string _playerName;

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

            _networkManager = new NetworkManager();
            _playerName = "Player_" + new Random().Next(1000, 9999);

            SubscribeToNetworkEvents();

            AddToLog("Игра создана, можно начать расстановку кораблей");
        }

        private void SubscribeToNetworkEvents()
        {
            _networkManager.MessageReceived += OnNetworkMessageReceived;
            _networkManager.Connected += OnNetworkConnected;
            _networkManager.Disconnected += OnNetworkDisconnected;
            _networkManager.ErrorOccurred += OnNetworkError;
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

            var shotMessage = GameMessage.CreateShotMessage(x, y);
            shotMessage.Sender = _playerName;

            bool sent = _networkManager.SendMessage(shotMessage);

            if (sent)
            {
                AddToLog("Ожидаем ответа противника...");
                return CellState.Empty; // Временное значение
            }
            else
            {
                AddToLog("Не удалось отправить выстрел!");
                return CellState.Empty;
            }
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
                var gameOverMessage = GameMessage.CreateGameOverMessage(false);
                gameOverMessage.Sender = _playerName;
                _networkManager.SendMessage(gameOverMessage);

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

            Task.Delay(5000).ContinueWith(_ =>
            {
                _networkManager.Disconnect();
            });

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

            _networkManager.Disconnect();

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


        // методы сетевого взаимодействия


        public void StartAsHost(int port = 12345)
        {
            IsHost = true;

            bool serverStarted = _networkManager.StartServer(port);

            if (serverStarted)
            {
                AddToLog($"Сервер запущен на порту {port}. Ожидаем подключения...");
                CurrentGameState = GameState.WaitingForConnection;
            }
            else
            {
                AddToLog("Не удалось запустить сервер");
            }
        }

        public void ConnectAsClient(string ipAddress, int port = 12345)
        {
            IsHost = false;

            AddToLog($"Подключаемся к {ipAddress}:{port}...");
            CurrentGameState = GameState.WaitingForConnection;

            bool connected = _networkManager.ConnectToServer(ipAddress, port);

            if (!connected)
            {
                AddToLog("Не удалось подключиться к серверу");
                CurrentGameState = GameState.Placement; 
            }
        }

        private void OnNetworkMessageReceived(GameMessage message)
        {
            AddToLog($"Получено сообщение: {message.MessageType}");

            switch (message.MessageType)
            {
                case MessageType.Connect:
                    HandleConnectMessage(message);
                    break;

                case MessageType.StartGame:
                    HandleStartGameMessage(message);
                    break;

                case MessageType.Shot:
                    HandleShotMessage(message);
                    break;

                case MessageType.ShotResult:
                    HandleShotResultMessage(message);
                    break;

                case MessageType.GameOver:
                    HandleGameOverMessage(message);
                    break;

                case MessageType.Chat:
                    HandleChatMessage(message);
                    break;

                default:
                    AddToLog($"Неизвестный тип сообщения: {message.MessageType}");
                    break;
            }
        }

        private void HandleConnectMessage(GameMessage message)
        {
            string opponentName = message.GetData("playerName", "Opponent");
            AddToLog($"{opponentName} подключился к игре!");

            if (IsHost)
            {

                Random random = new Random();
                bool opponentGoesFirst = random.Next(2) == 0;

                var startMessage = GameMessage.CreateStartGameMessage(!opponentGoesFirst);
                startMessage.Sender = _playerName;
                _networkManager.SendMessage(startMessage);

                StartGame(!opponentGoesFirst);
            }
        }

        private void HandleStartGameMessage(GameMessage message)
        {
            bool iGoFirst = message.GetData("youGoFirst", false);
            StartGame(iGoFirst);
        }

        private void HandleShotMessage(GameMessage message)
        {
            int x = message.GetData("x", -1);
            int y = message.GetData("y", -1);

            if (x >= 0 && y >= 0)
            {

                CellState result = ProcessEnemyShot(x, y);

                var resultMessage = GameMessage.CreateShotResultMessage(x, y, result);
                resultMessage.Sender = _playerName;
                _networkManager.SendMessage(resultMessage);
            }
        }

        private void HandleShotResultMessage(GameMessage message)
        {
            int x = message.GetData("x", -1);
            int y = message.GetData("y", -1);
            string resultStr = message.GetData("result", "Miss");

            CellState result = CellState.Miss;
            if (Enum.TryParse(resultStr, out CellState parsedResult))
            {
                result = parsedResult;
            }

            ProcessShotResult(x, y, result);
        }

        private void HandleGameOverMessage(GameMessage message)
        {
            bool iWon = message.GetData("youWon", false);
            EndGame(iWon);
        }

        private void HandleChatMessage(GameMessage message)
        {
            string text = message.GetData("text", "");
            string sender = message.Sender;

            if (!string.IsNullOrEmpty(text))
            {
                AddToLog($"{sender}: {text}");
            }
        }

        private void OnNetworkConnected()
        {
            AddToLog("Сетевое соединение установлено!");

            var connectMessage = GameMessage.CreateConnectMessage(_playerName);
            connectMessage.Sender = _playerName;
            _networkManager.SendMessage(connectMessage);
        }

        private void OnNetworkDisconnected()
        {
            AddToLog("Сетевое соединение разорвано!");

            if (CurrentGameState != GameState.GameOver)
            {
                EndGame(false);
            }
        }

        private void OnNetworkError(string error)
        {
            AddToLog($"Сетевая ошибка: {error}");
        }

        public void SendChatMessage(string text)
        {
            if (!string.IsNullOrEmpty(text))
            {
                var chatMessage = GameMessage.CreateChatMessage(text);
                chatMessage.Sender = _playerName;
                _networkManager.SendMessage(chatMessage);

                AddToLog($"Вы: {text}");
            }
        }

        public string GetLocalIP()
        {
            return _networkManager.LocalIP;
        }
    }
}