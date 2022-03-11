// Copyright (c) Martin Costello, 2022. All rights reserved.

export interface UserProfile {
    userId: string;
    owners: { owner: string; names: string[] }[];
}
