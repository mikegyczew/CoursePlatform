namespace Backend.DTOs;

public class AuthResponseDto
{
    public required string Token { get; set; }

    public int UserId { get; set; }

    public required string Email { get; set; }

    public required string Name { get; set; }
}
