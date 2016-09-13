// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Data.Entity;
using System.IO;
using System.Security.Principal;
using System.Web;
using System.Web.Mvc;
using Autofac;
using Elmah;
using NuGetGallery.Areas.Admin;
using NuGetGallery.Auditing;
using NuGetGallery.Configuration;
using NuGetGallery.Diagnostics;
using NuGetGallery.Infrastructure.Lucene;
using NuGetGallery.Areas.Admin.Models;
using NuGetGallery.Configuration.SecretReader;

namespace NuGetGallery
{
    public class DefaultDependenciesModule : Module
    {
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Maintainability", "CA1502:CyclomaticComplexity", Justification = "This code is more maintainable in the same function.")]
        protected override void Load(ContainerBuilder builder)
        {
            var diagnosticsService = new DiagnosticsService();
            builder.RegisterInstance(diagnosticsService)
                .AsSelf()
                .As<IDiagnosticsService>()
                .SingleInstance();

            var configService = new ConfigurationService(new SecretReaderFactory(diagnosticsService));
            var currentConfig = configService.GetCurrent().Result;

            builder.RegisterInstance(configService)
                .AsSelf()
                .As<PoliteCaptcha.IConfigurationSource>()
                .As<IGalleryConfigurationService>();

            builder.RegisterInstance(LuceneCommon.GetDirectory(currentConfig.LuceneIndexLocation))
                .As<Lucene.Net.Store.Directory>()
                .SingleInstance();

            ConfigureSearch(builder, currentConfig, diagnosticsService);

            if (!string.IsNullOrEmpty(currentConfig.AzureStorageConnectionString))
            {
                builder.Register(c => Factories.TableErrorLog.Create(configService))
                    .As<ErrorLog>()
                    .InstancePerLifetimeScope();
            }
            else
            {
                builder.Register(c => Factories.SqlErrorLog.Create(configService))
                    .As<ErrorLog>()
                    .InstancePerLifetimeScope();
            }

            builder.RegisterType<HttpContextCacheService>()
                .AsSelf()
                .As<ICacheService>()
                .InstancePerLifetimeScope();

            builder.RegisterType<ContentService>()
                .AsSelf()
                .As<IContentService>()
                .SingleInstance();
            
            builder.Register(c => Factories.EntitiesContext.Create(configService))
                .AsSelf()
                .As<IEntitiesContext>()
                .As<DbContext>()
                .InstancePerLifetimeScope();

            builder.RegisterType<EntityRepository<User>>()
                .AsSelf()
                .As<IEntityRepository<User>>()
                .InstancePerLifetimeScope();

            builder.RegisterType<EntityRepository<CuratedFeed>>()
                .AsSelf()
                .As<IEntityRepository<CuratedFeed>>()
                .InstancePerLifetimeScope();

            builder.RegisterType<EntityRepository<CuratedPackage>>()
                .AsSelf()
                .As<IEntityRepository<CuratedPackage>>()
                .InstancePerLifetimeScope();

            builder.RegisterType<EntityRepository<PackageRegistration>>()
                .AsSelf()
                .As<IEntityRepository<PackageRegistration>>()
                .InstancePerLifetimeScope();

            builder.RegisterType<EntityRepository<Package>>()
                .AsSelf()
                .As<IEntityRepository<Package>>()
                .InstancePerLifetimeScope();

            builder.RegisterType<EntityRepository<PackageDependency>>()
                .AsSelf()
                .As<IEntityRepository<PackageDependency>>()
                .InstancePerLifetimeScope();

            builder.RegisterType<EntityRepository<PackageDelete>>()
                .AsSelf()
                .As<IEntityRepository<PackageDelete>>()
                .InstancePerLifetimeScope();

            builder.RegisterType<EntityRepository<Credential>>()
                .AsSelf()
                .As<IEntityRepository<Credential>>()
                .InstancePerLifetimeScope();

            builder.RegisterType<EntityRepository<PackageOwnerRequest>>()
                .AsSelf()
                .As<IEntityRepository<PackageOwnerRequest>>()
                .InstancePerLifetimeScope();

            builder.RegisterType<CuratedFeedService>()
                .AsSelf()
                .As<ICuratedFeedService>()
                .InstancePerLifetimeScope();
            
            builder.Register(c => Factories.SupportRequestDbContext.Create(configService))
                .AsSelf()
                .As<ISupportRequestDbContext>()
                .InstancePerLifetimeScope();

            builder.RegisterType<SupportRequestService>()
                .AsSelf()
                .As<ISupportRequestService>()
                .InstancePerLifetimeScope();

            builder.RegisterType<UserService>()
                .AsSelf()
                .As<IUserService>()
                .InstancePerLifetimeScope();

            builder.RegisterType<PackageNamingConflictValidator>()
                .AsSelf()
                .As<IPackageNamingConflictValidator>()
                .InstancePerLifetimeScope();

            builder.RegisterType<PackageService>()
                .AsSelf()
                .As<IPackageService>()
                .InstancePerLifetimeScope();

            builder.RegisterType<PackageDeleteService>()
                .AsSelf()
                .As<IPackageDeleteService>()
                .InstancePerLifetimeScope();

            builder.RegisterType<EditPackageService>()
                .AsSelf()
                .InstancePerLifetimeScope();

            builder.RegisterType<FormsAuthenticationService>()
                .As<IFormsAuthenticationService>()
                .InstancePerLifetimeScope();

            builder.RegisterType<CookieTempDataProvider>()
                .As<ITempDataProvider>()
                .InstancePerLifetimeScope();

            builder.RegisterType<NuGetExeDownloaderService>()
                .AsSelf()
                .As<INuGetExeDownloaderService>()
                .InstancePerLifetimeScope();

            builder.RegisterType<StatusService>()
                .AsSelf()
                .As<IStatusService>()
                .InstancePerLifetimeScope();

            builder.RegisterType<MessageService>()
                .AsSelf()
                .As<IMessageService>()
                .InstancePerLifetimeScope();

            builder.Register(c => HttpContext.Current.User)
                .AsSelf()
                .As<IPrincipal>()
                .InstancePerLifetimeScope();

            switch (currentConfig.StorageType)
            {
                case StorageType.FileSystem:
                case StorageType.NotSpecified:
                    ConfigureForLocalFileSystem(builder, currentConfig);
                    break;
                case StorageType.AzureStorage:
                    ConfigureForAzureStorage(builder, configService);
                    break;
            }

            builder.RegisterType<FileSystemService>()
                .AsSelf()
                .As<IFileSystemService>()
                .SingleInstance();

            builder.RegisterType<PackageFileService>()
                .AsSelf()
                .As<IPackageFileService>()
                .InstancePerLifetimeScope();

            builder.RegisterType<UploadFileService>()
                .AsSelf()
                .As<IUploadFileService>()
                .InstancePerLifetimeScope();

            // todo: bind all package curators by convention
            builder.RegisterType<WebMatrixPackageCurator>()
                .AsSelf()
                .As<IAutomaticPackageCurator>()
                .InstancePerLifetimeScope();

            builder.RegisterType<Windows8PackageCurator>()
                .AsSelf()
                .As<IAutomaticPackageCurator>()
                .InstancePerLifetimeScope();

            // todo: bind all commands by convention
            builder.RegisterType<AutomaticallyCuratePackageCommand>()
                .AsSelf()
                .As<IAutomaticallyCuratePackageCommand>()
                .InstancePerLifetimeScope();

            ConfigureAutocomplete(builder, currentConfig);
        }

