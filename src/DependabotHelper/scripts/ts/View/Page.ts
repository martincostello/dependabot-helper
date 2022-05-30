// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

import { GitHubClient } from '../Client/GitHubClient';
import { StorageClient } from '../Client/StorageClient';
import { ErrorsElement } from './ErrorsElement';
import { RateLimitsElement } from './RateLimitsElement';

export abstract class Page {

    protected readonly gitHub: GitHubClient;
    protected readonly storage: StorageClient;
    private readonly errors: ErrorsElement;
    private readonly rateLimits: RateLimitsElement;

    constructor(gitHub: GitHubClient, storage: StorageClient, rateLimits: RateLimitsElement, errors: ErrorsElement) {
        this.gitHub = gitHub;
        this.storage = storage;
        this.rateLimits = rateLimits;
        this.errors = errors;
    }

    static findId(id: string): HTMLElement {
        return document.getElementById(id);
    }

    static ownerList(): HTMLElement {
        return Page.findId('owner-list');
    }

    static searchModal(): HTMLElement {
        return Page.findId('repo-search-modal');
    }

    protected showError(error: any) {
        this.errors.show(error);
    }

    protected updateRateLimits() {
        const limits = this.gitHub.rateLimits;
        this.rateLimits.update(limits);
    }
}
