using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CharacterEngineDiscord.Models.Common
{
    internal class AvailableCharacterResponse
    {
        public required string? MessageUuId { get; set; }
        public required string? Text { get; set; }
        public required string? ImageUrl { get; set; }
    }
}