        private static void ConfigureSearch(ContainerBuilder builder, IAppConfiguration currentConfig, IDiagnosticsService diagnosticsService)
        {
            if (currentConfig.ServiceDiscoveryUri == null)
            {
                builder.RegisterType<LuceneSearchService>()
                    .AsSelf()
                    .As<ISearchService>()
                    .InstancePerLifetimeScope();
                builder.RegisterType<LuceneIndexingService>()
                    .AsSelf()
                    .As<IIndexingService>()
                    .InstancePerLifetimeScope();
            }
            else
            {
                builder.Register(c => new ExternalSearchService(diagnosticsService, currentConfig.ServiceDiscoveryUri, currentConfig.SearchServiceResourceType))
                    .AsSelf()
                    .As<ISearchService>()
                    .As<IIndexingService>()
                    .InstancePerLifetimeScope();
            }
        }
        private static void ConfigureAutocomplete(ContainerBuilder builder, IAppConfiguration currentConfig)
        {
            if (currentConfig.ServiceDiscoveryUri != null &&
                !string.IsNullOrEmpty(currentConfig.AutocompleteServiceResourceType))
            {
                builder.Register(c => new AutocompleteServicePackageIdsQuery(currentConfig))
                    .AsSelf()
                    .As<IPackageIdsQuery>()
                    .SingleInstance();

                builder.Register(c => new AutocompleteServicePackageVersionsQuery(currentConfig))
                    .AsSelf()
                    .As<IPackageVersionsQuery>()
                    .InstancePerLifetimeScope();
            }
            else
            {
                builder.RegisterType<PackageIdsQuery>()
                    .AsSelf()
                    .As<IPackageIdsQuery>()
                    .InstancePerLifetimeScope();

                builder.RegisterType<PackageVersionsQuery>()
                    .AsSelf()
                    .As<IPackageVersionsQuery>()
                    .InstancePerLifetimeScope();
            }
        }

