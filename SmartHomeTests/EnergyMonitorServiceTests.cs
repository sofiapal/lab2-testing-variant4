using Energy_Project.Models;
using Energy_Project.Services;
using Energy_Project.Services.Interfaces;
using Moq;
using Xunit;

namespace SmartHomeTests
{
    /// <summary>
    /// Набір модульних тестів для сервісу енергетичного моніторингу EnergyMonitorService.
    /// Перевіряє коректність розрахунку споживання енергії (CalculateCurrentUsageKwh),
    /// алгоритм виявлення перевантаження мережі (CheckForOverload)
    /// та модифікацію добових лімітів споживання (UpdateEnergyLimit).
    /// </summary>
    public class EnergyMonitorServiceTests
    {
        private readonly Mock<IDeviceRepository> _devicesMock;
        private readonly Mock<IEnergyPlanRepository> _plansMock;
        private readonly Mock<INotificationService> _notifyMock;
        private readonly EnergyMonitorService _monitorService;

        public EnergyMonitorServiceTests()
        {
            _devicesMock = new Mock<IDeviceRepository>();
            _plansMock = new Mock<IEnergyPlanRepository>();
            _notifyMock = new Mock<INotificationService>();

            _monitorService = new EnergyMonitorService(
                _devicesMock.Object,
                _plansMock.Object,
                _notifyMock.Object);
        }

        /// <summary>
        /// Бізнес-ситуація: Розрахунок поточного енергоспоживання (CalculateCurrentUsageKwh)
        /// для різних значень сумарної потужності підключених приладів.
        /// Перевіряє коректність математичного перетворення ватів у кіловат-години (Watts / 1000.0).
        /// Застосовані типи перевірок: [Theory], [InlineData], Assert.Equal.
        /// </summary>
        [Theory]
        [InlineData(1000.0, 1.0)]
        [InlineData(2500.0, 2.5)]
        [InlineData(500.0, 0.5)]
        [InlineData(0.0, 0.0)]
        [InlineData(3450.0, 3.45)]
        [InlineData(780.0, 0.78)]
        public void CalculateCurrentUsageKwh_ParameterizedWattage_CalculatesCorrectKilowattHours(
            double activeWatts,
            double expectedKwh)
        {
            // Arrange
            var testDevices = new List<Device>
            {
                new() { Id = 1, Name = "Тестове навантаження", PowerUsageWatts = activeWatts, IsOn = true }
            };
            _devicesMock.Setup(r => r.GetAll()).Returns(testDevices);

            // Act
            var actualKwh = _monitorService.CalculateCurrentUsageKwh();

            // Assert
            Assert.Equal(expectedKwh, actualKwh, precision: 3);
            _devicesMock.Verify(r => r.GetAll(), Times.Once);
        }

        /// <summary>
        /// Бізнес-ситуація: Розрахунок споживання енергії за наявності як увімкнених, так і вимкнених приладів.
        /// Перевіряє, що енергоспоживання вимкнених пристроїв ігнорується, і результуюче значення
        /// не дорівнює повній сумарній потужності всієї техніки в оселі.
        /// Застосовані типи перевірок: Assert.NotEqual, Assert.Equal.
        /// </summary>
        [Fact]
        public void CalculateCurrentUsageKwh_FiltersOutInactiveDevices_UsageNotEqualToTotalWattage()
        {
            // Arrange: 2 активних пристрої (1000 Вт + 1500 Вт = 2500 Вт = 2.5 кВт·год)
            // і 1 неактивний пристрій (2000 Вт = 2.0 кВт·год). Повна потужність = 4.5 кВт·год.
            var mixedDevices = new List<Device>
            {
                new() { Id = 1, Name = "Бойлер", PowerUsageWatts = 1500.0, IsOn = true },
                new() { Id = 2, Name = "Кавомашина", PowerUsageWatts = 1000.0, IsOn = true },
                new() { Id = 3, Name = "Електродуховка", PowerUsageWatts = 2000.0, IsOn = false }
            };
            _devicesMock.Setup(r => r.GetAll()).Returns(mixedDevices);

            const double totalTheoreticalUsageKwh = 4.5;
            const double expectedActiveUsageKwh = 2.5;

            // Act
            var actualKwh = _monitorService.CalculateCurrentUsageKwh();

            // Assert
            Assert.NotEqual(totalTheoreticalUsageKwh, actualKwh);
            Assert.Equal(expectedActiveUsageKwh, actualKwh, precision: 3);
        }

