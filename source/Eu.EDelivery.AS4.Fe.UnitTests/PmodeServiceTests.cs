using Eu.EDelivery.AS4.Fe.Exceptions;
using Eu.EDelivery.AS4.Fe.Pmodes;
using Eu.EDelivery.AS4.Fe.Pmodes.Model;
using Eu.EDelivery.AS4.Fe.Services;
using NSubstitute;

namespace Eu.EDelivery.AS4.Fe.UnitTests;

public class PmodeServiceTests
{
    protected IPmodeService Service { get; private set; }
    protected IAs4PmodeSource Source { get; private set; }
    protected IEnumerable<SendingBasePmode> SendingPmodes { get; private set; }
    protected IEnumerable<ReceivingBasePmode> ReceivingPmodes { get; private set; }
    protected ReceivingBasePmode ReceivingBasePmode { get; private set; }
    protected SendingBasePmode SendingBasePmode { get; private set; }

    protected PmodeServiceTests()
    {
        Source = Substitute.For<IAs4PmodeSource>();
        Service = new PmodeService(Source, Default.SendingProcessingModeValidator, Default.ReceivingProcessingModeValidator, true);
        SendingPmodes = [];
        ReceivingPmodes = [];
        ReceivingBasePmode = new();
        SendingBasePmode = new();
    }

    protected void SetupPmodes(CancellationToken cancellationToken)
    {
        ReceivingBasePmode = new ReceivingBasePmode()
        {
            Name = "test1",
            Type = PmodeType.Receiving,
        };
        SendingBasePmode = new SendingBasePmode()
        {
            Name = "test2",
            Type = PmodeType.Sending,
        };
        ReceivingPmodes = [ReceivingBasePmode];
        SendingPmodes = [SendingBasePmode];

        Source.GetReceivingNamesAsync(cancellationToken).Returns(ReceivingPmodes.Select(pmode => pmode.Name!));
        Source.GetReceivingByNameAsync(Arg.Is(ReceivingBasePmode.Name), cancellationToken).Returns(ReceivingBasePmode);

        Source.GetSendingNamesAsync(cancellationToken).Returns(SendingPmodes.Select(pmode => pmode.Name!));
        Source.GetSendingByNameAsync(Arg.Is(SendingBasePmode.Name), cancellationToken).Returns(SendingBasePmode);
    }

    public class GetReceivingNames : PmodeServiceTests
    {
        [Fact]
        public async Task Calls_Source_And_Returns_Names()
        {
            // Arrange
            SetupPmodes(TestContext.Current.CancellationToken);

            // Act
            var result = await Service.GetReceivingNamesAsync(TestContext.Current.CancellationToken);

            // Assert
            await Source.Received().GetReceivingNamesAsync(TestContext.Current.CancellationToken);
            Assert.True(result.First() == ReceivingBasePmode.Name);
        }

        [Fact]
        public async Task Returns_Empty_List_When_No_Modes_Exist()
        {
            // Arrange
            SetupPmodes(TestContext.Current.CancellationToken);
            Source.GetReceivingNamesAsync(TestContext.Current.CancellationToken).Returns([]);

            // Act
            var result = await Service.GetReceivingNamesAsync(TestContext.Current.CancellationToken);

            // Assert
            Assert.False(result.Any());
        }
    }

    public class GetReceivingByName : PmodeServiceTests
    {
        [Fact]
        public async Task Throws_Exception_When_Parameters_Are_Null()
        {
            // Arrange
            SetupPmodes(TestContext.Current.CancellationToken);

            // Act
            await Assert.ThrowsAsync<ArgumentException>(() => Service.GetReceivingByNameAsync(string.Empty, TestContext.Current.CancellationToken));
        }
    }

    public class GetSendingNames : PmodeServiceTests
    {
        [Fact]
        public async Task Calls_Source_And_Returns_Pmode_When_It_Exists()
        {
            // Arrange
            SetupPmodes(TestContext.Current.CancellationToken);

            // Act
            var result = await Service.GetSendingNamesAsync(TestContext.Current.CancellationToken);

            // Assert
            Assert.NotNull(result);
            Assert.True(result.First() == SendingBasePmode.Name);
            await Source.Received().GetSendingNamesAsync(TestContext.Current.CancellationToken);
        }

        [Fact]
        public async Task Returns_Empty_List_When_No_Modes_Exist()
        {
            // Arrange
            SetupPmodes(TestContext.Current.CancellationToken);
            Source.GetSendingNamesAsync(TestContext.Current.CancellationToken).Returns([]);

            // Act
            var result = await Service.GetSendingNamesAsync(TestContext.Current.CancellationToken);

            // Assert
            await Source.Received().GetSendingNamesAsync(TestContext.Current.CancellationToken);
            Assert.False(result.Any());
        }
    }

