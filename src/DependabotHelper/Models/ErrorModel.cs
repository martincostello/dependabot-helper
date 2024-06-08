﻿// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

namespace MartinCostello.DependabotHelper.Models;

/// <summary>
/// A class representing the error model. This class cannot be inherited.
/// </summary>
/// <param name="statusCode">The HTTP status code.</param>
public sealed class ErrorModel(int statusCode)
{
    public int ErrorStatusCode { get; } = statusCode;

    public bool IsClientError { get; set; }

    public string Message { get; set; } = "Sorry, something went wrong.";

    public string? RequestId { get; set; }

    public bool ShowRequestId => !string.IsNullOrEmpty(RequestId);

    public string Title { get; set; } = "Error";

    public string Subtitle { get; set; } = "Error";
}
