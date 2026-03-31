using CRM.ViewModels.CompanySettings;

namespace CRM.Infrastructure.Trash
{
    public interface ITrashService
    {
        Task<TrashPageViewModel> GetPageModelAsync(TrashFilterInput filter, CancellationToken cancellationToken = default);
        Task<int> RestoreAsync(IEnumerable<string>? selectionKeys, CancellationToken cancellationToken = default);
        Task<int> PermanentlyDeleteAsync(IEnumerable<string>? selectionKeys, CancellationToken cancellationToken = default);
    }
}
