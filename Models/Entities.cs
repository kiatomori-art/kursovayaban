namespace WinFormsApp1.Models;

// Категория клиента и основные перечисления предметной области.
public enum ClientCategory { Взрослый, Ребёнок, Студент, Группа }
public enum ServiceType { РазовоеПосещение, Абонемент, ОбучениеПлаванию, ГрупповоеЗанятие }
public enum PaymentMethod { Наличные, БанковскаяКарта, БезналичныйРасчёт }
public enum PaymentStatus { Оплачен, Возврат, ОжидаетОплаты }
public enum SaleStatus { Оформлена, Отменена }

public sealed class Client
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string LastName { get; set; } = "";
    public string FirstName { get; set; } = "";
    public string MiddleName { get; set; } = "";
    public string Phone { get; set; } = "";
    public ClientCategory Category { get; set; } = ClientCategory.Взрослый;
    public bool IsActive { get; set; } = true;
    public string FullName => $"{LastName} {FirstName} {MiddleName}".Trim();
}

public sealed class Employee
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string FullName { get; set; } = "";
    public string Role { get; set; } = "Администратор";
}

public sealed class Pool
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "";
    public int Length { get; set; }
    public bool IsActive { get; set; } = true;
}

public sealed class Service
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "";
    public ServiceType Type { get; set; }
    public int DurationMinutes { get; set; } = 60;
    public Guid PoolId { get; set; }
}

public sealed class Tariff
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ServiceId { get; set; }
    public ClientCategory ClientCategory { get; set; }
    public decimal Price { get; set; }
    public DateTime ValidFrom { get; set; } = DateTime.Today;
    public DateTime ValidTo { get; set; } = DateTime.Today.AddYears(1);
    public int VisitsIncluded { get; set; } = 1;
}

public sealed class Session
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public DateTime Date { get; set; } = DateTime.Today;
    public TimeSpan StartTime { get; set; } = new(9, 0, 0);
    public TimeSpan EndTime { get; set; } = new(10, 0, 0);
    public Guid PoolId { get; set; }
    public string Activity { get; set; } = "Свободное плавание";
    public int Capacity { get; set; } = 20;
    public string CoachName { get; set; } = "";
}

public sealed class SaleItem
{
    public Guid ServiceId { get; set; }
    public Guid TariffId { get; set; }
    public int Quantity { get; set; }
    public decimal Price { get; set; }
    public decimal LineTotal => Quantity * Price;
}

public sealed class Sale
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public DateTime Date { get; set; } = DateTime.Now;
    public Guid ClientId { get; set; }
    public List<SaleItem> Items { get; set; } = [];
    public decimal Total { get; set; }
    public SaleStatus Status { get; set; } = SaleStatus.Оформлена;
}

public sealed class Ticket
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Number { get; set; } = "";
    public Guid ClientId { get; set; }
    public Guid SessionId { get; set; }
    public Guid SaleId { get; set; }
    public bool IsUsed { get; set; }
    public bool IsCancelled { get; set; }
}

public sealed class Subscription
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Number { get; set; } = "";
    public Guid ClientId { get; set; }
    public Guid ServiceId { get; set; }
    public Guid SaleId { get; set; }
    public DateTime ValidFrom { get; set; } = DateTime.Today;
    public DateTime ValidTo { get; set; } = DateTime.Today.AddMonths(1);
    public int InitialVisits { get; set; }
    public int UsedVisits { get; set; }
    public bool IsCancelled { get; set; }
    public int RemainingVisits => InitialVisits - UsedVisits;
}

public sealed class Payment
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid SaleId { get; set; }
    public DateTime Date { get; set; } = DateTime.Now;
    public decimal Amount { get; set; }
    public PaymentMethod Method { get; set; }
    public PaymentStatus Status { get; set; } = PaymentStatus.Оплачен;
}

public sealed class Visit
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ClientId { get; set; }
    public Guid SessionId { get; set; }
    public Guid? TicketId { get; set; }
    public Guid? SubscriptionId { get; set; }
    public DateTime VisitTime { get; set; } = DateTime.Now;
    public string EmployeeName { get; set; } = "";
}

public sealed class TrainingGroup
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "";
    public string Category { get; set; } = "";
    public string CoachName { get; set; } = "";
}
