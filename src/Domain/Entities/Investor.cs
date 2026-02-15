using System;
using FinLearnApp.Domain.ValueObjects;

namespace FinLearnApp.Domain.Entities;

public sealed class Investor
{
    public InvestorId Id { get; }
    public string Name { get; }

    public Investor(InvestorId id, string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Investor name is required.", nameof(name));
        }

        Id = id;
        Name = name;
    }
}
