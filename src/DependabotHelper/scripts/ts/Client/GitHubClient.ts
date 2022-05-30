// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

import { ApiError } from '../Models/ApiError';
import { RateLimits } from '../Models/RateLimits';
import { Repository } from '../Models/Repository';
import { RepositoryPullRequests } from '../Models/RepositoryPullRequests';

export class GitHubClient {

    readonly rateLimits: RateLimits;

    constructor() {
        this.rateLimits =
        {
            limit: null,
            remaining: null,
            resets: null,
        };
    }

    async approvePullRequest(owner: string, name: string, number: number): Promise<void> {

        const encodedOwner = encodeURIComponent(owner);
        const encodedName = encodeURIComponent(name);
        const encodedNumber = encodeURIComponent(number.toString(10));

        await this.postJson(
            `/github/repos/${encodedOwner}/${encodedName}/pulls/${encodedNumber}/approve`);
    }

    async getPullRequests(owner: string, name: string): Promise<RepositoryPullRequests> {

        const encodedOwner = encodeURIComponent(owner);
        const encodedName = encodeURIComponent(name);

        return await this.getJson<RepositoryPullRequests>(
            `/github/repos/${encodedOwner}/${encodedName}/pulls`);
    }

    async getRepositories(owner: string): Promise<Repository[]> {
        const encodedOwner = encodeURIComponent(owner);
        return await this.getJson<Repository[]>(`/github/repos/${encodedOwner}`);
    }

    async mergePullRequests(owner: string, name: string): Promise<void> {

        const encodedOwner = encodeURIComponent(owner);
        const encodedName = encodeURIComponent(name);

        await this.postJson(`/github/repos/${encodedOwner}/${encodedName}/pulls/merge`);
    }

    private async getJson<T>(url: string): Promise<T> {

        const response = await fetch(url);

        this.updateRateLimits(response.headers);

        let json: any;

        try {
            json = await response.json();
        } catch (error: any) {
            json = null;
        }

        if (!response.ok) {

            const apiError = json as ApiError;

            if (apiError) {
                throw new Error(`${json.title} (HTTP ${response.status})`);
            } else {
                throw new Error(response.status.toString(10));
            }
        }

        return json;
    }

    private async postJson(url: string): Promise<void> {

        const antiforgeryHeader = this.getMetaContent('x-antiforgery-header');
        const antiforgeryToken = this.getMetaContent('x-antiforgery-token');

        const payload = {};
        const headers = new Headers();

        headers.set('Accept', 'application/json');
        headers.set('Content-Type', 'application/json');
        headers.set(antiforgeryHeader, antiforgeryToken);

        const init = {
            method: 'POST',
            headers: headers,
            body: JSON.stringify(payload)
        };

        const response = await fetch(url, init);

        this.updateRateLimits(response.headers);

        if (!response.ok) {
            throw new Error(response.status.toString(10));
        }
    }

    private getMetaContent(name: string): string {
        const element = document.querySelector(`meta[name="${name}"]`);
        let content = '';

        if (element) {
            content = element.getAttribute('content');
        }

        return content;
    }

    private updateRateLimits(headers: Headers) {

        const limitKey = 'x-ratelimit-limit';
        const remainingKey = 'x-ratelimit-remaining';
        const resetKey = 'x-ratelimit-reset';

        if (headers.has(limitKey)) {
            this.rateLimits.limit = parseInt(headers.get(limitKey), 10);
            this.rateLimits.remaining = parseInt(headers.get(remainingKey), 10);
            this.rateLimits.resets = parseInt(headers.get(resetKey), 10);
        }
    }
}
