using Energy_Project.Models;
using Energy_Project.Services;
using Energy_Project.Services.Interfaces;
using Moq;
using Xunit;

namespace SmartHomeTests
{
    /// <summary>
    /// Набір модульних тестів для сервісу керування пристроями DeviceService.
    /// Перевіряє бізнес-логіку перемикання станів живлення (ToggleDevice)
    /// та вибірки активних споживачів енергії (GetActiveDevices) з використанням xUnit та Moq.
    /// </summary>
    public class DeviceServiceTests
    {
        private readonly Mock<IDeviceRepository> _deviceRepoMock;
        private readonly DeviceService _deviceService;

        public DeviceServiceTests()
        {
            _deviceRepoMock = new Mock<IDeviceRepository>();
            _deviceService = new DeviceService(_deviceRepoMock.Object);
        }

        /// <summary>
        /// Бізнес-ситуація: Увімкнення пристрою, який перебував у вимкненому стані.
        /// Перевіряє, що метод ToggleDevice успішно знаходить пристрій за Id,
        /// встановлює властивість IsOn = true, фіксує оновлення в репозиторії через Update(device)
        /// та повертає підтвердження успішного увімкнення (true).
        /// Застосовані типи перевірок: Assert.True, Verify (Times.Once).
        /// </summary>
        [Fact]
        public void ToggleDevice_DeviceExists_TurnsOnSuccessfully_ReturnsTrue()
        {
            // Arrange
            var existingDevice = new Device
            {
                Id = 1,
                Name = "Вітальня: Головне освітлення",
                PowerUsageWatts = 120.0,
                IsOn = false
            };
            _deviceRepoMock.Setup(r => r.GetById(1)).Returns(existingDevice);

            // Act
            var result = _deviceService.ToggleDevice(1, true);

            // Assert
            Assert.True(result);
            Assert.True(existingDevice.IsOn);
            _deviceRepoMock.Verify(r => r.GetById(1), Times.Once);
            _deviceRepoMock.Verify(r => r.Update(existingDevice), Times.Once);
        }

        /// <summary>
        /// Бізнес-ситуація: Вимкнення активного пристрою користувачем або автоматикою.
        /// Перевіряє, що метод ToggleDevice знаходить пристрій, змінює стан IsOn на false,
        /// зберігає стан через репозиторій та повертає false.
        /// Застосовані типи перевірок: Assert.False, Verify (Times.Once).
        /// </summary>
        [Fact]
        public void ToggleDevice_DeviceExists_TurnsOffSuccessfully_ReturnsFalse()
        {
            // Arrange
            var activeDevice = new Device
            {
                Id = 2,
                Name = "Кондиціонер спальні",
                PowerUsageWatts = 1500.0,
                IsOn = true
            };
            _deviceRepoMock.Setup(r => r.GetById(2)).Returns(activeDevice);

            // Act
            var result = _deviceService.ToggleDevice(2, false);

            // Assert
            Assert.False(result);
            Assert.False(activeDevice.IsOn);
            _deviceRepoMock.Verify(r => r.GetById(2), Times.Once);
            _deviceRepoMock.Verify(r => r.Update(activeDevice), Times.Once);
        }

        /// <summary>
        /// Бізнес-ситуація: Спроба перемикання стану неіснуючого або некоректно ідентифікованого пристрою.
        /// Перевіряє генерацію винятку ArgumentException з обов'язковим повідомленням "Device not found"
        /// та гарантує, що метод Update репозиторію не викликається жодного разу (запобігання корупції даних).
        /// Застосовані типи перевірок: Assert.Throws, Assert.Equal, Verify (Times.Never), It.IsAny.
        /// </summary>
        [Fact]
        public void ToggleDevice_DeviceNotFound_ThrowsArgumentException_AndDoesNotUpdateRepository()
        {
            // Arrange
            const int nonExistentDeviceId = 999;
            _deviceRepoMock.Setup(r => r.GetById(nonExistentDeviceId)).Returns((Device?)null);

            // Act & Assert
            var exception = Assert.Throws<ArgumentException>(() => _deviceService.ToggleDevice(nonExistentDeviceId, true));
            Assert.Equal("Device not found", exception.Message);

            // Забезпечення цілісності: репозиторій не оновлюється жодним об'єктом
            _deviceRepoMock.Verify(r => r.Update(It.IsAny<Device>()), Times.Never);
        }

