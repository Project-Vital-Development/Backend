using System.ComponentModel.DataAnnotations;

namespace ProjectVital.Application.Inputs;

public record LoginInput(
    [Required][EmailAddress][StringLength(255)] string Email,
    [Required][StringLength(255, MinimumLength = 8)] string Password
);
