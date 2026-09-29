using FluentValidation.TestHelper;
using Hato.Modules.Delivery.Application.BuildRequests.Commands;
using Xunit;

namespace Hato.Modules.Delivery.UnitTests;

public class CreateBuildRequestValidatorTests
{
    private readonly CreateBuildRequestCommandValidator _validator = new();

    [Theory]
    [InlineData("stage")]
    [InlineData("STAGE")]
    [InlineData("prod")]
    [InlineData("PROD")]
    [InlineData("both")]
    [InlineData("BOTH")]
    public void ValidChannels_PassValidation(string channel)
    {
        var command = new CreateBuildRequestCommand(channel);
        var result = _validator.TestValidate(command);
        result.ShouldNotHaveValidationErrorFor(x => x.Channel);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("invalid")]
    [InlineData("development")]
    [InlineData("testing")]
    public void InvalidChannels_FailValidation(string invalidChannel)
    {
        var command = new CreateBuildRequestCommand(invalidChannel);
        var result = _validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.Channel);
    }

    [Fact]
    public void IdempotencyKey_TooLong_FailsValidation()
    {
        var command = new CreateBuildRequestCommand("stage", IdempotencyKey: new string('x', 101));
        var result = _validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.IdempotencyKey);
    }
}
