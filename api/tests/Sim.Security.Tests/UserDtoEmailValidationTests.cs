using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using Sim.System.Application.Contracts.User;

namespace Sim.Security.Tests;

/// <summary>
/// 验证管理端 CreateUserDto/UpdateUserDto 的空邮箱可通过 ModelState 校验。
/// </summary>
public sealed class UserDtoEmailValidationTests
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void CreateUserDto_blank_email_passes_model_validation(string email)
    {
        var dto = DeserializeCreate(email);

        Assert.Null(dto.Email);
        Assert.Empty(Validate(dto));
    }

    [Fact]
    public void CreateUserDto_valid_email_is_trimmed_and_passes_model_validation()
    {
        var dto = DeserializeCreate("  user@example.com  ");

        Assert.Equal("user@example.com", dto.Email);
        Assert.Empty(Validate(dto));
    }

    [Theory]
    [InlineData("not-an-email")]
    [InlineData("user@")]
    [InlineData("@example.com")]
    public void CreateUserDto_invalid_email_fails_model_validation(string email)
    {
        var dto = DeserializeCreate(email);

        Assert.Contains(
            Validate(dto),
            result => result.MemberNames.Contains(nameof(CreateUserDto.Email)));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void UpdateUserDto_blank_email_passes_model_validation(string email)
    {
        var json = JsonSerializer.Serialize(new
        {
            id = Guid.NewGuid(),
            userName = "tester",
            nickName = "Tester",
            email
        });
        var dto = JsonSerializer.Deserialize<UpdateUserDto>(json, JsonOptions)
            ?? throw new InvalidOperationException("Failed to deserialize UpdateUserDto.");

        Assert.Null(dto.Email);
        Assert.Empty(Validate(dto));
    }

    private static CreateUserDto DeserializeCreate(string email)
    {
        var json = JsonSerializer.Serialize(new
        {
            userName = "tester",
            nickName = "Tester",
            email
        });
        return JsonSerializer.Deserialize<CreateUserDto>(json, JsonOptions)
            ?? throw new InvalidOperationException("Failed to deserialize CreateUserDto.");
    }

    private static IReadOnlyCollection<ValidationResult> Validate(object model)
    {
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(
            model,
            new ValidationContext(model),
            results,
            validateAllProperties: true);
        return results;
    }
}
