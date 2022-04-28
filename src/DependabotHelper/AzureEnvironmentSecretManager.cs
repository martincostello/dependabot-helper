﻿// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using Azure.Extensions.AspNetCore.Configuration.Secrets;
using Azure.Security.KeyVault.Secrets;

namespace MartinCostello.DependabotHelper;

public sealed class AzureEnvironmentSecretManager : KeyVaultSecretManager
{
    private const string Prefix = "DependabotHelper-";

    public override string GetKey(KeyVaultSecret secret)
    {
        return secret.Name[Prefix.Length..]
            .Replace("--", "_", StringComparison.Ordinal)
            .Replace("-", ":", StringComparison.Ordinal);
    }

    public override bool Load(SecretProperties secret)
    {
        return secret.Name.StartsWith(Prefix, StringComparison.Ordinal);
    }
}
