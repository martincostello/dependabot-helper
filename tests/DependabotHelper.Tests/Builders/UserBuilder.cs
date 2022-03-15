// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

namespace MartinCostello.DependabotHelper.Builders;

public sealed class UserBuilder : ResponseBuilder
{
    public UserBuilder()
    {
    }

    public UserBuilder(string login)
    {
        Login = login;
    }

    public string Login { get; set; } = RandomString();

    public string UserType { get; set; } = "user";

    public RepositoryBuilder CreateRepository(string? name = null, bool isFork = false, bool isPrivate = false)
    {
        return new(this, name)
        {
            IsFork = isFork,
            IsPrivate = isPrivate,
        };
    }

    public override object Build()
    {
        return new
        {
            avatar_url = $"https://avatars.githubusercontent.com/u/{Id}?v=4",
            id = Id,
            login = Login,
            type = UserType,
        };
    }
}
