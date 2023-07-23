using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CharacterEngineDiscord.Models.OpenAI
{
    public interface IOpenAiResponse
    {
        int Code { get; }
        string? ErrorReason { get; }
        public bool IsSuccessful { get; }
        public bool IsFailure { get; }
    }
}
