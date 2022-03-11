// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

import { RateLimits } from '../Models/RateLimits';

export class RateLimitsElement {

    private remaining: HTMLElement;
    private resets: HTMLElement;
    private total: HTMLElement;

    constructor() {
        this.remaining = document.getElementById('rate-limit-remaining');
        this.resets = document.getElementById('rate-limit-resets');
        this.total = document.getElementById('rate-limit-total');
    }

    update(limits: RateLimits): void {
        if (limits.limit) {
            this.remaining.innerText = limits.remaining.toLocaleString();
            this.resets.innerText = limits.resetsText;
            this.total.innerText = limits.limit.toLocaleString();
        }
    }
}
