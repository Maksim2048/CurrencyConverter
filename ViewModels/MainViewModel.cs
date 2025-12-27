using CurrencyConverter.Models;
using CurrencyConverter.Services;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;

namespace CurrencyConverter.ViewModels
{
    public class MainViewModel : INotifyPropertyChanged
    {
        private readonly CurrencyService _currencyService;
        private CurrencyData? _currentRates;

        // Выбранная дата для курсов
        private DateTime _selectedDate = DateTime.Today;
        public DateTime SelectedDate
        {
            get => _selectedDate;
            set
            {
                if (_selectedDate != value)
                {
                    _selectedDate = value;
                    OnPropertyChanged();
                    // При изменении даты загружаем новые курсы
                    _ = LoadCurrencyRatesAsync();
                }
            }
        }

        // Сумма для конвертации
        private decimal _amount = 100;
        public decimal Amount
        {
            get => _amount;
            set
            {
                if (_amount != value)
                {
                    _amount = value;
                    OnPropertyChanged();
                    // Пересчитываем результат
                    ConvertCurrency();
                }
            }
        }

        // Результат конвертации
        private decimal _convertedAmount;
        public decimal ConvertedAmount
        {
            get => _convertedAmount;
            set
            {
                if (_convertedAmount != value)
                {
                    _convertedAmount = value;
                    OnPropertyChanged();
                }
            }
        }

        // Подсказка о дате (если курс найден не на ту дату)
        private string _dateHint = string.Empty;
        public string DateHint
        {
            get => _dateHint;
            set
            {
                if (_dateHint != value)
                {
                    _dateHint = value;
                    OnPropertyChanged();
                }
            }
        }

        // Индикатор загрузки
        private bool _isLoading;
        public bool IsLoading
        {
            get => _isLoading;
            set
            {
                if (_isLoading != value)
                {
                    _isLoading = value;
                    OnPropertyChanged();
                }
            }
        }

        // Коллекция всех валют (для выпадающих списков)
        public ObservableCollection<CurrencyItem> Currencies { get; } = new();

        // Выбранная исходная валюта
        private CurrencyItem? _selectedFromCurrency;
        public CurrencyItem? SelectedFromCurrency
        {
            get => _selectedFromCurrency;
            set
            {
                if (!Equals(_selectedFromCurrency, value))
                {
                    _selectedFromCurrency = value;
                    OnPropertyChanged();
                    // Пересчитываем результат
                    ConvertCurrency();
                    // Сохраняем выбор
                    SaveSettings();
                }
            }
        }

        // Выбранная целевая валюта
        private CurrencyItem? _selectedToCurrency;
        public CurrencyItem? SelectedToCurrency
        {
            get => _selectedToCurrency;
            set
            {
                if (!Equals(_selectedToCurrency, value))
                {
                    _selectedToCurrency = value;
                    OnPropertyChanged();
                    // Пересчитываем результат
                    ConvertCurrency();
                    // Сохраняем выбор
                    SaveSettings();
                }
            }
        }

        // Команды для кнопок
        public ICommand SwapCurrenciesCommand { get; }

        public MainViewModel()
        {
            _currencyService = new CurrencyService();
            SwapCurrenciesCommand = new Command(SwapCurrencies);
            _ = InitializeAsync();
        }

        private async Task InitializeAsync()
        {
            // Сначала добавляем российский рубль
            var rub = new CurrencyItem
            {
                CharCode = "RUB",
                Name = "Российский рубль",
                Nominal = 1,
                Value = 1,
                Previous = 1
            };

            // Проверяем, есть ли уже рубль в коллекции
            if (!Currencies.Any(c => c.CharCode == "RUB"))
            {
                Currencies.Add(rub);
            }

            // Загружаем сохранённые настройки
            await LoadSettings();

            // Загружаем актуальные курсы
            await LoadCurrencyRatesAsync();

            // Если валюты не выбраны, устанавливаем по умолчанию
            SelectedFromCurrency ??= Currencies.FirstOrDefault(c => c.CharCode == "RUB");
            SelectedToCurrency ??= Currencies.FirstOrDefault(c => c.CharCode == "USD");
        }

