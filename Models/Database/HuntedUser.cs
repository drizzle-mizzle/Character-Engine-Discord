using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CharacterEngineDiscord.Models.Database
{
    public class HuntedUser
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }
        public required ulong UserId { get; set; }
        public required float Chance { get; set; }
        public required ulong CharacterWebhookId { get; set; }
        public virtual CharacterWebhook CharacterWebhook { get; set; } = null!;
    }
}
