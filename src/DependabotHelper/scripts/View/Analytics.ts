// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

export class Analytics {
    private readonly analyticsAttribute = 'analytics-event';
    private readonly analyticsSelector = `[${this.analyticsAttribute}]`;

    private emitter: (first: string, second: string, ...rest: any) => void;
    private observer: MutationObserver;

    initialize() {
        const hasAnalytics = 'gtag' in window;

        if (!hasAnalytics) {
            return;
        }

        this.emitter = (window as any).gtag;
        this.observer = new MutationObserver((mutations, _) => this.onElementAdded(mutations));
        this.observer.observe(document, {
            attributes: false,
            childList: true,
            characterData: false,
            subtree: true,
        });

        const elements = document.querySelectorAll(this.analyticsSelector);
        elements.forEach((element) => this.registerHandler(element));
    }

    private onElementAdded(mutations: MutationRecord[]) {
        for (const mutation of mutations) {
            for (const node of mutation.addedNodes) {
                const element = node as Element;

                if (!element?.querySelectorAll) {
                    continue;
                }

                const elements = element.querySelectorAll(this.analyticsSelector);
                const sentinelAttribute = 'analytics-callback';

                elements.forEach((child) => {
                    if (!child.hasAttribute(sentinelAttribute)) {
                        this.registerHandler(child);
                        child.setAttribute(sentinelAttribute, '');
                    }
                });
            }
        }
    }

    private registerHandler(element: Element) {
        const eventName = element.getAttribute(this.analyticsAttribute);

        if (!eventName) {
            return;
        }

        element.addEventListener('click', () => {
            this.track(eventName, element);
        });
    }

    private track(eventName: string, element: Element) {
        const properties = new Map<string, string>();

        for (const attribute of element.getAttributeNames()) {
            const prefix = 'analytics-property-';
            if (attribute.startsWith(prefix)) {
                const propertyName = attribute.substring(prefix.length);
                properties.set(propertyName, element.getAttribute(attribute));
            }
        }

        const eventData: any = {};

        for (const [key, value] of properties) {
            eventData[key] = value;
        }

        this.emitter('event', eventName, eventData);
    }
}
