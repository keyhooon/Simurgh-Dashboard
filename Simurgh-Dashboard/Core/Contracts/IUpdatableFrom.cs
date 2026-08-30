namespace SimurghDashboard.Core.Contracts;

public interface IUpdatableFrom<in TSource>
{
    bool UpdateFrom(TSource source);
}