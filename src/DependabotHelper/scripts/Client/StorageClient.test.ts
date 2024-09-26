// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

import { describe, expect, test } from '@jest/globals';
import { StorageClient } from './StorageClient';

describe('StorageClient', () => {
    test('stores user profiles', async () => {
        global.localStorage = new LocalStorageMock();

        // Arrange
        const userId = 'my-user';
        const client = new StorageClient(userId);

        // Act
        let owners = client.getOwners();
        expect(owners).toBeDefined();
        expect(owners.size).toBe(0);

        // Act
        client.setOwners('my-company', ['second', 'first']);

        // Assert
        owners = client.getOwners();
        expect(owners).toBeDefined();
        expect(owners.size).toBe(1);
        expect(owners.get('my-company')).toEqual(['first', 'second']);

        // Act
        client.setOwners(userId, ['my-project']);

        // Assert
        owners = client.getOwners();
        expect(owners).toBeDefined();
        expect(owners.size).toBe(2);
        expect(owners.get('my-company')).toEqual(['first', 'second']);
        expect(owners.get(userId)).toEqual(['my-project']);
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
