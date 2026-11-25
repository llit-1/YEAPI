using System.Collections.Concurrent;

namespace YEAPI.Services
{
    public class CashConnectionManager
    {
        private readonly ConcurrentDictionary<int, string> _cashConnections = new();
        private readonly ConcurrentDictionary<string, int> _connectionCashes = new();

        public void Add(int cashId, string connectionId)
        {
            _cashConnections[cashId] = connectionId;
            _connectionCashes[connectionId] = cashId;
        }

        public bool TryGetConnection(int cashId, out string? connectionId)
        {
            return _cashConnections.TryGetValue(cashId, out connectionId);
        }

        public bool TryGetCashId(string connectionId, out int cashId)
        {
            return _connectionCashes.TryGetValue(connectionId, out cashId);
        }

        public void Remove(string connectionId)
        {
            if (!_connectionCashes.TryRemove(
                    connectionId,
                    out var cashId))
                return;

            _cashConnections.TryRemove(
                new KeyValuePair<int, string>(
                    cashId,
                    connectionId));
        }

        public bool IsConnected(int cashId)
        {
            return _cashConnections.ContainsKey(cashId);
        }
    }
}