        /// <summary>
        /// Бізнес-ситуація: Верифікація точних параметрів об'єкта Device при передачі в метод репозиторію Update.
        /// Перевіряє, що стан сутності відповідає усім очікуваним атрибутам (Id, Name, PowerUsageWatts, IsOn).
        /// Застосовані типи перевірок: Verify (Times.Once), It.Is (предикатний матчер властивостей сутності).
        /// </summary>
        [Fact]
        public void ToggleDevice_ValidDevice_VerifiesRepositoryUpdateWithExactPredicate()
        {
            // Arrange
            var heater = new Device
            {
                Id = 3,
                Name = "Обігрівач дитячої",
                PowerUsageWatts = 2000.0,
                IsOn = false
            };
            _deviceRepoMock.Setup(r => r.GetById(3)).Returns(heater);

            // Act
            _deviceService.ToggleDevice(3, true);

            // Assert: репозиторій отримав пристрій з точними характеристиками
            _deviceRepoMock.Verify(r => r.Update(It.Is<Device>(d =>
                d.Id == 3 &&
                d.Name == "Обігрівач дитячої" &&
                d.PowerUsageWatts == 2000.0 &&
                d.IsOn == true)),
                Times.Once);
        }

        /// <summary>
        /// Бізнес-ситуація: Багаторазове послідовне перемикання станів різних пристроїв системи «розумний дім».
        /// Перевіряє, що метод Update викликається строго визначену кількість разів (Times.Exactly),
        /// без пропусків та зайвих викликів.
        /// Застосовані типи перевірок: Verify (Times.Exactly), It.IsAny.
        /// </summary>
        [Fact]
        public void ToggleDevice_MultipleSequentialCalls_VerifiesExactInvocationCount()
        {
            // Arrange
            var dev1 = new Device { Id = 10, Name = "Бойлер", PowerUsageWatts = 1800.0, IsOn = false };
            var dev2 = new Device { Id = 11, Name = "Телевізор", PowerUsageWatts = 150.0, IsOn = false };
            var dev3 = new Device { Id = 12, Name = "Пральна машина", PowerUsageWatts = 2200.0, IsOn = false };

            _deviceRepoMock.Setup(r => r.GetById(10)).Returns(dev1);
            _deviceRepoMock.Setup(r => r.GetById(11)).Returns(dev2);
            _deviceRepoMock.Setup(r => r.GetById(12)).Returns(dev3);

            // Act
            _deviceService.ToggleDevice(10, true);
            _deviceService.ToggleDevice(11, true);
            _deviceService.ToggleDevice(12, true);

            // Assert: Update викликано рівно 3 рази
            _deviceRepoMock.Verify(r => r.Update(It.IsAny<Device>()), Times.Exactly(3));
        }

        /// <summary>
        /// Бізнес-ситуація: Параметризоване перемикання стану пристрою на різні цільові режими.
        /// Перевіряє коректність роботи логіки перемикання для набору вхідних булевих станів (true, false),
        /// забезпечуючи збіг повернутого результату з очікуваним станом.
        /// Застосовані типи перевірок: [Theory], [InlineData], Assert.Equal, Verify (Times.Once).
        /// </summary>
        [Theory]
        [InlineData(101, false, true, true)]
        [InlineData(102, true, false, false)]
        [InlineData(103, false, false, false)]
        [InlineData(104, true, true, true)]
        public void ToggleDevice_ParameterizedStates_TogglesDeviceCorrectly(
            int deviceId,
            bool initialStatus,
            bool targetStatus,
            bool expectedResult)
        {
            // Arrange
            var testDevice = new Device
            {
                Id = deviceId,
                Name = $"Тестовий пристрій #{deviceId}",
                PowerUsageWatts = 250.0,
                IsOn = initialStatus
            };
            _deviceRepoMock.Setup(r => r.GetById(deviceId)).Returns(testDevice);

            // Act
            var actualResult = _deviceService.ToggleDevice(deviceId, targetStatus);

            // Assert
            Assert.Equal(expectedResult, actualResult);
            Assert.Equal(targetStatus, testDevice.IsOn);
            _deviceRepoMock.Verify(r => r.Update(testDevice), Times.Once);
        }

