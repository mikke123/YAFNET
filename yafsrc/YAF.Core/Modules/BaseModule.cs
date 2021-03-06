/* Yet Another Forum.NET
 * Copyright (C) 2006-2013 Jaben Cargman
 * http://www.yetanotherforum.net/
 * 
 * This program is free software; you can redistribute it and/or
 * modify it under the terms of the GNU General Public License
 * as published by the Free Software Foundation; either version 2
 * of the License, or (at your option) any later version.
 * 
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 * GNU General Public License for more details.
 * 
 * You should have received a copy of the GNU General Public License
 * along with this program; if not, write to the Free Software
 * Foundation, Inc., 59 Temple Place - Suite 330, Boston, MA  02111-1307, USA.
 */
namespace YAF.Core
{
    #region Using

    using System;
    using System.Collections.Generic;
    using System.Data.Common;
    using System.Linq;
    using System.Reflection;

    using Autofac;
    using Autofac.Core;

    using YAF.Classes;
    using YAF.Core.BBCode;
    using YAF.Core.Data;
    using YAF.Core.Data.Filters;
    using YAF.Core.Extensions;
    using YAF.Core.Nntp;
    using YAF.Core.Services;
    using YAF.Core.Services.Cache;
    using YAF.Core.Services.Startup;
    using YAF.Types;
    using YAF.Types.Attributes;
    using YAF.Types.Extensions;
    using YAF.Types.Interfaces;
    using YAF.Types.Interfaces.Data;
    using YAF.Utils;

    #endregion

    /// <summary>
    ///     The module for all singleton scoped items...
    /// </summary>
    public class BaseModule : IModule, IHaveComponentRegistry
    {
        #region Public Properties

        /// <summary>
        ///     Gets or sets ComponentRegistry.
        /// </summary>
        public IComponentRegistry ComponentRegistry { get; set; }

        /// <summary>
        ///     Gets or sets ExtensionAssemblies.
        /// </summary>
        public IList<Assembly> ExtensionAssemblies { get; protected set; }

        #endregion

        #region Public Methods and Operators

        /// <summary>
        /// Apply the module to the component registry.
        /// </summary>
        /// <param name="componentRegistry">
        /// Component registry to apply configuration to.
        /// </param>
        public void Configure([NotNull] IComponentRegistry componentRegistry)
        {
            CodeContracts.ArgumentNotNull(componentRegistry, "componentRegistry");

            this.ComponentRegistry = componentRegistry;

            this.ExtensionAssemblies =
                new YafModuleScanner().GetModules("YAF*.dll").OrderByDescending(x => x.GetAssemblySortOrder()).ToList();

            // handle registration...
            this.RegisterExternalModules();

            // external first...
            this.RegisterDynamicServices(this.ExtensionAssemblies.Where(a => a != Assembly.GetExecutingAssembly()));

            // internal bindings next...
            this.RegisterDynamicServices(new[] { Assembly.GetExecutingAssembly() });

            // TODO: refactor into individual modules.
            this.RegisterGeneral();
            this.RegisterServices();
            this.RegisterEventBindings();
            this.RegisterMembershipProviders();
            this.RegisterStartupServices();
            this.RegisterModules();
            this.RegisterPages();
        }

        #endregion

        #region Methods

        /// <summary>
        /// The register data bindings.
        /// </summary>
        /// <param name="builder">
        /// The builder.
        /// </param>
        private void RegisterDataBindings(ContainerBuilder builder)
        {
            // data
            builder.RegisterType<DbAccessProvider>().As<IDbAccessProvider>().SingleInstance();
            builder.Register(c => c.Resolve<IComponentContext>().Resolve<IDbAccessProvider>().Instance).As<IDbAccess>().InstancePerDependency().PreserveExistingDefaults();
            builder.Register((c, p) => DbProviderFactories.GetFactory(p.TypedAs<string>())).ExternallyOwned().PreserveExistingDefaults();

            builder.RegisterType<DynamicDbFunction>().As<IDbFunction>().InstancePerDependency();

            // register generic IRepository handler, which can be easily overriden by more advanced repository handler
            builder.RegisterGeneric(typeof(BasicRepository<>)).As(typeof(IRepository<>)).InstancePerDependency();

            // register filters -- even if they require YafContext, they MUST BE REGISTERED UNDER GENERAL SCOPE
            // Do the YafContext check inside the constructor and throw an exception if it's required.
            builder.RegisterType<StyleFilter>().As<IDbDataFilter>();
        }

