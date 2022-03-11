// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

import { Classes } from './Classes';

export class Selectors {
    static countApproved = '.repo-approved-count';
    static countError = '.repo-error-count';
    static countPending = '.repo-pending-count';
    static countSuccess = '.repo-success-count';
    static itemTemplate = '.' + Classes.itemTemplate;
    static loader = '.loader';
    static mergePullRequests = '.repo-merge';
    static ownerName = '.owner-name';
    static ownerTemplate = '.' + Classes.ownerTemplate;
    static refreshPullRequests = '.repo-refresh';
    static repositoryList = '.repo-list';
    static repositoryName = '.repo-name';
}
