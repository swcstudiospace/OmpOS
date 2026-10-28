using System.Text;
using OmpOS.Presentation.Mentions;
using Shouldly;
using Xunit;

namespace OmpOS.Presentation.Tests.Mentions;

/// <summary>Every mention source over fakes: what it lists for a query, what it hides, how it resolves and reads.</summary>
public sealed class MentionSourceTests
{
    private sealed class PrefixIgnore(string prefix) : IMentionIgnoreFilter
    {
        public bool IsIgnored(string relativePath, bool isDirectory) => relativePath.StartsWith(prefix, StringComparison.Ordinal);
    }

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    /// <summary>The file source ranks by the typed name, lists the shallowest files first for an empty query, cuts at the limit and never lists an ignored file.</summary>
    [Fact]
    public async Task FilesAreRankedCutAndFilteredByTheIgnoreRules()
    {
        var world = new FakeMentionWorld();
        world.AddFile("src/App.cs");
        world.AddFile("src/Util.cs");
        world.AddFile("build/AppOut.cs");
        world.AddFile("README.md");
        var source = new FileMentionSource(world, new PrefixIgnore("build/"));

        var typed = await source.SearchAsync("app", 8, Token).ConfigureAwait(true);
        var empty = await source.SearchAsync(string.Empty, 2, Token).ConfigureAwait(true);

        typed.Items.Select(static item => item.Id).ShouldBe(["src/App.cs"]);
        empty.Items.Select(static item => item.Id).ShouldBe(["README.md", "src/App.cs"]);
        empty.HasMore.ShouldBeTrue();
        typed.Items[0].Detail.ShouldBe("src");
        typed.Items[0].SizeBytes.ShouldBe(7);
    }

    /// <summary>A file that usually holds secrets is never listed, except when the user types its exact name, and then it asks for confirmation.</summary>
    [Fact]
    public async Task SecretFilesAreListedOnlyWhenTheExactNameIsTypedAndNeedConfirmation()
    {
        var world = new FakeMentionWorld();
        world.AddFile(".env", "KEY=1");
        world.AddFile("config/prod.pem", "pem");
        world.AddFile("src/App.cs");
        var source = new FileMentionSource(world);

        (await source.SearchAsync(string.Empty, 8, Token).ConfigureAwait(true)).Items.Select(static item => item.Id).ShouldBe(["src/App.cs"]);
        (await source.SearchAsync("env", 8, Token).ConfigureAwait(true)).Items.ShouldBeEmpty();
        var exact = (await source.SearchAsync(".env", 8, Token).ConfigureAwait(true)).Items.ShouldHaveSingleItem();
        var byBaseName = (await source.SearchAsync("prod.pem", 8, Token).ConfigureAwait(true)).Items.ShouldHaveSingleItem();

        exact.RequiresConfirmation.ShouldBeTrue();
        byBaseName.Id.ShouldBe("config/prod.pem");
        byBaseName.RequiresConfirmation.ShouldBeTrue();
    }

    /// <summary>A slash after a folder lists what is directly inside it: files from the file source, folders from the folder source.</summary>
    [Fact]
    public async Task ASlashDescendsIntoAFolder()
    {
        var world = new FakeMentionWorld();
        world.AddFolder("src");
        world.AddFile("src/App.cs");
        world.AddFolder("src/views");
        world.AddFile("src/views/Main.cs");
        var files = new FileMentionSource(world);
        var folders = new FolderMentionSource(world);

        (await files.SearchAsync("src/", 8, Token).ConfigureAwait(true)).Items.Select(static item => item.Id).ShouldBe(["src/App.cs"]);
        var inside = (await folders.SearchAsync("src/", 8, Token).ConfigureAwait(true)).Items.ShouldHaveSingleItem();
        inside.Display.ShouldBe("src/views/");
        (await folders.SearchAsync(string.Empty, 8, Token).ConfigureAwait(true)).Items.Select(static item => item.Display).ShouldBe(["src/", "src/views/"]);
    }

