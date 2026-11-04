using Domain.Filters;

namespace Application.Interfaces;

public interface IBookCache
{
    string BookKey(int id);
    string ListKey(BookFilter filter);
    bool TryGet<T>(string key, out T? value);
    void Set<T>(string key, T value);
    void Invalidate();
}
