// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

import { afterEach, beforeEach, describe, expect, jest, test } from '@jest/globals';
import { GitHubClient } from './GitHubClient';
import { RepositoryPullRequests, Repository, MergeMethod } from '../Models/index';

describe('GitHubClient', () => {
    let client: GitHubClient;
    let fetch: jest.Mock;
    let responseBody: any = {};
    let responseHeaders: Headers = new Headers({
        ['content-type']: 'application/json',
        ['x-ratelimit-limit']: '5000',
        ['x-ratelimit-remaining']: '4999',
        ['x-ratelimit-reset']: '1372700873',
    });
    let responseStatus: number = 200;

    beforeEach(() => {
        client = new GitHubClient('x-anti-forgery-header', 'x-anti-forgery-token');
        fetch = jest.fn(() =>
            Promise.resolve({
                headers: responseHeaders,
                json: () => Promise.resolve(responseBody),
                ok: responseStatus < 400,
                status: responseStatus,
            })
        ) as jest.Mock;
        (global as any).fetch = fetch;
    });

    afterEach(() => {
        jest.resetAllMocks();
    });

    test('should approve a pull request', async () => {
        await client.approvePullRequest('owner', 'repo', 42);
        expect(fetch).toHaveBeenCalledWith('/github/repos/owner/repo/pulls/42/approve', expect.any(Object));
    });

    test('should get pull requests', async () => {
        const pulls: RepositoryPullRequests = {
            dependabotHtmlUrl: 'https://github.com/App-vNext/Polly/network/updates',
            mergeMethods: [MergeMethod.merge, MergeMethod.rebase, MergeMethod.squash],
            all: [],
            error: [],
            pending: [],
            success: [],
            approved: [],
            id: 9864166,
            name: 'Polly',
            htmlUrl: 'https://github.com/App-vNext/Polly/pulls',
            isFork: false,
            isPrivate: false,
        };
        responseBody = pulls;

        const result = await client.getPullRequests('owner', 'repo');

        expect(fetch).toHaveBeenCalledWith('/github/repos/owner/repo/pulls');
        expect(result).toBeDefined();
        expect(result).toBeDefined();
        expect(result.dependabotHtmlUrl).toBe('https://github.com/App-vNext/Polly/network/updates');
    });

    test('should get repositories', async () => {
        const repos: Repository[] = [
            {
                id: 9864166,
                name: 'Polly',
                htmlUrl: 'https://github.com/App-vNext/Polly',
                isFork: false,
                isPrivate: false,
            },
            {
                id: 47483221,
                name: 'Polly-Samples',
                htmlUrl: 'https://github.com/App-vNext/Polly-Samples',
                isFork: false,
                isPrivate: false,
            },
        ];
        responseBody = repos;

        const result = await client.getRepositories('owner');
        expect(fetch).toHaveBeenCalledWith('/github/repos/owner');
        expect(result).toBeDefined();
        expect(result.length).toBe(2);
        expect(result[0]).toBeDefined();
        expect(result[0].name).toBe('Polly');
        expect(result[1]).toBeDefined();
        expect(result[1].name).toBe('Polly-Samples');
    });

    test('should merge pull requests', async () => {
        await client.mergePullRequests('owner', 'repo', MergeMethod.merge);
        expect(fetch).toHaveBeenCalledWith('/github/repos/owner/repo/pulls/merge?mergeMethod=Merge', expect.any(Object));
    });

    test('should handle rate limits', async () => {
        await client.approvePullRequest('owner', 'repo', 1);
        expect(client.rateLimits).toBeDefined();
        expect(client.rateLimits.limit).toBe(5000);
        expect(client.rateLimits.remaining).toBe(4999);
        expect(client.rateLimits.resets).toBe(1372700873);
    });

    test('should throw an error for non-200 responses', async () => {
        responseStatus = 404;
        await expect(client.approvePullRequest('owner', 'repo', 1)).rejects.toThrow('404');
    });
});
