// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using Azure.Security.KeyVault.Secrets;

namespace MartinCostello.DependabotHelper;

public static class AzureEnvironmentSecretManagerTests
{
    [Theory]
    [InlineData("DependabotHelper-Some-Secret", "Some:Secret")]
    [InlineData("DependabotHelper-Another-Secret-Thing", "Another:Secret:Thing")]
    public static void GetKey_Returns_Correct_Value(string name, string expected)
    {
        // Arrange
        var target = new AzureEnvironmentSecretManager();

        var secret = new KeyVaultSecret(name, string.Empty);

        // Act
        string actual = target.GetKey(secret);

        // Assert
        actual.ShouldBe(expected);
    }

    [Theory]
    [InlineData("DependabotHelper-Some-Secret", true)]
    [InlineData("DependabotHelper-Another-Secret-Thing", true)]
    [InlineData("Something-Some-Secret", false)]
    [InlineData("Something-Some-Secret-With-DependabotHelper-In-It", false)]
    public static void Load_Returns_Correct_Value(string name, bool expected)
    {
        // Arrange
        var target = new AzureEnvironmentSecretManager();

        var secret = new SecretProperties(name);

        // Act
        bool actual = target.Load(secret);

        // Assert
        actual.ShouldBe(expected);
    }
}
