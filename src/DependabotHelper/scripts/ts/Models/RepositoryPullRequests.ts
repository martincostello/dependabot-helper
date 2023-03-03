// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

import { MergeMethod } from './MergeMethod';
import { PullRequest } from './PullRequest';
import { Repository } from './Repository';

export interface RepositoryPullRequests extends Repository {
    dependabotHtmlUrl: string;
    mergeMethods: MergeMethod[];
    all: PullRequest[];
    error: PullRequest[];
    pending: PullRequest[];
    success: PullRequest[];
    approved: PullRequest[];
}
