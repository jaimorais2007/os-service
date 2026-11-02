namespace OSService.Tests;

public class UnitTest1
{
    [Fact]
    public void Test()
    {
        // Arrange
        int number1 = 2;
        int number2 = 3;

        // Act
        int result = number1 + number2;

        // Assert
        Assert.Equal(5, result);
    }
}
