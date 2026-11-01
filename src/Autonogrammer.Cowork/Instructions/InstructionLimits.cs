namespace Autonogrammer.Cowork.Instructions;

/// <summary>D-09, D-10 and D-11 budgets. Values are KiB converted to bytes.</summary>
public static class InstructionLimits
{
    /// <summary>Global instructions. 8 KiB.</summary>
    public const int GlobalInstructionsBytes = 8 * 1024;

    /// <summary>Project instructions. 16 KiB.</summary>
    public const int ProjectInstructionsBytes = 16 * 1024;

    /// <summary>Standing-context block. 28 KiB (global 8 + project 16 + memory brief 4).</summary>
    public const int StandingContextBytes = 28 * 1024;

    /// <summary>The fixed system-prompt paragraph. 2 KiB.</summary>
    public const int SystemPromptParagraphBytes = 2 * 1024;

    /// <summary>The memory brief. 4 KiB.</summary>
    public const int MemoryBriefBytes = 4 * 1024;

    /// <summary>The system-prompt file. 8 KiB.</summary>
    public const int SystemPromptFileBytes = 8 * 1024;

    /// <summary>System-prompt base apportionment. 4 KiB.</summary>
    public const int SystemPromptBaseBytes = 4 * 1024;

    /// <summary>System-prompt standing paragraph apportionment. 2 KiB.</summary>
    public const int SystemPromptStandingParagraphBytes = 2 * 1024;

    /// <summary>System-prompt browser apportionment. 1 KiB.</summary>
    public const int SystemPromptBrowserBytes = 1 * 1024;

    /// <summary>System-prompt computer apportionment. 1 KiB.</summary>
    public const int SystemPromptComputerBytes = 1 * 1024;
}
