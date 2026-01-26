namespace Transaction.Domain.Common;

public static class Guard
{
    public static void AgainstNullOrWhiteSpace(string value, string fieldName)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new DomainException($"{fieldName} cannot be empty.");
    }

    public static void AgainstNegativeOrZero(decimal value, string fieldName)
    {
        if (value <= 0)
            throw new DomainException($"{fieldName} must be greater than zero.");
    }
}
