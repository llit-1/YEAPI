using Microsoft.AspNetCore.SignalR;
using YEAPI.Hubs;

namespace YEAPI.Services
{
    public class CashSignalRService
    {
        private readonly IHubContext<CashHub> _hubContext;

        public CashSignalRService(IHubContext<CashHub> hubContext)
        {
            _hubContext = hubContext;
        }

        public async Task NewOrder(Guid location, string orderId)
        {
            await _hubContext.Clients
                .Group($"location:{location}")
                .SendAsync("NewOrder", orderId);
        }

        public async Task OrderStatusChanged(Guid location, string orderId)
        {
            await _hubContext.Clients
                .Group($"location:{location}")
                .SendAsync("OrderStatusChanged", orderId);
        }

        public async Task OrderChanged(Guid location, string orderId)
        {
            await _hubContext.Clients
                .Group($"location:{location}")
                .SendAsync("OrderChanged", orderId);
        }

        public async Task OrderCanseled(Guid location, string orderId)
        {
            await _hubContext.Clients
                .Group($"location:{location}")
                .SendAsync("OrderCanseled", orderId);
        }

        public async Task CashUpdate(int cashId,int versionId,int taskId)
        {
            await _hubContext.Clients
                .Group($"cash:{cashId}")
                .SendAsync("CashUpdate", versionId, taskId);
        }

        public async Task CashDelete(int cashId,int taskId)
        {
            await _hubContext.Clients
                .Group($"cash:{cashId}")
                .SendAsync("CashDelete", taskId);
        }

        public async Task StopsUpdate(Guid location)
        {
            await _hubContext.Clients
                .Group($"location:{location}")
                .SendAsync("StopsUpdate");
        }

        public async Task StopsUpdateWithoutNotifications(Guid location)
        {
            await _hubContext.Clients
                .Group($"location:{location}")
                .SendAsync("StopsUpdateWithoutNotifications");
        }

        public async Task SendToCash(
            int cashId,
            object? data = null)
        {
            await _hubContext.Clients
                .Group($"cash:{cashId}")
                .SendAsync("SendMessage", data);
        }
    }
}