        private static void ConfigureForLocalFileSystem(ContainerBuilder builder, IAppConfiguration currentConfig)
        {
            builder.RegisterType<FileSystemFileStorageService>()
                .AsSelf()
                .As<IFileStorageService>()
                .SingleInstance();

            builder.RegisterInstance(NullReportService.Instance)
                .AsSelf()
                .As<IReportService>()
                .SingleInstance();

            builder.RegisterInstance(NullStatisticsService.Instance)
                .AsSelf()
                .As<IStatisticsService>()
                .SingleInstance();

            // Setup auditing
            var auditingPath = Path.Combine(
                FileSystemFileStorageService.ResolvePath(currentConfig.FileStorageDirectory),
                FileSystemAuditingService.DefaultContainerName);

            builder.RegisterInstance(new FileSystemAuditingService(auditingPath, FileSystemAuditingService.GetAspNetOnBehalfOf))
                .AsSelf()
                .As<AuditingService>()
                .SingleInstance();

            // If we're not using azure storage, then aggregate stats comes from SQL
            builder.RegisterType<SqlAggregateStatsService>()
                .AsSelf()
                .As<IAggregateStatsService>()
                .InstancePerLifetimeScope();
        }

        private static void ConfigureForAzureStorage(ContainerBuilder builder, IGalleryConfigurationService configService)
        {
            builder.RegisterType<CloudBlobClientWrapper>()
                .AsSelf()
                .As<ICloudBlobClient>()
                .SingleInstance();

            builder.RegisterType<CloudBlobFileStorageService>()
                .AsSelf()
                .As<IFileStorageService>()
                .SingleInstance();

            // when running on Windows Azure, we use a back-end job to calculate stats totals and store in the blobs
            builder.RegisterType<JsonAggregateStatsService>()
                .AsSelf()
                .As<IAggregateStatsService>()
                .SingleInstance();

            // when running on Windows Azure, pull the statistics from the warehouse via storage
            builder.RegisterType<CloudReportService>()
                .AsSelf()
                .As<IReportService>()
                .SingleInstance();

            // when running on Windows Azure, download counts come from the downloads.v1.json blob
            builder.RegisterType<CloudDownloadCountService>()
                .AsSelf()
                .As<IDownloadCountService>()
                .SingleInstance();

            builder.RegisterType<JsonStatisticsService>()
                .AsSelf()
                .As<IStatisticsService>()
                .SingleInstance();
            
            builder.Register(c => new CloudAuditingServiceWrapper(Factories.AuditingService.CreateAsync(configService)))
                .AsSelf()
                .As<AuditingService>()
                .SingleInstance();
        }
    }
}
