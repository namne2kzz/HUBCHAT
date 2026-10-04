namespace HUB.Realtime.WebApi.Presence;

/// <summary>User presence status. Online/Away/DoNotDisturb are "online" variants; Offline means no live connection.</summary>
public enum PresenceStatus { Offline = 0, Online = 1, Away = 2, DoNotDisturb = 3 }
