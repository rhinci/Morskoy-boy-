using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace SeaBattle.Models
{
    public static class JsonLogger
    {
        public static bool SaveGameLog(List<string> gameLog, string filePath)
        {
            try
            {
                var logData = new
                {
                    Timestamp = DateTime.Now,
                    TotalMoves = gameLog.Count,
                    LogEntries = gameLog.ToArray()
                };

                string json = JsonSerializer.Serialize(logData, new JsonSerializerOptions
                {
                    WriteIndented = true
                });

                File.WriteAllText(filePath, json);
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка сохранения лога: {ex.Message}");
                return false;
            }
        }

        public static List<string> LoadGameLog(string filePath)
        {
            try
            {
                if (!File.Exists(filePath))
                    return new List<string>();

                string json = File.ReadAllText(filePath);

                var logData = JsonSerializer.Deserialize<GameLogData>(json);

                return new List<string>(logData.LogEntries);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка загрузки лога: {ex.Message}");
                return new List<string>();
            }
        }

        private class GameLogData
        {
            public DateTime Timestamp { get; set; }
            public int TotalMoves { get; set; }
            public string[] LogEntries { get; set; }
        }
    }
}