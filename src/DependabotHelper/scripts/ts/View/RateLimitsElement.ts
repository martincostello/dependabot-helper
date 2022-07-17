// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

import moment from 'moment';
import { RateLimits } from '../Models/RateLimits';
import { Elements } from './Elements';
import { Page } from './Page';

export class RateLimitsElement {
    private readonly titleAttribute = 'title';

    private remaining: HTMLElement;
    private resets: HTMLElement;
    private total: HTMLElement;

    constructor() {
        this.remaining = Page.findId('rate-limit-remaining');
        this.resets = Page.findId('rate-limit-resets');
        this.total = Page.findId('rate-limit-total');

        setInterval(() => {
            this.updateRelativeTimestamps();
        }, 60 * 1000);
    }

    update(limits: RateLimits): void {
        if (!limits.limit) {
            return;
        }

        this.remaining.innerText = limits.remaining.toLocaleString();
        this.total.innerText = limits.limit.toLocaleString();

        const resetsAt = moment(limits.resets * 1000);

        this.resets.setAttribute(this.titleAttribute, resetsAt.format());
        this.updateRelativeTime(this.resets, resetsAt);

        Elements.show(this.remaining.parentElement);
    }

    private updateRelativeTimestamps() {
        const elements = document.querySelectorAll('.relative-timestamp');
        for (const element of elements) {
            const timestamp = element.getAttribute(this.titleAttribute);
            if (timestamp) {
                this.updateRelativeTime(<HTMLElement>element, moment(timestamp));
            }
        }
    }

    private updateRelativeTime(element: HTMLElement, timestamp: moment.Moment) {
        element.innerText = timestamp.fromNow();
    }
}
