// ManagerTransferStatusDto.cs
namespace ZEstate.Core.DTOs.Users;

public class ManagerTransferStatusDto
{
    public bool Pending { get; set; }
    public string? ToUserId { get; set; }
    public string? ToUserName { get; set; }
    public DateTime? InitiatedAt { get; set; }
    public DateTime? EffectiveAt { get; set; }
}
