using OmpOS.Presentation.Cloud;
using Shouldly;
using Xunit;

namespace OmpOS.Presentation.Tests.Cloud;

/// <summary>The rules for the new-agent entries: https repository addresses without credentials, git branch names, secret names that cannot be values, and the idle time.</summary>
public sealed class CloudInputValidatorTests
{
    private const string Canary = "s3cr3t-canary-value";

    /// <summary>Plain https addresses pass and come back normalised: lower-case host, no trailing slash, port kept.</summary>
    [Theory]
    [InlineData("https://github.com/owner/repo", "https://github.com/owner/repo")]
    [InlineData("  https://GitHub.com/owner/repo/  ", "https://github.com/owner/repo")]
    [InlineData("https://github.com/owner/repo.git", "https://github.com/owner/repo.git")]
    [InlineData("https://git.example.com:8443/team/app", "https://git.example.com:8443/team/app")]
    [InlineData("https://gitlab.com/group/sub/project", "https://gitlab.com/group/sub/project")]
    public void AValidRepositoryAddressIsNormalised(string entry, string expected)
    {
        var error = CloudInputValidator.ValidateRepositoryAddress(entry, out var repository);

        error.ShouldBeNull();
        repository.ShouldNotBeNull().Address.ShouldBe(expected);
    }

    /// <summary>An address that is not https, carries credentials or a query, or lacks the owner or the repository is refused with its own sentence.</summary>
    [Theory]
    [InlineData("", "RepositoryRequired")]
    [InlineData("   ", "RepositoryRequired")]
    [InlineData("http://github.com/owner/repo", "RepositoryNotHttps")]
    [InlineData("git@github.com:owner/repo.git", "RepositoryNotHttps")]
    [InlineData("ssh://git@github.com/owner/repo", "RepositoryNotHttps")]
    [InlineData("github.com/owner/repo", "RepositoryNotHttps")]
    [InlineData("https://user@github.com/owner/repo", "RepositoryHasCredentials")]
    [InlineData("https://user:" + Canary + "@github.com/owner/repo", "RepositoryHasCredentials")]
    [InlineData("https://" + Canary + "@github.com/owner/repo", "RepositoryHasCredentials")]
    [InlineData("https://github.com/owner/repo?token=" + Canary, "RepositoryHasQuery")]
    [InlineData("https://github.com/owner/repo#readme", "RepositoryHasQuery")]
    [InlineData("https://github.com/owner", "RepositoryNeedsOwnerAndName")]
    [InlineData("https://github.com/", "RepositoryNeedsOwnerAndName")]
    [InlineData("https://github.com/owner/my repo", "RepositoryInvalid")]
    public void ABadRepositoryAddressIsRefusedWithTheRightSentence(string entry, string reason)
    {
        var error = CloudInputValidator.ValidateRepositoryAddress(entry, out var repository);

        repository.ShouldBeNull();
        error.ShouldBe(reason switch
        {
            "RepositoryRequired" => CloudCopy.RepositoryRequired,
            "RepositoryNotHttps" => CloudCopy.RepositoryNotHttps,
            "RepositoryHasCredentials" => CloudCopy.RepositoryHasCredentials,
            "RepositoryHasQuery" => CloudCopy.RepositoryHasQuery,
            "RepositoryNeedsOwnerAndName" => CloudCopy.RepositoryNeedsOwnerAndName,
            _ => CloudCopy.RepositoryInvalid,
        });
        error.ShouldNotBeNull().ShouldNotContain(Canary, Case.Sensitive);
    }

    /// <summary>An address of more than 2048 characters is refused.</summary>
    [Fact]
    public void AnOverlongRepositoryAddressIsRefused()
    {
        var entry = "https://github.com/owner/" + new string('a', CloudInputValidator.MaxRepositoryUrlLength);

        CloudInputValidator.ValidateRepositoryAddress(entry, out var repository).ShouldBe(CloudCopy.RepositoryTooLong);
        repository.ShouldBeNull();
    }

    /// <summary>Two spellings of one repository have the same key; two repositories have different keys.</summary>
    [Fact]
    public void TheKeyIgnoresCaseAndTheGitSuffix()
    {
        CloudInputValidator.KeyOf("https://github.com/Owner/Repo.git").ShouldBe(CloudInputValidator.KeyOf("https://github.com/owner/repo"));
        CloudInputValidator.KeyOf("https://github.com/owner/repo").ShouldNotBe(CloudInputValidator.KeyOf("https://github.com/owner/other"), StringComparer.Ordinal);
    }

    /// <summary>The short name is owner and repository without the git suffix; a string that is not an address comes back unchanged.</summary>
    [Theory]
    [InlineData("https://github.com/owner/repo", "owner/repo")]
    [InlineData("https://github.com/owner/repo.git", "owner/repo")]
    [InlineData("https://gitlab.com/group/sub/project", "sub/project")]
    [InlineData("not an address", "not an address")]
    public void TheDisplayNameIsOwnerSlashRepository(string address, string expected)
    {
        CloudInputValidator.DisplayNameOf(address).ShouldBe(expected);
    }