        private async Task LoadCurrencyRatesAsync()
        {
            if (IsLoading) return;

            IsLoading = true;
            DateHint = string.Empty;

            try
            {
                // Получаем курсы через сервис
                var result = await _currencyService.GetRatesAsync(SelectedDate);
                _currentRates = result.response;

                if (_currentRates is not null)
                {
                    // Проверяем, совпадает ли фактическая дата с запрошенной
                    if (result.actualDate.Date != SelectedDate.Date)
                    {
                        DateHint = $"Курсы на {SelectedDate:dd.MM.yyyy} не найдены. " +
                                  $"Показаны курсы на {result.actualDate:dd.MM.yyyy}";
                        // Обновляем дату на фактическую
                        SelectedDate = result.actualDate;
                    }

                    // Обновляем коллекцию валют
                    await UpdateCurrencies(_currentRates.Valute.Values);

                    // Пересчитываем конвертацию
                    ConvertCurrency();

                    // Сохраняем настройки
                    SaveSettings();
                }
                else
                {
                    DateHint = "Не удалось загрузить курсы.";
                }
            }
            catch (Exception ex)
            {
                DateHint = $"Ошибка: {ex.Message}";
                Console.WriteLine($"Ошибка загрузки: {ex}");
            }
            finally
            {
                IsLoading = false;
            }
        }

        private async Task UpdateCurrencies(IEnumerable<CurrencyItem> newCurrencies)
        {
            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                // Сохраняем выбранные коды валют
                var selectedFromCode = SelectedFromCurrency?.CharCode;
                var selectedToCode = SelectedToCurrency?.CharCode;

                // Очищаем коллекцию
                Currencies.Clear();

                // Сначала добавляем RUB
                Currencies.Add(new CurrencyItem
                {
                    CharCode = "RUB",
                    Name = "Российский рубль",
                    Nominal = 1,
                    Value = 1,
                    Previous = 1
                });

                // Затем остальные валюты
                foreach (var CurrencyItem in newCurrencies.OrderBy(c => c.CharCode))
                {
                    Currencies.Add(CurrencyItem);
                }

                // Восстанавливаем выбранные валюты
                if (!string.IsNullOrEmpty(selectedFromCode))
                {
                    SelectedFromCurrency = Currencies.FirstOrDefault(c => c.CharCode == selectedFromCode);
                }

                if (!string.IsNullOrEmpty(selectedToCode))
                {
                    SelectedToCurrency = Currencies.FirstOrDefault(c => c.CharCode == selectedToCode);
                }

                // Если не удалось восстановить, устанавливаем значения по умолчанию
                SelectedFromCurrency ??= Currencies.FirstOrDefault(c => c.CharCode == "RUB");
                SelectedToCurrency ??= Currencies.FirstOrDefault(c => c.CharCode == "USD");
            });
        }

        private void ConvertCurrency()
        {
            if (SelectedFromCurrency is null || SelectedToCurrency is null || Amount <= 0)
            {
                ConvertedAmount = 0;
                return;
            }

            var amountInRubles = Amount * SelectedFromCurrency.RatePerOne; // стоимость в рублях
            ConvertedAmount = amountInRubles / SelectedToCurrency.RatePerOne; // стоимость в целевой валюте
        }

        // смена валют местами
        private void SwapCurrencies()
        {
            if (SelectedFromCurrency is not null && SelectedToCurrency is not null)
            {
                var temp = SelectedFromCurrency;
                SelectedFromCurrency = SelectedToCurrency;
                SelectedToCurrency = temp;
            }
        }

        private void SaveSettings()
        {
            try
            {
                Preferences.Set("SelectedDate", SelectedDate.ToString("yyyy-MM-dd"));
                Preferences.Set("Amount", Amount.ToString());
                Preferences.Set("FromCurrency", SelectedFromCurrency?.CharCode ?? "RUB");
                Preferences.Set("ToCurrency", SelectedToCurrency?.CharCode ?? "USD");
            }
            catch { }
        }

        private async Task LoadSettings()
        {
            try
            {
                await Task.Run(() =>
                {
                    // Загружаем дату
                    var dateStr = Preferences.Get("SelectedDate", string.Empty);
                    if (!string.IsNullOrEmpty(dateStr) && DateTime.TryParse(dateStr, out var savedDate))
                    {
                        SelectedDate = savedDate;
                    }

                    // Загружаем сумму
                    var amountStr = Preferences.Get("Amount", "100");
                    if (decimal.TryParse(amountStr, out var savedAmount))
                    {
                        Amount = savedAmount;
                    }
                });
            }
            catch { }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}