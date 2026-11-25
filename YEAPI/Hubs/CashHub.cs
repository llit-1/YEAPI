using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using YEAPI.Models;
using YEAPI.Services;

namespace YEAPI.Hubs
{
    public class CashHub : Hub
    {
        private readonly YEContext _db;
        private readonly CashConnectionManager _connectionManager;

        public CashHub(
            YEContext db,
            CashConnectionManager connectionManager)
        {
            _db = db;
            _connectionManager = connectionManager;
        }

        public async Task Register(int cashId)
        {
            var cash = await _db.YECashClients
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == cashId);

            if (cash == null)
                throw new HubException("Касса не найдена.");

            _connectionManager.Add(
                cash.Id,
                Context.ConnectionId);

            // Группа конкретной кассы
            await Groups.AddToGroupAsync(
                Context.ConnectionId,
                GetCashGroup(cash.Id));

            // Группа всех касс этой ТТ
            await Groups.AddToGroupAsync(
                Context.ConnectionId,
                GetLocationGroup(cash.LocationGUID));
        }

        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            _connectionManager.Remove(Context.ConnectionId);
            await base.OnDisconnectedAsync(exception);
        }

        private static string GetCashGroup(int cashId)
        {
            return $"cash:{cashId}";
        }

        private static string GetLocationGroup(Guid location)
        {
            return $"location:{location}";
        }
    }
}