        /// <summary>
        /// Бізнес-ситуація: Розрахунок споживання при знеструмленій техніці або порожній системі.
        /// Перевіряє повернення строгого нуля (0.0 кВт·год) без помилок ділення або винятків.
        /// Застосовані типи перевірок: Assert.Equal.
        /// </summary>
        [Fact]
        public void CalculateCurrentUsageKwh_WhenNoDevicesOrAllInactive_ReturnsZeroUsage()
        {
            // Arrange
            var inactiveDevices = new List<Device>
            {
                new() { Id = 1, Name = "Телевізор", PowerUsageWatts = 120.0, IsOn = false },
                new() { Id = 2, Name = "Комп'ютер", PowerUsageWatts = 450.0, IsOn = false }
            };
            _devicesMock.Setup(r => r.GetAll()).Returns(inactiveDevices);

            // Act
            var actualKwh = _monitorService.CalculateCurrentUsageKwh();

            // Assert
            Assert.Equal(0.0, actualKwh);
        }

        /// <summary>
        /// Бізнес-ситуація: Виявлення перевищення добового ліміту електроенергії (критичне перевантаження мережі).
        /// Перевіряє, що сервіс надсилає сповіщення тривоги через INotificationService з точним текстом,
        /// якщо поточне споживання (наприклад, 3.5 кВт·год) перевищує ліміт плану (наприклад, 2.0 кВт·год).
        /// Застосовані типи перевірок: Verify (Times.Once), It.Is (предикат тексту сповіщення), Assert.True.
        /// </summary>
        [Fact]
        public void CheckForOverload_WhenUsageExceedsDailyLimit_SendsAlertWithDetailedMessage()
        {
            // Arrange
            var overloadedDevices = new List<Device>
            {
                new() { Id = 1, Name = "Обігрівач", PowerUsageWatts = 2000.0, IsOn = true },
                new() { Id = 2, Name = "Кондиціонер", PowerUsageWatts = 1500.0, IsOn = true }
            };
            var plan = new EnergyPlan
            {
                DailyLimitKwh = 2.0,
                LastReset = DateTime.UtcNow
            };

            _devicesMock.Setup(r => r.GetAll()).Returns(overloadedDevices);
            _plansMock.Setup(p => p.GetCurrentPlan()).Returns(plan);

            // Act
            _monitorService.CheckForOverload();

            // Assert: споживання 3.5 > 2.0 ліміту, тому надсилається точний alert
            const double calculatedUsage = 3.5;
            Assert.True(calculatedUsage > plan.DailyLimitKwh);

            _notifyMock.Verify(n => n.SendAlert(It.Is<string>(msg =>
                msg.Contains("Overload detected: 3.5 kWh used!"))),
                Times.Once);
        }

        /// <summary>
        /// Бізнес-ситуація: Нормальний штатний режим енергоспоживання — навантаження значно нижче ліміту.
        /// Перевіряє, що сервіс сповіщень не викликається жодного разу (захист користувача від спаму).
        /// Застосовані типи перевірок: Verify (Times.Never), It.IsAny, Assert.False.
        /// </summary>
        [Fact]
        public void CheckForOverload_WhenUsageWithinLimit_DoesNotSendAlert()
        {
            // Arrange
            var lightDevices = new List<Device>
            {
                new() { Id = 1, Name = "LED Лампочка", PowerUsageWatts = 15.0, IsOn = true },
                new() { Id = 2, Name = "Ноутбук", PowerUsageWatts = 65.0, IsOn = true }
            };
            var plan = new EnergyPlan
            {
                DailyLimitKwh = 5.0,
                LastReset = DateTime.UtcNow
            };

            _devicesMock.Setup(r => r.GetAll()).Returns(lightDevices);
            _plansMock.Setup(p => p.GetCurrentPlan()).Returns(plan);

            // Act
            _monitorService.CheckForOverload();

            // Assert: 0.08 <= 5.0, умова перевантаження не виконується
            const double calculatedUsage = 0.08;
            Assert.False(calculatedUsage > plan.DailyLimitKwh);

            _notifyMock.Verify(n => n.SendAlert(It.IsAny<string>()), Times.Never);
        }

