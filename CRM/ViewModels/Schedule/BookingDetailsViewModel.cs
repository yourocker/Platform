using Core.Entities.CRM;

namespace CRM.ViewModels.Schedule;

public class BookingDetailsViewModel
{
    public CrmResourceBooking Booking { get; set; } = null!;
    public IReadOnlyList<BookingDetailsFieldViewModel> CustomFields { get; set; } = Array.Empty<BookingDetailsFieldViewModel>();
    public IReadOnlyList<CrmEvent> TimelineEvents { get; set; } = Array.Empty<CrmEvent>();
}

public class BookingDetailsFieldViewModel
{
    public string Label { get; set; } = string.Empty;
    public IReadOnlyList<BookingDetailsFieldValueViewModel> Values { get; set; } = Array.Empty<BookingDetailsFieldValueViewModel>();
}

public class BookingDetailsFieldValueViewModel
{
    public string Text { get; set; } = string.Empty;
    public string? Url { get; set; }
}
