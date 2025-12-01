using System.Collections.Concurrent;

namespace cafApi.Services
{
    public interface IActiveClientsTracker
    {
        void Heartbeat(string clientId);
        void Disconnect(string clientId);
        int GetActiveCount();
    }

    public class ActiveClientsTracker : IActiveClientsTracker
    {
        private readonly ConcurrentDictionary<string, DateTime> _lastSeen = new();
        private readonly TimeSpan _ttl = TimeSpan.FromSeconds(120);

        public void Heartbeat(string clientId)
        {
            if (string.IsNullOrWhiteSpace(clientId)) return;
            _lastSeen[clientId] = DateTime.UtcNow;
            Cleanup();
        }

        public void Disconnect(string clientId)
        {
            if (string.IsNullOrWhiteSpace(clientId)) return;
            _lastSeen.TryRemove(clientId, out _);
        }

        public int GetActiveCount()
        {
            Cleanup();
            var cutoff = DateTime.UtcNow - _ttl;
            return _lastSeen.Values.Count(dt => dt >= cutoff);
        }

        private void Cleanup()
        {
            var cutoff = DateTime.UtcNow - _ttl;
            foreach (var kv in _lastSeen)
            {
                if (kv.Value < cutoff)
                {
                    _lastSeen.TryRemove(kv.Key, out _);
                }
            }
        }
    }
}
