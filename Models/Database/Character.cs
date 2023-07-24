﻿using Discord.Interactions;

namespace CharacterEngineDiscord.Models.Database
{
    public class Character
    {
        public required string Id { get; set; }
        public required string? Tgt { get; set; }
        public required string Name { get; set; }
        public string? Title { get; set; }
        public string? Description { get; set; }
        public string? Greeting { get; set; }
        public string? AuthorName { get; set; }
        public required bool ImageGenEnabled { get; set; }
        public required string Link { get; set; }
        public string? AvatarUrl { get; set; }
        public required ulong Interactions { get; set; }

        // Tavern stuff
        public required ulong? Stars { get; set; }
        public required string? Personality { get; set; }
        public required string? Scenario { get; set; }
        public required string? ExampleDialogs { get; set; }

        public virtual List<CharacterWebhook> CharacterWebhooks { get; } = new();
    }
}