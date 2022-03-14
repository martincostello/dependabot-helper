﻿// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

namespace Shouldly;

public static class ShouldlyTaskExtensions
{
    public static async Task ShouldBe(this Task<string> task, string expected)
    {
        string actual = await task;
        actual.ShouldBe(expected);
    }

    public static async Task ShouldBe<T>(this Task<T> task, T expected)
    {
        T actual = await task;
        actual.ShouldBe(expected);
    }

    public static async Task ShouldBeFalse(this Task<bool> task)
    {
        bool actual = await task;
        actual.ShouldBeFalse();
    }

    public static async Task ShouldBeTrue(this Task<bool> task)
    {
        bool actual = await task;
        actual.ShouldBeTrue();
    }
}
