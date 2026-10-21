namespace MemoryVerse.Models;

public class User
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string Email { get; set; } = "";
    public string PasswordHash { get; set; } = "";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public record RegisterInput(string? Name, string? Email, string? Password);
public record LoginInput(string? Email, string? Password);