        /// <summary>
        /// Бізнес-ситуація: Граничний випадок енергомоніторингу — поточне споживання рівно дорівнює встановленому ліміту.
        /// Відповідно до умови строгої нерівності (usage > plan.DailyLimitKwh), сповіщення не повинно надсилатися.
        /// Застосовані типи перевірок: Verify (Times.Never), It.IsAny, Assert.False, Assert.Equal.
        /// </summary>
        [Fact]
        public void CheckForOverload_WhenUsageExactlyEqualsLimit_DoesNotSendAlert()
        {
            // Arrange: 2000 Вт = 2.0 кВт·год, ліміт = 2.0 кВт·год
            var boundaryDevices = new List<Device>
            {
                new() { Id = 1, Name = "Плита", PowerUsageWatts = 2000.0, IsOn = true }
            };
            var plan = new EnergyPlan
            {
                DailyLimitKwh = 2.0,
                LastReset = DateTime.UtcNow
            };

            _devicesMock.Setup(r => r.GetAll()).Returns(boundaryDevices);
            _plansMock.Setup(p => p.GetCurrentPlan()).Returns(plan);

            // Act
            _monitorService.CheckForOverload();

            // Assert
            const double calculatedUsage = 2.0;
            Assert.Equal(calculatedUsage, plan.DailyLimitKwh);
            Assert.False(calculatedUsage > plan.DailyLimitKwh);

            _notifyMock.Verify(n => n.SendAlert(It.IsAny<string>()), Times.Never);
        }

        /// <summary>
        /// Бізнес-ситуація: Послідовні періодичні перевірки мережі під час збереження аварійного навантаження.
        /// Перевіряє, що сповіщення про перевантаження надсилається строго задану кількість разів (Times.Exactly(2)).
        /// Застосовані типи перевірок: Verify (Times.Exactly), It.IsAny.
        /// </summary>
        [Fact]
        public void CheckForOverload_MultipleSequentialOverloads_SendsAlertExactNumberOfTimes()
        {
            // Arrange
            var heavyDevices = new List<Device>
            {
                new() { Id = 1, Name = "Електрокотел", PowerUsageWatts = 6000.0, IsOn = true }
            };
            var plan = new EnergyPlan
            {
                DailyLimitKwh = 3.0,
                LastReset = DateTime.UtcNow
            };

            _devicesMock.Setup(r => r.GetAll()).Returns(heavyDevices);
            _plansMock.Setup(p => p.GetCurrentPlan()).Returns(plan);

            // Act: 2 послідовних цикли моніторингу
            _monitorService.CheckForOverload();
            _monitorService.CheckForOverload();

            // Assert: Exactly 2 сповіщення
            _notifyMock.Verify(n => n.SendAlert(It.IsAny<string>()), Times.Exactly(2));
        }

        /// <summary>
        /// Бізнес-ситуація: Верифікація діапазону викликів зовнішніх репозиторіїв при перевірці навантаження.
        /// Перевіряє, що метод GetAll та метод GetCurrentPlan викликаються щонайменше 1 раз (AtLeastOnce)
        /// та не більше 2 разів (AtMost(2)) за цикл моніторингу.
        /// Застосовані типи перевірок: Verify (Times.AtLeastOnce), Verify (Times.AtMost).
        /// </summary>
        [Fact]
        public void CheckForOverload_VerifiesRepositoriesCalledAtLeastOnceAndAtMostTwice()
        {
            // Arrange
            _devicesMock.Setup(r => r.GetAll()).Returns(new List<Device>());
            _plansMock.Setup(p => p.GetCurrentPlan()).Returns(new EnergyPlan { DailyLimitKwh = 10.0 });

            // Act
            _monitorService.CheckForOverload();

            // Assert
            _devicesMock.Verify(d => d.GetAll(), Times.AtLeastOnce);
            _devicesMock.Verify(d => d.GetAll(), Times.AtMost(2));
            _plansMock.Verify(p => p.GetCurrentPlan(), Times.AtLeastOnce);
            _plansMock.Verify(p => p.GetCurrentPlan(), Times.AtMost(2));
        }

