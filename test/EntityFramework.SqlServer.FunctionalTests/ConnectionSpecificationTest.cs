// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Data.SqlClient;
using System.Linq;
using Microsoft.Data.Entity.Infrastructure;
using Microsoft.Data.Entity.SqlServer.FunctionalTests.TestModels;
using Microsoft.Framework.DependencyInjection;
using Xunit;
using CoreStrings = Microsoft.Data.Entity.Internal.Strings;

namespace Microsoft.Data.Entity.SqlServer.FunctionalTests
{
    public class ConnectionSpecificationTest
    {
        [Fact]
        public void Can_specify_connection_string_in_OnConfiguring()
        {
            var serviceCollection = new ServiceCollection();
            serviceCollection
                .AddEntityFramework()
                .AddSqlServer()
                .AddDbContext<StringInOnConfiguringContext>();

            var serviceProvider = serviceCollection.BuildServiceProvider();

            using (SqlServerNorthwindContext.GetSharedStore())
            {
                using (var context = serviceProvider.GetRequiredService<StringInOnConfiguringContext>())
                {
                    Assert.True(context.Customers.Any());
                }
            }
        }

        [Fact]
        public void Can_specify_connection_string_in_OnConfiguring_with_default_service_provider()
        {
            using (SqlServerNorthwindContext.GetSharedStore())
            {
                using (var context = new StringInOnConfiguringContext())
                {
                    Assert.True(context.Customers.Any());
                }
            }
        }

        private class StringInOnConfiguringContext : NorthwindContextBase
        {
            protected override void OnConfiguring(EntityOptionsBuilder optionsBuilder)
            {
                optionsBuilder.UseSqlServer(SqlServerNorthwindContext.ConnectionString);
            }
        }

        [Fact]
        public void Can_specify_connection_in_OnConfiguring()
        {
            var serviceCollection = new ServiceCollection();
            serviceCollection
                .AddScoped(p => new SqlConnection(SqlServerNorthwindContext.ConnectionString))
                .AddEntityFramework()
                .AddSqlServer()
                .AddDbContext<ConnectionInOnConfiguringContext>();

            var serviceProvider = serviceCollection.BuildServiceProvider();

            using (SqlServerNorthwindContext.GetSharedStore())
            {
                using (var context = serviceProvider.GetRequiredService<ConnectionInOnConfiguringContext>())
                {
                    Assert.True(context.Customers.Any());
                }
            }
        }

        [Fact]
        public void Can_specify_connection_in_OnConfiguring_with_default_service_provider()
        {
            using (SqlServerNorthwindContext.GetSharedStore())
            {
                using (var context = new ConnectionInOnConfiguringContext(new SqlConnection(SqlServerNorthwindContext.ConnectionString)))
                {
                    Assert.True(context.Customers.Any());
                }
            }
        }

        private class ConnectionInOnConfiguringContext : NorthwindContextBase
        {
            private readonly SqlConnection _connection;

            public ConnectionInOnConfiguringContext(SqlConnection connection)
            {
                _connection = connection;
            }

            protected override void OnConfiguring(EntityOptionsBuilder optionsBuilder)
            {
                optionsBuilder.UseSqlServer(_connection);
            }

            public override void Dispose()
            {
                _connection.Dispose();
                base.Dispose();
            }
        }

        private class StringInConfigContext : NorthwindContextBase
        {
            protected override void OnConfiguring(EntityOptionsBuilder optionsBuilder)
            {
                optionsBuilder.UseSqlServer("Database=Crunchie");
            }
        }

        [Fact]
        public void Throws_if_no_connection_found_in_config_without_UseSqlServer()
        {
            var serviceCollection = new ServiceCollection();
            serviceCollection
                .AddEntityFramework()
                .AddSqlServer()
                .AddDbContext<NoUseSqlServerContext>();

            var serviceProvider = serviceCollection.BuildServiceProvider();

            using (var context = serviceProvider.GetRequiredService<NoUseSqlServerContext>())
            {
                Assert.Equal(
                    CoreStrings.NoDataStoreConfigured,
                    Assert.Throws<InvalidOperationException>(() => context.Customers.Any()).Message);
            }
        }

        [Fact]
        public void Throws_if_no_config_without_UseSqlServer()
        {
            var serviceCollection = new ServiceCollection();
            serviceCollection
                .AddEntityFramework()
                .AddSqlServer()
                .AddDbContext<NoUseSqlServerContext>();

            var serviceProvider = serviceCollection.BuildServiceProvider();

            using (var context = serviceProvider.GetRequiredService<NoUseSqlServerContext>())
            {
                Assert.Equal(
                    CoreStrings.NoDataStoreConfigured,
                    Assert.Throws<InvalidOperationException>(() => context.Customers.Any()).Message);
            }
        }