        /// <summary>
        /// Бізнес-ситуація: Запит активних приладів у системі, де частина приладів працює, а частина вимкнена.
        /// Перевіряє, що сервіс повертає непорожню колекцію пристроїв та коректно фільтрує елементи (IsOn == true).
        /// Застосовані типи перевірок: Assert.NotEmpty, Assert.Equal, Assert.All, Assert.True.
        /// </summary>
        [Fact]
        public void GetActiveDevices_WhenActiveDevicesExist_ReturnsOnlyActiveDevices()
        {
            // Arrange
            var devices = new List<Device>
            {
                new() { Id = 1, Name = "Холодильник", PowerUsageWatts = 300.0, IsOn = true },
                new() { Id = 2, Name = "Мікрохвильовка", PowerUsageWatts = 1200.0, IsOn = false },
                new() { Id = 3, Name = "Wi-Fi Роутер", PowerUsageWatts = 15.0, IsOn = true },
                new() { Id = 4, Name = "Електрочайник", PowerUsageWatts = 2000.0, IsOn = false }
            };
            _deviceRepoMock.Setup(r => r.GetAll()).Returns(devices);

            // Act
            var activeDevices = _deviceService.GetActiveDevices().ToList();

            // Assert
            Assert.NotEmpty(activeDevices);
            Assert.Equal(2, activeDevices.Count);
            Assert.All(activeDevices, d => Assert.True(d.IsOn));
        }

        /// <summary>
        /// Бізнес-ситуація: Запит списку активних приладів, коли всі підключені споживачі енергії знеструмлені.
        /// Перевіряє повернення порожньої колекції без виникнення виняткових ситуацій.
        /// Застосовані типи перевірок: Assert.Empty.
        /// </summary>
        [Fact]
        public void GetActiveDevices_WhenNoDevicesAreActive_ReturnsEmptyCollection()
        {
            // Arrange
            var allOffDevices = new List<Device>
            {
                new() { Id = 1, Name = "Праска", PowerUsageWatts = 1800.0, IsOn = false },
                new() { Id = 2, Name = "Тостер", PowerUsageWatts = 850.0, IsOn = false }
            };
            _deviceRepoMock.Setup(r => r.GetAll()).Returns(allOffDevices);

            // Act
            var activeDevices = _deviceService.GetActiveDevices();

            // Assert
            Assert.Empty(activeDevices);
        }

        /// <summary>
        /// Бізнес-ситуація: Перевірка наявності конкретного активного споживача в списку увімкнених приладів.
        /// Перевіряє входження цільового екземпляра класу Device у повернуту колекцію за допомогою Assert.Contains.
        /// Застосовані типи перевірок: Assert.Contains.
        /// </summary>
        [Fact]
        public void GetActiveDevices_ContainsExpectedActiveDevice_ReturnsDeviceInCollection()
        {
            // Arrange
            var expectedDevice = new Device
            {
                Id = 42,
                Name = "Сервер розумного дому",
                PowerUsageWatts = 80.0,
                IsOn = true
            };
            var otherDevice = new Device
            {
                Id = 43,
                Name = "Вентилятор витяжки",
                PowerUsageWatts = 45.0,
                IsOn = false
            };

            _deviceRepoMock.Setup(r => r.GetAll()).Returns(new List<Device> { expectedDevice, otherDevice });

            // Act
            var activeDevices = _deviceService.GetActiveDevices();

            // Assert
            Assert.Contains(expectedDevice, activeDevices);
        }

