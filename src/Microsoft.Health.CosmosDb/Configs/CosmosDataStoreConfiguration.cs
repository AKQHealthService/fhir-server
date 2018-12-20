﻿// -------------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// -------------------------------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using Microsoft.Azure.Documents;
using Microsoft.Azure.Documents.Client;

namespace Microsoft.Health.CosmosDb.Configs
{
    public class CosmosDataStoreConfiguration
    {
        public string Host { get; set; }

        public string Key { get; set; }

        public string DatabaseId { get; set; }

        public int? InitialDatabaseThroughput { get; set; }

        public string FhirCollectionId { get; set; }

        public int? InitialFhirCollectionThroughput { get; set; }

        public string ControlPlaneCollectionId { get; set; }

        public int? InitialControlPlaneCollectionThroughput { get; set; }

        public ConnectionMode ConnectionMode { get; set; } = ConnectionMode.Direct;

        public Protocol ConnectionProtocol { get; set; } = Protocol.Tcp;

        public ConsistencyLevel? DefaultConsistencyLevel { get; set; }

        public bool AllowDatabaseCreation { get; set; } = true;

        public Uri RelativeDatabaseUri => string.IsNullOrEmpty(DatabaseId) ? null : UriFactory.CreateDatabaseUri(DatabaseId);

        public Uri RelativeFhirCollectionUri => string.IsNullOrEmpty(DatabaseId) || string.IsNullOrEmpty(FhirCollectionId) ? null : UriFactory.CreateDocumentCollectionUri(DatabaseId, FhirCollectionId);

        public Uri AbsoluteFhirCollectionUri => string.IsNullOrEmpty(Host) || RelativeFhirCollectionUri == null ? null : new Uri(new Uri(Host), RelativeFhirCollectionUri);

        public Uri RelativeControlPlaneCollectionUri => string.IsNullOrEmpty(DatabaseId) || string.IsNullOrEmpty(ControlPlaneCollectionId) ? null : UriFactory.CreateDocumentCollectionUri(DatabaseId, ControlPlaneCollectionId);

        public Uri AbsoluteControlPlaneCollectionUri => string.IsNullOrEmpty(Host) || RelativeControlPlaneCollectionUri == null ? null : new Uri(new Uri(Host), RelativeControlPlaneCollectionUri);

        public IList<string> PreferredLocations { get; set; }

        public int DataMigrationBatchSize { get; set; } = 100;

        public CosmosDataStoreRetryOptions RetryOptions { get; } = new CosmosDataStoreRetryOptions();

        public int? ContinuationTokenSizeLimitInKb { get; set; }
    }
}