    /// <summary>A resolved file is a mention with its path as name and token, a file URI and a size; a file the ignore rules exclude after the search cannot be attached.</summary>
    [Fact]
    public async Task ResolvingAFileAndRefusingAnIgnoredOne()
    {
        var world = new FakeMentionWorld();
        world.AddFile("src/App.cs", "class App;");
        var ignored = false;
        var source = new FileMentionSource(world, new TogglingIgnore(() => ignored));
        var candidate = (await source.SearchAsync("app", 8, Token).ConfigureAwait(true)).Items.Single();

        var mention = await source.ResolveAsync(candidate, Token).ConfigureAwait(true);

        mention.Kind.ShouldBe(MentionKind.File);
        mention.Token.ShouldBe("@src/App.cs");
        mention.Location.AbsoluteUri.ShouldBe("file:///work/src/App.cs");
        mention.SizeBytes.ShouldBe(10);
        (await mention.ReadAsync(100, Token).ConfigureAwait(true)).Text.ShouldBe("class App;");

        ignored = true;
        var refusal = await Should.ThrowAsync<MentionUnavailableException>(() => source.ResolveAsync(candidate, Token)).ConfigureAwait(true);
        refusal.Message.ShouldBe(MentionCopy.IgnoredByRule("src/App.cs"));
    }

    private sealed class TogglingIgnore(Func<bool> ignored) : IMentionIgnoreFilter
    {
        public bool IsIgnored(string relativePath, bool isDirectory) => ignored();
    }

    /// <summary>A folder mention lists the files under the folder, sorted, without ignored or secret files, and a small cap cuts the listing on a character boundary.</summary>
    [Fact]
    public async Task AFolderListingLeavesOutIgnoredAndSecretFilesAndCutsOnACharacterBoundary()
    {
        var world = new FakeMentionWorld();
        world.AddFolder("src");
        world.AddFile("src/b.cs");
        world.AddFile("src/a.cs");
        world.AddFile("src/.env");
        world.AddFile("src/gen/out.cs");
        world.AddFile("src/\u00e9\u00e9\u00e9\u00e9.cs");
        var source = new FolderMentionSource(world, new PrefixIgnore("src/gen"));
        var candidate = (await source.SearchAsync("src", 8, Token).ConfigureAwait(true)).Items.Single();
        var mention = await source.ResolveAsync(candidate, Token).ConfigureAwait(true);

        var full = await mention.ReadAsync(10_000, Token).ConfigureAwait(true);
        var cut = await mention.ReadAsync(35, Token).ConfigureAwait(true);

        mention.Token.ShouldBe("@src/");
        full.Text.ShouldBe("Folder src/\nsrc/a.cs\nsrc/b.cs\nsrc/\u00e9\u00e9\u00e9\u00e9.cs\n");
        full.Truncated.ShouldBeFalse();
        cut.Truncated.ShouldBeTrue();
        cut.SizeBytes.ShouldBe(Encoding.UTF8.GetByteCount(full.Text));
        Encoding.UTF8.GetByteCount(cut.Text).ShouldBeLessThanOrEqualTo(35);
        full.Text.StartsWith(cut.Text, StringComparison.Ordinal).ShouldBeTrue();
        cut.Text.ShouldBe("Folder src/\nsrc/a.cs\nsrc/b.cs\nsrc/");
    }

    /// <summary>The terminal source ranks terminals by title, says whether each runs, and reads output through the provider.</summary>
    [Fact]
    public async Task TerminalsAreListedWithTheirStateAndRead()
    {
        var world = new FakeMentionWorld();
        world.Terminals.Add(new MentionTerminalInfo("t1", "build watch", true));
        world.Terminals.Add(new MentionTerminalInfo("t2", "tests", false));
        world.TerminalOutput["t1"] = "compiling";
        var source = new TerminalMentionSource(world);

        var found = await source.SearchAsync("watch", 8, Token).ConfigureAwait(true);
        var mention = await source.ResolveAsync(found.Items.Single(), Token).ConfigureAwait(true);

        found.Items.Single().Detail.ShouldBe("running");
        (await source.SearchAsync(string.Empty, 8, Token).ConfigureAwait(true)).Items.Select(static item => item.Detail).ShouldBe(["running", "finished"]);
        mention.Location.AbsoluteUri.ShouldBe("ompos://terminal/t1");
        (await mention.ReadAsync(100, Token).ConfigureAwait(true)).Text.ShouldBe("compiling");
    }