        /// <summary>
        /// The register services.
        /// </summary>
        /// <param name="assemblies">
        /// The assemblies.
        /// </param>
        /// <exception cref="NotSupportedException">
        /// <c>NotSupportedException</c>.
        /// </exception>
        private void RegisterDynamicServices([NotNull] IEnumerable<Assembly> assemblies)
        {
            CodeContracts.ArgumentNotNull(assemblies, "assemblies");

            var builder = new ContainerBuilder();

            var classes = assemblies.FindClassesWithAttribute<ExportServiceAttribute>();

            var exclude = new List<Type> { typeof(IDisposable), typeof(IHaveServiceLocator), typeof(IHaveLocalization) };

            foreach (var c in classes)
            {
                var exportAttribute = c.GetAttribute<ExportServiceAttribute>();

                if (exportAttribute == null)
                {
                    continue;
                }

                var built = builder.RegisterType(c).As(c);

                Type[] typesToRegister = null;

                if (exportAttribute.RegisterSpecifiedTypes != null &&
                    exportAttribute.RegisterSpecifiedTypes.Any())
                {
                    // only register types provided...
                    typesToRegister = exportAttribute.RegisterSpecifiedTypes;
                }
                else
                {
                    // register all associated interfaces including inherited interfaces
                    typesToRegister = c.GetInterfaces().Where(i => !exclude.Contains(i)).ToArray();
                }

                if (exportAttribute.Named.IsSet())
                {
                    // register types as "Named"
                    built = typesToRegister.Aggregate(built, (current, regType) => current.Named(exportAttribute.Named, regType));
                }
                else
                {
                    // register types "As"
                    built = typesToRegister.Aggregate(built, (current, regType) => current.As(regType));
                }

                switch (exportAttribute.ServiceLifetimeScope)
                {
                    case ServiceLifetimeScope.Singleton:
                        built.SingleInstance();
                        break;

                    case ServiceLifetimeScope.Transient:
                        built.ExternallyOwned();
                        break;

                    case ServiceLifetimeScope.OwnedByContainer:
                        built.OwnedByLifetimeScope();
                        break;

                    case ServiceLifetimeScope.InstancePerScope:
                        built.InstancePerLifetimeScope();
                        break;

                    case ServiceLifetimeScope.InstancePerDependancy:
                        built.InstancePerDependency();
                        break;

                    case ServiceLifetimeScope.InstancePerContext:
                        built.InstancePerMatchingLifetimeScope(YafLifetimeScope.Context);
                        break;
                }
            }

            this.UpdateRegistry(builder);
        }

        /// <summary>
        ///     Register event bindings
        /// </summary>
        private void RegisterEventBindings()
        {
            var builder = new ContainerBuilder();

            builder.RegisterType<ServiceLocatorEventRaiser>().As<IRaiseEvent>().InstancePerLifetimeScope();
            builder.RegisterGeneric(typeof(FireEvent<>)).As(typeof(IFireEvent<>)).InstancePerLifetimeScope();

            //// scan assemblies for events to wire up...
            // builder.RegisterAssemblyTypes(this.ExtensionAssemblies.ToArray()).AsClosedTypesOf(typeof(IHandleEvent<>)).
            // AsImplementedInterfaces().InstancePerLifetimeScope();
            this.UpdateRegistry(builder);
        }

        /// <summary>
        ///     The register external modules.
        /// </summary>
        private void RegisterExternalModules()
        {
            var builder = new ContainerBuilder();

            var modules =
                this.ExtensionAssemblies
                    .Where(x => x != Assembly.GetExecutingAssembly())
                    .FindModules<IModule>()
                    .Select(m => Activator.CreateInstance(m) as IModule);

            modules.ForEach(builder.RegisterModule);

            this.UpdateRegistry(builder);
        }

