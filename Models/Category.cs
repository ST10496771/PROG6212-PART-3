using System.ComponentModel.DataAnnotations;

namespace RaceDayApi.Models;

public class Category
{
    public int CategoryID { get; set; }

    [Required, MaxLength(100)]
    public string CategoryName { get; set; } = string.Empty;

    [MaxLength(255)]
    public string? CategoryDescription { get; set; }

    public int EventID { get; set; }
    public Event? Event { get; set; }

    public List<EventEntrance> EventEntrances { get; set; } = new();
}
