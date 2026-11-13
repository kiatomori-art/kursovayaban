using WinFormsApp1.Models;

namespace WinFormsApp1.Services;

// Содержит бизнес-правила предметной области: продажи, билеты и абонементы.
public sealed class BusinessService(DataManager data)
{
    public decimal CalculatePrice(Tariff tariff, int quantity)
    {
        if (quantity <= 0) throw new ArgumentException("Количество должно быть больше нуля.");
        if (tariff.Price < 0) throw new ArgumentException("Цена не может быть отрицательной.");
        if (DateTime.Today < tariff.ValidFrom.Date || DateTime.Today > tariff.ValidTo.Date)
            throw new InvalidOperationException("Срок действия тарифа истёк или ещё не наступил.");
        return tariff.Price * quantity;
    }

    public Sale CreateSale(Guid clientId, Guid tariffId, int quantity,
        Guid? sessionId, PaymentMethod method)
    {
        var client = data.Clients.FirstOrDefault(x => x.Id == clientId)
            ?? throw new InvalidOperationException("Клиент не найден.");
        var tariff = data.Tariffs.FirstOrDefault(x => x.Id == tariffId)
            ?? throw new InvalidOperationException("Тариф не найден.");
        var service = data.Services.FirstOrDefault(x => x.Id == tariff.ServiceId)
            ?? throw new InvalidOperationException("Услуга для выбранного тарифа не найдена.");

        if (!client.IsActive) throw new InvalidOperationException("Клиент неактивен.");
        if (quantity <= 0) throw new ArgumentException("Количество должно быть положительным.");

        // Все проверки выполняются до изменения коллекций, чтобы не оставить частично оформленную продажу.
        decimal total = CalculatePrice(tariff, quantity);
        Session? selectedSession = null;

        if (service.Type == ServiceType.РазовоеПосещение)
        {
            if (sessionId is null)
                throw new InvalidOperationException("Для разового билета следует выбрать сеанс.");

            selectedSession = data.Sessions.FirstOrDefault(x => x.Id == sessionId.Value)
                ?? throw new InvalidOperationException("Сеанс не найден.");

            int occupied = data.Tickets.Count(t => t.SessionId == selectedSession.Id && !t.IsUsed && !t.IsCancelled);
            if (occupied + quantity > selectedSession.Capacity)
                throw new InvalidOperationException("На сеансе недостаточно свободных мест.");
        }

        var sale = new Sale
        {
            ClientId = clientId,
            Items =
            [
                new SaleItem
                {
                    ServiceId = service.Id,
                    TariffId = tariff.Id,
                    Quantity = quantity,
                    Price = tariff.Price
                }
            ],
            Total = total
        };

        data.Sales.Add(sale);
        data.Payments.Add(new Payment
        {
            SaleId = sale.Id,
            Amount = sale.Total,
            Method = method,
            Status = PaymentStatus.Оплачен
        });

        if (service.Type == ServiceType.РазовоеПосещение)
        {
            for (int i = 0; i < quantity; i++)
            {
                data.Tickets.Add(new Ticket
                {
                    Number = $"T-{DateTime.Now:yyMMddHHmmss}-{Guid.NewGuid().ToString("N")[..4]}",
                    ClientId = client.Id,
                    SessionId = selectedSession!.Id,
                    SaleId = sale.Id
                });
            }
        }
        else
        {
            // Количество занятий берётся из тарифа; quantity означает число приобретённых абонементов.
            for (int i = 0; i < quantity; i++)
            {
                data.Subscriptions.Add(new Subscription
                {
                    Number = $"A-{DateTime.Now:yyMMddHHmmss}-{Guid.NewGuid().ToString("N")[..4]}",
                    ClientId = client.Id,
                    ServiceId = service.Id,
                    SaleId = sale.Id,
                    ValidFrom = DateTime.Today,
                    ValidTo = DateTime.Today.AddMonths(1),
                    InitialVisits = tariff.VisitsIncluded
                });
            }
        }

        data.SaveAll();
        return sale;
    }

    public Visit RegisterTicketVisit(string number, Guid sessionId, string employeeName)
    {
        var ticket = data.Tickets.FirstOrDefault(x =>
            x.Number.Equals(number.Trim(), StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException("Билет с таким номером не найден.");
        if (ticket.IsCancelled) throw new InvalidOperationException("Этот билет аннулирован.");
        if (ticket.IsUsed) throw new InvalidOperationException("Этот билет уже использован.");
        if (ticket.SessionId != sessionId) throw new InvalidOperationException("Билет оформлен на другой сеанс.");

        ticket.IsUsed = true;
        var visit = new Visit
        {
            ClientId = ticket.ClientId,
            SessionId = sessionId,
            TicketId = ticket.Id,
            EmployeeName = employeeName
        };
        data.Visits.Add(visit);
        data.SaveAll();
        return visit;
    }

    public Visit RegisterSubscriptionVisit(string number, Guid sessionId, string employeeName)
    {
        var subscription = data.Subscriptions.FirstOrDefault(x =>
            x.Number.Equals(number.Trim(), StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException("Абонемент с таким номером не найден.");

        if (subscription.IsCancelled) throw new InvalidOperationException("Этот абонемент аннулирован.");
        if (DateTime.Today < subscription.ValidFrom.Date || DateTime.Today > subscription.ValidTo.Date)
            throw new InvalidOperationException("Срок действия абонемента истёк или ещё не наступил.");
        if (subscription.RemainingVisits <= 0)
            throw new InvalidOperationException("На абонементе закончились посещения.");

        subscription.UsedVisits++;
        var visit = new Visit
        {
            ClientId = subscription.ClientId,
            SessionId = sessionId,
            SubscriptionId = subscription.Id,
            EmployeeName = employeeName
        };
        data.Visits.Add(visit);
        data.SaveAll();
        return visit;
    }

    public decimal GetRevenue(DateTime from, DateTime to) =>
        data.Payments.Where(x => x.Status == PaymentStatus.Оплачен &&
                                 x.Date.Date >= from.Date && x.Date.Date <= to.Date)
            .Sum(x => x.Amount);
}