        /// <summary>
        /// Бізнес-ситуація: Верифікація діапазону частоти звернень до репозиторію під час моніторингу активних пристроїв.
        /// Перевіряє, що метод GetAll репозиторію викликається щонайменше один раз (AtLeastOnce)
        /// та не більше двох разів (AtMost(2)) при повторному запиті.
        /// Застосовані типи перевірок: Verify (Times.AtLeastOnce), Verify (Times.AtMost).
        /// </summary>
        [Fact]
        public void GetActiveDevices_VerifiesGetAllCalledAtLeastOnceAndAtMostTwice()
        {
            // Arrange
            _deviceRepoMock.Setup(r => r.GetAll()).Returns(new List<Device>());

            // Act
            _deviceService.GetActiveDevices();
            _deviceService.GetActiveDevices();

            // Assert
            _deviceRepoMock.Verify(r => r.GetAll(), Times.AtLeastOnce);
            _deviceRepoMock.Verify(r => r.GetAll(), Times.AtMost(2));
        }

        /// <summary>
        /// Бізнес-ситуація: Пошук та валідація окремих приладів у колекції активних пристроїв.
        /// Перевіряє, що увімкнений прилад успішно знаходиться (NotNull),
        /// а вимкнений прилад відсутній серед активних споживачів (Null).
        /// Застосовані типи перевірок: Assert.NotNull, Assert.Null.
        /// </summary>
        [Fact]
        public void GetActiveDevices_FilterLookup_AssertNotNullForActiveAndNullForInactive()
        {
            // Arrange
            var activeLamp = new Device { Id = 5, Name = "Настільна лампа", PowerUsageWatts = 40.0, IsOn = true };
            var offHeater = new Device { Id = 6, Name = "Калорифер", PowerUsageWatts = 2200.0, IsOn = false };

            _deviceRepoMock.Setup(r => r.GetAll()).Returns(new List<Device> { activeLamp, offHeater });

            // Act
            var activeDevices = _deviceService.GetActiveDevices().ToList();
            var foundActive = activeDevices.FirstOrDefault(d => d.Id == 5);
            var foundInactive = activeDevices.FirstOrDefault(d => d.Id == 6);

            // Assert
            Assert.NotNull(foundActive);
            Assert.Null(foundInactive);
        }

        /// <summary>
        /// Бізнес-ситуація: Порівняння кількості активних споживачів із загальною кількістю зареєстрованих приладів.
        /// Гарантує, що за наявності вимкнених пристроїв обсяг повернутої вибірки не дорівнює повній кількості сутностей.
        /// Застосовані типи перевірок: Assert.NotEqual.
        /// </summary>
        [Fact]
        public void GetActiveDevices_CountOfActiveDevices_NotEqualToTotalDevicesCount()
        {
            // Arrange
            var allDevices = new List<Device>
            {
                new() { Id = 1, Name = "Прилад 1", PowerUsageWatts = 100.0, IsOn = true },
                new() { Id = 2, Name = "Прилад 2", PowerUsageWatts = 200.0, IsOn = false },
                new() { Id = 3, Name = "Прилад 3", PowerUsageWatts = 300.0, IsOn = true },
                new() { Id = 4, Name = "Прилад 4", PowerUsageWatts = 400.0, IsOn = false },
                new() { Id = 5, Name = "Прилад 5", PowerUsageWatts = 500.0, IsOn = true }
            };
            _deviceRepoMock.Setup(r => r.GetAll()).Returns(allDevices);

            // Act
            var activeCount = _deviceService.GetActiveDevices().Count();

            // Assert
            Assert.NotEqual(allDevices.Count, activeCount);
            Assert.Equal(3, activeCount);
        }
    }
}
