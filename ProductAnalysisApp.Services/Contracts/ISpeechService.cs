using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ProductAnalysisApp.Services.Contracts
{
    public interface ISpeechService
    {
        Task<TranscribeResult> TranscribeAsync(
            byte[] audioBytes,
            string fileName = "audio.m4a",
            string language = "tr");

        Task<SynthesizeResult> SynthesizeAsync(
            string text,
            string? voiceId = null,
            double stability = 0.5,
            double similarityBoost = 0.75);
    }
}
