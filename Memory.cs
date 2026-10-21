namespace MemoryVerse.Models;

public class Memory
{
    public int Id { get; set; }
    public int UserId { get; set; }          // owner of this memory
    public string Title { get; set; } = "";
    public string Story { get; set; } = "";
    public DateTime MemoryDate { get; set; }
    public string Mood { get; set; } = "Happy";
    public string? Category { get; set; }
    public string? ImageUrl { get; set; }
    public bool IsFavorite { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public record MemoryInput(
    string? Title,
    string? Story,
    DateTime? MemoryDate,
    string? Mood,
    string? Category,
    string? ImageUrl);
