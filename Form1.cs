using System.ComponentModel;
using System.Globalization;
using WinFormsApp1.Models;
using WinFormsApp1.Services;

namespace WinFormsApp1;

public partial class Form1 : Form
{
    private readonly DataManager _data;
    private readonly BusinessService _business;
    private readonly TabControl _tabs = new() { Dock = DockStyle.Fill };
    private readonly Dictionary<string, DataGridView> _grids = [];
    private readonly Dictionary<string, TextBox> _searchBoxes = [];

    public Form1(DataManager data)
    {
        _data = data;
        _business = new BusinessService(data);

        InitializeComponent();
        Text = "Учёт услуг плавательного бассейна ПолесГУ";
        Width = 1280;
        Height = 760;
        MinimumSize = new Size(1000, 620);
        StartPosition = FormStartPosition.CenterScreen;
        Font = new Font("Segoe UI", 9.5f);

        var header = new Panel { Dock = DockStyle.Top, Height = 72, BackColor = Color.FromArgb(25, 75, 115) };
        header.Controls.Add(new Label
        {
            Text = "ПОЛЕССКИЙ ГОСУДАРСТВЕННЫЙ УНИВЕРСИТЕТ\nУчёт и расчёт услуг плавательного бассейна",
            ForeColor = Color.White,
            Font = new Font("Segoe UI", 14, FontStyle.Bold),
            AutoSize = true,
            Location = new Point(18, 12)
        });

        var footer = new StatusStrip();
        footer.Items.Add(new ToolStripStatusLabel($"Файловое хранилище JSON: {_data.DataDirectory}"));

        _tabs.SelectedIndexChanged += (_, _) =>
        {
            string key = _tabs.SelectedTab?.Text switch
            {
                "Клиенты" => "clients", "Услуги и тарифы" => "services",
                "Бассейны и расписание" => "sessions", "Билеты и абонементы" => "passes", _ => ""
            };
            if (!string.IsNullOrEmpty(key)) ApplySearch(key);
        };
        Controls.Add(_tabs);
        Controls.Add(header);
        Controls.Add(footer);
        BuildTabs();
        RefreshAll();
    }

    private void BuildTabs()
    {
        AddGridTab("Клиенты", "clients", ClientColumns(),
            ("Добавить", AddClient), ("Изменить", EditClient), ("Удалить", DeleteClient));
        AddGridTab("Услуги и тарифы", "services", ServiceColumns(),
            ("Добавить услугу", AddService), ("Изменить услугу", EditService), ("Удалить услугу", DeleteService),
            ("Добавить тариф", AddTariff), ("Изменить тариф", EditTariff), ("Удалить тариф", DeleteTariff));
        AddGridTab("Бассейны и расписание", "sessions", SessionColumns(),
            ("Добавить сеанс", AddSession), ("Изменить сеанс", EditSession), ("Удалить сеанс", DeleteSession));
        AddGridTab("Продажи и платежи", "sales", SaleColumns(),
            ("Оформить продажу", CreateSale), ("Обновить", RefreshAll));
        AddGridTab("Билеты и абонементы", "passes", PassColumns(),
            ("Аннулировать билет / абонемент", CancelPass), ("Обновить", RefreshAll));
        AddGridTab("Посещения", "visits", VisitColumns(),
            ("Зарегистрировать вход", RegisterVisit));
        AddGridTab("Отчёты", "reports", ReportColumns(),
            ("Сформировать отчёт", CreateReport), ("Экспорт в TXT", ExportReport));
    }

    private void AddGridTab(string title, string key, string[] columns,
        params (string Text, Action Action)[] actions)
    {
        var page = new TabPage(title);
        var top = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 48,
            Padding = new Padding(7),
            WrapContents = false
        };
        foreach (var action in actions)
        {
            var button = new Button { Text = action.Text, AutoSize = true, Height = 30 };
            button.Click += (_, _) => Safe(action.Action);
            top.Controls.Add(button);
        }
        if (key is "clients" or "services" or "sessions" or "passes")
        {
            // У каждого раздела собственное поле поиска: WinForms не должен
            // переносить один и тот же Control между несколькими вкладками.
            var search = new TextBox
            {
                Width = 240,
                PlaceholderText = "Поиск по текущему разделу...",
                Margin = new Padding(8, 3, 3, 3)
            };
            search.TextChanged += (_, _) => ApplySearch(key);
            _searchBoxes[key] = search;
            top.Controls.Add(search);
        }

