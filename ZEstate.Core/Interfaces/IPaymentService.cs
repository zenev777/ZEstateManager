using ZEstate.Core.DTOs.Payments;

namespace ZEstate.Core.Interfaces
{
    public interface IPaymentService
    {
        Task<RegisterPaymentResultDto> RegisterPaymentAsync(string userId, RegisterPaymentDto dto);
        Task<List<PaymentSummaryDto>> GetPaymentsAsync(string userId, int? apartmentId, DateTime? from, DateTime? to);
    }
}
