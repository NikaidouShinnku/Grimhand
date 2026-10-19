# Standalone fake-only checks. No Unity, Steam native library, cloud or account access.
$ErrorActionPreference = 'Stop'
$queueSource = Get-Content -LiteralPath (Join-Path $PSScriptRoot '../../Scripts/Steam/SteamAchievementQueue.cs') -Raw
Add-Type -TypeDefinition ($queueSource + @'

public sealed class FakeAchievementAdapter : Grimhand.Steam.ISteamAchievementAdapter
{
    public bool IsAvailable { get; set; } = true;
    public ulong AccountId { get; set; } = 1;
    public event System.Action<ulong, bool> StatsReceived;
    public event System.Action<bool> StatsStored;
    public int Requests, Sets, Stores;
    public bool Reject, Cached;
    public bool RequestCurrentStats() { Requests++; return !Reject; }
    public bool GetAchievement(string id, out bool unlocked) { unlocked = Cached; return !Reject; }
    public bool SetAchievement(string id) { Sets++; Cached = true; return !Reject; }
    public bool StoreStats() { Stores++; return !Reject; }
    public void Receive(ulong id, bool ok) { StatsReceived?.Invoke(id, ok); }
    public void Stored(bool ok) { StatsStored?.Invoke(ok); }
    public void Dispose() { }
}
public static class AchievementQueueChecks
{
    static void Check(bool condition, string name) { if (!condition) throw new System.Exception(name); }
    public static string Run()
    {
        ulong owner;
        bool stamp;
        var status = Grimhand.Steam.SteamAchievementOwnership.Resolve("", 0, false, out owner, out stamp);
        Check(status == Grimhand.Steam.SteamAchievementOwnership.Status.Waiting && !stamp, "offline does not claim owner");
        status = Grimhand.Steam.SteamAchievementOwnership.Resolve("", 123, false, out owner, out stamp);
        Check(status == Grimhand.Steam.SteamAchievementOwnership.Status.Waiting && !stamp, "editor/test account does not claim owner");
        status = Grimhand.Steam.SteamAchievementOwnership.Resolve("", 123, true, out owner, out stamp);
        Check(status == Grimhand.Steam.SteamAchievementOwnership.Status.Ready && stamp && owner == 123, "verified login claims empty owner");
        var persisted = owner.ToString(System.Globalization.CultureInfo.InvariantCulture);
        status = Grimhand.Steam.SteamAchievementOwnership.Resolve(persisted, 456, true, out owner, out stamp);
        Check(status == Grimhand.Steam.SteamAchievementOwnership.Status.Mismatch && !stamp && owner == 123, "cross-launch mismatch cannot overwrite owner");
        status = Grimhand.Steam.SteamAchievementOwnership.Resolve(persisted, 123, true, out owner, out stamp);
        Check(status == Grimhand.Steam.SteamAchievementOwnership.Status.Ready && !stamp, "cross-launch matching owner resumes");
        status = Grimhand.Steam.SteamAchievementOwnership.Resolve("bad-owner", 123, true, out owner, out stamp);
        Check(status == Grimhand.Steam.SteamAchievementOwnership.Status.Invalid && !stamp, "invalid owner fails closed");
        status = Grimhand.Steam.SteamAchievementOwnership.Resolve(" ", 123, true, out owner, out stamp);
        Check(status == Grimhand.Steam.SteamAchievementOwnership.Status.Invalid && !stamp, "nonempty whitespace owner never overwritten");
        var fake = new FakeAchievementAdapter();
        var ids = new System.Collections.Generic.HashSet<string> { "ONE" };
        var q = new Grimhand.Steam.SteamAchievementQueue(fake);
        q.Bind(() => ids);
        q.Tick(0);
        q.Tick(1);
        Check(fake.Requests == 1 && fake.Sets == 0, "wait for stats callback");
        fake.Receive(2, true); q.Tick(2);
        Check(fake.Sets == 0, "ignore other account callback");
        fake.Receive(1, true); q.Tick(3);
        Check(fake.Sets == 1 && fake.Stores == 1, "submit after readiness");
        fake.Stored(false); q.Tick(4);
        Check(fake.Requests == 1, "failure backoff");
        q.Tick(5); fake.Receive(1, true); q.Tick(6);
        Check(fake.Sets == 2 && fake.Stores == 2, "retry despite locally cached unlock");
        fake.Stored(true); q.Tick(8);
        Check(fake.Sets == 2, "commit only on success, no duplicate");
        ids.Add("TWO"); fake.Cached = false; q.Tick(10);
        Check(fake.Sets == 3, "discover newly earned ID");
        fake.AccountId = 2; fake.Stored(true); q.Tick(11);
        Check(fake.Sets == 3, "account change blocks writes and callback");
        fake.AccountId = 1; q.Tick(12);
        Check(fake.Requests == 3, "returning account must reload");
        q.Dispose(); fake.Receive(1, true); q.Tick(50);
        Check(fake.Sets == 3, "dispose unsubscribes and stops");

        fake = new FakeAchievementAdapter { Reject = true };
        q = new Grimhand.Steam.SteamAchievementQueue(fake);
        q.Bind(() => ids, 1); q.Tick(0); q.Tick(1); q.Tick(2); q.Tick(5); q.Tick(6);
        Check(fake.Requests == 3, "exponential retry schedule");
        q.Dispose();
        fake = new FakeAchievementAdapter { Cached = true };
        q = new Grimhand.Steam.SteamAchievementQueue(fake);
        q.Bind(() => ids, 1); q.Tick(0); fake.Receive(1, true); q.Tick(1);
        Check(fake.Sets == 0 && fake.Stores == 0, "preserve existing Steam unlocks");
        q.Bind(() => ids, 2); q.Tick(2);
        Check(fake.Requests == 1, "persisted owner mismatch blocks requests");
        q.Dispose();
        fake = new FakeAchievementAdapter();
        q = new Grimhand.Steam.SteamAchievementQueue(fake);
        q.Bind(() => ids); q.Tick(0); q.Tick(30); q.Tick(31); q.Tick(32);
        Check(fake.Requests == 2, "callback timeout retries with backoff");
        q.Bind(null); q.Tick(99);
        Check(fake.Requests == 2, "null binding stops work");
        q.Dispose();
        return "PASS: cross-launch ownership, offline/editor/test no-claim, verified login claim, invalid/mismatched owner, readiness, account filtering, store failure/cache retry, commit, live unlocks, account switching, disposal, exponential backoff, Steam unlock preservation, timeout, unbind.";
    }
}
'@)
[AchievementQueueChecks]::Run()
