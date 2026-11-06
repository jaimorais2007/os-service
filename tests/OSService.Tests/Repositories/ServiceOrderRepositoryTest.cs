
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using OSService.Domain.Entities;
using OSService.Infrastructure.Contexts;
using OSService.Infrastructure.Repositories;
using Xunit;

namespace OSService.Tests.Repositories
{
    public class ServiceOrderRepositoryTest : IDisposable
    {
        private readonly SqliteConnection _connection;
        private readonly DbContextOptions<ServiceOrderDbContext> _options;

        public ServiceOrderRepositoryTest()
        {
            _connection = new SqliteConnection("DataSource=:memory:");
            _connection.Open();

            _options = new DbContextOptionsBuilder<ServiceOrderDbContext>()
                .UseSqlite(_connection)
                .Options;

            using var context = CreateContext();
            context.Database.EnsureCreated();
        }

        private ServiceOrderDbContext CreateContext()
        {
            return new ServiceOrderDbContext(_options);
        }

        private static ServiceOrder CreateServiceOrder()
        {
            return new ServiceOrder(
                Guid.NewGuid(),
                Guid.NewGuid());
        }

        [Fact]
        public async Task AddAsync_ShouldTrackServiceOrder()
        {
            await using var context = CreateContext();

            var repository = new ServiceOrderRepository(context);
            var serviceOrder = CreateServiceOrder();

            await repository.AddAsync(serviceOrder);

            Assert.Equal(
                EntityState.Added,
                context.Entry(serviceOrder).State);
        }

        [Fact]
        public async Task SaveChangesAsync_ShouldPersistServiceOrder()
        {
            var serviceOrder = CreateServiceOrder();

            await using (var context = CreateContext())
            {
                var repository = new ServiceOrderRepository(context);

                await repository.AddAsync(serviceOrder);
                await repository.SaveChangesAsync();
            }

            await using (var context = CreateContext())
            {
                var persistedServiceOrder =
                    await context.ServiceOrders
                        .FirstOrDefaultAsync(x => x.Id == serviceOrder.Id);

                Assert.NotNull(persistedServiceOrder);
                Assert.Equal(
                    serviceOrder.CustomerId,
                    persistedServiceOrder.CustomerId);
                Assert.Equal(
                    serviceOrder.VehicleId,
                    persistedServiceOrder.VehicleId);
            }
        }

        [Fact]
        public async Task GetByIdAsync_WhenFound_ShouldReturnServiceOrder()
        {
            var serviceOrder = CreateServiceOrder();

            await using (var context = CreateContext())
            {
                context.ServiceOrders.Add(serviceOrder);
                await context.SaveChangesAsync();
            }

            await using (var context = CreateContext())
            {
                var repository = new ServiceOrderRepository(context);

                var result = await repository.GetByIdAsync(
                    serviceOrder.Id);

                Assert.NotNull(result);
                Assert.Equal(serviceOrder.Id, result.Id);
                Assert.Single(result.StatusHistory);
            }
        }

        [Fact]
        public async Task GetByIdAsync_WhenNotFound_ShouldReturnNull()
        {
            await using var context = CreateContext();

            var repository = new ServiceOrderRepository(context);

            var result = await repository.GetByIdAsync(Guid.NewGuid());

            Assert.Null(result);
        }

        [Fact]
        public async Task GetAllAsync_ShouldReturnServiceOrders()
        {
            var firstServiceOrder = CreateServiceOrder();
            var secondServiceOrder = CreateServiceOrder();

            await using (var context = CreateContext())
            {
                context.ServiceOrders.AddRange(
                    firstServiceOrder,
                    secondServiceOrder);

                await context.SaveChangesAsync();
            }

            await using (var context = CreateContext())
            {
                var repository = new ServiceOrderRepository(context);

                var result = await repository.GetAllAsync();

                Assert.Equal(2, result.Count);
                Assert.All(result, x =>
                    Assert.Single(x.StatusHistory));
            }
        }

        [Fact]
        public async Task GetAllAsync_ShouldReturnUntrackedEntities()
        {
            var serviceOrder = CreateServiceOrder();

            await using (var context = CreateContext())
            {
                context.ServiceOrders.Add(serviceOrder);
                await context.SaveChangesAsync();
            }

            await using (var context = CreateContext())
            {
                var repository = new ServiceOrderRepository(context);

                var result = await repository.GetAllAsync();

                Assert.Single(result);

                Assert.Empty(
                    context.ChangeTracker.Entries<ServiceOrder>());
            }
        }

        [Fact]
        public async Task GetByIdAsync_ShouldReturnTrackedEntity()
        {
            var serviceOrder = CreateServiceOrder();

            await using (var context = CreateContext())
            {
                context.ServiceOrders.Add(serviceOrder);
                await context.SaveChangesAsync();
            }

            await using (var context = CreateContext())
            {
                var repository = new ServiceOrderRepository(context);

                var result = await repository.GetByIdAsync(
                    serviceOrder.Id);

                Assert.NotNull(result);

                Assert.Equal(
                    EntityState.Unchanged,
                    context.Entry(result).State);
            }
        }

        [Fact]
        public async Task GetAllAsync_WhenEmpty_ShouldReturnEmptyList()
        {
            await using var context = CreateContext();

            var repository = new ServiceOrderRepository(context);

            var result = await repository.GetAllAsync();

            Assert.Empty(result);
        }

        public void Dispose()
        {
            _connection.Dispose();
        }
    }
}
