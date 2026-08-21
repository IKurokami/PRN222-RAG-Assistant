using Microsoft.EntityFrameworkCore;
using Pgvector.EntityFrameworkCore;
using PRN222.RagAssistant.Data;
using PRN222.RagAssistant.Domain.Entities;
using PRN222.RagAssistant.Infrastructure.Rag.Exceptions;
using PRN222.RagAssistant.Infrastructure.Services;
using Xunit;

namespace PRN222.RagAssistant.Tests;

public sealed class UserQuotaServiceTests
{
    private static ApplicationDbContext CreateInMemoryDbContext()
    {
        var postgresOptions = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql(
                "Host=unused;Database=unused;Username=unused;Password=unused",
                npgsql => npgsql.UseVector())
            .Options;
        using var postgresContext = new ApplicationDbContext(postgresOptions);

        var inMemoryOptions = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"quota-test-{Guid.NewGuid()}")
            .UseModel(postgresContext.Model)
            .Options;

        return new ApplicationDbContext(inMemoryOptions);
    }

    [Fact]
    public async Task GetRemainingQuotaAsync_ReturnsUserQuota_WhenUserExists()
    {
        // Arrange
        using var dbContext = CreateInMemoryDbContext();
        var userId = Guid.NewGuid();
        var user = new ApplicationUser
        {
            Id = userId,
            UserName = "student@test.com",
            Email = "student@test.com",
            DisplayName = "Test Student",
            CreatedAtUtc = DateTime.UtcNow,
            QuotaRemaining = 15
        };
        dbContext.Users.Add(user);
        await dbContext.SaveChangesAsync();

        var service = new UserQuotaService(dbContext);

        // Act
        var remaining = await service.GetRemainingQuotaAsync(userId);

        // Assert
        Assert.Equal(15, remaining);
    }

    [Fact]
    public async Task GetRemainingQuotaAsync_ReturnsZero_WhenUserDoesNotExist()
    {
        // Arrange
        using var dbContext = CreateInMemoryDbContext();
        var service = new UserQuotaService(dbContext);

        // Act
        var remaining = await service.GetRemainingQuotaAsync(Guid.NewGuid());

        // Assert
        Assert.Equal(0, remaining);
    }

    [Theory]
    [InlineData(10, true)]
    [InlineData(1, true)]
    [InlineData(0, false)]
    public async Task HasQuotaAsync_ReturnsExpectedResult(int quotaRemaining, bool expectedHasQuota)
    {
        // Arrange
        using var dbContext = CreateInMemoryDbContext();
        var userId = Guid.NewGuid();
        var user = new ApplicationUser
        {
            Id = userId,
            UserName = "user@test.com",
            Email = "user@test.com",
            DisplayName = "Test User",
            CreatedAtUtc = DateTime.UtcNow,
            QuotaRemaining = quotaRemaining
        };
        dbContext.Users.Add(user);
        await dbContext.SaveChangesAsync();

        var service = new UserQuotaService(dbContext);

        // Act
        var hasQuota = await service.HasQuotaAsync(userId);

        // Assert
        Assert.Equal(expectedHasQuota, hasQuota);
    }

    [Fact]
    public async Task HasQuotaAsync_ReturnsFalse_WhenUserDoesNotExist()
    {
        // Arrange
        using var dbContext = CreateInMemoryDbContext();
        var service = new UserQuotaService(dbContext);

        // Act
        var hasQuota = await service.HasQuotaAsync(Guid.NewGuid());

        // Assert
        Assert.False(hasQuota);
    }

    [Fact]
    public async Task ConsumeQuotaAsync_DecrementsQuotaByOne_WhenUserHasQuota()
    {
        // Arrange
        using var dbContext = CreateInMemoryDbContext();
        var userId = Guid.NewGuid();
        var user = new ApplicationUser
        {
            Id = userId,
            UserName = "user@test.com",
            Email = "user@test.com",
            DisplayName = "Test User",
            CreatedAtUtc = DateTime.UtcNow,
            QuotaRemaining = 5
        };
        dbContext.Users.Add(user);
        await dbContext.SaveChangesAsync();

        var service = new UserQuotaService(dbContext);

        // Act
        await service.ConsumeQuotaAsync(userId);

        // Assert
        var updatedUser = await dbContext.Users.FirstAsync(u => u.Id == userId);
        Assert.Equal(4, updatedUser.QuotaRemaining);
    }

    [Fact]
    public async Task ConsumeQuotaAsync_ThrowsInsufficientQuotaException_WhenQuotaIsZero()
    {
        // Arrange
        using var dbContext = CreateInMemoryDbContext();
        var userId = Guid.NewGuid();
        var user = new ApplicationUser
        {
            Id = userId,
            UserName = "user@test.com",
            Email = "user@test.com",
            DisplayName = "Test User",
            CreatedAtUtc = DateTime.UtcNow,
            QuotaRemaining = 0
        };
        dbContext.Users.Add(user);
        await dbContext.SaveChangesAsync();

        var service = new UserQuotaService(dbContext);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<InsufficientQuotaException>(() => service.ConsumeQuotaAsync(userId));
        Assert.Equal(userId, ex.UserId);
    }

    [Fact]
    public async Task ConsumeQuotaAsync_ThrowsInsufficientQuotaException_WhenUserDoesNotExist()
    {
        // Arrange
        using var dbContext = CreateInMemoryDbContext();
        var userId = Guid.NewGuid();
        var service = new UserQuotaService(dbContext);

        // Act & Assert
        await Assert.ThrowsAsync<InsufficientQuotaException>(() => service.ConsumeQuotaAsync(userId));
    }

    [Fact]
    public async Task GrantQuotaAsync_IncrementsQuotaByUnits_WhenUserExists()
    {
        // Arrange
        using var dbContext = CreateInMemoryDbContext();
        var userId = Guid.NewGuid();
        var user = new ApplicationUser
        {
            Id = userId,
            UserName = "user@test.com",
            Email = "user@test.com",
            DisplayName = "Test User",
            CreatedAtUtc = DateTime.UtcNow,
            QuotaRemaining = 5
        };
        dbContext.Users.Add(user);
        await dbContext.SaveChangesAsync();

        var service = new UserQuotaService(dbContext);

        // Act
        await service.GrantQuotaAsync(userId, 50);

        // Assert
        var updatedUser = await dbContext.Users.FirstAsync(u => u.Id == userId);
        Assert.Equal(55, updatedUser.QuotaRemaining);
    }

    [Fact]
    public async Task GrantQuotaAsync_DoesNothing_WhenQuotaUnitsZeroOrNegative()
    {
        // Arrange
        using var dbContext = CreateInMemoryDbContext();
        var userId = Guid.NewGuid();
        var user = new ApplicationUser
        {
            Id = userId,
            UserName = "user@test.com",
            Email = "user@test.com",
            DisplayName = "Test User",
            CreatedAtUtc = DateTime.UtcNow,
            QuotaRemaining = 10
        };
        dbContext.Users.Add(user);
        await dbContext.SaveChangesAsync();

        var service = new UserQuotaService(dbContext);

        // Act
        await service.GrantQuotaAsync(userId, 0);
        await service.GrantQuotaAsync(userId, -5);

        // Assert
        var updatedUser = await dbContext.Users.FirstAsync(u => u.Id == userId);
        Assert.Equal(10, updatedUser.QuotaRemaining);
    }
}
