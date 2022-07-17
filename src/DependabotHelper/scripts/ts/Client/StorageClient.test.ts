// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

import { describe, expect, jest, test } from '@jest/globals';
import { StorageClient } from './StorageClient';

describe('StorageClient', () => {
    test('should be defined', () => {
        expect(StorageClient).toBeDefined();
    });
});
