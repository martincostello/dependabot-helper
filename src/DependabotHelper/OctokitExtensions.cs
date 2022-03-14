// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

namespace Octokit;

public static class OctokitExtensions
{
    public static bool CanReview(this AuthorAssociation value)
        => value switch
           {
               AuthorAssociation.Collaborator => true,
               AuthorAssociation.Member => true,
               AuthorAssociation.Owner => true,
               _ => false,
           };

    public static bool IsPrivate(this Repository value)
        => value.Private || (value.Visibility.HasValue && value.Visibility.Value != RepositoryVisibility.Public);
}
