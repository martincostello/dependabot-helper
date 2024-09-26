// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

import { GitHubClient, StorageClient } from '../Client/index';
import { Configuration } from './Configuration';
import { ErrorsElement } from './ErrorsElement';
import { Manage } from './Manage';
import { Page } from './Page';
import { RateLimitsElement } from './RateLimitsElement';

export class App {
    async initialize(): Promise<void> {
        const userElement = document.querySelector('meta[name="x-user-id"]');
        const userId = userElement?.getAttribute('content') ?? '';

        if (!userId) {
            return;
        }

        const storage = new StorageClient(userId);
        const rateLimits = new RateLimitsElement();
        const errors = new ErrorsElement();

        let client: GitHubClient = null;

        if (Page.ownerList()) {
            client = this.createClient();
            const manage = new Manage(client, storage, rateLimits, errors);
            await manage.initialize();
        }

        if (Page.searchModal()) {
            if (!client) {
                client = this.createClient();
            }
            const config = new Configuration(client, storage, rateLimits, errors);
            config.initialize();
        }
    }

    private createClient(): GitHubClient {
        const antiforgeryHeader = this.getMetaContent('x-antiforgery-header');
        const antiforgeryToken = this.getMetaContent('x-antiforgery-token');

        return new GitHubClient(antiforgeryHeader, antiforgeryToken);
    }

    private getMetaContent(name: string): string {
        const element = document.querySelector(`meta[name="${name}"]`);
        return element?.getAttribute('content') ?? '';
    }
}