    /// <summary>Valid git branch names and a blank entry pass.</summary>
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("main")]
    [InlineData("feature/cloud-agents")]
    [InlineData("ompos/ab12cd")]
    [InlineData("release-1.2")]
    public void AValidBranchPasses(string branch)
    {
        CloudInputValidator.ValidateBranch(branch).ShouldBeNull();
    }

    /// <summary>Names git itself refuses, and names that would be taken for options, are refused.</summary>
    [Theory]
    [InlineData("two words")]
    [InlineData("a..b")]
    [InlineData("-leading-dash")]
    [InlineData("/leading-slash")]
    [InlineData("trailing/")]
    [InlineData("trailing.")]
    [InlineData("name.lock")]
    [InlineData("a/.hidden")]
    [InlineData("tilde~1")]
    [InlineData("caret^")]
    [InlineData("colon:name")]
    [InlineData("star*")]
    [InlineData("question?")]
    [InlineData("bracket[")]
    [InlineData("back\\slash")]
    [InlineData("a@{b")]
    [InlineData("@")]
    [InlineData("a//b")]
    public void AnInvalidBranchIsRefused(string branch)
    {
        CloudInputValidator.ValidateBranch(branch).ShouldBe(CloudCopy.BranchInvalid);
    }

    /// <summary>A branch name over 200 characters is refused with its own sentence.</summary>
    [Fact]
    public void AnOverlongBranchIsRefused()
    {
        CloudInputValidator.ValidateBranch(new string('b', CloudInputValidator.MaxBranchLength + 1))
            .ShouldBe(CloudCopy.BranchTooLong(CloudInputValidator.MaxBranchLength));
    }

    /// <summary>Names of the form of an environment variable pass.</summary>
    [Theory]
    [InlineData("API_KEY")]
    [InlineData("_private")]
    [InlineData("token2")]
    public void AValidSecretNamePasses(string name)
    {
        CloudInputValidator.ValidateSecretName(name).ShouldBeNull();
    }

    /// <summary>Anything that is not a plain name, including a name=value pair and a value pasted by mistake, is refused and never repeated.</summary>
    [Theory]
    [InlineData("1_STARTS_WITH_DIGIT")]
    [InlineData("HAS SPACE")]
    [InlineData("HAS-DASH")]
    [InlineData("API_KEY=" + Canary)]
    [InlineData("sk-live-" + Canary)]
    [InlineData("été")]
    public void ASecretEntryThatIsNotANameIsRefusedWithoutRepeatingIt(string entry)
    {
        var error = CloudInputValidator.ValidateSecretName(entry);

        error.ShouldBe(CloudCopy.SecretNameInvalid);
        error.ShouldNotBeNull().ShouldNotContain(Canary, Case.Sensitive);
    }

    /// <summary>An empty or overlong secret name has its own sentence.</summary>
    [Fact]
    public void AnEmptyOrOverlongSecretNameIsRefused()
    {
        CloudInputValidator.ValidateSecretName(" ").ShouldBe(CloudCopy.SecretNameRequired);
        CloudInputValidator.ValidateSecretName(new string('A', CloudInputValidator.MaxSecretNameLength + 1))
            .ShouldBe(CloudCopy.SecretNameTooLong(CloudInputValidator.MaxSecretNameLength));
    }

    /// <summary>The idle time is a whole number of minutes between the bounds; surrounding spaces are ignored.</summary>
    [Theory]
    [InlineData("1", 1)]
    [InlineData("15", 15)]
    [InlineData(" 20 ", 20)]
    [InlineData("240", 240)]
    public void AValidIdleTimeIsParsed(string entry, int minutes)
    {
        CloudInputValidator.ValidateIdleMinutes(entry, 240, out var parsed).ShouldBeNull();
        parsed.ShouldBe(minutes);
    }

    /// <summary>Zero, a negative number, a fraction, text and a time over the largest all fail with the same sentence that names the bounds.</summary>
    [Theory]
    [InlineData("0")]
    [InlineData("-5")]
    [InlineData("1.5")]
    [InlineData("abc")]
    [InlineData("")]
    [InlineData("241")]
    [InlineData("99999999999")]
    public void AnInvalidIdleTimeIsRefused(string entry)
    {
        CloudInputValidator.ValidateIdleMinutes(entry, 240, out var parsed).ShouldBe(CloudCopy.IdleInvalid(1, 240));
        parsed.ShouldBe(0);
    }

    /// <summary>A name needs one to eighty characters and no control character.</summary>
    [Fact]
    public void ANameNeedsTextOfTheRightLengthWithoutControlCharacters()
    {
        CloudInputValidator.ValidateName("Fix the build").ShouldBeNull();
        CloudInputValidator.ValidateName("  ").ShouldBe(CloudCopy.NameRequired);
        CloudInputValidator.ValidateName(null).ShouldBe(CloudCopy.NameRequired);
        CloudInputValidator.ValidateName(new string('n', CloudInputValidator.MaxNameLength)).ShouldBeNull();
        CloudInputValidator.ValidateName(new string('n', CloudInputValidator.MaxNameLength + 1)).ShouldBe(CloudCopy.NameTooLong(CloudInputValidator.MaxNameLength));
        CloudInputValidator.ValidateName("two\nlines").ShouldBe(CloudCopy.NameInvalid);
    }

    /// <summary>The first task may be empty and has an upper bound.</summary>
    [Fact]
    public void TheFirstTaskHasAnUpperBound()
    {
        CloudInputValidator.ValidatePrompt(null).ShouldBeNull();
        CloudInputValidator.ValidatePrompt(new string('p', CloudInputValidator.MaxPromptLength)).ShouldBeNull();
        CloudInputValidator.ValidatePrompt(new string('p', CloudInputValidator.MaxPromptLength + 1)).ShouldBe(CloudCopy.PromptTooLong(CloudInputValidator.MaxPromptLength));
    }
}
