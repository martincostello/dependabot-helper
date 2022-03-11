// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

import { RateLimits } from '../Models/RateLimits';
import { Repository } from '../Models/Repository';

export class GitHubClient {

    async isAuthenticated(): Promise<boolean> {

        const response = await fetch('/github/is-authenticated');

        if (!response.ok) {
            throw new Error(response.status.toString(10));
        }

        const body = await response.json();
        return body.isAuthenticated;
    }

    async getPullRequests(owner: string, name: string): Promise<Repository> {

        const response = await fetch(`/github/repos/${encodeURIComponent(owner)}/${encodeURIComponent(name)}/pulls`);

        if (!response.ok) {
            throw new Error(response.status.toString(10));
        }

        return await response.json();
    }

    async getRateLimits(): Promise<RateLimits> {

        const response = await fetch('/github/rate-limits');

        if (!response.ok) {
            throw new Error(response.status.toString(10));
        }

        return await response.json();
    }

    async getRepos(): Promise<Map<string, string[]>> {

        const response = await fetch('/github/repos');

        if (!response.ok) {
            throw new Error(response.status.toString(10));
        }

        const json = await response.text();
        const mapped = JSON.parse(json);

        const map = new Map();
        for (let k of Object.keys(mapped)) {
            map.set(k, mapped[k]);
        }

        return map;
    }

    async mergePullRequests(owner: string, name: string): Promise<void> {

        const antiforgeryHeader = document.querySelector('meta[name="x-antiforgery-header"]').getAttribute('content');
        const antiforgeryToken = document.querySelector('meta[name="x-antiforgery-token"]').getAttribute('content');

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
}
