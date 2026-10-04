namespace HUB.Chat.Domain.Enums;

/// <summary>Kind of external DASHBOARD resource a channel/thread is linked to.</summary>
public enum LinkedResourceType
{
    /// <summary>A DASHBOARD work item (User Story / Task / Bug / TestPlan).</summary>
    WorkItem = 0,
    /// <summary>A DASHBOARD wiki page.</summary>
    WikiPage = 1,
    /// <summary>A DASHBOARD sprint — the channel is the sprint's team discussion space.</summary>
    Sprint = 2,
}
