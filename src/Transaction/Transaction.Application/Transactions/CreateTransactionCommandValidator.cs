using FluentValidation;

namespace Transaction.Application.Transactions;

/// <summary>
/// Input validation for CreateTransactionCommand.
/// Complements domain invariants by validating at API boundary.
/// Domain guards in Transaction.Create() provide additional protection.
/// </summary>
public sealed class CreateTransactionCommandValidator : AbstractValidator<CreateTransactionCommand>
{
    public CreateTransactionCommandValidator()
    {
        // Amount validation
        RuleFor(x => x.Amount)
            .GreaterThan(0)
            .WithMessage("Amount must be greater than zero")
            .WithErrorCode("AMOUNT_MUST_BE_POSITIVE");

        RuleFor(x => x.Amount)
            .LessThanOrEqualTo(999_999_999.99m)
            .WithMessage("Amount exceeds maximum allowed value")
            .WithErrorCode("AMOUNT_EXCEEDS_MAXIMUM");

        RuleFor(x => x.Amount)
            .PrecisionScale(11, 2, ignoreTrailingZeros: true)
            .WithMessage("Amount must have at most 2 decimal places")
            .WithErrorCode("AMOUNT_INVALID_PRECISION");

        // Currency validation
        RuleFor(x => x.Currency)
            .NotEmpty()
            .WithMessage("Currency is required")
            .WithErrorCode("CURRENCY_REQUIRED");

        RuleFor(x => x.Currency)
            .Length(3, 3)
            .WithMessage("Currency must be a valid 3-letter code")
            .WithErrorCode("CURRENCY_INVALID_FORMAT");

        RuleFor(x => x.Currency)
            .Matches(@"^[A-Z]{3}$")
            .WithMessage("Currency must be uppercase letters (e.g., USD, EUR)")
            .WithErrorCode("CURRENCY_INVALID_FORMAT");

        // MerchantId validation
        RuleFor(x => x.MerchantId)
            .NotEmpty()
            .WithMessage("Merchant ID is required")
            .WithErrorCode("MERCHANT_ID_REQUIRED");

        RuleFor(x => x.MerchantId)
            .MaximumLength(100)
            .WithMessage("Merchant ID must not exceed 100 characters")
            .WithErrorCode("MERCHANT_ID_TOO_LONG");

        RuleFor(x => x.MerchantId)
            .Matches(@"^[a-zA-Z0-9_\-\.]+$")
            .WithMessage("Merchant ID contains invalid characters")
            .WithErrorCode("MERCHANT_ID_INVALID_FORMAT");

        // CorrelationId validation
        RuleFor(x => x.CorrelationId)
            .NotEmpty()
            .WithMessage("Correlation ID is required")
            .WithErrorCode("CORRELATION_ID_REQUIRED");

        RuleFor(x => x.CorrelationId)
            .Length(32)
            .WithMessage("Correlation ID must be 32 characters")
            .WithErrorCode("CORRELATION_ID_INVALID_LENGTH");

        RuleFor(x => x.CorrelationId)
            .Matches(@"^[a-f0-9]{32}$")
            .WithMessage("Correlation ID must be a valid hex string")
            .WithErrorCode("CORRELATION_ID_INVALID_FORMAT");
    }
}
