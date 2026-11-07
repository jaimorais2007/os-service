using Microsoft.Extensions.Logging;
using Moq;
using OSService.Domain.Entities;
using OSService.Infrastructure.Metrics;
using Xunit;

namespace OSService.Tests.Metrics
{
    public class ServiceOrderMetricsTests
    {
        private readonly Mock<ILogger<ServiceOrderMetrics>> _loggerMock;
        private readonly ServiceOrderMetrics _metrics;

        public ServiceOrderMetricsTests()
        {
            _loggerMock = new Mock<ILogger<ServiceOrderMetrics>>();
            _metrics = new ServiceOrderMetrics(_loggerMock.Object);
        }

        [Fact]
        public void CalculateServiceOrderStatusMeanTimeMetric_WithSingleStatus_ShouldNotThrow()
        {
            // Arrange
            var order = new ServiceOrder(Guid.NewGuid(), Guid.NewGuid());

            // Act
            var exception = Record.Exception(() => _metrics.CalculateServiceOrderStatusMeanTimeMetric(order));

            // Assert
            Assert.Null(exception);
        }

        [Fact]
        public void CalculateServiceOrderStatusMeanTimeMetric_WithMultipleStatuses_ShouldCalculateSuccessfully()
        {
            // Arrange
            var order = new ServiceOrder(Guid.NewGuid(), Guid.NewGuid());
            order.StartDiagnostics();
            order.FinishAnalysis(500m);

            // Act
            var exception = Record.Exception(() => _metrics.CalculateServiceOrderStatusMeanTimeMetric(order));

            // Assert
            Assert.Null(exception);
        }

        [Fact]
        public void CalculateServiceOrderStatusMeanTimeMetric_QuandoOrdemForNula_DeveCapturarExcecaoELogar()
        {
            // Act: passando null gera NullReferenceException interna que deve ser capturada no try-catch
            var exception = Record.Exception(() => _metrics.CalculateServiceOrderStatusMeanTimeMetric(null!));

            // Assert
            Assert.Null(exception);
        }

        [Fact]
        public void CalculateServiceOrderStatusMeanTimeMetric_SemLoggerEComExcecao_NaoDeveFalhar()
        {
            var metricsWithoutLogger = new ServiceOrderMetrics(null);

            var exception = Record.Exception(() => metricsWithoutLogger.CalculateServiceOrderStatusMeanTimeMetric(null!));

            Assert.Null(exception);
        }

        [Fact]
        public void CalculateServiceOrderStatusMeanTimeMetric_SemLoggerEComStatusUnico_NaoDeveFalhar()
        {
            var metricsWithoutLogger = new ServiceOrderMetrics(null);
            var order = new ServiceOrder(Guid.NewGuid(), Guid.NewGuid());

            var exception = Record.Exception(() => metricsWithoutLogger.CalculateServiceOrderStatusMeanTimeMetric(order));

            Assert.Null(exception);
        }

        [Fact]
        public void CalculateServiceOrderStatusMeanTimeMetric_SemLoggerEComSucesso_NaoDeveFalhar()
        {
            var metricsWithoutLogger = new ServiceOrderMetrics();
            var order = new ServiceOrder(Guid.NewGuid(), Guid.NewGuid());
            order.StartDiagnostics();

            var exception = Record.Exception(() => metricsWithoutLogger.CalculateServiceOrderStatusMeanTimeMetric(order));

            Assert.Null(exception);
        }

        [Fact]
        public void ServiceOrderMetrics_Constants_ShouldMatchExpectedNames()
        {
            Assert.Equal("ServiceOrder.StatusMeanTime", ServiceOrderMetrics.MeterName);
            Assert.Equal("service_order_status_mean_time_in_hours", ServiceOrderMetrics.MetricName);
            Assert.NotNull(ServiceOrderMetrics.ServiceOrderStatusMeanTime);
            Assert.NotNull(ServiceOrderMetrics.ServiceOrderStatusMeanTimeHistogram);
        }
    }
}