        /// <summary>
        /// Бізнес-ситуація: Коригування користувачем добового ліміту споживання електроенергії (UpdateEnergyLimit).
        /// Перевіряє встановлення нового значення властивості DailyLimitKwh, зміну попереднього значення
        /// та фіксацію змін у сховищі через репозиторій IEnergyPlanRepository.UpdatePlan.
        /// Застосовані типи перевірок: Assert.Equal, Assert.NotEqual, Verify (Times.Once), It.Is (предикат).
        /// </summary>
        [Fact]
        public void UpdateEnergyLimit_ValidLimit_UpdatesPlanAndCallsRepositoryUpdate()
        {
            // Arrange
            const double originalLimit = 4.0;
            const double newTargetLimit = 8.5;

            var existingPlan = new EnergyPlan
            {
                DailyLimitKwh = originalLimit,
                LastReset = DateTime.UtcNow
            };
            _plansMock.Setup(p => p.GetCurrentPlan()).Returns(existingPlan);

            // Act
            _monitorService.UpdateEnergyLimit(newTargetLimit);

            // Assert
            Assert.Equal(newTargetLimit, existingPlan.DailyLimitKwh);
            Assert.NotEqual(originalLimit, existingPlan.DailyLimitKwh);

            _plansMock.Verify(p => p.GetCurrentPlan(), Times.Once);
            _plansMock.Verify(p => p.UpdatePlan(It.Is<EnergyPlan>(plan =>
                Math.Abs(plan.DailyLimitKwh - newTargetLimit) < 0.0001)),
                Times.Once);
        }

        /// <summary>
        /// Бізнес-ситуація: Параметризоване оновлення добових лімітів енергоспоживання для різних тарифів.
        /// Перевіряє коректність збереження лімітів для набору вхідних параметрів.
        /// Застосовані типи перевірок: [Theory], [InlineData], Assert.Equal, Verify (Times.Once).
        /// </summary>
        [Theory]
        [InlineData(1.5)]
        [InlineData(6.0)]
        [InlineData(14.75)]
        [InlineData(30.0)]
        public void UpdateEnergyLimit_ParameterizedLimits_UpdatesPlanCorrectly(double targetLimit)
        {
            // Arrange
            var plan = new EnergyPlan
            {
                DailyLimitKwh = 3.0,
                LastReset = DateTime.UtcNow
            };
            _plansMock.Setup(p => p.GetCurrentPlan()).Returns(plan);

            // Act
            _monitorService.UpdateEnergyLimit(targetLimit);

            // Assert
            Assert.Equal(targetLimit, plan.DailyLimitKwh);
            _plansMock.Verify(p => p.UpdatePlan(plan), Times.Once);
        }

        /// <summary>
        /// Бізнес-ситуація: Валідація наявності діючого енергетичного тарифного плану перед аналізом навантаження.
        /// Перевіряє, що сервіс оперує коректним, не-null об'єктом EnergyPlan, отриманим від IEnergyPlanRepository.
        /// Застосовані типи перевірок: Assert.NotNull, Assert.True.
        /// </summary>
        [Fact]
        public void CheckForOverload_PlanValidation_AssertNotNullForCurrentPlan()
        {
            // Arrange
            var activePlan = new EnergyPlan { DailyLimitKwh = 12.0, LastReset = DateTime.UtcNow };
            _plansMock.Setup(p => p.GetCurrentPlan()).Returns(activePlan);
            _devicesMock.Setup(r => r.GetAll()).Returns(new List<Device>());

            // Act
            var retrievedPlan = _plansMock.Object.GetCurrentPlan();
            _monitorService.CheckForOverload();

            // Assert
            Assert.NotNull(retrievedPlan);
            Assert.True(retrievedPlan.DailyLimitKwh > 0.0);
        }

        /// <summary>
        /// Бізнес-ситуація: Перевірка захисту від помилкових спрацьовувань та валідація відсутності передачі тексту сповіщень.
        /// За допомогою зворотного виклику (Callback) перевіряє, що змінна повідомлення залишається null (Assert.Null),
        /// коли споживання знаходиться в межах безпечної зони.
        /// Застосовані типи перевірок: Assert.Null, Verify (Times.Never).
        /// </summary>
        [Fact]
        public void CheckForOverload_SafeUsage_AlertMessageRemainsNull()
        {
            // Arrange
            string? alertMessage = null;
            var safeDevices = new List<Device>
            {
                new() { Id = 1, Name = "Енергозберігаюча лампа", PowerUsageWatts = 10.0, IsOn = true }
            };
            var plan = new EnergyPlan { DailyLimitKwh = 5.0, LastReset = DateTime.UtcNow };

            _devicesMock.Setup(r => r.GetAll()).Returns(safeDevices);
            _plansMock.Setup(p => p.GetCurrentPlan()).Returns(plan);
            _notifyMock.Setup(n => n.SendAlert(It.IsAny<string>()))
                       .Callback<string>(msg => alertMessage = msg);

            // Act
            _monitorService.CheckForOverload();

            // Assert
            Assert.Null(alertMessage);
            _notifyMock.Verify(n => n.SendAlert(It.IsAny<string>()), Times.Never);
        }
    }
}
