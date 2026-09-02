using SimurghDashboard.Core.Contracts;

namespace SimurghDashboard.Core.Repositories
{
    public sealed class InMemoryRepository<T> : IRepository<T>
        where T : class, IIdentifiable
    {
        private readonly Dictionary<string, T> _items = new(StringComparer.OrdinalIgnoreCase);

        public T? GetById(string id)
            => _items.GetValueOrDefault(id);

        public IReadOnlyCollection<T> GetAll()
            => _items.Values.ToList().AsReadOnly();

        public void Upsert(T item)
            => _items[item.Id] = item;

        public bool Remove(string id)
            => _items.Remove(id);
    }

}
