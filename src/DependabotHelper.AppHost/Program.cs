// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

var builder = DistributedApplication.CreateBuilder(args);

const string BlobStorage = "AzureBlobStorage";
const string KeyVault = "AzureKeyVault";
const string Storage = "AzureStorage";

var storage = builder.AddAzureStorage(Storage)
                     .RunAsEmulator((container) =>
                     {
                         container.WithDataVolume()
                                  .WithLifetime(ContainerLifetime.Persistent);
                     });

var blobStorage = storage.AddBlobs(BlobStorage);

var secrets = builder.ExecutionContext.IsPublishMode
    ? builder.AddAzureKeyVault(KeyVault)
    : builder.AddConnectionString(KeyVault);

builder.AddProject<Projects.DependabotHelper>("DependabotHelper")
       .WithHttpHealthCheck("/version")
       .WithReference(blobStorage)
       .WithReference(secrets)
       .WaitFor(blobStorage);

var app = builder.Build();

app.Run();
