// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

import { GitHubClient, StorageClient } from '../Client/index';
import { Configuration } from './Configuration';
import { ErrorsElement } from './ErrorsElement';
import { Manage } from './Manage';
import { Page } from './Page';
import { RateLimitsElement } from './RateLimitsElement';

export class App {
    private readonly gitHub: GitHubClient;

    constructor() {
        this.gitHub = new GitHubClient();
    }

    async initialize(): Promise<void> {
        const userElement = document.querySelector('meta[name="x-user-id"]');
        const userId = userElement?.getAttribute('content') ?? '';

        if (!userId) {
            return;
        }

        const storage = new StorageClient();
        const rateLimits = new RateLimitsElement();
        const errors = new ErrorsElement();

        if (Page.ownerList()) {
            const manage = new Manage(this.gitHub, storage, rateLimits, errors);
            await manage.initialize();
        }

        if (Page.searchModal()) {
            const config = new Configuration(this.gitHub, storage, rateLimits, errors);
            config.initialize();
        }
    }
}
