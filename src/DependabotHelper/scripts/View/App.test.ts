// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

import { afterEach, beforeEach, describe, expect, jest, test } from '@jest/globals';
import { App } from './App';
import { StorageClient } from '../Client';

describe('App', () => {
    let app: App;

    beforeEach(() => {
        const storage = new LocalStorageMock();
        global.localStorage = storage;

        const storageClient = new StorageClient('123456');
        storageClient.setOwners('octocat', ['super-project']);

        /* eslint-disable max-len */
        document.body.innerHTML = `
<!DOCTYPE html>
<html lang="en">
<head>
    <meta name="x-antiforgery-header" content="x-antiforgery" />
    <meta name="x-antiforgery-token" content="anti-forgery-token" />
    <meta name="x-user-id" content="123456" />
</head>
<body>
    <main class="container body-content">
        <div class="alert alert-danger alert-dismissible fade show d-none" role="alert" id="error-alert">
            <h4 class="alert-heading">
                <span class="fa-solid fa-triangle-exclamation" aria-hidden="true"></span>
                Error
            </h4>
            <p class="error-message"></p>
            <p>
                <pre class="error-stack-trace"></pre>
            </p>
            <button type="button" class="btn-close close error-dismiss" aria-label="Close"></button>
        </div>
        <div id="not-configured" class="alert alert-warning d-none" role="alert">
            <p>
                <strong>You have not configured any repositories</strong>
            </p>
            <p>
                Configure which repositories you wish to manage Dependabot updates for <a href="/configure" title="Configure repositories to mange.">using this page</a>.
            </p>
        </div>
        <div id="owner-list" data-refresh-period="600000">
            <div class="d-none owner-item owner-template">
                <h2 class="owner-name pl-1"></h2>
                <div class="table-responsive">
                    <table class="table">
                        <caption class="owner-caption"></caption>
                        <thead>
                            <tr class="d-flex">
                                <th scope="col" class="col" aria-label="The name of the repository">Repository</th>
                                <th scope="col" class="col col-3" aria-label="The pull request status checks and approvals">Statuses</th>
                                <th scope="col" class="col col-3 col-md-2">Actions</th>
                            </tr>
                        </thead>
                        <tbody class="repo-list">
                            <tr class="d-none item-template repo-item">
                                <td class="col">
                                    <span class="loader spinner-border spinner-border-sm" role="status" aria-hidden="true"></span>
                                    <a class="align-middle repo-name" target="_blank"></a>
                                </td>
                                <td class="col col-2 col-md-3">
                                    <div class="container">
                                        <div class="row row-cols-1 row-cols-md-4">
                                            <div class="col status-container" title="The number of pending checks">
                                                <span class="repo-count-pending" aria-label="The number of pending checks">&hellip;</span>
                                                <span class="fa-solid fa-spinner repo-status text-warning" data-count="0" aria-hidden="true"></span>
                                            </div>
                                            <div class="col status-container" title="The number of failed checks">
                                                <span class="repo-count-error loading-count" aria-label="The number of failed checks">&hellip;</span>
                                                <span class="fa-solid fa-xmark repo-status text-danger" data-count="0" aria-hidden="true"></span>
                                            </div>
                                            <div class="col status-container" title="The number of successful checks">
                                                <span class="repo-count-success loading-count" aria-label="The number of successful checks">&hellip;</span>
                                                <span class="fa-solid fa-check repo-status text-success" data-count="0" aria-hidden="true"></span>
                                            </div>
                                            <div class="col status-container" title="The number of approvals">
                                                <span class="repo-count-approved loading-count" aria-label="The number of approvals">&hellip;</span>
                                                <span class="fa-solid fa-thumbs-up repo-status text-primary" data-count="0" aria-hidden="true"></span>
                                            </div>
                                        </div>
                                    </div>
                                </td>
                                <td class="col col-3 col-md-auto container">
                                    <div class="row-cols-1 row-cols-md-5">
                                        <div class="btn-group">
                                            <button class="btn btn-manage-action btn-secondary repo-refresh my-1" title="Refresh" type="button" disabled>
                                                <span class="fa fa-refresh" aria-label="Refresh the check and approval counts" role="img"></span>
                                                <span class="loader spinner-border spinner-border-sm d-none" role="status">
                                                    <span class="sr-only">Refreshing...</span>
                                                </span>
                                            </button>
                                        </div>
                                        <div class="btn-group">
                                            <a class="btn btn-manage-action btn-primary repo-configure disabled my-1" href="#" role="button" target="_blank" title="View the Dependabot configuration">
                                                <span class="fa-solid fa-robot" aria-hidden="true"></span>
                                            </a>
                                        </div>
                                        <div class="btn-group">
                                            <button class="btn btn-manage-action btn-pr repo-pull-requests my-1" title="View pull requests" type="button" data-bs-toggle="modal" data-bs-target="#pr-modal" disabled>
                                                <span class="fa-solid fa-code-pull-request" aria-hidden="true"></span>
                                            </button>
                                        </div>
                                        <div class="btn-group btn-manage-action">
                                            <button class="btn btn-success btn-merge repo-merge ml-1 my-1" title="Attempt to merge all mergeable pull requests" type="button" disabled>
                                                <span class="fa-solid fa-code-merge" aria-hidden="true"></span>
                                                <span class="loader spinner-border spinner-border-sm d-none" role="status">
                                                    <span class="sr-only">Merging...</span>
                                                </span>
                                            </button>
                                            <button type="button" class="btn btn-merge dropdown-toggle dropdown-toggle-split my-1 mr-1 repo-merge-methods-button" data-bs-toggle="dropdown" aria-expanded="false" disabled>
                                                <span class="visually-hidden">Toggle Dropdown</span>
                                            </button>
                                            <ul class="dropdown-menu repo-merge-methods">
                                                <li><button class="dropdown-item merge-method merge-method-merge d-none" value="Merge">Create a merge commit</button></li>
                                                <li><button class="dropdown-item merge-method merge-method-squash d-none" value="Squash">Squash and merge</button></li>
                                                <li><button class="dropdown-item merge-method merge-method-rebase d-none" value="Rebase">Rebase and merge</button></li>
                                            </ul>
                                        </div>
                                    </div>
                                </td>
                            </tr>
                        </tbody>
                    </table>
                </div>
            </div>
        </div>
        <div class="modal fade"
            id="pr-modal"
            role="dialog"
            tabindex="-1"
            aria-labelledby="pr-label"
            aria-hidden="true"
            data-bs-backdrop="static">
            <div class="modal-dialog modal-lg">
                <div class="modal-content">
                    <div class="modal-header">
                        <h5 class="modal-title" id="pr-label">Dependabot pull requests for <span class="pr-repo"></span></h5>
                        <button type="button" class="btn-close close" data-bs-dismiss="modal" aria-label="Close"></button>
                    </div>
                    <div class="modal-body">
                        <div class="table-responsive">
                            <table class="table">
                                <caption>Open Dependabot pull requests for <span class="pr-repo"></span>.</caption>
                                <thead>
                                    <tr>
                                        <th scope="col">Title</th>
                                        <th class="col-lg-2" scope="col">Status</th>
                                        <th class="col-lg-3" scope="col">Approved?</th>
                                    </tr>
                                </thead>
                                <tbody>
                                    <tr class="pr-template d-none">
                                        <td>
                                            <span class="fa-solid fa-code-merge text-github-open" aria-hidden="true"></span>
                                            <a href="" target="_blank" class="pr-title" title="View this pull request in GitHub">Title</a>
                                        </td>
                                        <td class="text-center">
                                            <span class="fa-solid fa-xmark text-danger pr-status-error d-none" data-count="0" aria-label="Error" title="Error"></span>
                                            <span class="fa-solid fa-spinner text-warning pr-status-pending d-none" data-count="0" aria-label="Pending" title="Pending"></span>
                                            <span class="fa-solid fa-check text-success pr-status-success d-none" data-count="0" aria-label="Success" title="Success"></span>
                                            <span class="fa-stack pr-status-conflict d-none" data-count="0" aria-label="Merge conflicts" title="Merge conflicts">
                                                <span class="fa-solid fa-code-merge fa-stack-1x text-muted"></span>
                                                <span class="fa-solid fa-ban fa-stack-2x text-danger"></span>
                                            </span>
                                        </td>
                                        <td class="text-center mt-3 mt-lg-1">
                                            <span>
                                                <span class="fa-solid fa-xmark text-danger pr-status-error d-none" data-count="0" aria-label="Error" title="Error"></span>
                                                <span class="fa-solid fa-spinner text-warning pr-status-pending d-none" data-count="0" aria-label="Pending" title="Pending"></span>
                                                <span class="fa-solid fa-check text-success pr-status-success d-none" data-count="0" aria-label="Success" title="Success"></span>
                                                <span class="fa-stack pr-status-conflict d-none" data-count="0" aria-label="Merge conflicts" title="Merge conflicts">
                                                    <span class="fa-solid fa-code-merge fa-stack-1x text-muted"></span>
                                                    <span class="fa-solid fa-ban fa-stack-2x text-danger"></span>
                                                </span>
                                            </span>
                                            <span>
                                                <span class="fa-solid fa-thumbs-up text-primary pr-is-approved d-none" aria-label="Approved" title="Approved"></span>
                                                <span class="fa-solid fa-thumbs-up text-secondary pr-approval-pending d-none" aria-label="Pending further approvals from others" title="Pending further approvals from others"></span>
                                                <button class="btn btn-success btn-approve pr-approve d-none" type="button" aria-label="Approve this pull request">
                                                    <span>Approve</span>
                                                    <span class="fa-solid fa-check d-none d-md-inline-block" aria-hidden="true"></span>
                                                    <span class="loader spinner-border spinner-border-sm d-none" role="status">
                                                        <span class="sr-only">Approving...</span>
                                                    </span>
                                                </button>
                                            </span>
                                        </td>
                                    </tr>
                                </tbody>
                            </table>
                        </div>
                    </div>
                </div>
            </div>
        </div>
        <footer>
            <p>
                <span>
                    <small>
                        GitHub API Limits:
                        <span id="rate-limit-remaining">4796</span>/<span id="rate-limit-total">5000</span>.
                        Resets
                        <span id="rate-limit-resets" class="relative-timestamp" title="2024-09-26 13:20:36Z">34 minutes from now</span>.
                    </small>
                </span>
                <span class="d-none d-lg-inline-block float-right">
                    <small>
                        Built <span id="build-date" class="relative-timestamp" title="2024-09-26 09:24:13Z">3 hours ago</span>.
                    </small>
                </span>
            </p>
        </footer>
    </main>
</body>
</html>
`;
        /* eslint-enable max-len */

        app = new App();
    });

    afterEach(() => {
        jest.restoreAllMocks();
    });

    test('initialize configures the app', async () => {
        await expect(app.initialize()).resolves.not.toThrow();
    });
});

class LocalStorageMock {
    readonly length: number;
    private store: Record<string, string>;

    constructor() {
        this.store = {};
    }

    clear() {
        this.store = {};
    }

    getItem(key: string) {
        return this.store[key] || null;
    }

    key(index: number): string | null {
        return Object.keys(this.store)[index] || null;
    }

    setItem(key: string, value: string) {
        this.store[key] = value;
    }

    removeItem(key: string) {
        delete this.store[key];
    }
}
