// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

import { PullRequest } from './PullRequest';

export interface Repository {
    id: number;
    name: string;
    htmlUrl: string;
    all: PullRequest[];
    error: PullRequest[];
    pending: PullRequest[];
    success: PullRequest[];
    approved: PullRequest[];
}