        private class NoUseSqlServerContext : NorthwindContextBase
        {
        }

        [Fact]
        public void Can_select_appropriate_provider_when_multiple_registered()
        {
            var serviceCollection = new ServiceCollection();
            serviceCollection
                .AddScoped<SomeService>()
                .AddEntityFramework()
                .AddSqlServer()
                .AddInMemoryStore()
                .AddDbContext<MultipleProvidersContext>();

            var serviceProvider = serviceCollection.BuildServiceProvider();

            using (SqlServerNorthwindContext.GetSharedStore())
            {
                MultipleProvidersContext context1;
                MultipleProvidersContext context2;

                using (var serviceScope = serviceProvider.GetRequiredService<IServiceScopeFactory>().CreateScope())
                {
                    using (context1 = serviceScope.ServiceProvider.GetRequiredService<MultipleProvidersContext>())
                    {
                        context1.UseSqlServer = true;

                        Assert.True(context1.Customers.Any());
                    }

                    using (var context1B = serviceScope.ServiceProvider.GetRequiredService<MultipleProvidersContext>())
                    {
                        Assert.Same(context1, context1B);
                    }

                    var someService = serviceScope.ServiceProvider.GetRequiredService<SomeService>();
                    Assert.Same(context1, someService.Context);
                }
                using (var serviceScope = serviceProvider.GetRequiredService<IServiceScopeFactory>().CreateScope())
                {
                    using (context2 = serviceScope.ServiceProvider.GetRequiredService<MultipleProvidersContext>())
                    {
                        context2.UseSqlServer = false;

                        Assert.False(context2.Customers.Any());
                    }

                    using (var context2B = serviceScope.ServiceProvider.GetRequiredService<MultipleProvidersContext>())
                    {
                        Assert.Same(context2, context2B);
                    }

                    var someService = serviceScope.ServiceProvider.GetRequiredService<SomeService>();
                    Assert.Same(context2, someService.Context);
                }

                Assert.NotSame(context1, context2);
            }
        }

        [Fact]
        public void Can_select_appropriate_provider_when_multiple_registered_with_default_service_provider()
        {
            using (SqlServerNorthwindContext.GetSharedStore())
            {
                using (var context = new MultipleProvidersContext())
                {
                    context.UseSqlServer = true;

                    Assert.True(context.Customers.Any());
                }

                using (var context = new MultipleProvidersContext())
                {
                    context.UseSqlServer = false;

                    Assert.False(context.Customers.Any());
                }
            }
        }

        private class MultipleProvidersContext : NorthwindContextBase
        {
            public bool UseSqlServer { get; set; }

            protected override void OnConfiguring(EntityOptionsBuilder optionsBuilder)
            {
                if (UseSqlServer)
                {
                    optionsBuilder.UseSqlServer(SqlServerNorthwindContext.ConnectionString);
                }
                else
                {
                    optionsBuilder.UseInMemoryStore();
                }
            }
        }

        private class SomeService
        {
            public SomeService(MultipleProvidersContext context)
            {
                Context = context;
            }

            public MultipleProvidersContext Context { get; }
        }

        [Fact]
        public void Can_depend_on_EntityOptions()
        {
            var serviceCollection = new ServiceCollection();
            serviceCollection
                .AddScoped(p => new SqlConnection(SqlServerNorthwindContext.ConnectionString))
                .AddEntityFramework()
                .AddSqlServer()
                .AddDbContext<OptionsContext>();

            var serviceProvider = serviceCollection.BuildServiceProvider();

            using (SqlServerNorthwindContext.GetSharedStore())
            {
                using (var context = serviceProvider.GetRequiredService<OptionsContext>())
                {
                    Assert.True(context.Customers.Any());
                }
            }
        }

        [Fact]
        public void Can_depend_on_EntityOptions_with_default_service_provider()
        {
            using (SqlServerNorthwindContext.GetSharedStore())
            {
                using (var context = new OptionsContext(
                    new EntityOptions<OptionsContext>(),
                    new SqlConnection(SqlServerNorthwindContext.ConnectionString)))
                {
                    Assert.True(context.Customers.Any());
                }
            }
        }

        private class OptionsContext : NorthwindContextBase
        {
            private readonly SqlConnection _connection;
            private readonly EntityOptions<OptionsContext> _options;

            public OptionsContext(EntityOptions<OptionsContext> options, SqlConnection connection)
                : base(options)
            {
                _options = options;
                _connection = connection;
            }

            protected override void OnConfiguring(EntityOptionsBuilder optionsBuilder)
            {
                Assert.Same(_options, optionsBuilder.Options);

                optionsBuilder.UseSqlServer(_connection);

                Assert.NotSame(_options, optionsBuilder.Options);
            }

