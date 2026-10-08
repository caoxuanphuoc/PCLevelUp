using GrowthTracker.Infrastructure.Updates;
using Microsoft.Extensions.Logging.Abstractions;

namespace GrowthTracker.Infrastructure.Tests;

public class UpdateServiceTests
{
    [Fact]
    public async Task NotInstalled_ReportsUnsupported_AndNeverChecks()
    {
        var service = new VelopackUpdateService(UpdateFeed.Default, NullLogger<VelopackUpdateService>.Instance);

        service.IsSupported.Should().BeFalse();
        service.CurrentVersion.Should().NotBeNullOrWhiteSpace();
        (await service.CheckAsync(includePrerelease: false)).Should().BeNull();
        service.Invoking(s => s.ApplyOnExitAndRestart()).Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Feed_DefaultsToGitHubReleases_AndCanBeOverriddenForTesting()
    {
        UpdateFeed.FromEnvironment(_ => null).Should().Be(UpdateFeed.Default);
        UpdateFeed.Default.RepositoryUrl.Should().StartWith("https://github.com/");

        var local = UpdateFeed.FromEnvironment(v => v == UpdateFeed.OverrideVariable ? "C:/feed" : null);
        local.LocalFeed.Should().Be("C:/feed");
        local.RepositoryUrl.Should().Be(UpdateFeed.Default.RepositoryUrl);
    }
}
