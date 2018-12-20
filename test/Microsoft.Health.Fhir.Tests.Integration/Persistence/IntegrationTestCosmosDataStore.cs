﻿// -------------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// -------------------------------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.Documents;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Health.CosmosDb.Configs;
using Microsoft.Health.CosmosDb.Features.Storage;
using Microsoft.Health.Extensions.DependencyInjection;
using Microsoft.Health.Fhir.Core.Features.Context;
using Microsoft.Health.Fhir.Core.Features.Persistence;
using Microsoft.Health.Fhir.CosmosDb.Features.Storage;
using Microsoft.Health.Fhir.CosmosDb.Features.Storage.StoredProcedures;
using Microsoft.Health.Fhir.CosmosDb.Features.Storage.Versioning;
using NSubstitute;
using NonDisposingScope = Microsoft.Health.CosmosDb.Features.Storage.NonDisposingScope;

namespace Microsoft.Health.Fhir.Tests.Integration.Persistence
{
    public class IntegrationTestCosmosDataStore : IDataStore, IDisposable
    {
        private readonly IDocumentClient _documentClient;
        private readonly FhirDataStore _dataStore;
        private readonly CosmosDataStoreConfiguration _cosmosDataStoreConfiguration;

        public IntegrationTestCosmosDataStore()
        {
            _cosmosDataStoreConfiguration = new CosmosDataStoreConfiguration
            {
                Host = Environment.GetEnvironmentVariable("CosmosDb:Host") ?? CosmosDbLocalEmulator.Host,
                Key = Environment.GetEnvironmentVariable("CosmosDb:Key") ?? CosmosDbLocalEmulator.Key,
                DatabaseId = Environment.GetEnvironmentVariable("CosmosDb:DatabaseId") ?? "FhirTests",
                FhirCollectionId = Guid.NewGuid().ToString(),
                AllowDatabaseCreation = true,
                PreferredLocations = Environment.GetEnvironmentVariable("CosmosDb:PreferredLocations")?.Split(';', StringSplitOptions.RemoveEmptyEntries),
            };

            var fhirStoredProcs = typeof(IFhirStoredProcedure).Assembly
                .GetTypes()
                .Where(x => !x.IsAbstract && typeof(IFhirStoredProcedure).IsAssignableFrom(x))
                .ToArray()
                .Select(type => (IFhirStoredProcedure)Activator.CreateInstance(type));

            var updaters = new IFhirCollectionUpdater[]
            {
                new FhirCollectionSettingsUpdater(NullLogger<FhirCollectionSettingsUpdater>.Instance, _cosmosDataStoreConfiguration),
                new FhirStoredProcedureInstaller(fhirStoredProcs),
            };

            var dbLock = new CosmosDbDistributedLockFactory(Substitute.For<Func<IScoped<IDocumentClient>>>(), NullLogger<CosmosDbDistributedLock>.Instance);

            var upgradeManager = new FhirCollectionUpgradeManager(updaters, _cosmosDataStoreConfiguration, dbLock, NullLogger<FhirCollectionUpgradeManager>.Instance);
            IDocumentClientTestProvider testProvider = new DocumentClientReadWriteTestProvider();

            var documentClientInitializer = new DocumentClientInitializer(testProvider, NullLogger<DocumentClientInitializer>.Instance, upgradeManager);
            _documentClient = documentClientInitializer.CreateDocumentClient(_cosmosDataStoreConfiguration);
            var fhirCollectionInitializer = new FhirCollectionInitializer(_cosmosDataStoreConfiguration, upgradeManager, NullLogger<FhirCollectionInitializer>.Instance);
            documentClientInitializer.InitializeDataStore(_documentClient, _cosmosDataStoreConfiguration, new List<ICollectionInitializer> { fhirCollectionInitializer }).GetAwaiter().GetResult();

            var cosmosDocumentQueryFactory = new FhirCosmosDocumentQueryFactory(Substitute.For<IFhirRequestContextAccessor>(), NullFhirDocumentQueryLogger.Instance);
            var fhirRequestContextAccessor = new FhirRequestContextAccessor();

            _dataStore = new FhirDataStore(
                new NonDisposingScope(_documentClient),
                _cosmosDataStoreConfiguration,
                cosmosDocumentQueryFactory,
                new RetryExceptionPolicyFactory(_cosmosDataStoreConfiguration),
                fhirRequestContextAccessor,
                NullLogger<FhirDataStore>.Instance);
        }

        public void Dispose()
        {
            _documentClient?.DeleteDocumentCollectionAsync(_cosmosDataStoreConfiguration.RelativeFhirCollectionUri).GetAwaiter().GetResult();
            _documentClient?.Dispose();
        }

        public async Task<UpsertOutcome> UpsertAsync(
            ResourceWrapper resource,
            WeakETag weakETag,
            bool allowCreate,
            bool keepHistory,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            return await _dataStore.UpsertAsync(resource, weakETag, allowCreate, keepHistory, cancellationToken);
        }

        public async Task<ResourceWrapper> GetAsync(ResourceKey key, CancellationToken cancellationToken = default(CancellationToken))
        {
            return await _dataStore.GetAsync(key, cancellationToken);
        }

        public async Task HardDeleteAsync(ResourceKey key, CancellationToken cancellationToken = default(CancellationToken))
        {
            await _dataStore.HardDeleteAsync(key, cancellationToken);
        }
    }
}
