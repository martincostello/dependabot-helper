// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

import { ChecksStatus } from './ChecksStatus';

export interface PullRequest {
    canApprove: boolean;
    htmlUrl: string;
    hasConflicts: boolean;
    isApproved: boolean;
    number: number;
    repositoryOwner: string;
    repositoryName: string;
    status: ChecksStatus;
    title: string;
}