    public class GetSendingByName : PmodeServiceTests
    {
        [Fact]
        public async Task Calls_Source_And_Returns_Pmode()
        {
            // Arrange
            SetupPmodes(TestContext.Current.CancellationToken);

            // Act
            var result = await Service.GetSendingByNameAsync(SendingBasePmode.Name!, TestContext.Current.CancellationToken);

            // Assert
            await Source.Received().GetSendingByNameAsync(Arg.Is(SendingBasePmode.Name!), TestContext.Current.CancellationToken);
            Assert.True(result == SendingBasePmode);
        }

        [Fact]
        public async Task Throws_Exception_When_Parameters_Are_Null()
        {
            // Arrange
            SetupPmodes(TestContext.Current.CancellationToken);

            // Act
            await Assert.ThrowsAsync<ArgumentException>(() => Service.GetSendingByNameAsync(string.Empty, TestContext.Current.CancellationToken));
        }
    }

    public class CreateReceivingPmode : PmodeServiceTests
    {
        [Fact]
        public async Task Throws_Exception_When_Parameters_Are_Null()
        {
            // Arrange
            SetupPmodes(TestContext.Current.CancellationToken);
            var pmode = new ReceivingBasePmode();

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentNullException>(() => Service.CreateReceivingAsync(pmode, TestContext.Current.CancellationToken));
        }

        [Fact]
        public async Task Throws_Exception_When_Pmode_Already_Exists()
        {
            // Arrange
            SetupPmodes(TestContext.Current.CancellationToken);
            var pmode = new ReceivingBasePmode()
            {
                Name = ReceivingBasePmode.Name,
            };

            await Assert.ThrowsAsync<AlreadyExistsException>(() => Service.CreateReceivingAsync(pmode, TestContext.Current.CancellationToken));
        }

        [Fact]
        public async Task Calls_Source_SaveReceiving()
        {
            // Arrange
            SetupPmodes(TestContext.Current.CancellationToken);
            var pmode = new ReceivingBasePmode()
            {
                Name = "newPmode",
            };

            // Act
            await Service.CreateReceivingAsync(pmode, TestContext.Current.CancellationToken);

            // Assert
            await Source.Received().CreateReceivingAsync(Arg.Is<ReceivingBasePmode>(x => x.Name == "newPmode"), TestContext.Current.CancellationToken);
        }
    }

    public class DeleteReceiving : PmodeServiceTests
    {
        [Fact]
        public async Task Throws_Exception_When_Parameters_Are_Null()
        {
            // Arrange
            SetupPmodes(TestContext.Current.CancellationToken);

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentNullException>(() => Service.DeleteReceivingAsync(null!, TestContext.Current.CancellationToken));
        }

        [Fact]
        public async Task Throws_Exception_When_Pmode_Doesnt_Exist()
        {
            // Arrange
            SetupPmodes(TestContext.Current.CancellationToken);

            // Act & Assert
            await Assert.ThrowsAsync<NotFoundException>(() => Service.DeleteReceivingAsync("new", TestContext.Current.CancellationToken));
        }

        [Fact]
        public async Task Deletes_Pmode()
        {
            // Arrange
            SetupPmodes(TestContext.Current.CancellationToken);

            // Act
            await Service.DeleteReceivingAsync(ReceivingBasePmode.Name!, TestContext.Current.CancellationToken);

            // Assert
            await Source.Received().DeleteReceivingAsync(ReceivingBasePmode.Name!, TestContext.Current.CancellationToken);
        }
    }

    public class CreateSending : PmodeServiceTests
    {
        [Fact]
        public async Task Throws_Exception_When_Parameters_Are_Null()
        {
            // Arrange
            SetupPmodes(TestContext.Current.CancellationToken);
            var pmode = new SendingBasePmode();

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentNullException>(() => Service.CreateSendingAsync(pmode, TestContext.Current.CancellationToken));
        }

        [Fact]
        public async Task Throws_Exception_When_Pmode_Already_Exists()
        {
            // Arrange
            SetupPmodes(TestContext.Current.CancellationToken);

            // Act & Assert
            await Assert.ThrowsAsync<AlreadyExistsException>(() => Service.CreateSendingAsync(SendingBasePmode, TestContext.Current.CancellationToken));
        }

        [Fact]
        public async Task Creates_The_Pmode()
        {
            // Arrange
            SetupPmodes(TestContext.Current.CancellationToken);
            var pmode = new SendingBasePmode()
            {
                Name = "newPmode",
            };

            // Act
            await Service.CreateSendingAsync(pmode, TestContext.Current.CancellationToken);

            // Assert
            await Source.Received().CreateSendingAsync(Arg.Is<SendingBasePmode>(x => x.Name == pmode.Name), TestContext.Current.CancellationToken);
        }
    }

    public class DeleteSending : PmodeServiceTests
    {
        [Fact]
        public async Task Throws_Exception_When_Parameters_Are_Null()
        {
            // Arrange
            SetupPmodes(TestContext.Current.CancellationToken);

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentNullException>(() => Service.DeleteSendingAsync(null!, TestContext.Current.CancellationToken));
        }

