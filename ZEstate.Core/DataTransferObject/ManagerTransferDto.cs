// ManagerTransferDto.cs
using System.ComponentModel.DataAnnotations;

namespace ZEstate.Core.DTOs.Users;

public class InitiateManagerTransferDto
{
    [Required]
    public string ToUserId { get; set; } = string.Empty;

    // Re-entering the current manager's own password guards against an accidental transfer.
    [Required]
    public string Password { get; set; } = string.Empty;
}
