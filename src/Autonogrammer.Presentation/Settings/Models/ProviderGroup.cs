namespace Autonogrammer.Presentation.Settings.Models;

/// <summary>The group a provider card sits in on the Models page.</summary>
public enum ProviderGroup
{
    /// <summary>A runtime on the user's own machine (Ollama, LM Studio, llama.cpp and similar), reached on a loopback address.</summary>
    Local = 0,

    /// <summary>A hosted API reached with a provider API key and billed per use.</summary>
    Cloud = 1,

    /// <summary>A subscription coding plan with its own key and published request limits.</summary>
    CodingPlan = 2,

    /// <summary>A provider the user defined; it is written to omp's <c>models.yml</c>.</summary>
    Custom = 3,

    /// <summary>A provider omp knows by itself, without any Autonogrammer entry.</summary>
    OmpBuiltIn = 4,
}
