using System;
using FinLearnApp.Domain.ValueObjects;

namespace FinLearnApp.Domain.Entities;

public sealed class Company
{
    public CompanyId Id { get; }
    public string Name { get; }

    public Company(CompanyId id, string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Company name is required.", nameof(name));
        }

        Id = id;
        Name = name;
    }
}
