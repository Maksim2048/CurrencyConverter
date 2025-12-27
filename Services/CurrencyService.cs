using CurrencyConverter.Models;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http.Json;
using System.Text;
using System.Threading.Tasks;

namespace CurrencyConverter.Services
{
    public class CurrencyService
    {
        private readonly HttpClient _httpClient;
        private readonly Dictionary<DateTime, CurrencyData> _cache = new();
        private const int MaxCacheSize = 30; // Храним курсы за последние 30 дней
        private const int MaxSearchDays = 7; // Ищем максимум 7 дней назад

        public CurrencyService()
        {
            _httpClient = new HttpClient
            {
                BaseAddress = new Uri("https://www.cbr-xml-daily.ru/"),
                Timeout = TimeSpan.FromSeconds(10)
            };
        }

        public async Task<(CurrencyData? response, DateTime actualDate)> GetRatesAsync(DateTime date)
        {
            date = date.Date;

            if (date > DateTime.Today)
            {
                Debug.WriteLine($"Запрошена будущая дата, используем сегодня");
                date = DateTime.Today;
            }

            // Проверяем кэш
            if (_cache.TryGetValue(date, out CurrencyData? cachedResponse))
            {
                Debug.WriteLine($"Кэш найден для даты: {date:yyyy-MM-dd}");
                return (cachedResponse, date);
            }

            // Пытаемся найти данные на запрошенную или ближайшую дату
            var result = await TryGetRatesForDateAsync(date);

            if (result.response is not null)
            {
                // Сохраняем в кэш
                AddToCache(result.actualDate, result.response);
                return (result.response, date);
            }

            // Если не нашли, ищем ближайшие даты
            for (int i = 1; i <= MaxSearchDays; i++)
            {
                var checkDate = date.AddDays(-i);
                result = await TryGetRatesForDateAsync(checkDate);

                // Проверяем кэш для checkDate
                if (_cache.TryGetValue(checkDate, out cachedResponse))
                {
                    Debug.WriteLine($"Кэш найден для даты: {checkDate:yyyy-MM-dd}");
                    return (cachedResponse, checkDate); 
                }

                if (result.response is not null)
                {
                    Debug.WriteLine($"Найдены курсы на {checkDate:yyyy-MM-dd} вместо {date:yyyy-MM-dd}");
                    AddToCache(checkDate, result.response);
                    return (result.response, checkDate);
                }
            }

            Debug.WriteLine($"Не удалось найти курсы за последние {MaxSearchDays} дней");
            return (null, date);
        }

        private async Task<(CurrencyData? response, DateTime actualDate)> TryGetRatesForDateAsync(DateTime date)
        {
            string url = date.Date == DateTime.Today.Date
                ? "daily_json.js"
                : $"archive/{date:yyyy}/{date:MM}/{date:dd}/daily_json.js";

            try
            {
                Debug.WriteLine($"Запрос: {_httpClient.BaseAddress}{url}");

                var response = await _httpClient.GetFromJsonAsync<CurrencyData>(url);

                if (response is not null)
                {
                    // Проверяем, что дата в ответе совпадает с запрошенной
                    var responseDate = response.Date.Date;

                    if (responseDate == date)
                    {
                        Debug.WriteLine($" Ответ содержит данные на запрошенную дату {date:yyyy-MM-dd}");
                        return (response, date);
                    }
                    else
                    {
                        // Сервер вернул данные на другую дату!
                        Debug.WriteLine($" Ответ содержит данные на {responseDate:yyyy-MM-dd}, а не на {date:yyyy-MM-dd}");

                        // Кэшируем их под правильной датой
                        _cache[responseDate] = response;

                        // Возвращаем null, потому что на нашу дату не нашли
                        return (null, date);
                    }
                }
            }
            catch (HttpRequestException ex)
            {
                Debug.WriteLine($"HTTP ошибка для даты {date:yyyy-MM-dd}: {ex.StatusCode}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Ошибка для даты {date:yyyy-MM-dd}: {ex.Message}");
            }

            return (null, date);
        }

        private void AddToCache(DateTime date, CurrencyData response)
        {
            _cache[date] = response;

            // Ограничиваем размер кэша
            if (_cache.Count > MaxCacheSize)
            {
                var oldestKey = _cache.Keys.OrderBy(k => k).First();
                _cache.Remove(oldestKey);
                Debug.WriteLine($"Удален из кэша: {oldestKey:yyyy-MM-dd}");
            }
        }


        // Метод для очистки кэша
        public void ClearCache()
        {
            _cache.Clear();
        }

        // Метод для получения списка доступных дат в кэше
        public IEnumerable<DateTime> GetCachedDates()
        {
            return _cache.Keys.OrderByDescending(k => k);
        }
    }
}
