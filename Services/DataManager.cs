using System.Text.Json;
using System.Text.Json.Serialization;
using WinFormsApp1.Models;

namespace WinFormsApp1.Services;

// DataManager отвечает только за чтение и запись локальных JSON-файлов.
// СУБД и SQL в проекте не используются.
public sealed class DataManager
{
    private readonly string _dataDirectory =
        Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data");

    private readonly JsonSerializerOptions _options = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public List<Client> Clients { get; private set; } = [];
    public List<Employee> Employees { get; private set; } = [];
    public List<Pool> Pools { get; private set; } = [];
    public List<Service> Services { get; private set; } = [];
    public List<Tariff> Tariffs { get; private set; } = [];
    public List<Session> Sessions { get; private set; } = [];
    public List<Sale> Sales { get; private set; } = [];
    public List<Ticket> Tickets { get; private set; } = [];
    public List<Subscription> Subscriptions { get; private set; } = [];
    public List<Payment> Payments { get; private set; } = [];
    public List<Visit> Visits { get; private set; } = [];
    public List<TrainingGroup> Groups { get; private set; } = [];

    public void LoadAll()
    {
        Directory.CreateDirectory(_dataDirectory);
        Clients = Load<List<Client>>("clients.json");
        Employees = Load<List<Employee>>("employees.json");
        Pools = Load<List<Pool>>("pools.json");
        Services = Load<List<Service>>("services.json");
        Tariffs = Load<List<Tariff>>("tariffs.json");
        Sessions = Load<List<Session>>("sessions.json");
        Sales = Load<List<Sale>>("sales.json");
        Tickets = Load<List<Ticket>>("tickets.json");
        Subscriptions = Load<List<Subscription>>("subscriptions.json");
        Payments = Load<List<Payment>>("payments.json");
        Visits = Load<List<Visit>>("visits.json");
        Groups = Load<List<TrainingGroup>>("groups.json");

        if (Pools.Count == 0)
        {
            Pools.Add(new Pool { Name = "Большой бассейн", Length = 50 });
            Pools.Add(new Pool { Name = "Малый бассейн", Length = 25 });
        }

        SaveAll();
    }

    private T Load<T>(string fileName) where T : new()
    {
        string path = Path.Combine(_dataDirectory, fileName);
        if (!File.Exists(path)) return new T();

        try
        {
            string json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<T>(json, _options) ?? new T();
        }
        catch (Exception ex)
        {
            throw new InvalidDataException(
                $"Не удалось прочитать файл {fileName}. Исходный файл не был изменён.", ex);
        }
    }

    private void Save<T>(string fileName, T value)
    {
        Directory.CreateDirectory(_dataDirectory);
        string path = Path.Combine(_dataDirectory, fileName);
        string tempPath = path + ".tmp";
        string json = JsonSerializer.Serialize(value, _options);
        File.WriteAllText(tempPath, json);
        File.Move(tempPath, path, true);
    }

    public void SaveAll()
    {
        Save("clients.json", Clients);
        Save("employees.json", Employees);
        Save("pools.json", Pools);
        Save("services.json", Services);
        Save("tariffs.json", Tariffs);
        Save("sessions.json", Sessions);
        Save("sales.json", Sales);
        Save("tickets.json", Tickets);
        Save("subscriptions.json", Subscriptions);
        Save("payments.json", Payments);
        Save("visits.json", Visits);
        Save("groups.json", Groups);
    }

    public string DataDirectory => _dataDirectory;
}
