// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

import { RateLimits } from '../Models/RateLimits';
import { Repository } from '../Models/Repository';
import { RepositoryPullRequests } from '../Models/RepositoryPullRequests';

export class GitHubClient {

    async isAuthenticated(): Promise<boolean> {
        const response = await this.getJson<any>('/github/is-authenticated');
        return response.isAuthenticated;
    }

    async getPullRequests(owner: string, name: string): Promise<RepositoryPullRequests> {
        return await this.getJson<RepositoryPullRequests>(
            `/github/repos/${encodeURIComponent(owner)}/${encodeURIComponent(name)}/pulls`);
    }

    async getRateLimits(): Promise<RateLimits> {
        return await this.getJson<RateLimits>('/github/rate-limits');
    }

    async getRepositories(owner: string): Promise<Repository[]> {
        return await this.getJson<Repository[]>(`/github/repos/${encodeURIComponent(owner)}`);
    }

    async mergePullRequests(owner: string, name: string): Promise<void> {

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

        const response = await fetch(`/github/repos/${encodeURIComponent(owner)}/${encodeURIComponent(name)}/pulls/merge`, init);

        if (!response.ok) {
            throw new Error(response.status.toString(10));
        }
    }

    private async getJson<T>(url: string): Promise<T> {

        const response = await fetch(url);

        if (!response.ok) {
            throw new Error(response.status.toString(10));
        }

        return await response.json();
    }

    private getMetaContent(name: string): string {
        const element = document.querySelector(`meta[name="${name}"]`);
        return element?.getAttribute('content') ?? '';
    }
}