    /// <summary>A chat source lists chats only and a side chat source lists side chats only, most recent first for an empty query.</summary>
    [Fact]
    public async Task ChatsAndSideChatsAreSeparatedAndRecentFirst()
    {
        var world = new FakeMentionWorld();
        var now = new DateTimeOffset(2026, 10, 27, 9, 0, 0, TimeSpan.Zero);
        world.Chats.Add(new MentionChatInfo("c1", "Old design chat", false, now.AddDays(-2)));
        world.Chats.Add(new MentionChatInfo("c2", "New design chat", false, now));
        world.Chats.Add(new MentionChatInfo("s1", "Side question", true, now.AddHours(-1)));
        world.Transcripts["c2"] = "hello";

        var chats = await new ChatMentionSource(MentionKind.Chat, world).SearchAsync(string.Empty, 8, Token).ConfigureAwait(true);
        var sides = await new ChatMentionSource(MentionKind.SideChat, world).SearchAsync(string.Empty, 8, Token).ConfigureAwait(true);
        var mention = await new ChatMentionSource(MentionKind.Chat, world).ResolveAsync(chats.Items[0], Token).ConfigureAwait(true);

        chats.Items.Select(static item => item.Id).ShouldBe(["c2", "c1"]);
        sides.Items.Select(static item => item.Id).ShouldBe(["s1"]);
        sides.Items.Single().Kind.ShouldBe(MentionKind.SideChat);
        (await mention.ReadAsync(100, Token).ConfigureAwait(true)).Text.ShouldBe("hello");
    }

    /// <summary>A git source shows how many files the diff changes, is listed disabled with the reason when it cannot be read, and refuses to attach then.</summary>
    [Fact]
    public async Task GitDiffRowsDescribeThemselvesAndDisableWithAReason()
    {
        var world = new FakeMentionWorld();
        var working = new GitDiffMentionSource(MentionKind.GitDiffWorking, world);
        var branch = new GitDiffMentionSource(MentionKind.GitDiffBranch, world);

        var workingRow = (await working.SearchAsync(string.Empty, 8, Token).ConfigureAwait(true)).Items.Single();
        var branchRow = (await branch.SearchAsync("main", 8, Token).ConfigureAwait(true)).Items.Single();
        workingRow.Detail.ShouldBe("Diff of working state, 2 files");
        branchRow.Detail.ShouldBe("Diff with main, 3 files");
        (await working.SearchAsync("zzz", 8, Token).ConfigureAwait(true)).Items.ShouldBeEmpty();
        (await working.SearchAsync("branch", 8, Token).ConfigureAwait(true)).Items.ShouldBeEmpty();
        var mention = await working.ResolveAsync(workingRow, Token).ConfigureAwait(true);
        (await mention.ReadAsync(100, Token).ConfigureAwait(true)).Text.ShouldContain("+one", Case.Sensitive);
        mention.Token.ShouldBe("@Commit");

        world.WorkingSummary = new MentionGitDiffSummary(false, 0, null, null);
        var disabled = (await working.SearchAsync(string.Empty, 8, Token).ConfigureAwait(true)).Items.Single();
        disabled.DisabledReason.ShouldBe(MentionCopy.NoRepository);
        disabled.IsEnabled.ShouldBeFalse();
        await Should.ThrowAsync<MentionUnavailableException>(() => working.ResolveAsync(disabled, Token)).ConfigureAwait(true);
    }

