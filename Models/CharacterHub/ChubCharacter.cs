using Discord;
using Microsoft.EntityFrameworkCore.Metadata.Internal;
using RestSharp;
using System.Data.Entity.Core.Common.CommandTrees.ExpressionBuilder;
using System;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Globalization;
using System.Net.NetworkInformation;
using System.Runtime.Intrinsics.X86;
using System.Security.Policy;
using System.Text;

namespace CharacterEngineDiscord.Models.CharacterHub
{
    public class ChubCharacter
    {
        public ulong CharacterID { get; }
        public string Name { get; }
        public DateTime CreatedAt { get; }
        public string Description { get; }
        public string AuthorName { get; }
        public string FullPath { get; }
        public ulong ChatsCount { get; }
        public ulong MessagesCount { get; }
        public decimal Rating { get; }
        public ulong RatingCount { get; }
        public ulong StarCount { get; }
        public string TagLine { get; }
        public string Tags { get; }

        public string? Personality { get; }
        public string? FirstMessage { get; }
        public string? Scenario { get; }
        public string? ExampleDialogs { get; }


        public ChubCharacter(dynamic node, bool full)
        {
            var desc = (string)node.description;
            var tagline = (string)node.tagline;
            CharacterID = (ulong)node.id;
            Name = node.name;
            CreatedAt = node.createdAt;
            Description = string.IsNullOrWhiteSpace(desc) ? "[No description]" : desc;
            AuthorName = ((string)node.fullPath).Split('/').First();
            FullPath = node.fullPath;
            ChatsCount = (ulong)node.nChats;
            MessagesCount = (ulong)node.nMessages;
            Rating = node.rating;
            RatingCount = (ulong)node.ratingCount;
            StarCount = (ulong)node.starCount;
            TagLine = string.IsNullOrWhiteSpace(tagline) ? "[No tagline]" : tagline;
            Tags = string.Join(',', node.topics);

            if (full)
            {
                var definition = node.definition;
                Personality = definition.personality;
                FirstMessage = definition.first_message;
                Scenario = definition.scenario;
                ExampleDialogs = definition.example_dialogs;
            }
        }
    }
}