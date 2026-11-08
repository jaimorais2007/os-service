
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

        [Fact]
        public async Task GetAllAsync_WithStatusFilter_ShouldReturnOnlyMatchingOrders()
        {
            await using var context = CreateContext();
            var repository = new ServiceOrderRepository(context);

            var order1 = CreateServiceOrder(); // Received
            var order2 = CreateServiceOrder();
            order2.StartDiagnostics(); // UnderAnalysis

            await repository.AddAsync(order1);
            await repository.AddAsync(order2);
            await repository.SaveChangesAsync();

            var receivedOrders = await repository.GetAllAsync(OSService.Domain.Enum.OrderStatus.Received);
            var analysisOrders = await repository.GetByStatusAsync(OSService.Domain.Enum.OrderStatus.UnderAnalysis);

            Assert.Single(receivedOrders);
            Assert.Equal(order1.Id, receivedOrders.First().Id);

            Assert.Single(analysisOrders);
            Assert.Equal(order2.Id, analysisOrders.First().Id);
        }

        [Fact]
        public async Task GetByIdAsync_ShouldIncludePartsAndServices()
        {
            var serviceOrder = CreateServiceOrder();
            var partId = Guid.NewGuid();
            var serviceId = Guid.NewGuid();
            serviceOrder.AddPart(partId, 3, 40m, "Spark Plug");
            serviceOrder.AddService(serviceId, 150m, "Tune Up");

            await using (var context = CreateContext())
            {
                var repository = new ServiceOrderRepository(context);
                await repository.AddAsync(serviceOrder);
                await repository.SaveChangesAsync();
            }

            await using (var context = CreateContext())
            {
                var repository = new ServiceOrderRepository(context);
                var loaded = await repository.GetByIdAsync(serviceOrder.Id);

                Assert.NotNull(loaded);
                Assert.Single(loaded.Parts);
                Assert.Equal(partId, loaded.Parts.First().PartId);
                Assert.Equal(120m, loaded.Parts.First().TotalPrice);

                Assert.Single(loaded.Services);
                Assert.Equal(serviceId, loaded.Services.First().ServiceId);
                Assert.Equal(150m, loaded.Services.First().Price);
            }
        }

        [Fact]
        public async Task GetByIdAndGetAll_WithMultiplePartsServicesAndStatuses_ShouldAvoidCartesianDuplication()
        {
            var serviceOrder = CreateServiceOrder();
            serviceOrder.StartDiagnostics();
            serviceOrder.AddPart(Guid.NewGuid(), 2, 50m, "Oil Filter");
            serviceOrder.AddPart(Guid.NewGuid(), 4, 30m, "Spark Plug");
            serviceOrder.AddService(Guid.NewGuid(), 120m, "Oil Change");
            serviceOrder.AddService(Guid.NewGuid(), 200m, "Engine Alignment");
            serviceOrder.FinishAnalysis(440m);

            await using (var context = CreateContext())
            {
                var repository = new ServiceOrderRepository(context);
                await repository.AddAsync(serviceOrder);
                await repository.SaveChangesAsync();
            }

            await using (var context = CreateContext())
            {
                var repository = new ServiceOrderRepository(context);
                var byId = await repository.GetByIdAsync(serviceOrder.Id);
                var all = await repository.GetAllAsync();

                Assert.NotNull(byId);
                Assert.Equal(3, byId.StatusHistory.Count);
                Assert.Equal(2, byId.Parts.Count);
                Assert.Equal(2, byId.Services.Count);

                var fromList = Assert.Single(all);
                Assert.Equal(3, fromList.StatusHistory.Count);
                Assert.Equal(2, fromList.Parts.Count);
                Assert.Equal(2, fromList.Services.Count);
            }
        }

        [Fact]
        public async Task ProcessedSagaEvents_ShouldPersistAndCheckIdempotency()
        {
            var eventId = Guid.NewGuid();
            var correlationId = Guid.NewGuid();

            await using (var context = CreateContext())
            {
                var repository = new ServiceOrderRepository(context);
                Assert.False(await repository.IsEventProcessedAsync(eventId));

                await repository.AddProcessedEventAsync(
                    new ProcessedSagaEvent(eventId, "PaymentConfirmed", correlationId));
                await repository.SaveChangesAsync();
            }

            await using (var context = CreateContext())
            {
                var repository = new ServiceOrderRepository(context);
                Assert.True(await repository.IsEventProcessedAsync(eventId));
            }
        }

        [Fact]
        public async Task OutboxRepository_ShouldGetPendingMarkFailedAndMarkPublished()
        {
            var msg1 = new OutboxMessage(
                Guid.NewGuid(),
                "BudgetApproved",
                Guid.NewGuid(),
                "{\"eventType\":\"BudgetApproved\"}",
                "00-4bf92f3577b34da6a3ce929d0e0e4736-00f067aa0ba902b7-01",
                DateTime.UtcNow.AddSeconds(-10));

            var msg2 = new OutboxMessage(
                Guid.NewGuid(),
                "ServiceOrderRejected",
                Guid.NewGuid(),
                "{\"eventType\":\"ServiceOrderRejected\"}",
                occurredAt: DateTime.UtcNow);

            await using (var context = CreateContext())
            {
                var repository = new ServiceOrderRepository(context);
                await repository.AddOutboxMessageAsync(msg2);
                await repository.AddOutboxMessageAsync(msg1);
                await repository.SaveChangesAsync();
            }

            await using (var context = CreateContext())
            {
                var outboxRepository = new OutboxRepository(context);
                var pending = await outboxRepository.GetPendingAsync(10);

                Assert.Equal(2, pending.Count);
                Assert.Equal(msg1.Id, pending[0].Id);
                Assert.Equal(msg2.Id, pending[1].Id);

                await outboxRepository.MarkFailedAsync(msg1.Id, "Temporary SNS outage");
                await outboxRepository.MarkFailedAsync(Guid.NewGuid(), "Missing id no-op");
            }

            await using (var context = CreateContext())
            {
                var outboxRepository = new OutboxRepository(context);
                var pending = await outboxRepository.GetPendingAsync(10);

                Assert.Equal(2, pending.Count);
                Assert.Equal(1, pending[0].Attempts);
                Assert.Equal("Temporary SNS outage", pending[0].LastError);

                await outboxRepository.MarkPublishedAsync(msg1.Id);
                await outboxRepository.MarkPublishedAsync(Guid.NewGuid());
            }

            await using (var context = CreateContext())
            {
                var outboxRepository = new OutboxRepository(context);
                var remaining = await outboxRepository.GetPendingAsync(10);

                Assert.Single(remaining);
                Assert.Equal(msg2.Id, remaining[0].Id);
            }
        }

        public void Dispose()
        {
            _connection.Dispose();
        }
    }
}