    /// <summary>Rules and skills list by name with their description and scope, and resolve to a markdown resource that reads the body.</summary>
    [Fact]
    public async Task RulesAndSkillsListWithDescriptionAndScope()
    {
        var world = new FakeMentionWorld();
        world.Rules.Add(new MentionConventionItem("style-guide", "How we write C#", "project"));
        world.Rules.Add(new MentionConventionItem("api", null, null));
        world.Skills.Add(new MentionConventionItem("deploy", "Ship it", null));
        world.Bodies["rule:style-guide"] = "Use records.";
        var rules = new ConventionMentionSource(MentionKind.Rule, world);
        var skills = new ConventionMentionSource(MentionKind.Skill, world);

        var found = await rules.SearchAsync(string.Empty, 8, Token).ConfigureAwait(true);
        var mention = await rules.ResolveAsync(found.Items.Single(static item => string.Equals(item.Id, "style-guide", StringComparison.Ordinal)), Token).ConfigureAwait(true);

        found.Items.Select(static item => item.Id).ShouldBe(["api", "style-guide"]);
        found.Items.Single(static item => string.Equals(item.Id, "style-guide", StringComparison.Ordinal)).Detail.ShouldBe("How we write C# (project)");
        (await skills.SearchAsync("dep", 8, Token).ConfigureAwait(true)).Items.Single().Detail.ShouldBe("Ship it");
        mention.Location.AbsoluteUri.ShouldBe("ompos://rule/style-guide");
        mention.MimeType.ShouldBe("text/markdown");
        (await mention.ReadAsync(100, Token).ConfigureAwait(true)).Text.ShouldBe("Use records.");
    }

    /// <summary>Without a configured host the pull request source is unavailable with the reason that names the fix; with one it lists numbers and titles.</summary>
    [Fact]
    public async Task PullRequestsAreUnavailableWithoutAHost()
    {
        var none = new PullRequestMentionSource(new UnavailablePullRequestSource());

        var result = await none.SearchAsync("12", 8, Token).ConfigureAwait(true);

        result.Items.ShouldBeEmpty();
        result.UnavailableReason.ShouldBe(MentionCopy.PullRequestsNoHost);
        none.Trigger.ShouldBe(MentionTrigger.Hash);
        await Should.ThrowAsync<MentionUnavailableException>(() => none.ResolveAsync(new MentionCandidate(MentionKind.PullRequest, "12", "12"), Token)).ConfigureAwait(true);

        var hosts = new FakePullRequests();
        hosts.Pulls.Add(new MentionPullRequest(123, "Fix crash on start", "open"));
        hosts.Pulls.Add(new MentionPullRequest(124, "Docs", "merged"));
        var configured = new PullRequestMentionSource(hosts);
        var found = (await configured.SearchAsync("crash", 8, Token).ConfigureAwait(true)).Items.Single();
        found.Display.ShouldBe("123");
        found.Detail.ShouldBe("Fix crash on start (open)");
        var mention = await configured.ResolveAsync(found, Token).ConfigureAwait(true);
        mention.Token.ShouldBe("#123");
        await Should.ThrowAsync<MentionUnavailableException>(() => configured.ResolveAsync(new MentionCandidate(MentionKind.PullRequest, "abc", "abc"), Token)).ConfigureAwait(true);
    }

    /// <summary>The browser row is disabled while no page is open; with a page it attaches the title, the address and the text.</summary>
    [Fact]
    public async Task TheBrowserRowNeedsAnOpenPage()
    {
        var world = new FakeMentionWorld();
        var source = new BrowserMentionSource(world);

        var closed = (await source.SearchAsync(string.Empty, 8, Token).ConfigureAwait(true)).Items.Single();
        closed.DisabledReason.ShouldBe(MentionCopy.BrowserNoPage);

        world.Page = new MentionBrowserPage("Docs home", new Uri("https://example.test/docs"), "Welcome", 7);
        var open = (await source.SearchAsync("docs", 8, Token).ConfigureAwait(true)).Items.Single();
        var mention = await source.ResolveAsync(open, Token).ConfigureAwait(true);
        var content = await mention.ReadAsync(1000, Token).ConfigureAwait(true);

        open.Detail.ShouldBe("Docs home");
        content.Text.ShouldBe("Docs home\nhttps://example.test/docs\n\nWelcome");
    }

    /// <summary>A source of one kind refuses another kind at construction.</summary>
    [Fact]
    public void SourcesRefuseAForeignKind()
    {
        var world = new FakeMentionWorld();

        Should.Throw<ArgumentOutOfRangeException>(() => new ChatMentionSource(MentionKind.File, world));
        Should.Throw<ArgumentOutOfRangeException>(() => new GitDiffMentionSource(MentionKind.Chat, world));
        Should.Throw<ArgumentOutOfRangeException>(() => new ConventionMentionSource(MentionKind.Browser, world));
    }
}
