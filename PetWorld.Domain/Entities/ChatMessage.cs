namespace PetWorld.Domain.Entities;

public class ChatMessage
{
    public int Id { get; set; }
    public string Question { get; set; } = string.Empty;
    public string Answer { get; set; } = string.Empty;
    public int Iterations { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