            public override void Dispose()
            {
                _connection.Dispose();
                base.Dispose();
            }
        }

        [Fact]
        public void Can_register_multiple_context_types()
        {
            var serviceCollection = new ServiceCollection();
            serviceCollection
                .AddEntityFramework()
                .AddSqlServer()
                .AddInMemoryStore()
                .AddDbContext<MultipleContext1>()
                .AddDbContext<MultipleContext2>();

            var serviceProvider = serviceCollection.BuildServiceProvider();

            using (SqlServerNorthwindContext.GetSharedStore())
            {
                using (var context = serviceProvider.GetRequiredService<MultipleContext1>())
                {
                    Assert.True(context.Customers.Any());
                }

                using (var context = serviceProvider.GetRequiredService<MultipleContext2>())
                {
                    Assert.False(context.Customers.Any());
                }
            }
        }

        [Fact]
        public void Can_register_multiple_context_types_with_default_service_provider()
        {
            using (SqlServerNorthwindContext.GetSharedStore())
            {
                using (var context = new MultipleContext1(new EntityOptions<MultipleContext1>()))
                {
                    Assert.True(context.Customers.Any());
                }

                using (var context = new MultipleContext2(new EntityOptions<MultipleContext2>()))
                {
                    Assert.False(context.Customers.Any());
                }
            }
        }

        private class MultipleContext1 : NorthwindContextBase
        {
            private readonly EntityOptions<MultipleContext1> _options;

            public MultipleContext1(EntityOptions<MultipleContext1> options)
                : base(options)
            {
                _options = options;
            }

            protected override void OnConfiguring(EntityOptionsBuilder optionsBuilder)
            {
                Assert.Same(_options, optionsBuilder.Options);

                optionsBuilder.UseSqlServer(SqlServerNorthwindContext.ConnectionString);

                Assert.NotSame(_options, optionsBuilder.Options);
            }
        }

        private class MultipleContext2 : NorthwindContextBase
        {
            private readonly EntityOptions<MultipleContext2> _options;

            public MultipleContext2(EntityOptions<MultipleContext2> options)
                : base(options)
            {
                _options = options;
            }

            protected override void OnConfiguring(EntityOptionsBuilder optionsBuilder)
            {
                Assert.Same(_options, optionsBuilder.Options);

                optionsBuilder.UseInMemoryStore();

                Assert.NotSame(_options, optionsBuilder.Options);
            }
        }

        [Fact]
        public void Can_depend_on_non_generic_options_when_only_one_context()
        {
            var serviceCollection = new ServiceCollection();
            serviceCollection
                .AddEntityFramework()
                .AddSqlServer()
                .AddInMemoryStore()
                .AddDbContext<NonGenericOptionsContext>();

            var serviceProvider = serviceCollection.BuildServiceProvider();

            using (SqlServerNorthwindContext.GetSharedStore())
            {
                using (var context = serviceProvider.GetRequiredService<NonGenericOptionsContext>())
                {
                    Assert.True(context.Customers.Any());
                }
            }
        }

        [Fact]
        public void Can_depend_on_non_generic_options_when_only_one_context_with_default_service_provider()
        {
            using (SqlServerNorthwindContext.GetSharedStore())
            {
                using (var context = new NonGenericOptionsContext(new EntityOptions<DbContext>()))
                {
                    Assert.True(context.Customers.Any());
                }
            }
        }

        private class NonGenericOptionsContext : NorthwindContextBase
        {
            private readonly EntityOptions _options;

            public NonGenericOptionsContext(EntityOptions options)
                : base(options)
            {
                _options = options;
            }

            protected override void OnConfiguring(EntityOptionsBuilder optionsBuilder)
            {
                Assert.Same(_options, optionsBuilder.Options);

                optionsBuilder.UseSqlServer(SqlServerNorthwindContext.ConnectionString);

                Assert.NotSame(_options, optionsBuilder.Options);
            }
        }

        private class NorthwindContextBase : DbContext
        {
            protected NorthwindContextBase()
            {
            }

            protected NorthwindContextBase(EntityOptions options)
                : base(options)
            {
            }

            public DbSet<Customer> Customers { get; set; }

            protected override void OnModelCreating(ModelBuilder modelBuilder)
            {
                modelBuilder.Entity<Customer>(b =>
                    {
                        b.Key(c => c.CustomerID);
                        b.ForSqlServer().Table("Customers");
                    });
            }
        }

        private class Customer
        {
            public string CustomerID { get; set; }
            public string CompanyName { get; set; }
            public string Fax { get; set; }
        }
    }
}