        var grid = CreateGrid(columns);
        _grids[key] = grid;
        page.Controls.Add(grid);
        page.Controls.Add(top);
        _tabs.TabPages.Add(page);
    }

    private static DataGridView CreateGrid(string[] columns)
    {
        var grid = new DataGridView
        {
            Dock = DockStyle.Fill,
            ReadOnly = true,
            AllowUserToAddRows = false,
            AllowUserToDeleteRows = false,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            MultiSelect = false,
            BackgroundColor = Color.White,
            RowHeadersVisible = false
        };
        foreach (string column in columns)
        {
            grid.Columns.Add(column, column);
            if (column.StartsWith("__", StringComparison.Ordinal))
                grid.Columns[column]!.Visible = false;
        }
        return grid;
    }

    private static string[] ClientColumns() => ["ID", "ФИО", "Телефон", "Категория", "Активен"];
    private static string[] ServiceColumns() => ["Тип", "Наименование", "Бассейн", "Длительность", "Категория тарифа", "Цена", "Действует с", "Действует до", "Количество занятий", "__ServiceId", "__TariffId"];
    private static string[] SessionColumns() => ["ID", "Дата", "Начало", "Окончание", "Бассейн", "Занятие", "Вместимость", "Тренер"];
    private static string[] SaleColumns() => ["Дата", "Клиент", "Сумма", "Статус", "Оплата"];
    private static string[] PassColumns() => ["Вид", "Номер", "Клиент", "Срок действия", "Использовано", "Осталось", "Статус", "__PassId", "__SaleId"];
    private static string[] VisitColumns() => ["Дата и время", "Клиент", "Сеанс", "Основание", "Сотрудник"];
    private static string[] ReportColumns() => ["Показатель", "Значение"];

    private void RefreshAll()
    {
        Fill("clients", _data.Clients.Select(c => new object?[] { c.Id, c.FullName, c.Phone, c.Category, c.IsActive }));
        Fill("services", _data.Services.SelectMany(s => _data.Tariffs.Where(t => t.ServiceId == s.Id)
            .DefaultIfEmpty().Select(t => new object?[]
            {
                s.Type, s.Name, _data.Pools.FirstOrDefault(p => p.Id == s.PoolId)?.Name ?? "—",
                s.DurationMinutes, t?.ClientCategory.ToString() ?? "Тариф не задан",
                t?.Price.ToString("0.00") ?? "—", t?.ValidFrom.ToShortDateString() ?? "—",
                t?.ValidTo.ToShortDateString() ?? "—", t?.VisitsIncluded.ToString() ?? "—",
                s.Id.ToString(), t?.Id.ToString() ?? ""
            })));
        Fill("sessions", _data.Sessions.Select(s => new object?[]
        {
            s.Id, s.Date.ToShortDateString(), s.StartTime.ToString(@"hh\:mm"),
            s.EndTime.ToString(@"hh\:mm"), _data.Pools.FirstOrDefault(p => p.Id == s.PoolId)?.Name,
            s.Activity, s.Capacity, s.CoachName
        }));
        Fill("sales", _data.Sales.Select(s => new object?[]
        {
            s.Date.ToString("g"), _data.Clients.FirstOrDefault(c => c.Id == s.ClientId)?.FullName,
            s.Total.ToString("0.00"), s.Status,
            _data.Payments.FirstOrDefault(p => p.SaleId == s.Id)?.Status.ToString() ?? "Нет платежа"
        }));
        Fill("passes", _data.Tickets.Select(t => new object?[]
        {
            "Билет", t.Number, _data.Clients.FirstOrDefault(c => c.Id == t.ClientId)?.FullName,
            _data.Sessions.FirstOrDefault(s => s.Id == t.SessionId)?.Date.ToShortDateString(),
            t.IsUsed ? "Да" : "Нет", t.IsUsed || t.IsCancelled ? "0" : "1",
            t.IsCancelled ? "Аннулирован" : t.IsUsed ? "Использован" : "Действует",
            t.Id.ToString(), t.SaleId.ToString()
        }).Concat(_data.Subscriptions.Select(a => new object?[]
        {
            "Абонемент", a.Number, _data.Clients.FirstOrDefault(c => c.Id == a.ClientId)?.FullName,
            $"{a.ValidFrom:d} – {a.ValidTo:d}", a.UsedVisits, a.IsCancelled ? 0 : a.RemainingVisits,
            a.IsCancelled ? "Аннулирован" : a.RemainingVisits > 0 && DateTime.Today <= a.ValidTo.Date ? "Действует" : "Недействителен",
            a.Id.ToString(), a.SaleId.ToString()
        })));
        Fill("visits", _data.Visits.Select(v => new object?[]
        {
            v.VisitTime.ToString("g"),
            _data.Clients.FirstOrDefault(c => c.Id == v.ClientId)?.FullName,
            _data.Sessions.FirstOrDefault(s => s.Id == v.SessionId) is { } ss
                ? $"{ss.Date:d} {ss.StartTime:hh\\:mm}" : "—",
            v.TicketId.HasValue ? "Билет" : "Абонемент", v.EmployeeName
        }));
        Fill("reports", new[]
        {
            new object?[] { "Количество клиентов", _data.Clients.Count },
            new object?[] { "Количество услуг", _data.Services.Count },
            new object?[] { "Количество тарифов", _data.Tariffs.Count },
            new object?[] { "Количество сеансов", _data.Sessions.Count },
            new object?[] { "Продажи", _data.Sales.Count },
            new object?[] { "Зарегистрированные посещения", _data.Visits.Count },
            new object?[] { "Выручка за всё время, BYN", _data.Payments.Where(p => p.Status == PaymentStatus.Оплачен).Sum(p => p.Amount).ToString("0.00") }
        });
        ApplySearch("clients");
    }

    private void Fill(string key, IEnumerable<object?[]> rows)
    {
        if (!_grids.TryGetValue(key, out var grid)) return;
        grid.Rows.Clear();
        foreach (var row in rows) grid.Rows.Add(row);
    }

    private void ApplySearch(string key)
    {
        if (!_grids.TryGetValue(key, out var grid)) return;
        string q = _searchBoxes.TryGetValue(key, out var search)
            ? search.Text.Trim()
            : string.Empty;
        foreach (DataGridViewRow row in grid.Rows)
        {
            bool visible = string.IsNullOrWhiteSpace(q) ||
                row.Cells.Cast<DataGridViewCell>().Any(c =>
                    Convert.ToString(c.Value)?.Contains(q, StringComparison.OrdinalIgnoreCase) == true);
            row.Visible = visible;
        }
    }

    private void AddClient()
    {
        using var dlg = new InputDialog("Новый клиент",
            ("Фамилия", ""), ("Имя", ""), ("Отчество", ""), ("Телефон", ""),
            ("Категория клиента", "Взрослый"));
        if (dlg.ShowDialog(this) != DialogResult.OK) return;
        if (string.IsNullOrWhiteSpace(dlg.Value("Фамилия")) || string.IsNullOrWhiteSpace(dlg.Value("Имя")))
            throw new ArgumentException("Фамилия и имя обязательны.");
        if (!Enum.TryParse<ClientCategory>(dlg.Value("Категория клиента"), true, out var category))
            throw new ArgumentException("Неизвестная категория клиента.");
        _data.Clients.Add(new Client
        {
            LastName = dlg.Value("Фамилия"), FirstName = dlg.Value("Имя"),
            MiddleName = dlg.Value("Отчество"), Phone = dlg.Value("Телефон"), Category = category
        });
        _data.SaveAll(); RefreshAll();
    }

    private void EditClient()
    {
        var client = Selected<Client>("clients", row => _data.Clients.FirstOrDefault(x => x.Id.ToString() == Convert.ToString(row.Cells[0].Value)));
        if (client is null) return;
        using var dlg = new InputDialog("Изменение клиента",
            ("Фамилия", client.LastName), ("Имя", client.FirstName),
            ("Отчество", client.MiddleName), ("Телефон", client.Phone),
            ("Категория клиента", client.Category.ToString()));
        if (dlg.ShowDialog(this) != DialogResult.OK) return;
        if (string.IsNullOrWhiteSpace(dlg.Value("Фамилия")) || string.IsNullOrWhiteSpace(dlg.Value("Имя")))
            throw new ArgumentException("Фамилия и имя обязательны.");
        client.LastName = dlg.Value("Фамилия"); client.FirstName = dlg.Value("Имя");
        client.MiddleName = dlg.Value("Отчество"); client.Phone = dlg.Value("Телефон");
        if (!Enum.TryParse<ClientCategory>(dlg.Value("Категория клиента"), true, out var category))
            throw new ArgumentException("Неизвестная категория клиента.");
        client.Category = category;
        _data.SaveAll(); RefreshAll();
    }

    private void DeleteClient()
    {
        var client = Selected<Client>("clients", row => _data.Clients.FirstOrDefault(x => x.Id.ToString() == Convert.ToString(row.Cells[0].Value)));
        if (client is null) return;
        if (_data.Sales.Any(s => s.ClientId == client.Id))
            throw new InvalidOperationException("Нельзя удалить клиента, у которого есть продажи. Можно изменить его статус вручную.");
        if (MessageBox.Show($"Удалить клиента {client.FullName}?", "Подтверждение",
            MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
        _data.Clients.Remove(client); _data.SaveAll(); RefreshAll();
    }

    private void AddService()
    {
        using var dlg = new InputDialog("Новая услуга",
            ("Наименование", "Свободное плавание"),
            ("Тип услуги", "РазовоеПосещение"),
            ("Длительность, мин", "60"),
            ("Номер бассейна (1 или 2)", "1"));
        if (dlg.ShowDialog(this) != DialogResult.OK) return;
        var values = ReadServiceValues(dlg);
        _data.Services.Add(new Service
        {
            Name = values.Name, Type = values.Type, DurationMinutes = values.Duration,
            PoolId = values.PoolId
        });
        _data.SaveAll(); RefreshAll();
    }

    private void EditService()
    {
        var service = Selected<Service>("services", row =>
            _data.Services.FirstOrDefault(x => x.Id.ToString() == Convert.ToString(row.Cells[9].Value)));
        if (service is null) return;
        int poolIndex = Math.Max(1, _data.Pools.FindIndex(p => p.Id == service.PoolId) + 1);
        using var dlg = new InputDialog("Изменение услуги",
            ("Наименование", service.Name), ("Тип услуги", service.Type.ToString()),
            ("Длительность, мин", service.DurationMinutes.ToString()),
            ("Номер бассейна (1 или 2)", poolIndex.ToString()));
        if (dlg.ShowDialog(this) != DialogResult.OK) return;
        var values = ReadServiceValues(dlg);
        service.Name = values.Name; service.Type = values.Type;
        service.DurationMinutes = values.Duration; service.PoolId = values.PoolId;
        _data.SaveAll(); RefreshAll();
    }

    private (string Name, ServiceType Type, int Duration, Guid PoolId) ReadServiceValues(InputDialog dlg)
    {
        string name = dlg.Value("Наименование");
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Введите наименование услуги.");
        if (!Enum.TryParse<ServiceType>(dlg.Value("Тип услуги"), true, out var type))
            throw new ArgumentException("Неверный тип услуги.");
        if (!int.TryParse(dlg.Value("Длительность, мин"), out int duration) || duration <= 0)
            throw new ArgumentException("Длительность должна быть положительной.");
        if (!int.TryParse(dlg.Value("Номер бассейна (1 или 2)"), out int poolNo) || poolNo < 1 || poolNo > _data.Pools.Count)
            throw new ArgumentException("Указан неверный номер бассейна.");
        return (name, type, duration, _data.Pools[poolNo - 1].Id);
    }

    private void AddTariff()
    {
        if (_data.Services.Count == 0) throw new InvalidOperationException("Сначала следует добавить услугу.");
        using var dlg = new InputDialog("Новый тариф",
            ("Номер услуги", "1"), ("Категория клиента", "Взрослый"),
            ("Цена, BYN", "0.00"), ("Количество занятий", "1"),
            ("Действует с (гггг-мм-дд)", DateTime.Today.ToString("yyyy-MM-dd")),
            ("Действует до (гггг-мм-дд)", DateTime.Today.AddYears(1).ToString("yyyy-MM-dd")));
        MessageBox.Show(string.Join(Environment.NewLine, _data.Services.Select((x, i) => $"{i + 1}. {x.Name}")),
            "Список услуг", MessageBoxButtons.OK, MessageBoxIcon.Information);
        if (dlg.ShowDialog(this) != DialogResult.OK) return;
        var values = ReadTariffValues(dlg);
        _data.Tariffs.Add(new Tariff
        {
            ServiceId = values.ServiceId, ClientCategory = values.Category,
            Price = values.Price, VisitsIncluded = values.Count,
            ValidFrom = values.From, ValidTo = values.To
        });
        _data.SaveAll(); RefreshAll();
    }

    private void EditTariff()
    {
        var tariff = Selected<Tariff>("services", row =>
            Guid.TryParse(Convert.ToString(row.Cells[10].Value), out var id)
                ? _data.Tariffs.FirstOrDefault(t => t.Id == id) : null);
        if (tariff is null) return;
        int serviceIndex = Math.Max(1, _data.Services.FindIndex(s => s.Id == tariff.ServiceId) + 1);
        using var dlg = new InputDialog("Изменение тарифа",
            ("Номер услуги", serviceIndex.ToString()), ("Категория клиента", tariff.ClientCategory.ToString()),
            ("Цена, BYN", tariff.Price.ToString("0.00", CultureInfo.InvariantCulture)),
            ("Количество занятий", tariff.VisitsIncluded.ToString()),
            ("Действует с (гггг-мм-дд)", tariff.ValidFrom.ToString("yyyy-MM-dd")),
            ("Действует до (гггг-мм-дд)", tariff.ValidTo.ToString("yyyy-MM-dd")));
        if (dlg.ShowDialog(this) != DialogResult.OK) return;
        var values = ReadTariffValues(dlg);
        if (_data.Sales.Any(s => s.Items.Any(i => i.TariffId == tariff.Id)) &&
            (tariff.ServiceId != values.ServiceId || tariff.Price != values.Price || tariff.ClientCategory != values.Category))
            throw new InvalidOperationException("У тарифа уже есть продажи. Нельзя менять услугу, цену или категорию, чтобы сохранить историю покупок. Можно изменить только даты и количество занятий.");
        tariff.ServiceId = values.ServiceId; tariff.ClientCategory = values.Category;
        tariff.Price = values.Price; tariff.VisitsIncluded = values.Count;
        tariff.ValidFrom = values.From; tariff.ValidTo = values.To;
        _data.SaveAll(); RefreshAll();
    }

    private (Guid ServiceId, ClientCategory Category, decimal Price, int Count, DateTime From, DateTime To) ReadTariffValues(InputDialog dlg)
    {
        if (!int.TryParse(dlg.Value("Номер услуги"), out int index) || index < 1 || index > _data.Services.Count)
            throw new ArgumentException("Неверный номер услуги.");
        if (!Enum.TryParse<ClientCategory>(dlg.Value("Категория клиента"), true, out var category))
            throw new ArgumentException("Неверная категория клиента.");
        string priceText = dlg.Value("Цена, BYN").Replace(',', '.');
        if (!decimal.TryParse(priceText, NumberStyles.Number, CultureInfo.InvariantCulture, out decimal price) || price < 0)
            throw new ArgumentException("Цена должна быть неотрицательным числом.");
        if (!int.TryParse(dlg.Value("Количество занятий"), out int count) || count <= 0)
            throw new ArgumentException("Количество занятий должно быть положительным.");
        if (!DateTime.TryParseExact(dlg.Value("Действует с (гггг-мм-дд)"), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var from) ||
            !DateTime.TryParseExact(dlg.Value("Действует до (гггг-мм-дд)"), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var to) || to < from)
            throw new ArgumentException("Введите даты в формате гггг-мм-дд. Дата окончания не может быть раньше даты начала.");
        return (_data.Services[index - 1].Id, category, price, count, from, to);
    }

    private void DeleteTariff()
    {
        var tariff = Selected<Tariff>("services", row =>
            Guid.TryParse(Convert.ToString(row.Cells[10].Value), out var id)
                ? _data.Tariffs.FirstOrDefault(t => t.Id == id) : null);
        if (tariff is null) return;
        if (_data.Sales.Any(s => s.Items.Any(i => i.TariffId == tariff.Id)))
            throw new InvalidOperationException("Этот тариф уже использовался в продажах. Удаление запрещено, чтобы сохранить историю покупок.");
        var service = _data.Services.FirstOrDefault(x => x.Id == tariff.ServiceId);
        if (MessageBox.Show($"Удалить тариф «{service?.Name} — {tariff.ClientCategory}»?", "Подтверждение",
            MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
        _data.Tariffs.Remove(tariff); _data.SaveAll(); RefreshAll();
    }

    private void DeleteService()
    {
        var service = Selected<Service>("services", row =>
            _data.Services.FirstOrDefault(x => x.Id.ToString() == Convert.ToString(row.Cells[9].Value)));
        if (service is null) return;
        if (_data.Sales.Any(s => s.Items.Any(i => i.ServiceId == service.Id)))
            throw new InvalidOperationException("Услуга уже использовалась в продажах и не может быть удалена.");
        if (MessageBox.Show($"Удалить услугу «{service.Name}» вместе со всеми её тарифами?", "Подтверждение",
            MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
        _data.Services.Remove(service);
        _data.Tariffs.RemoveAll(t => t.ServiceId == service.Id);
        _data.SaveAll(); RefreshAll();
    }

    private void AddSession()
    {
        using var dlg = new InputDialog("Новый сеанс",
            ("Дата (гггг-мм-дд)", DateTime.Today.ToString("yyyy-MM-dd")),
            ("Начало (чч:мм)", "09:00"), ("Окончание (чч:мм)", "10:00"),
            ("Номер бассейна (1 или 2)", "1"), ("Вид занятия", "Свободное плавание"),
            ("Вместимость", "20"), ("Тренер", ""));
        if (dlg.ShowDialog(this) != DialogResult.OK) return;
        if (string.IsNullOrWhiteSpace(dlg.Value("Вид занятия")))
            throw new ArgumentException("Введите вид занятия.");
        if (!DateTime.TryParse(dlg.Value("Дата (гггг-мм-дд)"), out var date) ||
            !TimeSpan.TryParse(dlg.Value("Начало (чч:мм)"), out var start) ||
            !TimeSpan.TryParse(dlg.Value("Окончание (чч:мм)"), out var end) || end <= start)
            throw new ArgumentException("Проверьте дату и время сеанса.");
        if (!int.TryParse(dlg.Value("Номер бассейна (1 или 2)"), out int poolNo) || poolNo < 1 || poolNo > _data.Pools.Count)
            throw new ArgumentException("Неверный номер бассейна.");
        if (!int.TryParse(dlg.Value("Вместимость"), out int capacity) || capacity <= 0)
            throw new ArgumentException("Вместимость должна быть положительной.");
        _data.Sessions.Add(new Session
        {
            Date = date, StartTime = start, EndTime = end, PoolId = _data.Pools[poolNo - 1].Id,
            Activity = dlg.Value("Вид занятия"), Capacity = capacity, CoachName = dlg.Value("Тренер")
        });
        _data.SaveAll(); RefreshAll();
    }

    private void EditSession()
    {
        var session = Selected<Session>("sessions", row =>
            Guid.TryParse(Convert.ToString(row.Cells[0].Value), out var id)
                ? _data.Sessions.FirstOrDefault(x => x.Id == id) : null);
        if (session is null) return;
        if (_data.Tickets.Any(t => t.SessionId == session.Id) || _data.Visits.Any(v => v.SessionId == session.Id))
            throw new InvalidOperationException("Нельзя изменять сеанс, на который уже оформлены билеты или посещения.");
        int poolIndex = Math.Max(1, _data.Pools.FindIndex(p => p.Id == session.PoolId) + 1);
        using var dlg = new InputDialog("Изменение сеанса",
            ("Дата (гггг-мм-дд)", session.Date.ToString("yyyy-MM-dd")),
            ("Начало (чч:мм)", session.StartTime.ToString(@"hh\:mm")),
            ("Окончание (чч:мм)", session.EndTime.ToString(@"hh\:mm")),
            ("Номер бассейна (1 или 2)", poolIndex.ToString()),
            ("Вид занятия", session.Activity), ("Вместимость", session.Capacity.ToString()),
            ("Тренер", session.CoachName));
        if (dlg.ShowDialog(this) != DialogResult.OK) return;
        if (string.IsNullOrWhiteSpace(dlg.Value("Вид занятия"))) throw new ArgumentException("Введите вид занятия.");
        if (!DateTime.TryParseExact(dlg.Value("Дата (гггг-мм-дд)"), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date) ||
            !TimeSpan.TryParse(dlg.Value("Начало (чч:мм)"), out var start) ||
            !TimeSpan.TryParse(dlg.Value("Окончание (чч:мм)"), out var end) || end <= start)
            throw new ArgumentException("Проверьте дату и время сеанса.");
        if (!int.TryParse(dlg.Value("Номер бассейна (1 или 2)"), out int poolNo) || poolNo < 1 || poolNo > _data.Pools.Count)
            throw new ArgumentException("Неверный номер бассейна.");
        if (!int.TryParse(dlg.Value("Вместимость"), out int capacity) || capacity <= 0)
            throw new ArgumentException("Вместимость должна быть положительной.");
        session.Date = date; session.StartTime = start; session.EndTime = end;
        session.PoolId = _data.Pools[poolNo - 1].Id; session.Activity = dlg.Value("Вид занятия");
        session.Capacity = capacity; session.CoachName = dlg.Value("Тренер");
        _data.SaveAll(); RefreshAll();
    }

    private void DeleteSession()
    {
        var session = Selected<Session>("sessions", row =>
            _data.Sessions.FirstOrDefault(s => s.Id.ToString() == Convert.ToString(row.Cells[0].Value)));
        if (session is null) return;
        if (_data.Tickets.Any(t => t.SessionId == session.Id) || _data.Visits.Any(v => v.SessionId == session.Id))
            throw new InvalidOperationException("Нельзя удалить сеанс, связанный с билетами или посещениями.");
        _data.Sessions.Remove(session); _data.SaveAll(); RefreshAll();
    }

    private void CreateSale()
    {
        if (_data.Clients.Count == 0 || _data.Tariffs.Count == 0)
            throw new InvalidOperationException("Для оформления продажи нужны клиент и тариф.");
        string clients = string.Join(Environment.NewLine, _data.Clients.Select((c, i) => $"{i + 1}. {c.FullName}"));
        string tariffs = string.Join(Environment.NewLine, _data.Tariffs.Select((t, i) =>
        {
            var s = _data.Services.FirstOrDefault(x => x.Id == t.ServiceId);
            return $"{i + 1}. {s?.Name}, {t.ClientCategory}, {t.Price:0.00} BYN";
        }));
        using var dlg = new InputDialog("Оформление продажи",
            ("Номер клиента", "1"), ("Номер тарифа", "1"), ("Количество", "1"),
            ("Номер сеанса (для разового билета)", "1"),
            ("Способ оплаты", "Наличные"));
        var help = MessageBox.Show($"Клиенты:\n{clients}\n\nТарифы:\n{tariffs}",
            "Справочная информация", MessageBoxButtons.OKCancel, MessageBoxIcon.Information);
        if (help != DialogResult.OK || dlg.ShowDialog(this) != DialogResult.OK) return;
        if (!int.TryParse(dlg.Value("Номер клиента"), out int ci) || ci < 1 || ci > _data.Clients.Count)
            throw new ArgumentException("Неверный номер клиента.");
        if (!int.TryParse(dlg.Value("Номер тарифа"), out int ti) || ti < 1 || ti > _data.Tariffs.Count)
            throw new ArgumentException("Неверный номер тарифа.");
        if (!int.TryParse(dlg.Value("Количество"), out int qty) || qty <= 0)
            throw new ArgumentException("Количество должно быть положительным.");
        if (!Enum.TryParse<PaymentMethod>(dlg.Value("Способ оплаты"), true, out var method))
            throw new ArgumentException("Неверный способ оплаты.");
        Guid? sessionId = null;
        var service = _data.Services.FirstOrDefault(s => s.Id == _data.Tariffs[ti - 1].ServiceId);
        if (service?.Type == ServiceType.РазовоеПосещение)
        {
            if (!int.TryParse(dlg.Value("Номер сеанса (для разового билета)"), out int si) || si < 1 || si > _data.Sessions.Count)
                throw new ArgumentException("Неверный номер сеанса.");
            sessionId = _data.Sessions[si - 1].Id;
        }
        var sale = _business.CreateSale(_data.Clients[ci - 1].Id, _data.Tariffs[ti - 1].Id, qty, sessionId, method);
        string passInfo = _data.Tickets.Where(t => t.SaleId == sale.Id).Select(t => t.Number)
            .Concat(_data.Subscriptions.Where(s => s.SaleId == sale.Id).Select(s => s.Number))
            .Aggregate("", (a, b) => a + Environment.NewLine + b);
        MessageBox.Show($"Продажа оформлена.\nСумма: {sale.Total:0.00} BYN\nНомер билета/абонемента:{passInfo}",
            "Готово", MessageBoxButtons.OK, MessageBoxIcon.Information);
        RefreshAll();
    }

    private void RegisterVisit()
    {
        if (_data.Sessions.Count == 0) throw new InvalidOperationException("Сначала следует создать сеанс.");
        string sessionList = string.Join(Environment.NewLine, _data.Sessions.Select((s, i) =>
            $"{i + 1}. {s.Date:d} {s.StartTime:hh\\:mm}, {_data.Pools.FirstOrDefault(p => p.Id == s.PoolId)?.Name}"));
        using var dlg = new InputDialog("Регистрация посещения",
            ("Номер билета или абонемента", ""), ("Номер сеанса", "1"),
            ("Тип основания", "Билет"), ("Сотрудник", "Администратор"));
        MessageBox.Show(sessionList, "Сеансы", MessageBoxButtons.OK, MessageBoxIcon.Information);
        if (dlg.ShowDialog(this) != DialogResult.OK) return;
        if (!int.TryParse(dlg.Value("Номер сеанса"), out int index) || index < 1 || index > _data.Sessions.Count)
            throw new ArgumentException("Неверный номер сеанса.");
        var sessionId = _data.Sessions[index - 1].Id;
        string type = dlg.Value("Тип основания");
        Visit visit = type.Equals("Абонемент", StringComparison.OrdinalIgnoreCase)
            ? _business.RegisterSubscriptionVisit(dlg.Value("Номер билета или абонемента"), sessionId, dlg.Value("Сотрудник"))
            : _business.RegisterTicketVisit(dlg.Value("Номер билета или абонемента"), sessionId, dlg.Value("Сотрудник"));
        MessageBox.Show($"Посещение зарегистрировано: {visit.VisitTime:g}", "Готово",
            MessageBoxButtons.OK, MessageBoxIcon.Information);
        RefreshAll();
    }

    private void CancelPass()
    {
        var row = _grids["passes"].CurrentRow;
        if (row is null) return;
        string type = Convert.ToString(row.Cells[0].Value) ?? "";
        string number = Convert.ToString(row.Cells[1].Value) ?? "";
        bool isTicket = type == "Билет";
        var ticket = isTicket ? _data.Tickets.FirstOrDefault(x => x.Number == number) : null;
        var subscription = !isTicket ? _data.Subscriptions.FirstOrDefault(x => x.Number == number) : null;
        if (ticket is null && subscription is null) return;
        if (ticket?.IsCancelled == true || subscription?.IsCancelled == true)
            throw new InvalidOperationException("Этот билет или абонемент уже аннулирован.");
        if (ticket?.IsUsed == true || (subscription?.UsedVisits ?? 0) > 0)
            throw new InvalidOperationException("Нельзя аннулировать использованный билет или абонемент с посещениями.");
        Guid saleId = ticket?.SaleId ?? subscription!.SaleId;
        if (MessageBox.Show($"Аннулировать {type.ToLowerInvariant()} № {number}?", "Подтверждение",
            MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
        if (ticket is not null) ticket.IsCancelled = true;
        if (subscription is not null) subscription.IsCancelled = true;
        bool allCancelled = _data.Tickets.Where(x => x.SaleId == saleId).All(x => x.IsCancelled) &&
                            _data.Subscriptions.Where(x => x.SaleId == saleId).All(x => x.IsCancelled);
        if (allCancelled)
        {
            var sale = _data.Sales.FirstOrDefault(x => x.Id == saleId);
            if (sale is not null) sale.Status = SaleStatus.Отменена;
            foreach (var payment in _data.Payments.Where(x => x.SaleId == saleId))
                payment.Status = PaymentStatus.Возврат;
        }
        _data.SaveAll(); RefreshAll();
    }

    private void CreateReport()
    {
        var from = DateTime.Today.AddMonths(-1);
        var to = DateTime.Today;
        var revenue = _business.GetRevenue(from, to);
        Fill("reports", new[]
        {
            new object?[] { "Период", $"{from:d} – {to:d}" },
            new object?[] { "Количество клиентов", _data.Clients.Count },
            new object?[] { "Количество продаж за период", _data.Sales.Count(s => s.Date.Date >= from && s.Date.Date <= to) },
            new object?[] { "Количество платежей за период", _data.Payments.Count(p => p.Date.Date >= from && p.Date.Date <= to && p.Status == PaymentStatus.Оплачен) },
            new object?[] { "Выручка за период, BYN", revenue.ToString("0.00") },
            new object?[] { "Посещений за период", _data.Visits.Count(v => v.VisitTime.Date >= from && v.VisitTime.Date <= to) },
            new object?[] { "Выдано билетов", _data.Tickets.Count },
            new object?[] { "Оформлено абонементов", _data.Subscriptions.Count }
        });
    }

    private void ExportReport()
    {
        CreateReport();
        using var dialog = new SaveFileDialog { Filter = "Текстовый файл (*.txt)|*.txt", FileName = "Отчёт_бассейн.txt" };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        var lines = _grids["reports"].Rows.Cast<DataGridViewRow>()
            .Select(r => $"{r.Cells[0].Value}: {r.Cells[1].Value}");
        File.WriteAllLines(dialog.FileName, new[]
        {
            "ОТЧЁТ ПО УЧЁТУ УСЛУГ БАССЕЙНА ПОЛЕСГУ",
            $"Дата формирования: {DateTime.Now:g}",
            ""
        }.Concat(lines));
        MessageBox.Show("Отчёт сохранён.", "Экспорт", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    private T? Selected<T>(string key, Func<DataGridViewRow, T?> resolver) where T : class
    {
        var row = _grids[key].CurrentRow;
        return row is null ? null : resolver(row);
    }

    private void Safe(Action action)
    {
        try { action(); }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "Не удалось выполнить операцию",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }
}
