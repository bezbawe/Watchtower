using System.ComponentModel.DataAnnotations;

namespace Watchtower.Entities;

public class BaseEntity
{
    [Key]
    public Guid Id { get; set; }
}