        /// <summary>
        ///     The register basic bindings.
        /// </summary>
        private void RegisterGeneral()
        {
            var builder = new ContainerBuilder();

            builder.Register(x => this.ExtensionAssemblies).Named<IList<Assembly>>("ExtensionAssemblies").SingleInstance();
            builder.RegisterType<AutoFacServiceLocatorProvider>().AsSelf().As<IServiceLocator>().As<IInjectServices>().InstancePerLifetimeScope();

            // register data bindings...
            this.RegisterDataBindings(builder);

            // YafContext registration...
            builder.RegisterType<YafContextPageProvider>().AsSelf().As<IReadOnlyProvider<YafContext>>().SingleInstance().PreserveExistingDefaults();
            builder.Register((k) => k.Resolve<IComponentContext>().Resolve<YafContextPageProvider>().Instance).ExternallyOwned().PreserveExistingDefaults();

            // Http Application Base
            builder.RegisterType<CurrentHttpApplicationStateBaseProvider>().SingleInstance().PreserveExistingDefaults();
            builder.Register(k => k.Resolve<IComponentContext>().Resolve<CurrentHttpApplicationStateBaseProvider>().Instance).ExternallyOwned().PreserveExistingDefaults();

            // Task Module
            builder.RegisterType<CurrentTaskModuleProvider>().SingleInstance().PreserveExistingDefaults();
            builder.Register(k => k.Resolve<IComponentContext>().Resolve<CurrentTaskModuleProvider>().Instance).ExternallyOwned().PreserveExistingDefaults();

            builder.RegisterType<YafNntp>().As<INewsreader>().InstancePerLifetimeScope().PreserveExistingDefaults();

            // cache bindings.
            builder.RegisterType<StaticLockObject>().As<IHaveLockObject>().SingleInstance().PreserveExistingDefaults();
            builder.RegisterType<HttpRuntimeCache>().As<IDataCache>().SingleInstance().PreserveExistingDefaults();

            // Shared object store -- used for objects local only
            builder.RegisterType<HttpRuntimeCache>().As<IObjectStore>().SingleInstance().PreserveExistingDefaults();

            this.UpdateRegistry(builder);
        }

        /// <summary>
        ///     Register membership providers
        /// </summary>
        private void RegisterMembershipProviders()
        {
            var builder = new ContainerBuilder();

            // membership
            builder.RegisterType<CurrentMembershipProvider>().AsSelf().InstancePerLifetimeScope().PreserveExistingDefaults();
            builder.Register(x => x.Resolve<IComponentContext>().Resolve<CurrentMembershipProvider>().Instance).ExternallyOwned().PreserveExistingDefaults();

            // roles
            builder.RegisterType<CurrentRoleProvider>().AsSelf().InstancePerLifetimeScope().PreserveExistingDefaults();
            builder.Register(x => x.Resolve<IComponentContext>().Resolve<CurrentRoleProvider>().Instance).ExternallyOwned().PreserveExistingDefaults();

            // profiles
            builder.RegisterType<CurrentProfileProvider>().AsSelf().InstancePerLifetimeScope().PreserveExistingDefaults();
            builder.Register(x => x.Resolve<IComponentContext>().Resolve<CurrentProfileProvider>().Instance).ExternallyOwned().PreserveExistingDefaults();

            this.UpdateRegistry(builder);
        }

        /// <summary>
        ///     The register modules.
        /// </summary>
        private void RegisterModules()
        {
            var builder = new ContainerBuilder();

            // forum modules...
            builder.RegisterAssemblyTypes(this.ExtensionAssemblies.ToArray())
                   .AssignableTo<IBaseForumModule>()
                   .As<IBaseForumModule>()
                   .InstancePerLifetimeScope();

            // editor modules...
            builder.RegisterAssemblyTypes(this.ExtensionAssemblies.ToArray()).AssignableTo<ForumEditor>().As<ForumEditor>().InstancePerLifetimeScope();

            builder.RegisterModule<UtilitiesModule>();

            this.UpdateRegistry(builder);
        }

        /// <summary>
        ///     The register pages
        /// </summary>
        private void RegisterPages()
        {
            var builder = new ContainerBuilder();

            builder.RegisterAssemblyTypes(this.ExtensionAssemblies.ToArray())
                   .AssignableTo<ILocatablePage>()
                   .AsImplementedInterfaces()
                   .SingleInstance();

            this.UpdateRegistry(builder);
        }

