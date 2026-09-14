namespace Simurgh.Dashboard.Core.Contracts;

public interface IRepository<T>
    where T : IIdentifiable
{
    T? GetById(string id);
    IReadOnlyCollection<T> GetAll();
    void Upsert(T item);
    bool Remove(string id);
}