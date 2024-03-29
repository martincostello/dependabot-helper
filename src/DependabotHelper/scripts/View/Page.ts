// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

import { GitHubClient, StorageClient } from '../Client/index';
import { ErrorsElement } from './ErrorsElement';
import { RateLimitsElement } from './RateLimitsElement';

export abstract class Page {
    constructor(
        protected readonly gitHub: GitHubClient,
        protected readonly storage: StorageClient,
        private readonly rateLimits: RateLimitsElement,
        private readonly errors: ErrorsElement
    ) {}

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
