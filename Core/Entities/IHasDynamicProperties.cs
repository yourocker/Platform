namespace Core.Entities
{
    // Любая сущность, которая хочет иметь динамические поля, должна реализовать этот интерфейс
    public interface IHasDynamicProperties
    {
        string? Properties { get; set; }
    }
}