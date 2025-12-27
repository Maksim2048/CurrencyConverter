using System.Text.Json.Serialization;

namespace CurrencyConverter.Models
{
    public class CurrencyData
    {
        [JsonPropertyName("Date")]
        public DateTime Date { get; set; }

        [JsonPropertyName("PreviousDate")]
        public DateTime PreviousDate { get; set; }

        [JsonPropertyName("PreviousURL")]
        public string PreviousURL { get; set; } = string.Empty;

        [JsonPropertyName("Timestamp")]
        public DateTime Timestamp { get; set; }

        
        [JsonPropertyName("Valute")]
        public Dictionary<string, CurrencyItem> Valute { get; set; } = new();
    }

    // Класс для одной валюты
    public class CurrencyItem
    {
        [JsonPropertyName("ID")]
        public string ID { get; set; } = string.Empty;

        [JsonPropertyName("NumCode")]
        public string NumCode { get; set; } = string.Empty;

        [JsonPropertyName("CharCode")]
        public string CharCode { get; set; } = string.Empty; // USD, EUR и т.д.

        [JsonPropertyName("Nominal")]
        public int Nominal { get; set; } 

        [JsonPropertyName("Name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("Value")]
        public decimal Value { get; set; } // Курс в рублях

        [JsonPropertyName("Previous")]
        public decimal Previous { get; set; } // Курс на предыдущий день

        // Курс для 1 единицы валюты (Value / Nominal)
        public decimal RatePerOne => Value / Nominal;

        public string DisplayText
        {
            get => $"{CharCode} - {Name}";
        }
    }
}