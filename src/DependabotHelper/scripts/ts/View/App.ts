// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

import { GitHubClient } from '../Client/GitHubClient';

export class App {

    private readonly client: GitHubClient;

    constructor() {
        this.client = new GitHubClient();
    }

    async initialize(): Promise<void> {
        if (await this.client.isAuthenticated()) {
            // TODO
        }
    }
}
