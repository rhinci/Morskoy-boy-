using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;
using SeaBattle.Enums;

namespace SeaBattle.Network
{
    public class GameMessage
    {
        private MessageType _messageType;
        private Dictionary<string, object> _data;
        private string _sender;

        public GameMessage()
        {
            _data = new Dictionary<string, object>();
            _sender = "Unknown";
        }

        public GameMessage(MessageType type) : this()
        {
            _messageType = type;
        }

        public GameMessage(MessageType type, Dictionary<string, object> data) : this(type)
        {
            _data = data ?? new Dictionary<string, object>();
        }

        [JsonPropertyName("type")]
        public MessageType MessageType
        {
            get { return _messageType; }
            set { _messageType = value; }
        }

        [JsonPropertyName("data")]
        public Dictionary<string, object> Data
        {
            get { return _data; }
            set { _data = value ?? new Dictionary<string, object>(); }
        }

        [JsonPropertyName("sender")]
        public string Sender
        {
            get { return _sender; }
            set { _sender = value; }
        }

        [JsonPropertyName("timestamp")]
        public DateTime Timestamp { get; set; } = DateTime.Now;

        public void AddData(string key, object value)
        {
            if (_data == null)
                _data = new Dictionary<string, object>();

            _data[key] = value;
        }

        public T GetData<T>(string key, T defaultValue = default)
        {
            if (_data == null || !_data.ContainsKey(key))
                return defaultValue;

            try
            {
                var value = _data[key];

                if (value is T typedValue)
                    return typedValue;

                if (typeof(T) == typeof(int))
                {
                    if (value is long longValue)
                        return (T)(object)(int)longValue;
                    if (value is double doubleValue)
                        return (T)(object)(int)doubleValue;
                }

                return (T)Convert.ChangeType(value, typeof(T));
            }
            catch
            {
                return defaultValue;
            }
        }

        public bool HasData(string key)
        {
            return _data != null && _data.ContainsKey(key);
        }

        public static GameMessage CreateConnectMessage(string playerName)
        {
            var message = new GameMessage(MessageType.Connect);
            message.AddData("playerName", playerName);
            return message;
        }

        public static GameMessage CreateStartGameMessage(bool youGoFirst)
        {
            var message = new GameMessage(MessageType.StartGame);
            message.AddData("youGoFirst", youGoFirst);
            return message;
        }

        public static GameMessage CreateShotMessage(int x, int y)
        {
            var message = new GameMessage(MessageType.Shot);
            message.AddData("x", x);
            message.AddData("y", y);
            return message;
        }

        public static GameMessage CreateShotResultMessage(int x, int y, CellState result)
        {
            var message = new GameMessage(MessageType.ShotResult);
            message.AddData("x", x);
            message.AddData("y", y);
            message.AddData("result", result.ToString());
            return message;
        }

        public static GameMessage CreateGameOverMessage(bool youWon)
        {
            var message = new GameMessage(MessageType.GameOver);
            message.AddData("youWon", youWon);
            return message;
        }

        public static GameMessage CreateChatMessage(string text)
        {
            var message = new GameMessage(MessageType.Chat);
            message.AddData("text", text);
            return message;
        }

        public string ToJson()
        {
            var options = new JsonSerializerOptions
            {
                WriteIndented = false,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase, 
                Converters = { new JsonStringEnumConverter() }
            };

            return JsonSerializer.Serialize(this, options);
        }

        public static GameMessage FromJson(string json)
        {
            try
            {
                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                    Converters = { new JsonStringEnumConverter() }
                };

                return JsonSerializer.Deserialize<GameMessage>(json, options);
            }
            catch (Exception ex)
            {
                var errorMessage = new GameMessage(MessageType.Chat);
                errorMessage.AddData("text", $"Ошибка парсинга JSON: {ex.Message}");
                return errorMessage;
            }
        }

        public override string ToString()
        {
            return $"[{Timestamp:HH:mm:ss}] {MessageType}: {ToJson()}";
        }
    }
}