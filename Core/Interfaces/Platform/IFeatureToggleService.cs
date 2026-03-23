namespace Core.Interfaces.Platform
{
    public interface IFeatureToggleService
    {
        Task<bool> IsEnabledAsync(string featureCode);

        Task<IReadOnlyDictionary<string, bool>> GetAllAsync();

        Task SetAsync(string featureCode, bool isEnabled);
    }
}