        /// <summary>
        /// The register services.
        /// </summary>
        private void RegisterServices()
        {
            var builder = new ContainerBuilder();

            // optional defaults.
            builder.RegisterType<YafSendMail>().As<ISendMail>().SingleInstance().PreserveExistingDefaults();

            builder.RegisterType<YafSendNotification>().As<ISendNotification>().InstancePerLifetimeScope().PreserveExistingDefaults();

            builder.RegisterType<YafDigest>().As<IDigest>().InstancePerLifetimeScope().PreserveExistingDefaults();
            builder.RegisterType<DefaultUserDisplayName>().As<IUserDisplayName>().InstancePerLifetimeScope().PreserveExistingDefaults();
            builder.RegisterType<DefaultUrlBuilder>().As<IUrlBuilder>().InstancePerLifetimeScope().PreserveExistingDefaults();
            builder.RegisterType<YafBBCode>().As<IBBCode>().InstancePerLifetimeScope().PreserveExistingDefaults();
            builder.RegisterType<YafFormatMessage>().As<IFormatMessage>().InstancePerLifetimeScope().PreserveExistingDefaults();
            builder.RegisterType<YafDbBroker>().AsSelf().InstancePerLifetimeScope().PreserveExistingDefaults();
            builder.RegisterType<YafAvatars>().As<IAvatars>().InstancePerLifetimeScope().PreserveExistingDefaults();
            builder.RegisterType<TreatCacheKeyWithBoard>().As<ITreatCacheKey>().InstancePerLifetimeScope().PreserveExistingDefaults();
            builder.RegisterType<CurrentBoardId>().As<IHaveBoardID>().InstancePerLifetimeScope().PreserveExistingDefaults();

            builder.RegisterType<YafReadTrackCurrentUser>().As<IReadTrackCurrentUser>().InstancePerYafContext().PreserveExistingDefaults();

            builder.RegisterType<YafSession>().As<IYafSession>().InstancePerLifetimeScope().PreserveExistingDefaults();
            builder.RegisterType<YafBadWordReplace>().As<IBadWordReplace>().SingleInstance().PreserveExistingDefaults();

            builder.RegisterType<YafPermissions>().As<IPermissions>().InstancePerLifetimeScope().PreserveExistingDefaults();
            builder.RegisterType<YafDateTime>().As<IDateTime>().InstancePerLifetimeScope().PreserveExistingDefaults();
            builder.RegisterType<YafUserIgnored>().As<IUserIgnored>().InstancePerLifetimeScope().PreserveExistingDefaults();
            builder.RegisterType<YafBuddy>().As<IBuddy>().InstancePerLifetimeScope().PreserveExistingDefaults();

            builder.RegisterType<InstallUpgradeService>().AsSelf().PreserveExistingDefaults();

            // builder.RegisterType<RewriteUrlBuilder>().Named<IUrlBuilder>("rewriter").InstancePerLifetimeScope();
            builder.RegisterType<YafStopWatch>()
                   .As<IStopWatch>()
                   .InstancePerMatchingLifetimeScope(YafLifetimeScope.Context)
                   .PreserveExistingDefaults();

            // localization registration...
            builder.RegisterType<LocalizationProvider>().InstancePerLifetimeScope().PreserveExistingDefaults();
            builder.Register(k => k.Resolve<IComponentContext>().Resolve<LocalizationProvider>().Localization).PreserveExistingDefaults();

            // theme registration...
            builder.RegisterType<ThemeProvider>().InstancePerLifetimeScope().PreserveExistingDefaults();
            builder.Register(k => k.Resolve<IComponentContext>().Resolve<ThemeProvider>().Theme).PreserveExistingDefaults();

            // replace rules registration...
            builder.RegisterType<ProcessReplaceRulesProvider>()
                   .AsSelf()
                   .As<IReadOnlyProvider<IProcessReplaceRules>>()
                   .InstancePerLifetimeScope()
                   .PreserveExistingDefaults();

            builder.Register((k, p) => k.Resolve<IComponentContext>().Resolve<ProcessReplaceRulesProvider>(p).Instance).InstancePerLifetimeScope().PreserveExistingDefaults();

            // module resolution bindings...
            builder.RegisterGeneric(typeof(StandardModuleManager<>)).As(typeof(IModuleManager<>)).InstancePerLifetimeScope();

            // background emailing...
            builder.RegisterType<YafSendMailThreaded>().As<ISendMailThreaded>().SingleInstance().PreserveExistingDefaults();

            // style transformation...
            builder.RegisterType<StyleTransform>().As<IStyleTransform>().InstancePerYafContext().PreserveExistingDefaults();

            // board settings...
            builder.RegisterType<CurrentBoardSettings>().AsSelf().InstancePerYafContext().PreserveExistingDefaults();
            builder.Register(k => k.Resolve<IComponentContext>().Resolve<CurrentBoardSettings>().Instance).ExternallyOwned().PreserveExistingDefaults();

            // favorite topic is based on YafContext
            builder.RegisterType<YafFavoriteTopic>().As<IFavoriteTopic>().InstancePerYafContext().PreserveExistingDefaults();

            this.UpdateRegistry(builder);
        }

        /// <summary>
        ///     The register services.
        /// </summary>
        private void RegisterStartupServices()
        {
            var builder = new ContainerBuilder();

            builder.RegisterAssemblyTypes(Assembly.GetExecutingAssembly())
                   .AssignableTo<IStartupService>()
                   .As<IStartupService>()
                   .InstancePerLifetimeScope();

            builder.Register(
                x => x.Resolve<IComponentContext>()
                      .Resolve<IEnumerable<IStartupService>>()
                      .FirstOrDefault(t => t is StartupInitializeDb) as
                     StartupInitializeDb)
                   .InstancePerLifetimeScope();

            this.UpdateRegistry(builder);
        }

        #endregion
    }
}