        [Fact]
        public async Task Throws_Exception_When_Pmode_Doesnt_Exist()
        {
            // Arrange
            SetupPmodes(TestContext.Current.CancellationToken);

            // Act & Assert
            await Assert.ThrowsAsync<NotFoundException>(() => Service.DeleteSendingAsync("sendingPmode", TestContext.Current.CancellationToken));
        }

        [Fact]
        public async Task Deletes_Pmode()
        {
            // Arrange
            SetupPmodes(TestContext.Current.CancellationToken);

            // Act
            await Service.DeleteSendingAsync(SendingBasePmode.Name!, TestContext.Current.CancellationToken);

            // Assert
            await Source.Received().DeleteSendingAsync(Arg.Is(SendingBasePmode.Name!), TestContext.Current.CancellationToken);
        }
    }

    public class UpdateSending : PmodeServiceTests
    {
        [Fact]
        public async Task Throws_Exception_When_Parameters_Are_Null()
        {
            // Arrange
            SetupPmodes(TestContext.Current.CancellationToken);

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentNullException>(() => Service.UpdateSendingAsync(SendingBasePmode, null!, TestContext.Current.CancellationToken));
        }

        [Fact]
        public async Task Throws_Exception_When_A_Pmode_With_The_New_Name_Already_Exists()
        {
            // Arrange
            SetupPmodes(TestContext.Current.CancellationToken);
            var newPmode = new SendingBasePmode()
            {
                Name = SendingBasePmode.Name,
            };

            // Act
            await Assert.ThrowsAsync<AlreadyExistsException>(() => Service.UpdateSendingAsync(newPmode, "NEW", TestContext.Current.CancellationToken));
        }

        [Fact]
        public async Task Updates_Existing()
        {
            // Arrange
            SetupPmodes(TestContext.Current.CancellationToken);
            var newPmode = new SendingBasePmode()
            {
                Name = "NEW",
            };

            // Act
            await Service.UpdateSendingAsync(newPmode, SendingBasePmode.Name!, TestContext.Current.CancellationToken);

            // Assert
            await Source.UpdateSendingAsync(Arg.Is<SendingBasePmode>(x => x.Name == "NEW"), Arg.Is(SendingBasePmode.Name!), TestContext.Current.CancellationToken);
        }

        [Fact]
        public async Task Update_Existing_When_Name_IsNot_Changed()
        {
            // Arrange
            SetupPmodes(TestContext.Current.CancellationToken);

            // Act
            await Service.UpdateSendingAsync(SendingBasePmode, SendingBasePmode.Name!, TestContext.Current.CancellationToken);

            // Assert
            await Source.UpdateSendingAsync(Arg.Is<SendingBasePmode>(x => x.Name == "NEW"), Arg.Is(SendingBasePmode.Name!), TestContext.Current.CancellationToken);
        }
    }

    public class UpdateReceiving : PmodeServiceTests
    {
        [Fact]
        public async Task Throws_Exception_When_Parameters_Are_Null()
        {
            // Arrange
            SetupPmodes(TestContext.Current.CancellationToken);

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentNullException>(() => Service.UpdateReceivingAsync(ReceivingBasePmode, null!, TestContext.Current.CancellationToken));
        }

        [Fact]
        public async Task Throws_Exception_When_A_Pmode_With_The_New_Name_Already_Exists()
        {
            // Arrange
            SetupPmodes(TestContext.Current.CancellationToken);
            var newPmode = new ReceivingBasePmode()
            {
                Name = ReceivingBasePmode.Name,
            };

            // Act
            await Assert.ThrowsAsync<AlreadyExistsException>(() => Service.UpdateReceivingAsync(newPmode, "NEW", TestContext.Current.CancellationToken));
        }

        [Fact]
        public async Task Updates_Existing()
        {
            // Arrange
            SetupPmodes(TestContext.Current.CancellationToken);
            var newPmode = new ReceivingBasePmode()
            {
                Name = "NEW",
            };

            // Act
            await Service.UpdateReceivingAsync(newPmode, ReceivingBasePmode.Name!, TestContext.Current.CancellationToken);

            // Assert
            await Source.UpdateReceivingAsync(Arg.Is<ReceivingBasePmode>(x => x.Name == "NEW"), Arg.Is(ReceivingBasePmode.Name!), TestContext.Current.CancellationToken);
        }

        [Fact]
        public async Task Update_Existing_When_Name_IsNot_Changed()
        {
            // Arrange
            SetupPmodes(TestContext.Current.CancellationToken);

            // Act
            await Service.UpdateReceivingAsync(ReceivingBasePmode, ReceivingBasePmode.Name!, TestContext.Current.CancellationToken);

            // Assert
            await Source.UpdateReceivingAsync(Arg.Is<ReceivingBasePmode>(x => x.Name == "NEW"), Arg.Is(ReceivingBasePmode.Name!), TestContext.Current.CancellationToken);
        }
    }
}
