// Copyright (c) Martin Costello, 2022. All rights reserved.

export interface Repository {
    id: number;
    name: string;
    htmlUrl: string;
    isFork: boolean;
    isPrivate: boolean;
}
