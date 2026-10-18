using System.ComponentModel.DataAnnotations;
using SupportDesk.Application.Configuration;

namespace SupportDesk.UnitTests.Application.Configuration;

/// <summary>
/// The Sla section is validated when the API starts, so a bad environment fails at once rather
/// than quietly producing wrong due dates.
/// </summary>
public class SlaOptionsValidationTests
{
    private static SlaOptions Valid() => new()
    {
        BaseWindowHours = new Dictionary<string, int>
        {
            ["Low"] = 72,
            ["Medium"] = 24,
            ["High"] = 8,
            ["Critical"] = 4
        },
        PremiumCustomerMultiplier = 0.5,
        MinimumWindowHours = 1,
        AtRiskThresholdPercent = 25
    };

    private static List<ValidationResult> Validate(SlaOptions options)
    {
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(options, new ValidationContext(options), results, validateAllProperties: true);
        return results;
    }

    [Fact]
    public void TheDefaultConfiguration_IsValid() => Assert.Empty(Validate(Valid()));

    [Theory]
    [InlineData(0.0)]
    [InlineData(-0.5)]
    [InlineData(1.5)]
    public void ThePremiumMultiplier_MustBeAboveZeroAndNoMoreThanOne(double multiplier)
    {
        var options = Valid();
        options.PremiumCustomerMultiplier = multiplier;

        Assert.NotEmpty(Validate(options));
    }

    [Theory]
    [InlineData(0.5)]
    [InlineData(1.0)]
    public void ThePremiumMultiplier_AcceptsValuesInRange(double multiplier)
    {
        var options = Valid();
        options.PremiumCustomerMultiplier = multiplier;

        Assert.Empty(Validate(options));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void TheMinimumWindow_MustBePositive(int hours)
    {
        var options = Valid();
        options.MinimumWindowHours = hours;

        Assert.NotEmpty(Validate(options));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(100)]
    public void TheAtRiskThreshold_MustBeBetween1And99Percent(int percent)
    {
        var options = Valid();
        options.AtRiskThresholdPercent = percent;

        Assert.NotEmpty(Validate(options));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(99)]
    public void TheAtRiskThreshold_AcceptsTheEdgesOfTheRange(int percent)
    {
        var options = Valid();
        options.AtRiskThresholdPercent = percent;

        Assert.Empty(Validate(options));
    }
}
