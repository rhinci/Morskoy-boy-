using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using SeaBattle.Enums;
using SeaBattle.Models;

namespace SeaBattle.Network
{
    public class NetworkManager
    {

        private TcpListener _tcpListener;
        private TcpClient _tcpClient;
        private NetworkStream _stream;

        private Thread _listeningThread;
        private bool _isListening;

        private string _remoteIp; 
        private int _remotePort;

        public event Action<GameMessage> MessageReceived;
        public event Action Connected; 
        public event Action Disconnected;
        public event Action<string> ErrorOccurred;

        public bool IsConnected { get; private set; }
        public bool IsHost { get; private set; }
        public string LocalIP { get; private set; }
        public int LocalPort { get; private set; }

        public NetworkManager()
        {
            IsConnected = false;
            _isListening = false;
            LocalIP = GetLocalIPAddress();
        }

        public bool StartServer(int port)
        {
            try
            {
                IsHost = true;
                LocalPort = port;

                _tcpListener = new TcpListener(IPAddress.Any, port);
                _tcpListener.Start();

                OnErrorOccurred($"Сервер запущен на порту {port}. Ожидаем подключения...");

                Task.Run(() => WaitForClientConnection());

                return true;
            }
            catch (Exception ex)
            {
                OnErrorOccurred($"Ошибка запуска сервера: {ex.Message}");
                return false;
            }
        }

        private async Task WaitForClientConnection()
        {
            try
            {
                OnErrorOccurred("Ожидаем подключения клиента...");

                _tcpClient = await _tcpListener.AcceptTcpClientAsync();
                _stream = _tcpClient.GetStream();

                var remoteEndPoint = _tcpClient.Client.RemoteEndPoint as IPEndPoint;
                _remoteIp = remoteEndPoint?.Address.ToString();
                _remotePort = remoteEndPoint?.Port ?? 0;

                IsConnected = true;
                OnErrorOccurred($"Клиент подключен: {_remoteIp}:{_remotePort}");

                _tcpListener.Stop();

                StartListening();

                OnConnected();
            }
            catch (Exception ex)
            {
                OnErrorOccurred($"Ошибка при ожидании подключения: {ex.Message}");
            }
        }

        public bool ConnectToServer(string ipAddress, int port)
        {
            try
            {
                IsHost = false;

                OnErrorOccurred($"Подключаемся к {ipAddress}:{port}...");

                _tcpClient = new TcpClient();
                _tcpClient.Connect(ipAddress, port);

                _stream = _tcpClient.GetStream();

                var localEndPoint = _tcpClient.Client.LocalEndPoint as IPEndPoint;
                LocalPort = localEndPoint?.Port ?? 0;

                IsConnected = true;
                OnErrorOccurred($"Успешно подключились к серверу");

                StartListening();

                OnConnected();
                return true;
            }
            catch (Exception ex)
            {
                OnErrorOccurred($"Ошибка подключения: {ex.Message}");
                return false;
            }
        }

        // Общие методы

        private void StartListening()
        {
            if (_isListening) return;

            _isListening = true;
            _listeningThread = new Thread(ListenForMessages);
            _listeningThread.IsBackground = true;
            _listeningThread.Start();
        }

        private void ListenForMessages()
        {
            byte[] buffer = new byte[4096];
            StringBuilder messageBuilder = new StringBuilder();

            try
            {
                while (_isListening && IsConnected && _stream != null)
                {

                    int bytesRead = _stream.Read(buffer, 0, buffer.Length);

                    if (bytesRead == 0)
                    {
                        Disconnect();
                        break;
                    }

                    string receivedData = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                    messageBuilder.Append(receivedData);

                    ProcessReceivedData(messageBuilder);
                }
            }
            catch (Exception ex)
            {
                if (_isListening)
                {
                    OnErrorOccurred($"Ошибка при чтении сообщений: {ex.Message}");
                    Disconnect();
                }
            }
        }

        private void ProcessReceivedData(StringBuilder dataBuilder)
        {
            string data = dataBuilder.ToString();

            string[] messages = data.Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);

            if (!data.EndsWith("\n") && messages.Length > 0)
            {
                dataBuilder.Clear();
                dataBuilder.Append(messages[messages.Length - 1]);

                Array.Resize(ref messages, messages.Length - 1);
            }
            else
            {
                dataBuilder.Clear();
            }

            foreach (string messageJson in messages)
            {
                try
                {
                    GameMessage message = GameMessage.FromJson(messageJson.Trim());

                    OnMessageReceived(message);
                }
                catch (Exception ex)
                {
                    OnErrorOccurred($"Ошибка парсинга сообщения: {ex.Message}");
                }
            }
        }

        public bool SendMessage(GameMessage message)
        {
            if (!IsConnected || _stream == null)
            {
                OnErrorOccurred("Нет соединения для отправки сообщения");
                return false;
            }

            try
            {
                string json = message.ToJson();

                json += "\n";

                byte[] data = Encoding.UTF8.GetBytes(json);

                _stream.Write(data, 0, data.Length);
                _stream.Flush();

                return true;
            }
            catch (Exception ex)
            {
                OnErrorOccurred($"Ошибка отправки сообщения: {ex.Message}");
                Disconnect();
                return false;
            }
        }

        public bool SendText(string text)
        {
            var message = GameMessage.CreateChatMessage(text);
            return SendMessage(message);
        }

        public void Disconnect()
        {
            try
            {
                _isListening = false;
                IsConnected = false;

                if (_listeningThread != null && _listeningThread.IsAlive)
                {
                    _listeningThread.Join(1000); 
                }

                _stream?.Close();
                _tcpClient?.Close();
                _tcpListener?.Stop();

                OnDisconnected();
                OnErrorOccurred("Соединение разорвано");
            }
            catch (Exception ex)
            {
                OnErrorOccurred($"Ошибка при отключении: {ex.Message}");
            }
        }

        private string GetLocalIPAddress()
        {
            try
            {
                var host = Dns.GetHostEntry(Dns.GetHostName());
                foreach (var ip in host.AddressList)
                {
                    if (ip.AddressFamily == AddressFamily.InterNetwork)
                    {
                        return ip.ToString();
                    }
                }
                return "127.0.0.1";
            }
            catch
            {
                return "127.0.0.1";
            }
        }


        private void OnMessageReceived(GameMessage message)
        {
            MessageReceived?.Invoke(message);
        }

        private void OnConnected()
        {
            Connected?.Invoke();
        }

        private void OnDisconnected()
        {
            Disconnected?.Invoke();
        }

        private void OnErrorOccurred(string error)
        {
            ErrorOccurred?.Invoke(error);
        }

        public static void TestSerialization()
        {
            Console.WriteLine("=== Тестирование сериализации ===");

            var shotMessage = GameMessage.CreateShotMessage(5, 3);
            shotMessage.Sender = "Player1";
            string json1 = shotMessage.ToJson();
            Console.WriteLine($"Выстрел: {json1}");

            var parsedMessage = GameMessage.FromJson(json1);
            Console.WriteLine($"Парсинг: Type={parsedMessage.MessageType}, X={parsedMessage.GetData("x", -1)}");

            var resultMessage = GameMessage.CreateShotResultMessage(5, 3, CellState.Hit);
            string json2 = resultMessage.ToJson();
            Console.WriteLine($"Результат: {json2}");
        }
    }
}